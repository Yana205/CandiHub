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
        // Both start knobs default to the mock's behaviour (no delay, tiers 0–3 known) so
        // bare configs and tests are unaffected; the shipped values live in Tuning.asset.
        public float startDelaySec = 0f;
        public float startCalmSec = 0f;
        public float startCalmScale = 0.55f;
        public int startDiscovered = 4;
        public int discoverMerges = 1;
        public float entranceTime = 1.3f;
        public bool endOfDay = false;
        public bool boostsOn = true;
        public float shakePower = 1f;
        public float chargePerMerge = 0.14f;
        public bool soundOn = true;

        /// <summary>Which furoshiki swatch is active (view-only — not part of ISimConfig).</summary>
        public int clothColorIndex = 0;

        // Tier B — "Beyond the mock" (§6.3)
        public int substeps = 3;
        public float floorSag = 26f;
        // v2 layout: raised from SimField.FY (372) so the pile rests inside the drawn
        // candy box (Yana's play-mode placement, 2026-08-07).
        public float floorY = 250f;
        // Defaults are the mock's walls (SimField.WL/WR) so tests reproduce it exactly;
        // the v2 values that hug the drawn candy box live in Tuning.asset.
        public float wallLeft = SimField.WL;
        public float wallRight = SimField.WR;
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

        // Rhythm & difficulty (2026-08-08) — all default to the classic instant-merge feel,
        // so a fresh config still reproduces the mock exactly.
        public float mergeOverlapPct = 0f;
        public float mergeTouchSec = 0f;
        public float idleMergeSec = 0f;
        public float kinPull = 0f;

        // The deal (2026-08-09) — mock defaults; the shipped 20–30s-run values (bias 2,
        // 12s big-deal wait) live in Tuning.asset.
        public float spawnBias = 1f;
        public float bigDealDelaySec = 0f;
        public int dealTopMargin = 0;

        // Economy — coins from serves buy boosts (spec 2026-08-04). A tier-2..5 order
        // pays 11..20, so the 30-coin clearance costs about two serves.
        public int coinBase = 5;
        public int coinPerTier = 3;
        public int clearanceCost = 30;

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

        /// <summary>
        /// Per-tier size fractions. Null (the default) means every tier is at 100%, which keeps
        /// a bare SimConfigData reproducing the mock — the real values come from Pastries.asset
        /// via TuningConfig, so they can travel with the sprites on a chain reorder.
        /// </summary>
        public float[] tierSize = null;

        public float Gravity => gravity;
        public float Bounciness => bounciness;
        public float SizeScale => sizeScale;
        public float RotationAmount => rotationAmount;
        public float MergeGrowTime => mergeGrowTime;
        public float ComboDelay => comboDelay;
        public int CustomerEverySec => customerEverySec;
        public float StartDelaySec => startDelaySec;
        public float StartCalmSec => startCalmSec;
        public float StartCalmScale => startCalmScale;
        public int StartDiscovered => startDiscovered;
        public int DiscoverMerges => discoverMerges;
        public float EntranceTime => entranceTime;
        public bool EndOfDay => endOfDay;
        public bool BoostsOn => boostsOn;
        public float ShakePower => shakePower;
        public float ChargePerMerge => chargePerMerge;
        public bool SoundOn => soundOn;

        public int Substeps => substeps;
        public float FloorSag => floorSag;
        public float FloorY => floorY;
        public float WallLeft => wallLeft;
        public float WallRight => wallRight;
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

        public float MergeOverlapPct => mergeOverlapPct;
        public float MergeTouchSec => mergeTouchSec;
        public float IdleMergeSec => idleMergeSec;
        public float KinPull => kinPull;
        public float SpawnBias => spawnBias;
        public float BigDealDelaySec => bigDealDelaySec;
        public int DealTopMargin => dealTopMargin;

        public int CoinBase => coinBase;
        public int CoinPerTier => coinPerTier;
        public int ClearanceCost => clearanceCost;

        public bool TopOut => topOut;
        public float TopOutLine => topOutLine;
        public float TopOutGrace => topOutGrace;
        public bool ShowDangerLine => showDangerLine;

        public float TimeScale => timeScale;
        public bool Paused => paused;

        public float TierSize(int tier)
            => (tierSize != null && tier >= 0 && tier < tierSize.Length && tierSize[tier] > 0f)
               ? tierSize[tier] : 1f;
    }
}
