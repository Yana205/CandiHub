namespace PanDulce.Core
{
    /// <summary>
    /// A plain mutable ISimConfig. Tests use it directly; the TuningConfig ScriptableObject
    /// holds one and forwards to it, which keeps the defaults in exactly one place.
    ///
    /// Every default here MUST equal the mock's constant so a fresh config reproduces the
    /// mock exactly — that is the whole point of the tuning workflow (§6.3).
    /// </summary>
    [System.Serializable]
    public sealed class SimConfigData : ISimConfig
    {
        // Tier A — the mock's props (§6.2)
        public float gravity = 1500f;
        public float bounciness = 0.08f;
        public float sizeScale = 1.3f;
        public float rotationAmount = 0.2f;
        public float mergeGrowTime = 0.85f;
        public float comboDelay = 0.5f;
        public int customerEverySec = 18;
        public EntranceStyle entranceStyle = EntranceStyle.Walk;
        public float entranceTime = 0.7f;
        public bool endOfDay = false;
        public bool boostsOn = true;
        public float shakePower = 1f;
        public float chargePerMerge = 0.14f;
        public bool soundOn = true;

        // Tier B — "Beyond the mock" (§6.3)
        public int substeps = 3;
        public float floorSag = 26f;
        public float centerPull = 34f;
        public float groundFriction = 9f;
        public float comboWindow = 1.4f;
        public float mergePopVy = -70f;
        public float squishAmount = 1f;
        public float particleScale = 1f;
        public float dropCooldown = 0.5f;
        public float dropVy = 60f;
        public float flySec = 0.9f;
        public float happyMs = 1400f;
        public int startingBodies = 9;
        public float shakeDuration = 0.6f;

        // Challenge — ported from Docs/web-reference (§13 decision).
        // topOutLine is re-anchored from the reference's 92: that was 14% down a
        // DROP_Y 36 → FLOOR 442 column, and this frame's column is 36 → 372.
        public bool topOut = true;
        public float topOutLine = 82f;
        public float topOutGrace = 2.2f;
        public bool showDangerLine = true;

        // Editor-only
        public float timeScale = 1f;
        public bool paused = false;

        public float Gravity => gravity;
        public float Bounciness => bounciness;
        public float SizeScale => sizeScale;
        public float RotationAmount => rotationAmount;
        public float MergeGrowTime => mergeGrowTime;
        public float ComboDelay => comboDelay;
        public int CustomerEverySec => customerEverySec;
        public EntranceStyle EntranceStyle => entranceStyle;
        public float EntranceTime => entranceTime;
        public bool EndOfDay => endOfDay;
        public bool BoostsOn => boostsOn;
        public float ShakePower => shakePower;
        public float ChargePerMerge => chargePerMerge;
        public bool SoundOn => soundOn;

        public int Substeps => substeps;
        public float FloorSag => floorSag;
        public float CenterPull => centerPull;
        public float GroundFriction => groundFriction;
        public float ComboWindow => comboWindow;
        public float MergePopVy => mergePopVy;
        public float SquishAmount => squishAmount;
        public float ParticleScale => particleScale;
        public float DropCooldown => dropCooldown;
        public float DropVy => dropVy;
        public float FlySec => flySec;
        public float HappyMs => happyMs;
        public int StartingBodies => startingBodies;
        public float ShakeDuration => shakeDuration;

        public bool TopOut => topOut;
        public float TopOutLine => topOutLine;
        public float TopOutGrace => topOutGrace;
        public bool ShowDangerLine => showDangerLine;

        public float TimeScale => timeScale;
        public bool Paused => paused;
    }
}
