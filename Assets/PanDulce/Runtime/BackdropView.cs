using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// V2 layout: a plain paper-white ground behind the hand-drawn lineart (LayoutArt
    /// prefab). The old procedural shop — wall, window, counter, drawers — is replaced by
    /// that art; only the bled backdrop panel remains so letterbox slack on real phone
    /// aspects shows paper rather than flat #cfa06b bars, which would read as a bug.
    /// </summary>
    public sealed class BackdropView : GeneratedView
    {
        const float BleedX = 120f;   // per side
        const float BleedY = 150f;   // above and below the frame

        protected override void Build()
        {
            ViewFactory.Rect(Content, "Paper", Shapes.White,
                             -BleedX, -BleedY, 430f + BleedX * 2f, 880f + BleedY * 2f,
                             Palette.Hex("#f7f4ee"), "Background", 0);
        }
    }
}
