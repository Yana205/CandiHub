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

    /// <summary>
    /// The §5.1 contain-fit, as pure arithmetic over screen dimensions.
    ///
    /// The stage lives in world space at a fixed scale of 1, so fitting is the camera's job:
    /// widen the orthographic frustum until the 446 × 900 safe box fits inside it. Scaling
    /// the stage transform instead would double-apply the mapping the camera already does —
    /// a screen-px/stage-px ratio is not a world-space scale factor.
    /// </summary>
    public static class StageFit
    {
        /// <summary>
        /// Screen px per stage px once the safe box is contained. This is the honest meaning
        /// of the ratio: it converts between the two pixel spaces, which is what
        /// <c>SafeAreaInset</c> needs when turning <c>Screen.safeArea</c> into stage px.
        /// </summary>
        public static float ScreenPxPerStagePx(int screenW, int screenH)
        {
            if (screenW <= 0 || screenH <= 0) return 1f;

            float s = Mathf.Min(screenH / StageCoords.SafeH, screenW / StageCoords.SafeW);
            return s > 0f && !float.IsNaN(s) ? s : 1f;
        }

        /// <summary>
        /// Orthographic half-height, in world units, that contains the safe box.
        ///
        /// Equivalent to <c>max(4.5, 2.23 / aspect)</c>: tall phones are width-limited and
        /// get vertical slack, which is what the backdrop bleed exists to fill.
        /// </summary>
        public static float OrthographicSize(int screenW, int screenH)
        {
            if (screenH <= 0) return StageCoords.SafeH * StageCoords.PX * 0.5f;

            return screenH / ScreenPxPerStagePx(screenW, screenH) * StageCoords.PX * 0.5f;
        }
    }
}
