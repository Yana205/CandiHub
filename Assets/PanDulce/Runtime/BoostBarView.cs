using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Boost bar — stage (0,824) 430 × 56 (§8.7). Shown only when boostsOn.
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

        /// <summary>How long a deny shake takes to decay to nothing, in seconds.</summary>
        const float DenyShakeSec = 0.35f;

        /// <summary>Peak side travel of a deny shake, in stage px.</summary>
        const float DenyShakePx = 4f;

        /// <summary>
        /// Charge badge geometry, in stage px. Only the width moves: the label swaps between
        /// "0%" and "READY!", which is close to three times wider, so a fixed plate either
        /// clips the word or leaves the figure swimming. The RIGHT edge is what stays pinned
        /// — the badge sits on the button's top-right corner, and growing rightward would run
        /// it under the clearance button that starts at stage x 250.
        /// </summary>
        const float BadgeRight = 256f;
        const float BadgeTop = 824f;
        const float BadgeH = 21f;
        const float BadgeMinW = 36f;      // the "0%" plate the bar was authored at
        const float BadgePadX = 9f;       // cream either side of the text
        const float BadgeRim = 2f;        // how far BadgeBorder stands proud of Badge
        const float BadgeBaseline = 14f;  // label centre, down from the plate top

        /// <summary>
        /// The slab, in stage px. Authored by hand in the scene (Lital, 2026-08-09) and read
        /// back from there: deliberately larger than the 446 × 965 an iPhone 15 shows, so the
        /// drawing runs off both sides and past the bottom instead of ending in a seam. The
        /// top edge stays on 824, which is the line §8.7 authored the bar against.
        /// </summary>
        const float BarX = -34.7f;
        const float BarY = 824f;
        const float BarW = 477.4f;
        const float BarH = 128.45f;

        /// <summary>Edge bands, kept for the no-art fallback. Both sit on the slab's top edge.</summary>
        const float BorderH = 3f;
        const float SheenY = BarY + BorderH;

        /// <summary>
        /// Where both buttons sit relative to the y values §8.7 authored them at, in stage px.
        ///
        /// The slab was redrawn taller and the buttons stayed pinned near its top edge, which
        /// left them riding high with a wide empty band underneath. This re-centres the pair
        /// in the strip the slab actually shows — 806 down to the screen's bottom edge — and
        /// cancels the node's own +11 nudge so the two buttons straddle the screen centre.
        /// Everything inside a button is placed relative to its root, so shifting the roots
        /// carries the icons, labels, charge track and both badges along with them.
        /// </summary>
        const float ButtonShiftX = -11f;
        const float ButtonShiftY = 26f;

        /// <summary>The roots' resting local position. Sync composes the wiggle and the ready
        /// pulse onto this, so the shift cannot be overwritten a frame later.</summary>
        static Vector3 ButtonHome => StageCoords.Stage(ButtonShiftX, ButtonShiftY);

        SpriteRenderer button, chargeFill, clearFace, badge, badgeBorder;
        TextMeshPro label, badgeLabel, clearLabel, priceLabel;
        Transform buttonRoot, clearRoot;
        float shownCharge, denyAt, clearDenyAt;
        bool shownReady, shownCanBuy;
        int shownCost;
        string shownBadge;

        /// <summary>
        /// Face tints for the ready/idle states. The drawn button carries its own colour, so
        /// with art both are white and only the alpha still separates the two states —
        /// multiplying Amber over the salmon would just muddy it.
        /// </summary>
        Color readyTint, idleTint;

        protected override void Build()
        {
            shownCharge = -1f;
            denyAt = -1f;
            clearDenyAt = -1f;
            shownReady = false;
            shownCanBuy = false;
            shownCost = -1;
            shownBadge = null;
            var t = Content;

            // The drawn slab already carries its own top edge and highlight, so it replaces
            // the generated gradient plus the TopBorder and TopSheen strips that stood in for
            // them.
            Sprite barArt = skin != null ? skin.BottomBar : null;
            ViewFactory.Rect(t, "Background",
                             barArt != null ? barArt : Shapes.VerticalGradient(64, 1f, 0.85f),
                             BarX, BarY, BarW, BarH,
                             barArt != null ? Color.white : Palette.BarTop, "Overlay", 30);
            if (barArt == null)
            {
                ViewFactory.Rect(t, "TopBorder", Shapes.White, BarX, BarY,
                                 BarW, BorderH, Palette.BarBorder, "Overlay", 31);
                ViewFactory.Rect(t, "TopSheen", Shapes.White, BarX, SheenY,
                                 BarW, BorderH,
                                 new Color(1f, 225f/255f, 180f/255f, 0.22f), "Overlay", 31);
            }

            Sprite faceArt = skin != null ? skin.Button : null;
            Sprite plateArt = skin != null ? skin.Plate : null;
            readyTint = faceArt != null ? Color.white : Palette.Amber;
            idleTint = faceArt != null ? Color.white : Palette.AmberDeep;

            buttonRoot = ViewFactory.Node(t, "ShakeButton", ButtonShiftX, ButtonShiftY).transform;

            // The shadow reuses the face drawing so its corners match; only the tint differs.
            ViewFactory.Plate(buttonRoot, "Shadow", faceArt, 12f, 836f, 226f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            button = ViewFactory.Plate(buttonRoot, "Face", faceArt, 12f, 833f, 226f, 46f, 15,
                                       readyTint, "Overlay", 32);

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
                                      48f, 851f, 184f, 15f, Palette.Cream, "Overlay", 34);

            ViewFactory.Rect(buttonRoot, "ChargeTrack", Shapes.RoundedRect(12, 12, 4),
                             45f, 864f, 160f, 7f,
                             Palette.WithAlpha(Palette.Hex("#6f4a2c"), 0.4f), "Overlay", 34);
            chargeFill = ViewFactory.Rect(buttonRoot, "ChargeFill", Shapes.RoundedRect(12, 12, 4),
                                          45f, 864f, 1f, 7f, Palette.Cream, "Overlay", 35);

            // Badge overlapping the button's top-right corner. Built at its narrowest and
            // then fitted, so the plates start out matching whatever the label says.
            badgeBorder = ViewFactory.Plate(buttonRoot, "BadgeBorder", plateArt,
                                            BadgeRight - BadgeMinW - BadgeRim, BadgeTop - BadgeRim,
                                            BadgeMinW + BadgeRim * 2f, BadgeH + BadgeRim * 2f, 12,
                                            Palette.Hex("#6f4a2c"), "Overlay", 35);
            badge = ViewFactory.Plate(buttonRoot, "Badge", plateArt,
                                      BadgeRight - BadgeMinW, BadgeTop, BadgeMinW, BadgeH, 10,
                                      BadgeTint(plateArt), "Overlay", 36);
            badgeLabel = ViewFactory.Label(buttonRoot, "BadgeLabel", "0%",
                                           BadgeRight - BadgeMinW, BadgeTop + BadgeBaseline,
                                           BadgeMinW, 11f, BadgeInk(plateArt), "Overlay", 37);
            FitBadge();

            // --- Day-old clearance: coin-priced, pops every tier-0/1 pastry ---
            clearRoot = ViewFactory.Node(t, "ClearanceButton", ButtonShiftX, ButtonShiftY).transform;

            ViewFactory.Plate(clearRoot, "Shadow", faceArt, 250f, 836f, 168f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            clearFace = ViewFactory.Plate(clearRoot, "Face", faceArt, 250f, 833f, 168f, 46f, 15,
                                          Palette.WithAlpha(idleTint, 0.55f), "Overlay", 32);

            // coin icon: gold rim, cream fill, $ stamp. The drawn coin lives on the price
            // badge below instead — one coin per button is enough.
            ViewFactory.Rect(clearRoot, "CoinRim", Shapes.Circle(32), 262f, 843f, 18f, 18f,
                             Palette.Hex("#c07f1c"), "Overlay", 33);
            ViewFactory.Rect(clearRoot, "CoinFill", Shapes.Circle(32), 264f, 845f, 14f, 14f,
                             Palette.Cream, "Overlay", 34);
            ViewFactory.Label(clearRoot, "CoinStamp", "$", 262f, 856f, 18f, 10f,
                              Palette.Hex("#c07f1c"), "Overlay", 35);

            clearLabel = ViewFactory.Label(clearRoot, "Label", "Clear day-olds",
                                           271f, 854.8f, 130f, 14f, Palette.Cream, "Overlay", 34);
            clearLabel.alpha = 0.55f;

            // Price badge overlapping the button's top-right corner. The coin-counter drawing
            // is a coin plus a number pill, so it takes the whole badge: the inner Price
            // plate would cover it, and the figure moves onto the pill the way the top bar's
            // coin chip does. Sized to the drawing's own 524 × 191 aspect, right edge kept
            // where the badge already ended.
            Sprite priceArt = skin != null ? skin.CoinChip : null;
            if (priceArt != null)
            {
                ViewFactory.Rect(clearRoot, "PriceBorder", priceArt, 368f, 822f, 60f, 22f,
                                 Color.white, "Overlay", 35);
                priceLabel = ViewFactory.Label(clearRoot, "PriceLabel", "$30", 391f, 833f, 35f, 11f,
                                               Palette.Crust, "Overlay", 37);
            }
            else
            {
                ViewFactory.Panel(clearRoot, "PriceBorder", 388f, 822f, 40f, 25f, 12,
                                  Palette.Hex("#6f4a2c"), "Overlay", 35);
                ViewFactory.Plate(clearRoot, "Price", plateArt, 390f, 824f, 36f, 21f, 10,
                                  BadgeTint(plateArt), "Overlay", 36);
                priceLabel = ViewFactory.Label(clearRoot, "PriceLabel", "$30", 390f, 838f, 36f, 11f,
                                               BadgeInk(plateArt), "Overlay", 37);
            }
        }

        /// <summary>
        /// Sizes the badge plates to whatever the label currently reads, growing leftward
        /// from the fixed right edge. Call after any change to BadgeLabel.text.
        /// </summary>
        void FitBadge()
        {
            // GetPreferredValues measures in local units; wrapping is off, so this is the
            // unwrapped run. A font asset that is not ready yet reports 0, and the minimum
            // width covers that — the next Sync re-fits with a real measurement.
            float textW = badgeLabel.GetPreferredValues(badgeLabel.text).x / StageCoords.PX;
            float w = Mathf.Max(BadgeMinW, textW + BadgePadX * 2f);
            float x = BadgeRight - w;

            ViewFactory.Place(badgeBorder, x - BadgeRim, BadgeTop - BadgeRim,
                              w + BadgeRim * 2f, BadgeH + BadgeRim * 2f);
            ViewFactory.Place(badge, x, BadgeTop, w, BadgeH);
            ViewFactory.Place(badgeLabel, x, BadgeTop + BadgeBaseline, w);
        }

        /// <summary>Untinted when the drawing carries its own colour, else the flat chip fill.</summary>
        static Color BadgeTint(Sprite art) => art != null ? Color.white : Palette.ChipFill;

        /// <summary>The drawn badge is cream, so its figure has to darken to stay legible.</summary>
        static Color BadgeInk(Sprite art) => art != null ? Palette.Crust : Palette.Cream;

        public void Sync(float charge, bool ready, bool boostsOn, float now)
        {
            if (!IsBuilt) return;
            SetVisible(boostsOn);
            if (!boostsOn) return;

            if (!Mathf.Approximately(charge, shownCharge))
            {
                shownCharge = charge;
                ViewFactory.Place(chargeFill, 45f, 864f, Mathf.Max(1f, 160f * charge), 7f);
            }

            // Keyed on the string, not on charge: the badge has to re-fit on every width
            // change, and "99%" → "READY!" is the widest jump of all.
            string badgeText = ready ? "READY!" : $"{Mathf.RoundToInt(charge * 100f)}%";
            if (badgeText != shownBadge)
            {
                shownBadge = badgeText;
                badgeLabel.text = badgeText;
                FitBadge();
            }

            if (ready != shownReady)
            {
                shownReady = ready;
                label.text = ready ? "Shake the furoshiki!" : "Merge desserts to charge!";
            }

            // Charging reads at 0.72 opacity; ready breathes over ReadyPulsePeriod (§8.7).
            float alpha = ready ? 1f : 0.72f;
            button.color = Palette.WithAlpha(ready ? readyTint : idleTint, alpha);
            label.alpha = alpha;

            // Deny wiggle: a decaying side-shake after a tap on the uncharged button.
            float wiggle = DenyWiggle(ref denyAt, now);

            // Ready breathe: composes with the wiggle above — wiggle owns x, pulse owns y.
            float pulse = ready ? Mathf.Sin(now / ReadyPulsePeriod * Mathf.PI * 2f) : 0f;
            buttonRoot.localPosition = ButtonHome
                                     + new Vector3(wiggle * StageCoords.PX,
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
                clearFace.color = Palette.WithAlpha(canBuy ? readyTint : idleTint, alpha);
                clearLabel.alpha = alpha;
            }

            // Same deny grammar as the shake button: a decaying side-shake.
            float wiggle = DenyWiggle(ref clearDenyAt, now);
            clearRoot.localPosition = ButtonHome + new Vector3(wiggle * StageCoords.PX, 0f, 0f);
        }

        /// <summary>
        /// The decaying side-shake after a rejected tap, in stage px — and the one place that
        /// owns a deny stamp's lifetime, since both buttons shake to the same grammar.
        ///
        /// The stamps are taken from Sim.Now, which Restart winds back to 0, so a stamp can
        /// end up in the FUTURE. Such a stamp has to be DROPPED rather than measured against:
        /// (1 - k) is a decay only while k climbs from 0 towards 1, and a negative k turns it
        /// into growth. That was the bug behind the button teleporting across the x axis on
        /// itch — tap the uncharged button, die, hit Play again, and a two-minute-old stamp
        /// threw it ±1500 stage px across a 446 px screen on every single frame, for as long
        /// as the new run's clock took to climb back to the old stamp. It only showed up for
        /// players who had been denied before restarting, which is why some devices looked
        /// fine. Clearing the stamp here also stops it re-firing a spurious shake later, at
        /// the moment the new clock passes it.
        /// </summary>
        static float DenyWiggle(ref float stamp, float now)
        {
            if (now < stamp) stamp = -1f;
            if (stamp < 0f) return 0f;

            float k = (now - stamp) / DenyShakeSec;
            return k < 1f ? Mathf.Sin(k * Mathf.PI * 4f) * (1f - k) * DenyShakePx : 0f;
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

        /// <summary>Stage-px rect of the shake button face, for hit testing without a Canvas.
        /// Carries ButtonShift so taps follow the re-centred face, not where §8.7 drew it.</summary>
        public Rect ButtonRect
        {
            get
            {
                Vector2 o = StageOffset;
                return new Rect(12f + ButtonShiftX + o.x, 833f + ButtonShiftY + o.y, 226f, 46f);
            }
        }

        /// <summary>Stage-px rect of the clearance button face.</summary>
        public Rect ClearanceRect
        {
            get
            {
                Vector2 o = StageOffset;
                return new Rect(250f + ButtonShiftX + o.x, 833f + ButtonShiftY + o.y, 168f, 46f);
            }
        }
    }
}
