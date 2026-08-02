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

        public bool dead;

        public void Reset()
        {
            x = y = vx = vy = rot = vrot = 0f;
            tier = 0;
            spawnT = 1f;
            squish = 0f;
            bornAt = 0f;
            dead = false;
        }
    }

    /// <summary>A merge/serve puff or spark. The sim owns particle motion, not Unity.</summary>
    public sealed class Particle
    {
        public float x, y, vx, vy;
        public float t, life, r;
        public bool star;   // sparks draw as a '+', puffs as a disc
    }

    /// <summary>A rising label: "Combo 3!", "New in the case!", "Shake!".</summary>
    public sealed class FloatText
    {
        public float x, y, t;
        public string text;
    }
}
