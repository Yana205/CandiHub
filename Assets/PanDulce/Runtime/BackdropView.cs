using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The two strips of ground above and below the painted layout (LayoutArt prefab).
    ///
    /// The art canvas covers stage y −51 … 749; the 430 × 880 frame is taller than that and
    /// letterbox slack on real phone aspects adds more. Sideways the layout fills its own
    /// slack — its wall and floor pieces carry a smeared twin (see Version2ArtImport) — but
    /// nothing extends it vertically, so this view closes the top and bottom with the wall's
    /// pink and the floor's brown. Without it the overhang shows the camera's clear colour,
    /// which reads as a bug rather than as a room.
    ///
    /// Each strip is tinted to the exact pixel the canvas ends on, so the join is invisible
    /// by construction rather than by eye.
    /// </summary>
    public sealed class BackdropView : GeneratedView
    {
        const float BleedX = 200f;   // per side
        const float BleedY = 260f;   // above and below the frame

        /// <summary>Stage y of the art canvas top and bottom edge (800 px tall, offset −51).</summary>
        const float CanvasTop = -51f;
        const float CanvasBottom = 749f;

        // Sampled from the outermost row of BG/background.png and BG/desk.png.
        static readonly Color WallTop = Palette.Hex("#fcb2ac");
        static readonly Color FloorBottom = Palette.Hex("#7e4e2f");

        protected override void Build()
        {
            float w = 430f + BleedX * 2f;
            float top = -BleedY, bottom = 880f + BleedY;

            ViewFactory.Rect(Content, "WallOverhang", Shapes.White,
                             -BleedX, top, w, CanvasTop - top, WallTop, "Background", 0);
            ViewFactory.Rect(Content, "FloorOverhang", Shapes.White,
                             -BleedX, CanvasBottom, w, bottom - CanvasBottom, FloorBottom, "Background", 0);
        }
    }
}
