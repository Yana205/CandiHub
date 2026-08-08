namespace PanDulce.Core
{
    /// <summary>
    /// The read-only knob interface the sim consumes. This is the seam that keeps Core
    /// Unity-free: the live TuningConfig ScriptableObject implements it, and tests pass
    /// a plain struct. Keys match the mock's prop names so configs round-trip (§10.2).
    /// </summary>
    public interface ISimConfig
    {
        // --- Tier A: the mock's props (§6.2) ---
        float Gravity { get; }
        float Bounciness { get; }
        float SizeScale { get; }
        float RotationAmount { get; }
        float MergeGrowTime { get; }
        float ComboDelay { get; }
        int CustomerEverySec { get; }
        /// <summary>Extra calm seconds added before the FIRST customer of a run only.</summary>
        float StartDelaySec { get; }
        /// <summary>How long the opening calm lasts: the run's first seconds play slowed,
        /// easing back to full tempo. 0 = off.</summary>
        float StartCalmSec { get; }
        /// <summary>Tempo at the very first moment of the calm, as a fraction of full
        /// speed (0.55 = 55%). Eases to 1 over StartCalmSec.</summary>
        float StartCalmScale { get; }
        /// <summary>How many tiers begin discovered — in colour, spawnable, orderable.</summary>
        int StartDiscovered { get; }
        float EntranceTime { get; }
        bool EndOfDay { get; }
        bool BoostsOn { get; }
        float ShakePower { get; }
        float ChargePerMerge { get; }
        bool SoundOn { get; }

        // --- Tier B: hard-coded in the mock, exposed here (§6.3) ---
        int Substeps { get; }
        float FloorSag { get; }
        float FloorY { get; }
        float CenterPull { get; }
        float GroundFriction { get; }
        float ComboWindow { get; }
        float MergePopVy { get; }
        float SquishAmount { get; }
        float ParticleScale { get; }
        float DropCooldown { get; }
        float DropVy { get; }
        float FlySec { get; }
        float HappyMs { get; }
        int StartingBodies { get; }
        float ShakeDuration { get; }

        // --- Rhythm & difficulty (2026-08-08): how deliberate a merge has to be ---

        /// <summary>Overlap depth required to merge, as a fraction of the smaller dessert's
        /// radius. 0 = a graze merges instantly (classic); higher demands a real squeeze —
        /// a landing drop, the pile's weight, or a shake.</summary>
        float MergeOverlapPct { get; }

        /// <summary>How long two matching desserts must stay in contact before they may
        /// merge. 0 = instant (classic).</summary>
        float MergeTouchSec { get; }

        /// <summary>Acceleration (sim px/s²) pulling matching desserts toward each other
        /// when they are within about a diameter. 0 = off (classic).</summary>
        float KinPull { get; }

        // --- Economy: coins from serves buy boosts (spec 2026-08-04) ---
        int CoinBase { get; }
        int CoinPerTier { get; }
        int ClearanceCost { get; }

        // --- Challenge layer, ported from Docs/web-reference (§13 decision) ---
        bool TopOut { get; }
        float TopOutLine { get; }
        float TopOutGrace { get; }
        bool ShowDangerLine { get; }

        // --- Editor-only ---
        float TimeScale { get; }
        bool Paused { get; }

        /// <summary>
        /// Per-dessert size, as a fraction of the tier's authored size (1 = 100%). Multiplies
        /// on top of <see cref="SizeScale"/> and reaches BOTH the drawn sprite and the physics
        /// circle, so enlarging a dessert also enlarges the room it takes in the pile.
        ///
        /// It is a method, not an array, because the values live with the sprites in
        /// Pastries.asset — they travel with a dessert when Studio reorders the merge chain.
        /// </summary>
        float TierSize(int tier);
    }
}
