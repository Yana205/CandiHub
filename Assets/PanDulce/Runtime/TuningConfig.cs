using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The live tuning asset. GameRoot reads it every frame, which is what makes a Tweaks
    /// window edit apply on the next frame with no restart.
    ///
    /// Edits persist past play mode by design — a tuning session is a committable diff on
    /// Tuning.asset (§10.4).
    /// </summary>
    [CreateAssetMenu(fileName = "Tuning", menuName = "Pan Dulce/Tuning Config")]
    public sealed class TuningConfig : ScriptableObject, ISimConfig
    {
        [SerializeField] SimConfigData data = new SimConfigData();

        [Tooltip("Where per-dessert sizes come from. They live with the sprites so they follow " +
                 "a dessert when Studio reorders the merge chain.")]
        [SerializeField] PastryDatabase pastries;

        /// <summary>Direct access for the Tweaks window's schema delegates.</summary>
        public SimConfigData Data => data;

        /// <summary>Editor-time wiring, from StageBuilder — see TierSize.</summary>
        public void EditorAssign(PastryDatabase db) => pastries = db;

        public void ResetToMockDefaults() => data = new SimConfigData();

        public float Gravity => data.Gravity;
        public float Bounciness => data.Bounciness;
        public float SizeScale => data.SizeScale;
        public float RotationAmount => data.RotationAmount;
        public float MergeGrowTime => data.MergeGrowTime;
        public float ComboDelay => data.ComboDelay;
        public int CustomerEverySec => data.CustomerEverySec;
        public float StartDelaySec => data.StartDelaySec;
        public float StartCalmSec => data.StartCalmSec;
        public float StartCalmScale => data.StartCalmScale;
        public int StartDiscovered => data.StartDiscovered;
        public int DiscoverMerges => data.DiscoverMerges;
        public float EntranceTime => data.EntranceTime;
        public bool EndOfDay => data.EndOfDay;
        public bool BoostsOn => data.BoostsOn;
        public float ShakePower => data.ShakePower;
        public float ChargePerMerge => data.ChargePerMerge;
        public bool SoundOn => data.SoundOn;
        public int ClothColorIndex => data.clothColorIndex;

        public int Substeps => data.Substeps;
        public float FloorSag => data.FloorSag;
        public float FloorY => data.FloorY;
        public float WallLeft => data.WallLeft;
        public float WallRight => data.WallRight;
        public float CenterPull => data.CenterPull;
        public float GroundFriction => data.GroundFriction;
        public float ComboWindow => data.ComboWindow;
        public float MergePopVy => data.MergePopVy;
        public float SquishAmount => data.SquishAmount;
        public float ParticleScale => data.ParticleScale;
        public float DropCooldown => data.DropCooldown;
        public float DropVy => data.DropVy;
        public float FlySec => data.FlySec;
        public float HappyMs => data.HappyMs;
        public int StartingBodies => data.StartingBodies;
        public float ShakeDuration => data.ShakeDuration;

        public float MergeOverlapPct => data.MergeOverlapPct;
        public float MergeTouchSec => data.MergeTouchSec;
        public float IdleMergeSec => data.IdleMergeSec;
        public float KinPull => data.KinPull;
        public bool FirstMergeInstant => data.FirstMergeInstant;
        public float SpawnBias => data.SpawnBias;
        public float BigDealDelaySec => data.BigDealDelaySec;
        public int DealTopMargin => data.DealTopMargin;

        public int CoinBase => data.CoinBase;
        public int CoinPerTier => data.CoinPerTier;
        public int ClearanceCost => data.ClearanceCost;

        public bool TopOut => data.TopOut;
        public float TopOutLine => data.TopOutLine;
        public float TopOutGrace => data.TopOutGrace;
        public bool ShowDangerLine => data.ShowDangerLine;

        public float TimeScale => data.TimeScale;
        public bool Paused => data.Paused;

        /// <summary>
        /// Per-dessert size. Read from Pastries.asset when it is bound — that keeps ONE number
        /// behind both the sprite and the physics circle, which is the whole point: a dessert
        /// enlarged in Studio also claims more room in the pile. Falls back to the plain config
        /// (all 100%) when unbound, so a bare Tuning.asset still runs.
        /// </summary>
        public float TierSize(int tier)
            => pastries != null ? pastries.TierSize(tier) : data.TierSize(tier);
    }
}
