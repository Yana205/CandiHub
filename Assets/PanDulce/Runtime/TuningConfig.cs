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

        /// <summary>Direct access for the Tweaks window's schema delegates.</summary>
        public SimConfigData Data => data;

        public void ResetToMockDefaults() => data = new SimConfigData();

        public float Gravity => data.Gravity;
        public float Bounciness => data.Bounciness;
        public float SizeScale => data.SizeScale;
        public float RotationAmount => data.RotationAmount;
        public float MergeGrowTime => data.MergeGrowTime;
        public float ComboDelay => data.ComboDelay;
        public int CustomerEverySec => data.CustomerEverySec;
        public float EntranceTime => data.EntranceTime;
        public bool EndOfDay => data.EndOfDay;
        public bool BoostsOn => data.BoostsOn;
        public float ShakePower => data.ShakePower;
        public float ChargePerMerge => data.ChargePerMerge;
        public bool SoundOn => data.SoundOn;
        public int ClothColorIndex => data.clothColorIndex;

        public int Substeps => data.Substeps;
        public float FloorSag => data.FloorSag;
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

        public bool TopOut => data.TopOut;
        public float TopOutLine => data.TopOutLine;
        public float TopOutGrace => data.TopOutGrace;
        public bool ShowDangerLine => data.ShowDangerLine;

        public float TimeScale => data.TimeScale;
        public bool Paused => data.Paused;
    }
}
