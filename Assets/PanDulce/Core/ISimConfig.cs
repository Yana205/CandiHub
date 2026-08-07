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
    }
}
