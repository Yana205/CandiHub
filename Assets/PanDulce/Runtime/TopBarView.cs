using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Top HUD — stage (0,0) 430 × 56 (§8.2).
    ///
    /// The centre carries the "Sweet Bakery" title per the mock; score lives on the run-end card.
    ///
    /// The generated brown panel and its border are gone (2026-08-09, Yana's call): they
    /// covered the noren and the 菓子パン sign, which is the shop's real signage. In their
    /// place the drawn slab from the skin hangs off the frame's top-left, and the chips are
    /// drawings too, so each widget carries its own fill instead of borrowing the bar's.
    /// </summary>
    public sealed class TopBarView : GeneratedView
    {
        /// <summary>
        /// Chip band, in stage px. Authored placement (Lital, 2026-08-08): both plates and
        /// both figures centre on local y -0.281, so the plate top is that centre less half
        /// its height and the labels sit straight on it.
        ///
        /// Trimmed 11% from the original 104 × 38 (Lital, 2026-08-10). Each chip shrinks
        /// toward its OWN centre, so neither moves — only the margins around them open up.
        /// The width follows the height so the drawing keeps its aspect: the plates are
        /// sliced with a zero border, which makes any off-aspect rect a straight stretch of
        /// the panda and the coin.
        /// </summary>
        const float ChipW = 93f;
        const float ChipH = 34f;
        const float ChipMidY = 28.1f;
        const float ChipY = ChipMidY - ChipH * 0.5f;

        /// <summary>Figure size, scaled with the plate so it keeps clear of the pill's edge.</summary>
        const float ChipLabelSize = 13.4f;

        /// <summary>
        /// The slab, in stage px. Authored by hand in the scene (Lital, 2026-08-09) and read
        /// back from there: deliberately larger than the 446 × 965 an iPhone 15 shows, and
        /// hung past the frame's top-left, so the drawing runs off every edge instead of
        /// ending in a seam. Only the bottom edge is pinned — it stays on the 56 px bar line,
        /// which is what the chips and the title are placed against.
        /// </summary>
        const float BarX = -46.21f;
        const float BarY = -74.48f;
        const float BarW = 501.7f;
        const float BarH = 130.49f;

        /// <summary>Border band, kept for the no-art fallback. Sits on the slab's bottom edge.</summary>
        const float BorderY = 52f;
        const float BorderH = 4f;

        TextMeshPro customersLabel, coinsLabel;
        int shownServed = -1, shownCoins = -1;

        protected override void Build()
        {
            shownServed = -1;
            shownCoins = -1;
            var t = Content;

            // The drawn slab already carries its own bottom edge, so it replaces both the
            // generated gradient and the BottomBorder strip that used to sit on top of it.
            Sprite barArt = skin != null ? skin.TopBar : null;
            ViewFactory.Rect(t, "Background",
                             barArt != null ? barArt : Shapes.VerticalGradient(64, 1f, 0.89f),
                             BarX, BarY, BarW, BarH,
                             barArt != null ? Color.white : Palette.BarTop, "Overlay", 0);
            if (barArt == null)
                ViewFactory.Rect(t, "BottomBorder", Shapes.White, BarX, BorderY,
                                 BarW, BorderH, Palette.BarBorder, "Overlay", 1);

            // Both chips are drawings of an icon plus a number pill, so they are sized to the
            // art's own aspect (28 px tall would squash the panda) and the label is pushed
            // into the pill. The drawn icon now says "customers" and "coins", so the labels
            // carry the bare figure — a "Customers: n" caption would run over the face.
            Sprite customersArt = skin != null ? skin.CustomersChip : null;
            ViewFactory.Plate(t, "CustomersChip", customersArt,
                              15.5f, ChipY, ChipW, ChipH, 10, ChipTint(customersArt), "Overlay", 2);
            customersLabel = ViewFactory.Label(t, "CustomersLabel", "0",
                                               48.2f, ChipMidY, 58f, ChipLabelSize,
                                               ChipInk(customersArt), "Overlay", 3);

            // Sweet Bakery — 22px 800 cream with a 2px dark drop (§8.2)
            ViewFactory.Label(t, "TitleShadow", "Sweet Bakery", 115f, 32f, 200f, 22f,
                              Palette.Hex("#6f4a2c"), "Overlay", 2);
            ViewFactory.Label(t, "Title", "Sweet Bakery", 115f, 30f, 200f, 22f,
                              Palette.Cream, "Overlay", 3);
            // The next-dessert preview lives on the play-area plaque only (NextPlaqueView).

            // Coin chip on the right — serves pay in, the clearance boost draws out.
            Sprite coinArt = skin != null ? skin.CoinChip : null;
            // Both chips keep the centres they were authored on, so the pair stays symmetric
            // about the frame centre the title is set on; the trim shows as wider margins.
            ViewFactory.Plate(t, "CoinChip", coinArt,
                              321.5f, ChipY, ChipW, ChipH, 10, ChipTint(coinArt), "Overlay", 2);
            coinsLabel = ViewFactory.Label(t, "CoinsLabel", "$0",
                                           354.1f, ChipMidY, 60f, ChipLabelSize,
                                           ChipInk(coinArt), "Overlay", 3);
        }

        /// <summary>Untinted when the drawing carries its own colour, else the flat chip fill.</summary>
        static Color ChipTint(Sprite art) => art != null ? Color.white : Palette.ChipFill;

        /// <summary>The art's pill is cream, so the figure has to darken to stay legible.</summary>
        static Color ChipInk(Sprite art) => art != null ? Palette.Crust : Palette.Cream;

        public void Sync(int served, int coins)
        {
            if (!IsBuilt) return;
            if (served != shownServed && customersLabel != null)
            { shownServed = served; customersLabel.text = served.ToString(); }
            if (coins != shownCoins && coinsLabel != null)
            { shownCoins = coins; coinsLabel.text = $"${coins}"; }
        }
    }
}
