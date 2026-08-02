using System.Collections.Generic;
using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The fail state, ported verbatim from Docs/web-reference/js/game.js checkTopOut().
    ///
    /// The three filters below are what make this fair rather than infuriating: a pastry
    /// that is still growing in, freshly born, or moving fast cannot end the run. Only a
    /// body that has *settled* above the line counts, and the timer drains at 2× when the
    /// pile drops back — so a brief spike is survivable.
    /// </summary>
    public sealed class TopOutWatch
    {
        public const float SettledAge = 1.2f;      // a body younger than this is ignored
        public const float SettledSpeed = 90f;     // |vy| above this means it is still moving

        /// <summary>How long the pile has been over the line, in seconds.</summary>
        public float DangerT { get; private set; }

        /// <summary>True once the line has been crossed long enough to blink the warning.</summary>
        public bool Blinking => DangerT > 0.25f;

        /// <summary>Fraction of the grace period elapsed, 0→1. For UI.</summary>
        public float DangerFraction(float grace) => grace <= 0f ? 0f : Mathf.Clamp01(DangerT / grace);

        /// <summary>Advances the watch. Returns true on the frame the run should end.</summary>
        public bool Tick(float dt, IReadOnlyList<Body> bodies, float now, ISimConfig cfg)
        {
            if (!cfg.TopOut) { DangerT = 0f; return false; }

            float line = cfg.TopOutLine;
            bool over = false;

            for (int i = 0; i < bodies.Count; i++)
            {
                Body b = bodies[i];
                if (b.dead) continue;
                if (b.spawnT < 1f) continue;                        // still popping in
                if (now - b.bornAt < SettledAge) continue;          // too young to blame
                if (Mathf.Abs(b.vy) > SettledSpeed) continue;       // still in motion
                if (b.y - TierTable.Er(b, cfg.SizeScale) < line) { over = true; break; }
            }

            if (over)
            {
                DangerT += dt;
                if (DangerT > cfg.TopOutGrace) return true;
            }
            else
            {
                DangerT = Mathf.Max(0f, DangerT - dt * 2f);
            }

            return false;
        }

        public void Reset() => DangerT = 0f;
    }
}
