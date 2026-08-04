using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Display case — stage (9,300) 412 × 96, five slots with a sliding window (§8.4).
    /// The window follows progress: start = clamp(highestDiscovered - 3, 0, 6).
    /// </summary>
    public sealed class DisplayCaseView : GeneratedView
    {
        const int Slots = 5;

        readonly SpriteRenderer[] icons = new SpriteRenderer[Slots];
        readonly TextMeshPro[] labels = new TextMeshPro[Slots];
        int shownStart, shownMax;

        protected override void Build()
        {
            shownStart = shownMax = -1;
            var t = Content;

            // knob on top
            ViewFactory.Panel(t, "Knob", 200f, 289f, 30f, 11f, 5, Palette.Hex("#e0c079"), "Case", 1);

            // glass: gradient fill + rail + two rotated glare stripes. The opaque white
            // border cover is gone on purpose — the fill alone reads as glass.
            ViewFactory.Rect(t, "GlassFill", Shapes.VerticalAlphaGradient(64, 0.5f, 0.12f),
                             12f, 303f, 406f, 90f, Color.white, "Case", 1);
            ViewFactory.Rect(t, "Rail", Shapes.White, 12f, 315f, 406f, 2f,
                             new Color(1f, 1f, 1f, 0.75f), "Case", 2);
            var glareA = ViewFactory.Rect(t, "GlareA", Shapes.White, 20f, 296f, 44f, 120f,
                                          new Color(1f, 1f, 1f, 0.30f), "Case", 3);
            glareA.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var glareB = ViewFactory.Rect(t, "GlareB", Shapes.White, 74f, 296f, 16f, 120f,
                                          new Color(1f, 1f, 1f, 0.22f), "Case", 3);
            glareB.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);

            // One Desserts node holds a Slot_i folder per dessert, pivoted at the icon
            // centre, so the whole shelf — or a single slot — moves as one unit.
            float slotW = 412f / Slots;
            var desserts = ViewFactory.Node(t, "Desserts").transform;
            for (int i = 0; i < Slots; i++)
            {
                float cx = 9f + slotW * (i + 0.5f);
                var slot = ViewFactory.Node(desserts, $"Slot_{i}", cx, 352f).transform;
                icons[i] = ViewFactory.Icon(slot, "Icon", database, i, 0f, 0f, 21f, "Case", 10);
                ViewFactory.Rect(slot, "LedgeShadow", Shapes.White, -26f, 25f, 52f, 2f,
                                 Palette.Hex("#b99f7c"), "Case", 11);
                ViewFactory.Rect(slot, "Ledge", Shapes.White, -26f, 20f, 52f, 5f,
                                 Palette.Hex("#d8c6ac"), "Case", 12);
                labels[i] = ViewFactory.Label(slot, "Label", "?", -30f, 36f, 60f, 9.5f,
                                              Palette.Hex("#7a5735"), "Case", 13);
            }
        }

        public void Sync(MergeSim sim)
        {
            if (!IsBuilt) return;
            int maxD = sim.HighestDiscovered;
            int start = Mathf.Clamp(maxD - 3, 0, TierTable.Count - Slots);
            if (start == shownStart && maxD == shownMax) return;
            shownStart = start;
            shownMax = maxD;

            for (int i = 0; i < Slots; i++)
            {
                int tier = start + i;
                bool found = sim.IsDiscovered(tier);
                if (database != null) icons[i].sprite = database.Pastry(tier);
                // Undiscovered entries render the sprite tinted flat, label '?' (§8.4).
                icons[i].color = found ? Color.white : Palette.Locked;
                labels[i].text = found ? TierTable.Names[tier] : "?";
            }
        }
    }
}
