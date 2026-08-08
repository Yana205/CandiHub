using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The opening-calm tempo ramp: a run's first seconds play in gentle slow motion and
    /// ease back up to full speed, so the starting pile settles dreamily while the
    /// silhouettes in the case do the talking. GameRoot multiplies the frame's dt by this,
    /// which slows physics, merges and the customer clock together while pointer input and
    /// real-time UI stay snappy.
    /// </summary>
    public static class StartCalm
    {
        /// <summary>
        /// Tempo multiplier at <paramref name="elapsedSec"/> real seconds into the run.
        /// Smoothstepped so full speed arrives without an audible gear change; calmScale
        /// is floored at 0.1 so a wild config can never freeze the shop.
        /// </summary>
        public static float Scale(float elapsedSec, float calmSec, float calmScale)
        {
            if (calmSec <= 0f || calmScale >= 1f) return 1f;
            calmScale = Mathf.Max(0.1f, calmScale);
            float t = Mathf.Clamp01(elapsedSec / calmSec);
            t = t * t * (3f - 2f * t);
            return calmScale + (1f - calmScale) * t;
        }
    }
}
