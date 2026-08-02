using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The 11 tiers: names and base radii (§6.1). Effective radius = radius × sizeScale,
    /// recomputed only when sizeScale changes.
    /// </summary>
    public static class TierTable
    {
        public const int Count = 11;
        public const int Max = Count - 1;

        public static readonly string[] Names =
        {
            "Cookie", "Muffin", "Kiss Cookie", "Biscuit", "Turnover", "Bread Roll",
            "Cinnamon Roll", "Shell Bun", "Piggy Cookie", "Flan", "Ring Cake"
        };

        public static readonly float[] BaseRadius =
        {
            13f, 17f, 22f, 27f, 33f, 40f, 48f, 57f, 67f, 78f, 90f
        };

        /// <summary>The canonical author radius for sprites — see §8.1.</summary>
        public const float CanonicalSpriteRadius = 200f;

        public static float EffectiveRadius(int tier, float sizeScale)
            => BaseRadius[tier] * sizeScale;

        /// <summary>
        /// Radius during the merge pop, easing back-out from 0.35 → 1.0 of full size.
        /// Handoff §7.1 — ported literally, including the magic 1.70158.
        /// </summary>
        public static float Er(Body b, float sizeScale)
        {
            float t = Mathf.Min(1f, b.spawnT);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            return BaseRadius[b.tier] * sizeScale * (0.35f + 0.65f * e);
        }
    }
}
