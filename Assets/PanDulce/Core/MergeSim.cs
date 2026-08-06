using System;
using System.Collections.Generic;
using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The bespoke positional solver: integrate, resolve collisions, merge, walls and the
    /// curved cloth floor. Handoff §7, ported literally.
    ///
    /// Deliberately NOT Rigidbody2D/CircleCollider2D — mass is r², substeps are fixed, and
    /// the floor curves with an inward pull. Unity 2D physics reproduces none of that, and
    /// half the tuning knobs would stop meaning anything.
    ///
    /// Step() must not allocate. Every list here is pre-sized and reused; dead bodies are
    /// swap-removed rather than filtered.
    /// </summary>
    public sealed class MergeSim
    {
        // --- events: Core raises, views subscribe. Views never poll the sim. ---
        public event Action<int, Vector2, int> Merged;      // tier, position, comboN
        public event Action<int, Vector2> TierDiscovered;   // tier, sim position (for effects)
        public event Action Shaken;

        public readonly List<Body> Bodies = new List<Body>(64);
        public readonly List<FloatText> Floats = new List<FloatText>(16);

        readonly Stack<Body> bodyPool = new Stack<Body>(64);
        readonly Stack<FloatText> floatPool = new Stack<FloatText>(16);
        readonly List<(Body a, Body c)> mergeBuffer = new List<(Body, Body)>(16);

        readonly bool[] discovered = new bool[TierTable.Count];
        readonly System.Random rng;
        ISimConfig cfg;
        int nextId = 1;

        public float Now { get; private set; }
        public int CurTier { get; private set; }
        public int NextTier { get; private set; }
        public int ComboN { get; private set; }
        public float ShakeUntil { get; private set; }
        public int HighestDiscovered { get; private set; }

        float lastMergeT = -999f;
        float canDropAt;
        float mergeLockUntil;

        /// <summary>The starting desserts settle for this long before any merge can fire.</summary>
        public const float StartMergeGraceSec = 1f;

        // --- coming to rest -----------------------------------------------------------
        // A settled pastry has THREE things still feeding it spin, and all three have to be
        // shut off or the pile turns forever:
        //   · the floor's rolling coupling, fed by the centre pull's permanent drift
        //     (friction and pull balance at ≈ CenterPull/GroundFriction, never at zero),
        //   · the tangential impulse from resting neighbours leaning on each other,
        //   · the wall's `vrot = ±vy/r` — an assignment, so it overwrites any damping.
        // The 0.99 per-substep decay swallows none of it. Bodies resting on OTHER bodies
        // never touch the floor branch at all, which is most of a real pile.

        /// <summary>Below this |vy| a floor contact counts as settled and vy is zeroed.</summary>
        const float FloorSleepVy = 20f;

        // Rest is judged on DISPLACEMENT, not velocity. A body wedged in a pile never has
        // a small vy — gravity adds and the contact cancels it every substep, and the
        // deeper the pile the harder that hums (36–54 px/s measured, going nowhere). Any
        // velocity threshold is therefore either too tight for a deep pile or too loose to
        // be safe. Where the body actually IS after a moment has neither problem.

        /// <summary>How long a window the settle test measures movement over.</summary>
        const float RestSampleSec = 0.15f;

        /// <summary>
        /// Move less than this (sim px) within a sample window and the body is going
        /// nowhere. 1.5 px per 0.15 s ≈ 10 px/s — well above the centre pull's endless
        /// creep, well below anything that reads as a roll.
        /// </summary>
        const float RestMoveEps = 1.5f;

        /// <summary>
        /// Contact approach speed that counts as a real hit rather than the pile leaning on
        /// itself; a hit wakes both bodies on the spot. Ten substeps of gravity ≈ 80 px/s —
        /// clear of settling chatter, far under a landing drop (300+ px/s).
        /// </summary>
        const float ImpactSubsteps = 10f;

        /// <summary>Spin decay rate per second once resting (≈0.3 s to a visual stop).</summary>
        const float RestSpinDamping = 12f;

        /// <summary>|vrot| under this (rad/s, ≈1°/s) is snapped to a dead stop.</summary>
        const float RestSpinSnap = 0.02f;

        /// <summary>
        /// A contact normal with at least this much downward component counts as support
        /// (±75° of straight down). Loose on purpose: a body bridging the V between two
        /// neighbours is held by two shallow contacts and nothing steeper. Marking a body
        /// supported cannot make it rest on its own — it still has to be going nowhere,
        /// and anything actually falling covers ground fast.
        /// </summary>
        const float SupportNormalY = 0.25f;

        /// <summary>
        /// Support has to lapse for this long before a body stops counting as resting.
        /// Neighbours lever each other a hair off their contacts constantly, and without
        /// the grace that chatter flickers the rest flag and lets the spin back in. Far
        /// shorter than a fall, which covers ground long before the grace runs out.
        /// </summary>
        const float RestSupportGrace = 0.12f;

        /// <summary>Only ease to upright from within this much rot error (rad). Wider and a
        /// body that stopped mid-tumble would creep around — the very thing being fixed.</summary>
        const float RestAlignWindow = 0.25f;

        /// <summary>How fast that last bit of tilt eases out, per second.</summary>
        const float RestAlignRate = 4f;

        public MergeSim(ISimConfig config, System.Random random = null)
        {
            cfg = config;
            rng = random ?? new System.Random();
            ResetRun();
        }

        public void SetConfig(ISimConfig config) => cfg = config;

        public bool IsDiscovered(int tier) => discovered[tier];
        public bool CanDropNow => Now >= canDropAt;
        public bool Shaking => Now < ShakeUntil;

        float Rand01() => (float)rng.NextDouble();
        float RandRange(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ---------------------------------------------------------------- frame

        /// <summary>One frame: advance time then run the fixed substeps (§7.2).</summary>
        public void Tick(float dt)
        {
            Now += dt;
            int n = Mathf.Max(1, cfg.Substeps);
            float sub = dt / n;
            for (int i = 0; i < n; i++) Step(sub);
        }

        void Step(float dt)
        {
            float rotAmt = cfg.RotationAmount;
            float e = cfg.Bounciness;
            float sizeScale = cfg.SizeScale;

            // Approach speed that separates a real hit from the pile leaning on itself.
            float impactV = cfg.Gravity * dt * ImpactSubsteps;

            // 1 — integrate
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];

                // Rest is judged on the contacts found last substep: contacts persist, and
                // the answer is needed before the contact/wall/floor code that feeds spin.
                if (b.supported) b.sinceSupport = 0f; else b.sinceSupport += dt;
                b.supported = false;

                b.restSampleT += dt;
                if (b.restSampleT >= RestSampleSec)
                {
                    float mx = b.x - b.restAnchorX, my = b.y - b.restAnchorY;
                    b.wentNowhere = mx * mx + my * my < RestMoveEps * RestMoveEps;
                    b.restAnchorX = b.x; b.restAnchorY = b.y; b.restSampleT = 0f;
                }
                b.atRest = b.wentNowhere && b.sinceSupport < RestSupportGrace;

                b.vy += cfg.Gravity * dt;
                b.x += b.vx * dt;
                b.y += b.vy * dt;
                b.rot += b.vrot * dt;
                float cap = 3f * rotAmt + 0.15f;
                b.vrot = Mathf.Clamp(b.vrot, -cap, cap) * 0.99f;
                b.squish *= (1f - 6f * dt);
                if (b.spawnT < 1f) b.spawnT = Mathf.Min(1f, b.spawnT + dt / cfg.MergeGrowTime);
            }

            // 2 — pairwise. O(n²) is fine; counts stay under ~40.
            mergeBuffer.Clear();
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body a = Bodies[i];
                if (a.dead) continue;
                for (int j = i + 1; j < Bodies.Count; j++)
                {
                    Body c = Bodies[j];
                    if (a.dead || c.dead) continue;

                    float ra = TierTable.Er(a, sizeScale), rc = TierTable.Er(c, sizeScale);
                    float dx = c.x - a.x, dy = c.y - a.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float min = ra + rc;
                    if (d >= min) continue;
                    if (d < 0.01f) { d = 0.01f; dx = 0.01f; dy = 0f; }
                    float nx = dx / d, ny = dy / d;

                    // merge gate — past the start grace, BOTH grown past 0.55 AND older
                    // than comboDelay
                    if (a.tier == c.tier && a.tier < TierTable.Max &&
                        Now >= mergeLockUntil &&
                        a.spawnT > 0.55f && c.spawnT > 0.55f &&
                        Now - a.bornAt > cfg.ComboDelay && Now - c.bornAt > cfg.ComboDelay)
                    {
                        a.dead = true; c.dead = true;
                        mergeBuffer.Add((a, c));
                        continue;
                    }

                    float ma = ra * ra, mc = rc * rc, tm = ma + mc, ov = min - d;   // mass = r²
                    a.x -= nx * ov * (mc / tm); a.y -= ny * ov * (mc / tm);
                    c.x += nx * ov * (ma / tm); c.y += ny * ov * (ma / tm);

                    // n runs a→c and +y is down, so a positive ny puts c underneath a.
                    if (ny > SupportNormalY) a.supported = true;
                    else if (ny < -SupportNormalY) c.supported = true;

                    float rvx = c.vx - a.vx, rvy = c.vy - a.vy;
                    float vn = rvx * nx + rvy * ny;
                    if (vn < 0f)
                    {
                        float jm = -(1f + e) * vn / (1f / ma + 1f / mc);
                        a.vx -= jm * nx / ma; a.vy -= jm * ny / ma;
                        c.vx += jm * nx / mc; c.vy += jm * ny / mc;
                        float q = Mathf.Min(0.28f, Mathf.Abs(vn) / 1500f) * cfg.SquishAmount;
                        if (q > 0.05f) { a.squish = Mathf.Max(a.squish, q); c.squish = Mathf.Max(c.squish, q); }
                        // Spin from the tangential impulse — but a settled body leaning on
                        // its neighbours re-approaches by a gravity nibble every substep,
                        // and that trickle is enough to keep the whole pile turning. A real
                        // hit wakes both bodies on the spot so the spin lands immediately;
                        // the pile's own chatter is ignored.
                        if (-vn > impactV) { a.Wake(); c.Wake(); }
                        float vt = rvx * -ny + rvy * nx, rk = 0.15f * rotAmt;
                        if (!a.atRest) a.vrot += (vt / ra) * rk;
                        if (!c.atRest) c.vrot += (vt / rc) * rk;
                    }
                }
            }

            // 3 — walls, and THE CURVED CLOTH FLOOR (the signature of this design)
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.dead) continue;
                float r = TierTable.Er(b, sizeScale);

                // The wall spin is an assignment, not an impulse — left ungated it would
                // overwrite the rest damping every substep for anything leaning on a wall.
                if (b.x - r < SimField.WL)
                {
                    b.x = SimField.WL + r;
                    b.vx = Mathf.Abs(b.vx) * e;
                    if (!b.atRest) b.vrot = -b.vy / r * 0.4f * rotAmt;
                }
                if (b.x + r > SimField.WR)
                {
                    b.x = SimField.WR - r;
                    b.vx = -Mathf.Abs(b.vx) * e;
                    if (!b.atRest) b.vrot = b.vy / r * 0.4f * rotAmt;
                }

                float fy = SimField.FloorAt(b.x, cfg.FloorSag);
                if (b.y + r > fy)
                {
                    float vi = b.vy;
                    b.y = fy - r;
                    b.vy = -Mathf.Abs(b.vy) * e * 0.6f;
                    if (Mathf.Abs(b.vy) < FloorSleepVy) b.vy = 0f;
                    b.vx *= (1f - cfg.GroundFriction * dt);                              // friction
                    b.vx -= ((b.x - SimField.CX) / SimField.HW) * cfg.CenterPull * dt;   // roll to the middle
                    b.supported = true;

                    // Only couple spin to travel when the body is actually travelling. The
                    // centre pull never stops, so a settled body keeps a few px/s of drift
                    // forever, and coupling that is what re-span the pile every substep.
                    if (!b.atRest)
                        b.vrot += (b.vx / r - b.vrot) * Mathf.Min(0.4f, 0.05f + 0.5f * rotAmt);

                    if (vi > 180f)
                        b.squish = Mathf.Max(b.squish, Mathf.Min(0.3f, vi / 1600f) * cfg.SquishAmount);
                }
            }

            // 3.5 — bring settled bodies to a stop. Covers the whole pile, not just the
            // bottom layer: a body held up by other bodies never reaches the floor branch.
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.dead || !b.atRest) continue;

                b.vrot -= b.vrot * Mathf.Min(1f, RestSpinDamping * dt);
                if (Mathf.Abs(b.vrot) >= RestSpinSnap) continue;

                b.vrot = 0f;
                float turn = 2f * Mathf.PI;
                float off = Mathf.Round(b.rot / turn) * turn - b.rot;   // nearest upright
                if (Mathf.Abs(off) < RestAlignWindow)
                    b.rot += off * Mathf.Min(1f, RestAlignRate * dt);
            }

            // 4 — apply merges, drop dead bodies, age floating text
            for (int i = 0; i < mergeBuffer.Count; i++) ApplyMerge(mergeBuffer[i].a, mergeBuffer[i].c);
            CompactDead();
            AgeFloats(dt);
        }

        void CompactDead()
        {
            for (int i = Bodies.Count - 1; i >= 0; i--)
            {
                if (!Bodies[i].dead) continue;
                Recycle(Bodies[i]);
                Bodies[i] = Bodies[Bodies.Count - 1];
                Bodies.RemoveAt(Bodies.Count - 1);
            }
        }

        void AgeFloats(float dt)
        {
            for (int i = Floats.Count - 1; i >= 0; i--)
            {
                FloatText f = Floats[i];
                f.t += dt;
                f.y -= 42f * dt;
                if (f.t < 1f) continue;
                floatPool.Push(f);
                Floats[i] = Floats[Floats.Count - 1];
                Floats.RemoveAt(Floats.Count - 1);
            }
        }

        // ---------------------------------------------------------------- merge

        void ApplyMerge(Body a, Body c)
        {
            int t2 = a.tier + 1;
            float sizeScale = cfg.SizeScale;
            float ra = TierTable.Er(a, sizeScale), rc = TierTable.Er(c, sizeScale);
            float x = (a.x * ra + c.x * rc) / (ra + rc);      // radius-weighted midpoint
            float y = (a.y * ra + c.y * rc) / (ra + rc);

            Body nb = MakeBody(x, y, t2, 0f);
            nb.vy = cfg.MergePopVy;
            nb.vx = (a.vx + c.vx) * 0.3f;

            ComboN = (Now - lastMergeT < cfg.ComboWindow) ? ComboN + 1 : 1;
            lastMergeT = Now;
            if (ComboN >= 2) AddFloat(x, y - TierTable.BaseRadius[t2] - 8f, $"Combo {ComboN}!");

            Merged?.Invoke(t2, new Vector2(x, y), ComboN);

            if (!discovered[t2])
            {
                discovered[t2] = true;
                if (t2 > HighestDiscovered) HighestDiscovered = t2;
                AddFloat(x, y - TierTable.BaseRadius[t2] - 26f, "New in the case!");
                TierDiscovered?.Invoke(t2, new Vector2(x, y));
            }
        }

        // ---------------------------------------------------------------- spawn

        /// <summary>Spawn pick is always weighted 4:3:2:1 over tiers 0–3 only (§6.1).</summary>
        public int Pick()
        {
            int roll = rng.Next(0, 10);          // 0..9
            if (roll < 4) return 0;
            if (roll < 7) return 1;
            if (roll < 9) return 2;
            return 3;
        }

        public Body MakeBody(float x, float y, int tier, float spawnT)
        {
            Body b = bodyPool.Count > 0 ? bodyPool.Pop() : new Body();
            b.Reset();
            b.id = nextId++;
            b.x = x; b.y = y;
            b.tier = tier;
            b.spawnT = spawnT;
            b.bornAt = Now;
            b.rot = RandRange(-0.15f, 0.15f);
            b.vrot = (Rand01() - 0.5f) * 2.4f * cfg.RotationAmount;
            b.Wake();                      // anchor the settle sample at the spawn point
            Bodies.Add(b);
            return b;
        }

        void Recycle(Body b) => bodyPool.Push(b);

        /// <summary>Drop the held pastry. Returns false while cooling down or folding shut.</summary>
        public bool Drop(float aimX, bool canDrop)
        {
            if (!canDrop || Now < canDropAt) return false;
            float r = TierTable.EffectiveRadius(CurTier, cfg.SizeScale);
            Body b = MakeBody(Mathf.Clamp(aimX, SimField.WL + r, SimField.WR - r),
                              SimField.DropY, CurTier, 1f);
            b.vy = cfg.DropVy;
            canDropAt = Now + cfg.DropCooldown;
            CurTier = NextTier;
            NextTier = Pick();
            return true;
        }

        /// <summary>Nearest body of the ordered tier within tolerance, or null.</summary>
        public Body ServableAt(float x, float y, int orderTier, float tolerance)
        {
            if (orderTier < 0) return null;
            Body best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.dead || b.tier != orderTier) continue;
                float dx = b.x - x, dy = b.y - y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < TierTable.Er(b, cfg.SizeScale) + tolerance && d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public void RemoveForServe(Body b)
        {
            b.dead = true;
            CompactDead();
        }

        /// <summary>Any live body at or below this tier — the clearance boost's deny check.</summary>
        public bool HasAnyUpToTier(int maxTier)
        {
            for (int i = 0; i < Bodies.Count; i++)
                if (!Bodies[i].dead && Bodies[i].tier <= maxTier) return true;
            return false;
        }

        /// <summary>
        /// Day-old clearance: despawn every body of tier &lt;= maxTier. Deliberately a
        /// despawn, NOT a merge — no Merged event fires, so no score and no boost charge
        /// can come from a purchased clear (spec 2026-08-04). The held pastry is never in
        /// Bodies, so it is exempt by construction. Fills <paramref name="removed"/> (when
        /// given) with each body's position and tier for the view's pop effects.
        /// </summary>
        public int RemoveUpToTier(int maxTier, List<(Vector2 pos, int tier)> removed = null)
        {
            int count = 0;
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.dead || b.tier > maxTier) continue;
                b.dead = true;
                count++;
                removed?.Add((new Vector2(b.x, b.y), b.tier));
            }
            if (count > 0) CompactDead();
            return count;
        }

        // ---------------------------------------------------------------- fx

        public void AddFloat(float x, float y, string text)
        {
            FloatText f = floatPool.Count > 0 ? floatPool.Pop() : new FloatText();
            f.x = x; f.y = y; f.t = 0f; f.text = text;
            Floats.Add(f);
        }

        // ---------------------------------------------------------------- shake

        /// <summary>Launch the pile to reshuffle it (§7.7).</summary>
        public void DoShake()
        {
            float p = cfg.ShakePower;
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                b.vy = -(190f + Rand01() * 190f) * p;
                b.vx = (Rand01() - 0.5f) * 350f * p;
                b.vrot += (Rand01() - 0.5f) * 3.2f * cfg.RotationAmount;
                b.squish = 0.2f;
                b.Wake();                  // the whole pile is in the air — none of it rests
            }
            ShakeUntil = Now + cfg.ShakeDuration;
            AddFloat(SimField.CX, 176f, "Shake!");
            Shaken?.Invoke();
        }

        /// <summary>
        /// Cloth wobble offset while shaking, in sim px. The mock writes 26 and 12 into a
        /// transform whose scale is 2, so the on-screen amplitude is half — hence 13 and 6.
        /// A literal port of those numbers shakes twice as hard as designed.
        /// </summary>
        public Vector2 ShakeOffset()
        {
            if (!Shaking) return Vector2.zero;
            float sh = (ShakeUntil - Now) / Mathf.Max(0.0001f, cfg.ShakeDuration);
            return new Vector2(Mathf.Sin(Now * 47f) * sh * 13f,
                               Mathf.Cos(Now * 39f) * sh * 6f);
        }

        // ---------------------------------------------------------------- reset

        /// <summary>Clear everything and re-seed the cloth (§7.9).</summary>
        public void ResetRun()
        {
            for (int i = 0; i < Bodies.Count; i++) bodyPool.Push(Bodies[i]);
            Bodies.Clear();
            for (int i = 0; i < Floats.Count; i++) floatPool.Push(Floats[i]);
            Floats.Clear();

            Now = 0f;
            ComboN = 0;
            lastMergeT = -999f;
            canDropAt = 0f;
            ShakeUntil = 0f;
            mergeLockUntil = StartMergeGraceSec;
            HighestDiscovered = 3;

            for (int t = 0; t < TierTable.Count; t++) discovered[t] = t <= 3;   // tiers 0–3 start discovered

            CurTier = Pick();
            NextTier = Pick();

            int n = Mathf.Max(0, cfg.StartingBodies);
            for (int i = 0; i < n; i++)
                MakeBody(60f + Rand01() * 300f, 300f - i * 34f, Pick(), 1f);
        }

        public void EmptyCloth()
        {
            for (int i = 0; i < Bodies.Count; i++) bodyPool.Push(Bodies[i]);
            Bodies.Clear();
        }

        public void RevealTier(int tier)
        {
            if (tier < 0 || tier >= TierTable.Count || discovered[tier]) return;
            discovered[tier] = true;
            if (tier > HighestDiscovered) HighestDiscovered = tier;
            TierDiscovered?.Invoke(tier, new Vector2(SimField.CX, 200f));
        }

        public void RelockCase()
        {
            for (int t = 0; t < TierTable.Count; t++) discovered[t] = t <= 3;
            HighestDiscovered = 3;
        }
    }
}
