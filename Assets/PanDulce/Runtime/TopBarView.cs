using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Top HUD — stage (0,0) 430 × 56 (§8.2).
    ///
    /// The centre carries the "Sweet Bakery" title per the mock; score lives on the run-end card.
    ///
    /// Bar-less since the painted art landed (2026-08-09, Yana's call): the brown panel and
    /// its border used to be the backdrop for these widgets, but they covered the noren and
    /// the 菓子パン sign, which is the shop's real signage. The chips carry their own fill,
    /// and the title keeps its dark drop, so each widget still reads against the pink wall.
    /// </summary>
    public sealed class TopBarView : GeneratedView
    {
        TextMeshPro customersLabel, coinsLabel;
        int shownServed = -1, shownCoins = -1;

        protected override void Build()
        {
            shownServed = -1;
            shownCoins = -1;
            var t = Content;

            ViewFactory.Panel(t, "CustomersChip", 10f, 14f, 104f, 28f, 10, Palette.ChipFill, "Overlay", 2);
            customersLabel = ViewFactory.Label(t, "CustomersLabel", "Customers: 0",
                                               10f, 28f, 104f, 14f, Palette.Cream, "Overlay", 3);

            // Sweet Bakery — 22px 800 cream with a 2px dark drop (§8.2)
            ViewFactory.Label(t, "TitleShadow", "Sweet Bakery", 115f, 32f, 200f, 22f,
                              Palette.Hex("#6f4a2c"), "Overlay", 2);
            ViewFactory.Label(t, "Title", "Sweet Bakery", 115f, 30f, 200f, 22f,
                              Palette.Cream, "Overlay", 3);
            // The next-dessert preview lives on the play-area plaque only (NextPlaqueView).

            // Coin chip on the right — serves pay in, the clearance boost draws out.
            ViewFactory.Panel(t, "CoinChip", 326f, 14f, 94f, 28f, 10, Palette.ChipFill, "Overlay", 2);
            coinsLabel = ViewFactory.Label(t, "CoinsLabel", "$0",
                                           326f, 28f, 94f, 14f, Palette.Cream, "Overlay", 3);
        }

        public void Sync(int served, int coins)
        {
            if (!IsBuilt) return;
            if (served != shownServed) { shownServed = served; customersLabel.text = $"Customers: {served}"; }
            if (coins != shownCoins) { shownCoins = coins; coinsLabel.text = $"${coins}"; }
        }
    }
}
