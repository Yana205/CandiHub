using PanDulce.Core;
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

            ViewFactory.Rect(t, "Background", Shapes.White, 0f, 824f, 430f, 120f,
                             Palette.BarBottom, "Overlay", 30);
            ViewFactory.Rect(t, "TopBorder", Shapes.White, 0f, 824f, 430f, 3f,
                             Palette.BarBorder, "Overlay", 31);

            buttonRoot = ViewFactory.Node(t, "ShakeButton").transform;

            button = ViewFactory.Panel(buttonRoot, "Face", 89f, 833f, 252f, 46f, 15,
                                       Palette.Amber, "Overlay", 32);
            label = ViewFactory.Label(buttonRoot, "Label", "Shake the furoshiki!",
                                      89f, 852f, 252f, 15f, Palette.DarkCrust, "Overlay", 34);

            ViewFactory.Rect(buttonRoot, "ChargeTrack", Shapes.RoundedRect(12, 12, 4),
                             135f, 862f, 160f, 7f,
                             Palette.WithAlpha(Palette.Hex("#6f4a2c"), 0.4f), "Overlay", 34);
            chargeFill = ViewFactory.Rect(buttonRoot, "ChargeFill", Shapes.RoundedRect(12, 12, 4),
                                          135f, 862f, 1f, 7f, Palette.Cream, "Overlay", 35);

            ViewFactory.Panel(buttonRoot, "Badge", 318f, 824f, 34f, 23f, 11,
                              Palette.ChipFill, "Overlay", 35);
            badgeLabel = ViewFactory.Label(buttonRoot, "BadgeLabel", "0%", 318f, 840f, 34f, 11f,
                                           Palette.Cream, "Overlay", 36);
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
                    new Vector3((135f + w * 0.5f) * StageCoords.PX, -(862f + 3.5f) * StageCoords.PX, 0f);
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

    /// <summary>Order bubble — stage (132,136), pops in after the customer arrives (§8.4).</summary>
    public sealed class OrderBubbleView : GeneratedView
    {
        SpriteRenderer icon;
        TextMeshPro nameLabel;
        float shownAt;
        int shownTier;

        protected override void Build()
        {
            shownAt = -1f;
            shownTier = -1;
            var t = Content;

            ViewFactory.Panel(t, "Box", 132f, 136f, 190f, 62f, 18, Palette.Hex("#fffaf0"), "Overlay", 10);
            icon = ViewFactory.Icon(t, "Icon", database, 2, 156f, 167f, 16f, "Overlay", 12);
            nameLabel = ViewFactory.Label(t, "Name", "", 178f, 162f, 138f, 15f,
                                          Palette.Hex("#6b4a2e"), "Overlay", 12,
                                          TextAlignmentOptions.Left);
            ViewFactory.Label(t, "Hint", "tap it in the cloth to hand it over",
                              178f, 180f, 138f, 9f, Palette.Hex("#a58358"), "Overlay", 12,
                              TextAlignmentOptions.Left, FontStyles.Normal);
            SetVisible(false);
        }

        public void Show(int tier, float now)
        {
            if (!IsBuilt || tier < 0) { Hide(); return; }
            SetVisible(true);
            shownAt = now;
            if (tier == shownTier) return;
            shownTier = tier;
            if (database != null) icon.sprite = database.Pastry(tier);
            nameLabel.text = $"{TierTable.Names[tier]}, please!";
        }

        public void Hide()
        {
            SetVisible(false);
            shownTier = -1;
            shownAt = -1f;
        }

        void Update()
        {
            if (shownAt < 0f || !IsBuilt) return;
            // pop: .35s back-out, matching cubic-bezier(.34,1.56,.64,1)
            float k = Mathf.Clamp01((Time.time - shownAt) / 0.35f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            Content.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
        }
    }

    /// <summary>
    /// The hanging sign (§8.4). Reads "next customer in / {n}s" when closed,
    /// "now serving / ♥" when open.
    /// </summary>
    public sealed class SignView : GeneratedView
    {
        TextMeshPro topLine, bigLine;
        string shownTop, shownBig;

        protected override void Build()
        {
            shownTop = shownBig = null;
            var t = Content;

            var ropeL = ViewFactory.Rect(t, "RopeLeft", Shapes.White, 40f, 54f, 3f, 26f,
                                         Palette.ChipFill, "Furniture", 10);
            ropeL.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
            var ropeR = ViewFactory.Rect(t, "RopeRight", Shapes.White, 88f, 54f, 3f, 26f,
                                         Palette.ChipFill, "Furniture", 10);
            ropeR.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);

            // drop shadow, border, face — three stacked panels fake border+shadow (§8.4)
            ViewFactory.Panel(t, "BoardShadow", 8f, 80f, 118f, 76f, 10, Palette.Crust, "Furniture", 10);
            ViewFactory.Panel(t, "BoardBorder", 8f, 76f, 118f, 76f, 10, Palette.Wood, "Furniture", 11);
            ViewFactory.Panel(t, "Board", 11f, 79f, 112f, 70f, 8, Palette.Hex("#fffaf0"), "Furniture", 12);

            topLine = ViewFactory.Label(t, "TopLabel", "next customer in", 8f, 98f, 118f, 10f,
                                        Palette.Hex("#a58358"), "Furniture", 13,
                                        TextAlignmentOptions.Center, FontStyles.Normal);
            bigLine = ViewFactory.Label(t, "BigLabel", "18s", 8f, 130f, 118f, 30f,
                                        Palette.Crust, "Furniture", 13);
        }

        public void Sync(ShopState state, int secondsShown)
        {
            if (!IsBuilt) return;
            string top = state == ShopState.Closed ? "next customer in" : "now serving";
            string big = state == ShopState.Closed ? $"{secondsShown}s" : "♥";
            if (top != shownTop) { shownTop = top; topLine.text = top; }
            if (big != shownBig) { shownBig = big; bigLine.text = big; }
        }

        void Update()
        {
            // swing ±1.2° over 3.6s, pivoting at the ropes
            float a = Mathf.Sin(Time.time / 3.6f * Mathf.PI * 2f) * 1.2f;
            transform.localRotation = Quaternion.Euler(0f, 0f, a);
        }
    }

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

            ViewFactory.Rect(t, "Scrim", Shapes.White, -40f, -40f, 510f, 960f,
                             new Color(0f, 0f, 0f, 0.35f), "Overlay", 60);
            ViewFactory.Panel(t, "Card", 55f, 300f, 320f, 240f, 20, Palette.Hex("#fffaf0"), "Overlay", 61);
            ViewFactory.Label(t, "Title", "Sold out!", 55f, 350f, 320f, 34f, Palette.Crust, "Overlay", 62);
            scoreLabel = ViewFactory.Label(t, "Score", "0", 55f, 410f, 320f, 44f,
                                           Palette.Hex("#c9502f"), "Overlay", 62);
            bestLabel = ViewFactory.Label(t, "Best", "best 0", 55f, 452f, 320f, 14f,
                                          Palette.Hex("#a58358"), "Overlay", 62,
                                          TextAlignmentOptions.Center, FontStyles.Normal);
            ViewFactory.Panel(t, "Button", 125f, 480f, 180f, 46f, 14, Palette.Amber, "Overlay", 62);
            ViewFactory.Label(t, "ButtonLabel", "Play again", 125f, 508f, 180f, 17f,
                              Palette.DarkCrust, "Overlay", 63);
            SetVisible(false);
        }

        public Rect ButtonRect => new Rect(125f, 480f, 180f, 46f);

        public void Show(int score, int best, bool newBest)
        {
            if (!IsBuilt) return;
            SetVisible(true);
            scoreLabel.text = score.ToString("N0");
            bestLabel.text = newBest ? "new best!" : $"best {best:N0}";
        }

        public void Hide() => SetVisible(false);
    }
}
