using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Boost bar — stage (0,824) 430 × 56 (§8.7). Shown only when boostsOn.
    /// The button is 252 × 46 stage px, which is finger-sized on a real phone.
    /// </summary>
    public sealed class BoostBarView : GeneratedView
    {
        SpriteRenderer button, chargeFill;
        TextMeshPro label, badgeLabel;
        Transform buttonRoot;
        float shownCharge;

        protected override void Build()
        {
            shownCharge = -1f;
            var t = Content;

            ViewFactory.Rect(t, "Background", Shapes.VerticalGradient(64, 1f, 0.85f),
                             0f, 824f, 430f, 120f, Palette.BarTop, "Overlay", 30);
            ViewFactory.Rect(t, "TopBorder", Shapes.White, 0f, 824f, 430f, 3f,
                             Palette.BarBorder, "Overlay", 31);
            ViewFactory.Rect(t, "TopSheen", Shapes.White, 0f, 827f, 430f, 3f,
                             new Color(1f, 225f/255f, 180f/255f, 0.22f), "Overlay", 31);

            buttonRoot = ViewFactory.Node(t, "ShakeButton").transform;

            ViewFactory.Panel(buttonRoot, "Shadow", 89f, 836f, 252f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            button = ViewFactory.Panel(buttonRoot, "Face", 89f, 833f, 252f, 46f, 15,
                                       Palette.Amber, "Overlay", 32);

            // shaker icon: rotated cream square + knot circle + two motion dashes (§8.7)
            var square = ViewFactory.Panel(buttonRoot, "IconSquare", 102f, 845f, 17f, 17f, 4,
                                           Palette.Cream, "Overlay", 33);
            square.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            ViewFactory.Rect(buttonRoot, "IconKnotRim", Shapes.Circle(32), 105f, 838f, 12f, 12f,
                             Palette.Hex("#c07f1c"), "Overlay", 33);
            ViewFactory.Rect(buttonRoot, "IconKnot", Shapes.Circle(32), 106f, 839f, 10f, 10f,
                             Palette.Cream, "Overlay", 34);
            ViewFactory.Rect(buttonRoot, "Dash1", Shapes.White, 94f, 850f, 7f, 2.5f,
                             Palette.WithAlpha(Palette.Cream, 0.85f), "Overlay", 33);
            ViewFactory.Rect(buttonRoot, "Dash2", Shapes.White, 122f, 858f, 7f, 2.5f,
                             Palette.WithAlpha(Palette.Cream, 0.85f), "Overlay", 33);

            label = ViewFactory.Label(buttonRoot, "Label", "Shake the furoshiki!",
                                      107f, 851f, 234f, 15f, Palette.DarkCrust, "Overlay", 34);

            ViewFactory.Rect(buttonRoot, "ChargeTrack", Shapes.RoundedRect(12, 12, 4),
                             135f, 864f, 160f, 7f,
                             Palette.WithAlpha(Palette.Hex("#6f4a2c"), 0.4f), "Overlay", 34);
            chargeFill = ViewFactory.Rect(buttonRoot, "ChargeFill", Shapes.RoundedRect(12, 12, 4),
                                          135f, 864f, 1f, 7f, Palette.Cream, "Overlay", 35);

            // badge overlapping the button's top-right corner
            ViewFactory.Panel(buttonRoot, "BadgeBorder", 320f, 822f, 40f, 25f, 12,
                              Palette.Hex("#6f4a2c"), "Overlay", 35);
            ViewFactory.Panel(buttonRoot, "Badge", 322f, 824f, 36f, 21f, 10,
                              Palette.ChipFill, "Overlay", 36);
            badgeLabel = ViewFactory.Label(buttonRoot, "BadgeLabel", "0%", 322f, 838f, 36f, 11f,
                                           Palette.Cream, "Overlay", 37);
        }

        public void Sync(float charge, bool ready, bool boostsOn, float now)
        {
            if (!IsBuilt) return;
            SetVisible(boostsOn);
            if (!boostsOn) return;

            if (!Mathf.Approximately(charge, shownCharge))
            {
                shownCharge = charge;
                float w = Mathf.Max(1f, 160f * charge);
                chargeFill.size = new Vector2(w * StageCoords.PX, 7f * StageCoords.PX);
                chargeFill.transform.localPosition =
                    new Vector3((135f + w * 0.5f) * StageCoords.PX, -(864f + 3.5f) * StageCoords.PX, 0f);
                badgeLabel.text = ready ? "READY!" : $"{Mathf.RoundToInt(charge * 100f)}%";
            }

            // Charging reads at 0.72 opacity; ready pulses over 1.15s (§8.7).
            float alpha = ready ? 1f : 0.72f;
            button.color = Palette.WithAlpha(ready ? Palette.Amber : Palette.AmberDeep, alpha);
            label.alpha = alpha;

            float pulse = ready ? Mathf.Sin(now / 1.15f * Mathf.PI * 2f) : 0f;
            buttonRoot.localPosition = new Vector3(0f, pulse * 2f * StageCoords.PX, 0f);
            buttonRoot.localScale = Vector3.one * (1f + Mathf.Max(0f, pulse) * 0.035f);
        }

        /// <summary>Stage-px rect of the button face, for hit testing without a Canvas.</summary>
        public Rect ButtonRect => new Rect(89f, 833f, 252f, 46f);
    }
}
