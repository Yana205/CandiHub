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

        public bool dead;

        public void Reset()
        {
            x = y = vx = vy = rot = vrot = 0f;
            tier = 0;
            spawnT = 1f;
            squish = 0f;
            bornAt = 0f;
            supported = false;
            sinceSupport = 999f;
            restAnchorX = restAnchorY = restSampleT = 0f;
            wentNowhere = false;
            atRest = false;
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
