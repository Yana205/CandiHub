using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The 11 tiers: names and base radii (§6.1).
    ///
    /// Effective radius = BaseRadius × sizeScale × tierSize. Both multipliers are fractions of
    /// the authored size, never absolute pixel counts: BaseRadius keeps the chain's proportions,
    /// sizeScale scales the whole pile, tierSize scales one dessert. Every radius the game asks
    /// for goes through here, so art and collision can never disagree about how big something is.
    ///
    /// The tierSize parameters default to 1 so the tests and any call site that has no database
    /// in reach keep reading the plain tier radius.
    /// </summary>
    public static class TierTable
    {
        public const int Count = 11;
        public const int Max = Count - 1;

        public static readonly string[] Names =
        {
            "Mochi", "Matcha Mochi", "Mango Mochi", "Purin", "Berry Donut", "Matcha Donut",
            "Choco Donut", "Roll Cake", "Sakura Pan", "Honey Pan", "Melon Pan"
        };

        public static readonly float[] BaseRadius =
        {
            13f, 17f, 22f, 27f, 33f, 40f, 48f, 57f, 67f, 78f, 90f
        };

        /// <summary>The canonical author radius for sprites — see §8.1.</summary>
        public const float CanonicalSpriteRadius = 200f;

        public static float EffectiveRadius(int tier, float sizeScale, float tierSize = 1f)
            => BaseRadius[tier] * sizeScale * tierSize;

        /// <summary>Effective radius with both multipliers read off the live config.</summary>
        public static float EffectiveRadius(int tier, ISimConfig cfg)
            => EffectiveRadius(tier, cfg.SizeScale, cfg.TierSize(tier));

        /// <summary>
        /// Radius during the merge pop, easing back-out from 0.35 → 1.0 of full size.
        /// Handoff §7.1 — ported literally, including the magic 1.70158.
        /// </summary>
        public static float Er(Body b, float sizeScale, float tierSize = 1f)
        {
            float t = Mathf.Min(1f, b.spawnT);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            return BaseRadius[b.tier] * sizeScale * tierSize * (0.35f + 0.65f * e);
        }

        /// <summary>Popping radius with both multipliers read off the live config.</summary>
        public static float Er(Body b, ISimConfig cfg)
            => Er(b, cfg.SizeScale, cfg.TierSize(b.tier));
    }
}
