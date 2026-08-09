using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Boost buttons — stage (0,824) 430 × 56 (§8.7). Shown only when boostsOn.
    /// Two buttons since the coin economy: the charge-driven shake on the left
    /// (12–238) and the coin-priced Day-old clearance on the right (250–418).
    /// Both stay finger-sized on a real phone.
    /// </summary>
    public sealed class BoostBarView : GeneratedView
    {
        /// <summary>Seconds per breath of the ready pulse. §8.7 specifies 1.15; slowed so
        /// the charged button breathes instead of bouncing.</summary>
        const float ReadyPulsePeriod = 1.4f;

        /// <summary>Vertical travel of the ready pulse, in stage px (§8.7 specifies 2).</summary>
        const float ReadyPulseBobPx = 1f;

        /// <summary>Peak scale-up of the ready pulse (§8.7 specifies 0.035).</summary>
        const float ReadyPulseScale = 0.015f;

        SpriteRenderer button, chargeFill, clearFace;
        TextMeshPro label, badgeLabel, clearLabel, priceLabel;
        Transform buttonRoot, clearRoot;
        float shownCharge, denyAt, clearDenyAt;
        bool shownReady, shownCanBuy;
        int shownCost;

        protected override void Build()
        {
            shownCharge = -1f;
            denyAt = -1f;
            clearDenyAt = -1f;
            shownReady = false;
            shownCanBuy = false;
            shownCost = -1;
            var t = Content;

            // Bar-less since the painted art landed (2026-08-09, Yana's call) — the panel,
            // border and sheen are gone. Both buttons already carry a drop shadow and a
            // solid face, so they read on their own against the floor.
            buttonRoot = ViewFactory.Node(t, "ShakeButton").transform;

            ViewFactory.Panel(buttonRoot, "Shadow", 12f, 836f, 226f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            button = ViewFactory.Panel(buttonRoot, "Face", 12f, 833f, 226f, 46f, 15,
                                       Palette.Amber, "Overlay", 32);

            // shaker icon: rotated cream square + knot circle + two motion dashes (§8.7)
            var square = ViewFactory.Panel(buttonRoot, "IconSquare", 25f, 845f, 17f, 17f, 4,
                                           Palette.Cream, "Overlay", 33);
            square.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            ViewFactory.Rect(buttonRoot, "IconKnotRim", Shapes.Circle(32), 28f, 838f, 12f, 12f,
                             Palette.Hex("#c07f1c"), "Overlay", 33);
            ViewFactory.Rect(buttonRoot, "IconKnot", Shapes.Circle(32), 29f, 839f, 10f, 10f,
                             Palette.Cream, "Overlay", 34);
            ViewFactory.Rect(buttonRoot, "Dash1", Shapes.White, 17f, 850f, 7f, 2.5f,
                             Palette.WithAlpha(Palette.Cream, 0.85f), "Overlay", 33);
            ViewFactory.Rect(buttonRoot, "Dash2", Shapes.White, 45f, 858f, 7f, 2.5f,
                             Palette.WithAlpha(Palette.Cream, 0.85f), "Overlay", 33);

            // Starts on the hint text; Sync swaps it once the meter is ready.
            label = ViewFactory.Label(buttonRoot, "Label", "Merge desserts to charge!",
                                      48f, 851f, 184f, 13f, Palette.DarkCrust, "Overlay", 34);

            ViewFactory.Rect(buttonRoot, "ChargeTrack", Shapes.RoundedRect(12, 12, 4),
                             45f, 864f, 160f, 7f,
                             Palette.WithAlpha(Palette.Hex("#6f4a2c"), 0.4f), "Overlay", 34);
            chargeFill = ViewFactory.Rect(buttonRoot, "ChargeFill", Shapes.RoundedRect(12, 12, 4),
                                          45f, 864f, 1f, 7f, Palette.Cream, "Overlay", 35);

            // badge overlapping the button's top-right corner
            ViewFactory.Panel(buttonRoot, "BadgeBorder", 218f, 822f, 40f, 25f, 12,
                              Palette.Hex("#6f4a2c"), "Overlay", 35);
            ViewFactory.Panel(buttonRoot, "Badge", 220f, 824f, 36f, 21f, 10,
                              Palette.ChipFill, "Overlay", 36);
            badgeLabel = ViewFactory.Label(buttonRoot, "BadgeLabel", "0%", 220f, 838f, 36f, 11f,
                                           Palette.Cream, "Overlay", 37);

            // --- Day-old clearance: coin-priced, pops every tier-0/1 pastry ---
            clearRoot = ViewFactory.Node(t, "ClearanceButton").transform;

            ViewFactory.Panel(clearRoot, "Shadow", 250f, 836f, 168f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            clearFace = ViewFactory.Panel(clearRoot, "Face", 250f, 833f, 168f, 46f, 15,
                                          Palette.WithAlpha(Palette.AmberDeep, 0.55f), "Overlay", 32);

            // coin icon: gold rim, cream fill, $ stamp
            ViewFactory.Rect(clearRoot, "CoinRim", Shapes.Circle(32), 262f, 843f, 18f, 18f,
                             Palette.Hex("#c07f1c"), "Overlay", 33);
            ViewFactory.Rect(clearRoot, "CoinFill", Shapes.Circle(32), 264f, 845f, 14f, 14f,
                             Palette.Cream, "Overlay", 34);
            ViewFactory.Label(clearRoot, "CoinStamp", "$", 262f, 856f, 18f, 10f,
                              Palette.Hex("#c07f1c"), "Overlay", 35);

            clearLabel = ViewFactory.Label(clearRoot, "Label", "Clear day-olds",
                                           284f, 851f, 130f, 12f, Palette.DarkCrust, "Overlay", 34);
            clearLabel.alpha = 0.55f;

            // price badge overlapping the button's top-right corner
            ViewFactory.Panel(clearRoot, "PriceBorder", 388f, 822f, 40f, 25f, 12,
                              Palette.Hex("#6f4a2c"), "Overlay", 35);
            ViewFactory.Panel(clearRoot, "Price", 390f, 824f, 36f, 21f, 10,
                              Palette.ChipFill, "Overlay", 36);
            priceLabel = ViewFactory.Label(clearRoot, "PriceLabel", "$30", 390f, 838f, 36f, 11f,
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
                    new Vector3((45f + w * 0.5f) * StageCoords.PX, -(864f + 3.5f) * StageCoords.PX, 0f);
                badgeLabel.text = ready ? "READY!" : $"{Mathf.RoundToInt(charge * 100f)}%";
            }

            if (ready != shownReady)
            {
                shownReady = ready;
                label.text = ready ? "Shake the furoshiki!" : "Merge desserts to charge!";
            }

            // Charging reads at 0.72 opacity; ready breathes over ReadyPulsePeriod (§8.7).
            float alpha = ready ? 1f : 0.72f;
            button.color = Palette.WithAlpha(ready ? Palette.Amber : Palette.AmberDeep, alpha);
            label.alpha = alpha;

            // Deny wiggle: a decaying side-shake after a tap on the uncharged button.
            float denyK = denyAt >= 0f ? (now - denyAt) / 0.35f : 2f;
            float wiggle = denyK < 1f ? Mathf.Sin(denyK * Mathf.PI * 4f) * (1f - denyK) * 4f : 0f;

            // Ready breathe: composes with the wiggle above — wiggle owns x, pulse owns y.
            float pulse = ready ? Mathf.Sin(now / ReadyPulsePeriod * Mathf.PI * 2f) : 0f;
            buttonRoot.localPosition = new Vector3(wiggle * StageCoords.PX,
                                                   pulse * ReadyPulseBobPx * StageCoords.PX, 0f);
            buttonRoot.localScale = Vector3.one * (1f + Mathf.Max(0f, pulse) * ReadyPulseScale);
        }

        /// <summary>Affordability + availability drive the clearance button's read.</summary>
        public void SyncClearance(int coins, int cost, bool hasTargets, float now)
        {
            if (!IsBuilt) return;

            if (cost != shownCost)
            {
                shownCost = cost;
                priceLabel.text = $"${cost}";
            }

            bool canBuy = coins >= cost && hasTargets;
            if (canBuy != shownCanBuy)
            {
                shownCanBuy = canBuy;
                float alpha = canBuy ? 1f : 0.55f;
                clearFace.color = Palette.WithAlpha(canBuy ? Palette.Amber : Palette.AmberDeep, alpha);
                clearLabel.alpha = alpha;
            }

            // Same deny grammar as the shake button: a decaying side-shake.
            float denyK = clearDenyAt >= 0f ? (now - clearDenyAt) / 0.35f : 2f;
            float wiggle = denyK < 1f ? Mathf.Sin(denyK * Mathf.PI * 4f) * (1f - denyK) * 4f : 0f;
            clearRoot.localPosition = new Vector3(wiggle * StageCoords.PX, 0f, 0f);
        }

        /// <summary>Tap landed on the button while it was not ready — shake the head.</summary>
        public void Deny(float now) => denyAt = now;

        /// <summary>Clearance tap that could not go through — broke, or nothing to clear.</summary>
        public void DenyClearance(float now) => clearDenyAt = now;

        /// <summary>
        /// The bar's authored scene offset in stage px. The node is hand-positioned in the
        /// scene (no SafeAreaInset), so the hit rects must follow the transform or taps
        /// would land where the bar used to be drawn.
        /// </summary>
        Vector2 StageOffset
            => new Vector2(transform.localPosition.x / StageCoords.PX,
                           -transform.localPosition.y / StageCoords.PX);

        /// <summary>Stage-px rect of the shake button face, for hit testing without a Canvas.</summary>
        public Rect ButtonRect
        {
            get { Vector2 o = StageOffset; return new Rect(12f + o.x, 833f + o.y, 226f, 46f); }
        }

        /// <summary>Stage-px rect of the clearance button face.</summary>
        public Rect ClearanceRect
        {
            get { Vector2 o = StageOffset; return new Rect(250f + o.x, 833f + o.y, 168f, 46f); }
        }
    }
}
