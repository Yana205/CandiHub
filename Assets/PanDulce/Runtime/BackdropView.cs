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
            ViewFactory.Rect(t, "WallShade", Shapes.White, -BleedX, 336f, 430f + BleedX * 2f, 60f,
                             new Color(0.63f, 0.43f, 0.24f, 0.13f), "Background", 1);

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
            // frame + sky
            ViewFactory.Panel(w, "Frame", 26f, 72f, 378f, 206f, 8, Palette.Wood, "Background", 10);
            ViewFactory.Rect(w, "Sky", Shapes.VerticalGradient(64, 1f, 0.93f),
                             33f, 79f, 364f, 192f, Palette.Hex("#dfe7e0"), "Background", 11);

            // skyline — five striped buildings sitting on the street
            float baseY = 79f + 192f - 44f;
            (float x, float wd, float h, string fill)[] blocks =
            {
                (-6f, 80f, 88f, "#c3b39c"),
                (66f, 64f, 124f, "#b4a389"),
                (126f, 88f, 74f, "#ccbca4"),
                (208f, 72f, 112f, "#bcab92"),
                (274f, 98f, 86f, "#c8b8a0"),
            };
            foreach (var b in blocks)
            {
                float x = 33f + Mathf.Max(0f, b.x);
                float wd = Mathf.Min(b.wd, 33f + 364f - x);
                if (wd <= 0f) continue;
                ViewFactory.Rect(w, "Building", Shapes.White, x, baseY - b.h, wd, b.h,
                                 Palette.Hex(b.fill), "Background", 12);
            }

            // clouds
            ViewFactory.Panel(w, "Cloud0", 67f, 99f, 58f, 14f, 7, new Color(1f, 1f, 1f, 0.85f), "Background", 13);
            ViewFactory.Panel(w, "Cloud1", 87f, 90f, 30f, 14f, 7, new Color(1f, 1f, 1f, 0.85f), "Background", 13);
            ViewFactory.Panel(w, "Cloud2", 305f, 111f, 46f, 12f, 6, new Color(1f, 1f, 1f, 0.7f), "Background", 13);

            // lamppost
            ViewFactory.Rect(w, "LamppostPole", Shapes.White, 133f, baseY - 112f, 4f, 112f,
                             Palette.Hex("#8a7a66"), "Background", 14);
            ViewFactory.Panel(w, "LamppostHead", 123f, baseY - 118f, 24f, 15f, 6,
                              Palette.Hex("#f0d79b"), "Background", 15);

            // street
            ViewFactory.Rect(w, "Street", Shapes.White, 33f, baseY, 364f, 44f,
                             Palette.Hex("#cbbca6"), "Background", 16);
            ViewFactory.Rect(w, "StreetEdge", Shapes.White, 33f, baseY, 364f, 4f,
                             Palette.Hex("#b8a68d"), "Background", 17);
            ViewFactory.Rect(w, "StreetDashes", Shapes.Dashes(18, 18, 3), 33f, baseY + 24f, 364f, 3f,
                             new Color(1f, 1f, 1f, 0.8f), "Background", 18);

            // mullions
            ViewFactory.Rect(w, "MullionL", Shapes.White, 145f, 79f, 5f, 192f,
                             Palette.Hex("#b5854f"), "Background", 19);
            ViewFactory.Rect(w, "MullionR", Shapes.White, 280f, 79f, 5f, 192f,
                             Palette.Hex("#b5854f"), "Background", 19);
        }
    }
}
