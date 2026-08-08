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
        /// <summary>
        /// Chip band, in stage px. Authored placement (Lital, 2026-08-08): both plates and
        /// both figures centre on local y -0.281, so the plate top is that centre less half
        /// its height and the labels sit straight on it.
        /// </summary>
        const float ChipH = 38f;
        const float ChipMidY = 28.1f;
        const float ChipY = ChipMidY - ChipH * 0.5f;

        TextMeshPro customersLabel, coinsLabel;
        int shownServed = -1, shownCoins = -1;

        protected override void Build()
        {
            shownServed = -1;
            shownCoins = -1;
            var t = Content;

            ViewFactory.Rect(t, "Background", Shapes.VerticalGradient(64, 1f, 0.89f),
                             0f, -60f, 430f, 116f, Palette.BarTop, "Overlay", 0);
            ViewFactory.Rect(t, "BottomBorder", Shapes.White, 0f, 52f, 430f, 4f,
                             Palette.BarBorder, "Overlay", 1);

            // Both chips are drawings of an icon plus a number pill, so they are sized to the
            // art's own aspect (28 px tall would squash the panda) and the label is pushed
            // into the pill. The drawn icon now says "customers" and "coins", so the labels
            // carry the bare figure — a "Customers: n" caption would run over the face.
            Sprite customersArt = skin != null ? skin.CustomersChip : null;
            ViewFactory.Plate(t, "CustomersChip", customersArt,
                              10f, ChipY, 104f, ChipH, 10, ChipTint(customersArt), "Overlay", 2);
            customersLabel = ViewFactory.Label(t, "CustomersLabel", "0",
                                               50f, ChipMidY, 58f, 15f, ChipInk(customersArt), "Overlay", 3);

            // Sweet Bakery — 22px 800 cream with a 2px dark drop (§8.2)
            ViewFactory.Label(t, "TitleShadow", "Sweet Bakery", 115f, 32f, 200f, 22f,
                              Palette.Hex("#6f4a2c"), "Overlay", 2);
            ViewFactory.Label(t, "Title", "Sweet Bakery", 115f, 30f, 200f, 22f,
                              Palette.Cream, "Overlay", 3);
            // The next-dessert preview lives on the play-area plaque only (NextPlaqueView).

            // Coin chip on the right — serves pay in, the clearance boost draws out.
            Sprite coinArt = skin != null ? skin.CoinChip : null;
            ViewFactory.Plate(t, "CoinChip", coinArt,
                              316f, ChipY, 104f, ChipH, 10, ChipTint(coinArt), "Overlay", 2);
            coinsLabel = ViewFactory.Label(t, "CoinsLabel", "$0",
                                           356f, ChipMidY, 60f, 15f, ChipInk(coinArt), "Overlay", 3);
        }

        /// <summary>Untinted when the drawing carries its own colour, else the flat chip fill.</summary>
        static Color ChipTint(Sprite art) => art != null ? Color.white : Palette.ChipFill;

        /// <summary>The art's pill is cream, so the figure has to darken to stay legible.</summary>
        static Color ChipInk(Sprite art) => art != null ? Palette.Crust : Palette.Cream;

        public void Sync(int served, int coins)
        {
            if (!IsBuilt) return;
            if (served != shownServed) { shownServed = served; customersLabel.text = served.ToString(); }
            if (coins != shownCoins) { shownCoins = coins; coinsLabel.text = $"${coins}"; }
        }
    }
}
