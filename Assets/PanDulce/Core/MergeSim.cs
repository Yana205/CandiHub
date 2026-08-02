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
        public event Action<int> TierDiscovered;
        public event Action Shaken;

        public readonly List<Body> Bodies = new List<Body>(64);
        public readonly List<Particle> Particles = new List<Particle>(160);
        public readonly List<FloatText> Floats = new List<FloatText>(16);

        readonly Stack<Body> bodyPool = new Stack<Body>(64);
        readonly Stack<Particle> particlePool = new Stack<Particle>(160);
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

            // 1 — integrate
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
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

                    // merge gate — BOTH grown past 0.55 AND older than comboDelay
                    if (a.tier == c.tier && a.tier < TierTable.Max &&
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

                    float rvx = c.vx - a.vx, rvy = c.vy - a.vy;
                    float vn = rvx * nx + rvy * ny;
                    if (vn < 0f)
                    {
                        float jm = -(1f + e) * vn / (1f / ma + 1f / mc);
                        a.vx -= jm * nx / ma; a.vy -= jm * ny / ma;
                        c.vx += jm * nx / mc; c.vy += jm * ny / mc;
                        float q = Mathf.Min(0.28f, Mathf.Abs(vn) / 1500f) * cfg.SquishAmount;
                        if (q > 0.05f) { a.squish = Mathf.Max(a.squish, q); c.squish = Mathf.Max(c.squish, q); }
                        float vt = rvx * -ny + rvy * nx, rk = 0.15f * rotAmt;
                        a.vrot += (vt / ra) * rk; c.vrot += (vt / rc) * rk;
                    }
                }
            }

            // 3 — walls, and THE CURVED CLOTH FLOOR (the signature of this design)
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.dead) continue;
                float r = TierTable.Er(b, sizeScale);

                if (b.x - r < SimField.WL)
                {
                    b.x = SimField.WL + r;
                    b.vx = Mathf.Abs(b.vx) * e;
                    b.vrot = -b.vy / r * 0.4f * rotAmt;
                }
                if (b.x + r > SimField.WR)
                {
                    b.x = SimField.WR - r;
                    b.vx = -Mathf.Abs(b.vx) * e;
                    b.vrot = b.vy / r * 0.4f * rotAmt;
                }

                float fy = SimField.FloorAt(b.x, cfg.FloorSag);
                if (b.y + r > fy)
                {
                    float vi = b.vy;
                    b.y = fy - r;
                    b.vy = -Mathf.Abs(b.vy) * e * 0.6f;
                    if (Mathf.Abs(b.vy) < 20f) b.vy = 0f;
                    b.vx *= (1f - cfg.GroundFriction * dt);                              // friction
                    b.vx -= ((b.x - SimField.CX) / SimField.HW) * cfg.CenterPull * dt;   // roll to the middle
                    b.vrot += (b.vx / r - b.vrot) * Mathf.Min(0.4f, 0.05f + 0.5f * rotAmt);
                    if (vi > 180f)
                        b.squish = Mathf.Max(b.squish, Mathf.Min(0.3f, vi / 1600f) * cfg.SquishAmount);
                }
            }

            // 4 — apply merges, drop dead bodies, age particles and text
            for (int i = 0; i < mergeBuffer.Count; i++) ApplyMerge(mergeBuffer[i].a, mergeBuffer[i].c);
            CompactDead();
            AgeParticles(dt);
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

        void AgeParticles(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle p = Particles[i];
                p.t += dt;
                if (p.t >= p.life)
                {
                    particlePool.Push(p);
                    Particles[i] = Particles[Particles.Count - 1];
                    Particles.RemoveAt(Particles.Count - 1);
                    continue;
                }
                p.vx *= 0.96f;
                p.vy *= 0.96f;
                p.vy -= (p.star ? 20f : 60f) * dt;   // lift
                p.x += p.vx * dt;
                p.y += p.vy * dt;
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

            Burst(x, y, TierTable.BaseRadius[t2]);

            ComboN = (Now - lastMergeT < cfg.ComboWindow) ? ComboN + 1 : 1;
            lastMergeT = Now;
            if (ComboN >= 2) AddFloat(x, y - TierTable.BaseRadius[t2] - 8f, $"Combo {ComboN}!");

            Merged?.Invoke(t2, new Vector2(x, y), ComboN);

            if (!discovered[t2])
            {
                discovered[t2] = true;
                if (t2 > HighestDiscovered) HighestDiscovered = t2;
                AddFloat(x, y - TierTable.BaseRadius[t2] - 26f, "New in the case!");
                TierDiscovered?.Invoke(t2);
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
            Burst(b.x, b.y, TierTable.BaseRadius[b.tier]);
            b.dead = true;
            CompactDead();
        }

        // ---------------------------------------------------------------- fx

        /// <summary>9 puffs + 6 sparks, per merge and per serve (§7.5).</summary>
        public void Burst(float x, float y, float r)
        {
            float k = cfg.ParticleScale;
            int nA = Mathf.RoundToInt(9f * k), nB = Mathf.RoundToInt(6f * k);

            for (int i = 0; i < nA; i++)
            {
                float a = Rand01() * 6.28f, s = 60f + Rand01() * 110f;
                Particle p = NewParticle();
                p.x = x + Mathf.Cos(a) * r * 0.5f;
                p.y = y + Mathf.Sin(a) * r * 0.5f;
                p.vx = Mathf.Cos(a) * s; p.vy = Mathf.Sin(a) * s;
                p.t = 0f; p.life = 0.5f + Rand01() * 0.25f;
                p.r = 4f + Rand01() * 5f; p.star = false;
            }
            for (int i = 0; i < nB; i++)
            {
                float a = Rand01() * 6.28f, s = 90f + Rand01() * 130f;
                Particle p = NewParticle();
                p.x = x; p.y = y;
                p.vx = Mathf.Cos(a) * s; p.vy = Mathf.Sin(a) * s;
                p.t = 0f; p.life = 0.6f;
                p.r = 3f + Rand01() * 3f; p.star = true;
            }
        }

        Particle NewParticle()
        {
            Particle p = particlePool.Count > 0 ? particlePool.Pop() : new Particle();
            Particles.Add(p);
            return p;
        }

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
            for (int i = 0; i < Particles.Count; i++) particlePool.Push(Particles[i]);
            Particles.Clear();
            for (int i = 0; i < Floats.Count; i++) floatPool.Push(Floats[i]);
            Floats.Clear();

            Now = 0f;
            ComboN = 0;
            lastMergeT = -999f;
            canDropAt = 0f;
            ShakeUntil = 0f;
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
            TierDiscovered?.Invoke(tier);
        }

        public void RelockCase()
        {
            for (int t = 0; t < TierTable.Count; t++) discovered[t] = t <= 3;
            HighestDiscovered = 3;
        }
    }
}
