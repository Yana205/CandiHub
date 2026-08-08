using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The 5 gameplay tiers: names and base radii (§6.1, v2 chain).
    ///
    /// Color variants (Matcha Mochi, Berry Donut, …) are NOT tiers — they are skin tracks
    /// inside PastryDatabase that swap a tier's art without touching its radius. Names here
    /// are the Original track's fallbacks; PastryDatabase.Name() may override per skin.
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
        public const int Count = 5;
        public const int Max = Count - 1;

        public static readonly string[] Names =
        {
            "Mochi", "Purin", "Melon Pan", "Choco Donut", "Roll Cake"
        };

        // Geometric 13 → 43.2 over 4 merges (×1.35 each). The top matches the footprint the
        // old chain's hand-tuned Melon Pan actually used on screen (~69 px effective at the
        // live sizeScale 1.59) — the 350 px field cannot rest a pile of anything much bigger.
        public static readonly float[] BaseRadius =
        {
            13f, 17.6f, 23.7f, 32f, 43.2f
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
