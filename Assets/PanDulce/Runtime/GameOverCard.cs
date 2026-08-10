using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The run-end card. A top-out closes the bakery rather than showing a failure screen —
    /// the fold and knot already exist, so the ending reuses them and this card just reports.
    /// </summary>
    public sealed class GameOverCard : GeneratedView
    {
        TextMeshPro scoreLabel, bestLabel;

        protected override void Build()
        {
            var t = Content;

            Sprite plateArt = skin != null ? skin.Plate : null;
            Sprite buttonArt = skin != null ? skin.Button : null;

            ViewFactory.Rect(t, "Scrim", Shapes.White, -40f, -40f, 510f, 960f,
                             new Color(0f, 0f, 0f, 0.35f), "Overlay", 60);
            ViewFactory.Plate(t, "Card", plateArt, 55f, 300f, 320f, 240f, 20,
                              plateArt != null ? Color.white : Palette.Hex("#fffaf0"), "Overlay", 61);
            ViewFactory.Label(t, "Title", "Sold out!", 55f, 350f, 320f, 34f, Palette.Crust, "Overlay", 62);
            scoreLabel = ViewFactory.Label(t, "Score", "0", 55f, 410f, 320f, 44f,
                                           Palette.Hex("#c9502f"), "Overlay", 62);
            bestLabel = ViewFactory.Label(t, "Best", "best 0", 55f, 452f, 320f, 14f,
                                          Palette.Hex("#a58358"), "Overlay", 62,
                                          TextAlignmentOptions.Center, FontStyles.Normal);
            ViewFactory.Plate(t, "Button", buttonArt, 125f, 480f, 180f, 46f, 14,
                              buttonArt != null ? Color.white : Palette.Amber, "Overlay", 62);
            ViewFactory.Label(t, "ButtonLabel", "Play again", 125f, 508f, 180f, 17f,
                              Palette.DarkCrust, "Overlay", 63);
            SetVisible(false);
        }

        public Rect ButtonRect => new Rect(125f, 480f, 180f, 46f);

        public void Show(int score, int best, bool newBest)
        {
            if (!IsBuilt) return;
            SetVisible(true);
            if (scoreLabel != null) scoreLabel.text = score.ToString("N0");
            if (bestLabel != null) bestLabel.text = newBest ? "new best!" : $"best {best:N0}";
        }

        public void Hide() => SetVisible(false);
    }
}
