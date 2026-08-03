using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Top bar — stage (0,0) 430 × 56 (§8.2).
    ///
    /// The centre carries the "Sweet Bakery" title per the mock; score lives on the run-end card.
    /// </summary>
    public sealed class TopBarView : GeneratedView
    {
        TextMeshPro customersLabel;
        SpriteRenderer nextIcon;
        int shownServed = -1, shownNext = -1;

        protected override void Build()
        {
            shownServed = shownNext = -1;
            var t = Content;

            ViewFactory.Rect(t, "Background", Shapes.VerticalGradient(64, 1f, 0.89f),
                             0f, -60f, 430f, 116f, Palette.BarTop, "Overlay", 0);
            ViewFactory.Rect(t, "BottomBorder", Shapes.White, 0f, 52f, 430f, 4f,
                             Palette.BarBorder, "Overlay", 1);

            ViewFactory.Panel(t, "CustomersChip", 10f, 14f, 104f, 28f, 10, Palette.ChipFill, "Overlay", 2);
            customersLabel = ViewFactory.Label(t, "CustomersLabel", "Customers: 0",
                                               10f, 28f, 104f, 14f, Palette.Cream, "Overlay", 3);

            // Sweet Bakery — 22px 800 cream with a 2px dark drop (§8.2)
            ViewFactory.Label(t, "TitleShadow", "Sweet Bakery", 115f, 32f, 200f, 22f,
                              Palette.Hex("#6f4a2c"), "Overlay", 2);
            ViewFactory.Label(t, "Title", "Sweet Bakery", 115f, 30f, 200f, 22f,
                              Palette.Cream, "Overlay", 3);

            ViewFactory.Panel(t, "NextChip", 316f, 10f, 104f, 36f, 10, Palette.ChipFill, "Overlay", 2);
            ViewFactory.Label(t, "NextLabel", "Next", 322f, 30f, 40f, 13f, Palette.Cream, "Overlay", 3);
            nextIcon = ViewFactory.Icon(t, "NextIcon", database, 0, 396f, 28f, 14f, "Overlay", 3);
        }

        public void Sync(int served, int nextTier)
        {
            if (!IsBuilt) return;
            if (served != shownServed) { shownServed = served; customersLabel.text = $"Customers: {served}"; }
            if (nextTier != shownNext && database != null)
            {
                shownNext = nextTier;
                nextIcon.sprite = database.Pastry(nextTier);
            }
        }
    }
}
