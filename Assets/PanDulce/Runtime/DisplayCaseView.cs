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

            // glass: white border panel + gradient fill + rail + two rotated glare stripes (§8.4)
            ViewFactory.Panel(t, "GlassBorder", 9f, 300f, 412f, 96f, 14,
                              new Color(1f, 1f, 1f, 0.92f), "Case", 0);
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

            float slotW = 412f / Slots;
            for (int i = 0; i < Slots; i++)
            {
                float cx = 9f + slotW * (i + 0.5f);
                icons[i] = ViewFactory.Icon(t, $"Slot_{i}_Icon", database, i, cx, 352f, 21f, "Case", 10);
                ViewFactory.Rect(t, $"Slot_{i}_LedgeShadow", Shapes.White, cx - 26f, 377f, 52f, 2f,
                                 Palette.Hex("#b99f7c"), "Case", 11);
                ViewFactory.Rect(t, $"Slot_{i}_Ledge", Shapes.White, cx - 26f, 372f, 52f, 5f,
                                 Palette.Hex("#d8c6ac"), "Case", 12);
                labels[i] = ViewFactory.Label(t, $"Slot_{i}_Label", "?", cx - 30f, 388f, 60f, 9.5f,
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
