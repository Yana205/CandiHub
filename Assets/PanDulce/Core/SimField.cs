namespace PanDulce.Core
{
    /// <summary>
    /// The play-canvas geometry, in sim pixels, y-down. Handoff §5.
    /// Every tuning constant in the design is expressed in these units — converting
    /// to metres would silently invalidate all of them, so the sim never leaves them.
    /// </summary>
    public static class SimField
    {
        public const float CW = 418f;   // play canvas width
        public const float CH = 400f;   // play canvas height

        public const float WL = 34f;    // left wall
        public const float WR = 384f;   // right wall
        public const float FY = 372f;   // nominal floor line

        public const float BL = 26f;    // cloth left edge
        public const float BR = 392f;   // cloth right edge

        public const float DropY = 36f; // spawn height

        /// <summary>Cloth centre — (BL + BR) / 2.</summary>
        public const float CX = (BL + BR) * 0.5f;    // 209

        /// <summary>Cloth half-width — (BR - BL) / 2.</summary>
        public const float HW = (BR - BL) * 0.5f;    // 183

        /// <summary>1 sim px = 0.01 world units, i.e. PPU 100.</summary>
        public const float PX = 0.01f;

        /// <summary>
        /// The sagging cloth floor: rises by floorSag at the edges, flat at the centre.
        /// This curve is the signature of the design — it makes the pile settle into a
        /// bowl instead of a stack.
        /// </summary>
        public static float FloorAt(float x, float floorSag)
        {
            float k = UnityEngine.Mathf.Min(1f, UnityEngine.Mathf.Abs(x - CX) / HW);
            return FY - floorSag * k * k;
        }
    }
}
