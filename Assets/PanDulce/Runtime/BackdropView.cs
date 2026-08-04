using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The shop wall, window scene, counter and lower wall (§8.3).
    ///
    /// Everything gameplay-relevant stays inside the 430 × 880 frame; the panels here
    /// deliberately extend past it (the "bleed") so the letterbox slack on real phone
    /// aspects shows wall texture rather than flat #cfa06b bars, which would read as a bug.
    /// </summary>
    public sealed class BackdropView : GeneratedView
    {
        const float BleedX = 120f;   // per side
        const float BleedY = 150f;   // above the top bar and below the boost bar

        protected override void Build()
        {
            var t = Content;

            // --- wall, bled wide and high ---
            ViewFactory.Rect(t, "ShopWall", Shapes.White,
                             -BleedX, 56f - BleedY, 430f + BleedX * 2f, 340f + BleedY,
                             Palette.WallFill, "Background", 0);

            var seams = ViewFactory.Rect(t, "WallSeams", Shapes.VerticalStripes(46, 2),
                                         -BleedX, 56f - BleedY, 430f + BleedX * 2f, 340f + BleedY,
                                         new Color(0.55f, 0.38f, 0.21f, 0.16f), "Background", 1);
            seams.drawMode = SpriteDrawMode.Tiled;
            seams.tileMode = SpriteTileMode.Continuous;
            seams.size = new Vector2((430f + BleedX * 2f) * StageCoords.PX, (340f + BleedY) * StageCoords.PX);

            ViewFactory.Rect(t, "WallShade", Shapes.White, -BleedX, 336f, 430f + BleedX * 2f, 60f,
                             new Color(0.63f, 0.43f, 0.24f, 0.13f), "Background", 2);

            BuildWindow(ViewFactory.Node(t, "Window").transform);

            // --- counter ---
            ViewFactory.Rect(t, "CounterLip", Shapes.White, -BleedX, 392f, 430f + BleedX * 2f, 12f,
                             Palette.Hex("#d9a469"), "Furniture", 20);
            ViewFactory.Rect(t, "CounterLipEdge", Shapes.White, -BleedX, 402f, 430f + BleedX * 2f, 2f,
                             Palette.Hex("#ab7742"), "Furniture", 21);

            ViewFactory.Rect(t, "DrawerBand", Shapes.White, -BleedX, 404f, 430f + BleedX * 2f, 20f,
                             Palette.Hex("#a86e3c"), "Furniture", 30);
            ViewFactory.Rect(t, "DrawerBandEdge", Shapes.White, -BleedX, 421f, 430f + BleedX * 2f, 3f,
                             Palette.ChipFill, "Furniture", 31);
            for (int i = 0; i < 3; i++)
            {
                float w = (430f - 24f - 20f) / 3f;
                ViewFactory.Panel(t, $"Drawer_{i}", 12f + i * (w + 10f), 409f, w, 9f, 2,
                                  Palette.Hex("#8f5a2a"), "Furniture", 32);
            }

            // --- lower wall, bled wide and low ---
            ViewFactory.Rect(t, "LowerWall", Shapes.VerticalGradient(64, 1f, 0.72f),
                             -BleedX, 424f, 430f + BleedX * 2f, 456f + BleedY,
                             Palette.Hex("#b98244"), "Background", 2);
            ViewFactory.Rect(t, "LowerWallTop", Shapes.White, -BleedX, 424f, 430f + BleedX * 2f, 8f,
                             Palette.Hex("#6b4527"), "Background", 3);
        }

        void BuildWindow(Transform w)
        {
            // frame — 7px border effect: wood panel behind, scene inset on top
            ViewFactory.Panel(w, "Frame", 26f, 72f, 378f, 206f, 8, Palette.Wood, "Background", 10);
            ViewFactory.Rect(w, "FrameShadow", Shapes.White, 26f, 278f, 378f, 5f,
                             Palette.Crust, "Background", 10);

            var scene = ViewFactory.Rect(w, "Scene", database != null ? database.WindowScene : null,
                                         33f, 79f, 364f, 192f, Color.white, "Background", 11);
            // The PNG is 756×412 px at PPU 100 → 756×412 stage px in Simple mode. The inset is
            // 364×192, and 756/412 == 378/206 == the same aspect, so one uniform factor fits it.
            scene.drawMode = SpriteDrawMode.Simple;
            scene.transform.localScale = Vector3.one * (364f / 756f);
            scene.transform.localPosition = StageCoords.Stage(33f + 182f, 79f + 96f);
        }
    }
}
