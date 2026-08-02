using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The bridge between the sim's canvas pixels (y-down) and Unity world units (y-up).
    ///
    /// The sim stays in canvas px because every tuning constant in the design is expressed
    /// in them. Only rendering flips — sprites are authored as they appear on screen, so
    /// the flip applies to positions, not art (§5).
    /// </summary>
    public static class StageCoords
    {
        /// <summary>The design frame: 430 × 880 stage px.</summary>
        public const float StageW = 430f;
        public const float StageH = 880f;

        /// <summary>The play canvas sits at stage (6, 424), so that is the sim origin.</summary>
        public const float PlayOriginX = 6f;
        public const float PlayOriginY = 424f;

        /// <summary>1 stage/sim px = 0.01 world units (PPU 100).</summary>
        public const float PX = 0.01f;

        /// <summary>Safe box used by the fitter — 446 × 900 around the 430 × 880 frame.</summary>
        public const float SafeW = 446f;
        public const float SafeH = 900f;

        /// <summary>Stage-local px (y-down from top-left) → local world units under the stage root.</summary>
        public static Vector3 Stage(float x, float y, float z = 0f)
            => new Vector3(x * PX, -y * PX, z);

        public static Vector3 Stage(Vector2 v) => Stage(v.x, v.y);

        /// <summary>Sim px → stage px.</summary>
        public static Vector2 SimToStage(Vector2 sim)
            => new Vector2(PlayOriginX + sim.x, PlayOriginY + sim.y);

        /// <summary>Stage px → sim px.</summary>
        public static Vector2 StageToSim(Vector2 stage)
            => new Vector2(stage.x - PlayOriginX, stage.y - PlayOriginY);

        public static float ToWorld(float stagePx) => stagePx * PX;

        /// <summary>
        /// Canvas +rotation is clockwise on screen (y-down); Unity +z is counter-clockwise.
        /// </summary>
        public static float RotationDegrees(float bodyRotRadians)
            => -bodyRotRadians * Mathf.Rad2Deg;
    }
}
