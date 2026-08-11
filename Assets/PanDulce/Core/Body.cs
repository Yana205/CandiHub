namespace PanDulce.Core
{
    /// <summary>One pastry in the cloth. Plain data — pooled, never allocated per merge.</summary>
    public sealed class Body
    {
        public int id;
        public float x, y;
        public float vx, vy;
        public float rot, vrot;
        public int tier;

        /// <summary>Color track: 0 = Original, 1.. = skin tracks. Purely which art the
        /// dessert wears — same radius, same physics, and since 2026-08-11 no merge
        /// gate either: any colors of a tier merge. Same-color parents pass the color
        /// on (normalized to 0 where the next tier has no variant art); mixed parents
        /// roll a surprise color.</summary>
        public int skin;

        /// <summary>0→1 grow-in progress. Drops start at 1; merge products start at 0.</summary>
        public float spawnT;

        /// <summary>Impact flattening, decays each step.</summary>
        public float squish;

        /// <summary>Sim time this body came into existence — gates re-merging.</summary>
        public float bornAt;

        /// <summary>
        /// Something was holding this body up during the last substep — the cloth, or a
        /// body below it. Accumulated by the solver each substep, then consumed.
        /// </summary>
        public bool supported;

        /// <summary>Sim seconds since support was last seen, to ride out contact chatter.</summary>
        public float sinceSupport;

        /// <summary>Where the body was when the current settle sample opened, and how long
        /// ago that was. Rest is "hasn't gone anywhere", not "is moving slowly".</summary>
        public float restAnchorX, restAnchorY, restSampleT;

        /// <summary>Result of the last completed settle sample: it went nowhere.</summary>
        public bool wentNowhere;

        /// <summary>
        /// Settled: supported and going nowhere. The solver stops feeding this body spin
        /// and damps what it has, otherwise a resting pastry turns forever.
        /// </summary>
        public bool atRest;

        /// <summary>Touched a same-tier neighbour during the last substep — accumulated by
        /// the pair loop, consumed into <see cref="kinTouchT"/> at the top of the next.</summary>
        public bool kinTouch;

        /// <summary>Sim seconds of sustained same-tier contact, for the merge touch delay.</summary>
        public float kinTouchT;

        /// <summary>Sim seconds since kin contact was last seen — resting neighbours chatter
        /// around exact contact, so the touch timer forgives sub-quarter-second gaps
        /// instead of resetting on every flicker.</summary>
        public float kinGapT;

        /// <summary>Born from a merge (not dropped/spawned). Only these wait out the
        /// combo delay — the brake targets chain reactions, never a player's throw.</summary>
        public bool bornOfMerge;

        /// <summary>The current kin contact BEGAN with a real impact — a throw, a knock, a
        /// shake (relative speed past MergeSim.StrikeSpeed). Struck contacts merge on the
        /// fast lane (MergeTouchSec); contacts that drifted together at rest take the slow
        /// lane (IdleMergeSec). Latched for the life of the contact, like squeezed.</summary>
        public bool struck;

        /// <summary>The current kin contact was pressed past the merge-squeeze requirement
        /// at least once — latched for the life of the contact, cleared when it breaks.
        /// Pressure is an instant (a landing drop, pile weight, a shake); the touch timer
        /// is a duration. Without the latch the two knobs could never both be satisfied
        /// in the same frame, and squeeze>0 + touch>0 meant nothing ever merged.</summary>
        public bool squeezed;

        public bool dead;

        public void Reset()
        {
            x = y = vx = vy = rot = vrot = 0f;
            tier = 0;
            skin = 0;
            spawnT = 1f;
            squish = 0f;
            bornAt = 0f;
            supported = false;
            sinceSupport = 999f;
            restAnchorX = restAnchorY = restSampleT = 0f;
            wentNowhere = false;
            atRest = false;
            kinTouch = false;
            kinTouchT = 0f;
            kinGapT = 999f;
            bornOfMerge = false;
            struck = false;
            squeezed = false;
            dead = false;
        }

        /// <summary>
        /// Knock the body out of rest and restart its settle sample — for anything that
        /// should visibly move it again: a real impact, a shake, a fresh spawn.
        /// </summary>
        public void Wake()
        {
            atRest = false;
            wentNowhere = false;
            restSampleT = 0f;
            restAnchorX = x;
            restAnchorY = y;
        }
    }

    /// <summary>A rising label: "Combo 3!", "New in the case!", "Shake!".</summary>
    public sealed class FloatText
    {
        public float x, y, t;
        public string text;
    }
}
