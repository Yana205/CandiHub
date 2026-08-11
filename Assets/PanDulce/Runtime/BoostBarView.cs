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

        /// <summary>
        /// The roots' resting local positions, read back from the scene at the end of Build.
        ///
        /// Read rather than computed: the constants above only decide where the roots FIRST
        /// land, and from then on the scene owns them. Sync composes the wiggle and the ready
        /// pulse onto whatever is read here, so an animation can never drag a hand-placed
        /// button back to ButtonShiftX/Y a frame later.
        /// </summary>
        Vector3 buttonHome, clearHome;

        /// <summary>
        /// The shake root's resting scale, read back with buttonHome. The ready pulse used
        /// to write <c>Vector3.one * (1 + pulse)</c>, which silently undid any scale the
        /// designer set on the node — the button played 6% bigger than it was authored and
        /// its face, hanging ~8.5 units below the pivot, sagged ~50 stage px out of the row.
        /// </summary>
        Vector3 buttonScaleHome = Vector3.one;

        SpriteRenderer button, chargeFill, clearFace, badge, badgeBorder;
        TextMeshPro label, badgeLabel, clearLabel, priceLabel;
        Transform buttonRoot, clearRoot;
        float shownCharge, denyAt, clearDenyAt;
        bool shownReady, shownCanBuy;
        int shownCost;
        string shownBadge;

        /// <summary>Clearance root's resting scale — captured with clearHome, same reason.</summary>
        Vector3 clearScaleHome = Vector3.one;

        // Interaction feel (Yana, 2026-08-11: "the booster buttons are boring"). All of it
        // composes multiplicatively onto the scale homes, and every sim-clock stamp gets the
        // same future-stamp drop DenyWiggle needs — Restart rewinds Sim.Now.
        const float PressScale = 0.93f;     // squash while the finger is down
        const float PressDropPx = 2f;       // and sink a touch, like a real key
        const float CelebrateSec = 0.7f;    // READY! soft swell + cream badge glow
        const float FirePunchSec = 0.35f;   // jolt when a boost actually fires
        bool pressShake, pressClear;
        float pressKShake = 1f, pressKClear = 1f;
        float readyAt = -1f, shakeFiredAt = -1f, clearFiredAt = -1f;
        Color badgeBase, badgeBorderBase;

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

            buttonRoot = ViewFactory.NodeTransform(t, "ShakeButton", ButtonShiftX, ButtonShiftY);

            // The shadow reuses the face drawing so its corners match; only the tint differs.
            ViewFactory.Plate(buttonRoot, "Shadow", faceArt, 12f, 836f, 226f, 46f, 15,
                              Palette.Hex("#6f4a2c"), "Overlay", 31);
            button = ViewFactory.Plate(buttonRoot, "Face", faceArt, 12f, 833f, 226f, 46f, 15,
                                       readyTint, "Overlay", 32);

            // shaker icon: rotated cream square + knot circle + two motion dashes (§8.7)
            ViewFactory.Panel(buttonRoot, "IconSquare", 25f, 845f, 17f, 17f, 4,
                              Palette.Cream, "Overlay", 33, -12f);
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
            clearRoot = ViewFactory.NodeTransform(t, "ClearanceButton", ButtonShiftX, ButtonShiftY);

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

            clearLabel = ViewFactory.Label(clearRoot, "Label", "Toss the minis",
                                           271f, 854.8f, 130f, 14f, Palette.Cream, "Overlay", 34);
            if (clearLabel != null) clearLabel.alpha = 0.55f;

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

            buttonHome = buttonRoot != null ? buttonRoot.localPosition : Vector3.zero;
            buttonScaleHome = buttonRoot != null ? buttonRoot.localScale : Vector3.one;
            clearHome = clearRoot != null ? clearRoot.localPosition : Vector3.zero;
            clearScaleHome = clearRoot != null ? clearRoot.localScale : Vector3.one;
            badgeBase = badge != null ? badge.color : Color.white;
            badgeBorderBase = badgeBorder != null ? badgeBorder.color : Color.white;
        }

        /// <summary>
        /// Sizes the badge plates to whatever the label currently reads, growing leftward
        /// from the fixed right edge. Call after any change to BadgeLabel.text.
        /// </summary>
        void FitBadge()
        {
            if (badgeLabel == null) return;

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
            if (badgeText != shownBadge && badgeLabel != null)
            {
                shownBadge = badgeText;
                badgeLabel.text = badgeText;
                FitBadge();
            }

            if (ready != shownReady)
            {
                shownReady = ready;
                if (label != null)
                    label.text = ready ? "Shake the furoshiki!" : "Merge desserts to charge!";
                if (ready) readyAt = now;   // charging paid off — one big bounce + badge flash
            }

            // Charging reads at 0.72 opacity; ready breathes over ReadyPulsePeriod (§8.7).
            float alpha = ready ? 1f : 0.72f;
            if (button != null) button.color = Palette.WithAlpha(ready ? readyTint : idleTint, alpha);
            if (label != null) label.alpha = alpha;

            // Deny wiggle: a decaying side-shake after a tap on the uncharged button.
            float wiggle = DenyWiggle(ref denyAt, now);

            // READY! celebration: one soft happy swell while the badge warms to cream.
            // The first cut bounced hard and blinked amber — it read as an alarm, not a
            // treat (Yana, 2026-08-11: "kinda scary") — so no blinking, one gentle arc.
            if (now < readyAt) readyAt = -1f;
            float celebrate = 0f, glow = 0f;
            if (readyAt >= 0f)
            {
                float ck = (now - readyAt) / CelebrateSec;
                if (ck >= 1f) readyAt = -1f;
                else
                {
                    celebrate = Mathf.Sin(ck * Mathf.PI) * 0.1f;
                    glow = Mathf.Sin(ck * Mathf.PI) * 0.5f;
                }
            }
            if (badge != null) badge.color = Color.Lerp(badgeBase, Palette.Cream, glow);
            if (badgeBorder != null)
                badgeBorder.color = Color.Lerp(badgeBorderBase, Palette.Cream, glow * 0.6f);

            // Press squash eases toward its target so release springs back, never snaps.
            pressKShake = Squash(pressKShake, pressShake);

            // Fire jolt: the moment a shake actually spends the meter.
            float fired = Punch(ref shakeFiredAt, now, FirePunchSec, 0.15f);

            // Ready breathe: composes with the wiggle above — wiggle owns x, pulse owns y.
            // Both offset buttonHome, which is wherever the button was placed in the scene.
            float pulse = ready ? Mathf.Sin(now / ReadyPulsePeriod * Mathf.PI * 2f) : 0f;
            if (buttonRoot != null)
            {
                buttonRoot.localPosition = buttonHome
                                         + new Vector3(wiggle * StageCoords.PX,
                                                       (pulse * ReadyPulseBobPx
                                                        - (1f - pressKShake) / (1f - PressScale) * PressDropPx)
                                                       * StageCoords.PX, 0f);
                buttonRoot.localScale = buttonScaleHome
                                      * (1f + Mathf.Max(0f, pulse) * ReadyPulseScale)
                                      * pressKShake * (1f + celebrate + fired);
            }
        }

        /// <summary>
        /// A decaying sinusoidal scale punch off a sim-clock stamp — 0 when idle. Owns the
        /// stamp's lifetime the way DenyWiggle does, future-stamp drop included.
        /// </summary>
        static float Punch(ref float stamp, float now, float duration, float strength)
        {
            if (now < stamp) stamp = -1f;
            if (stamp < 0f) return 0f;
            float k = (now - stamp) / duration;
            if (k >= 1f) { stamp = -1f; return 0f; }
            return Mathf.Sin(k * Mathf.PI) * (1f - k) * strength;
        }

        /// <summary>One step of the press squash — quick ease toward held/released.</summary>
        static float Squash(float k, bool pressed)
            => Mathf.Lerp(k, pressed ? PressScale : 1f,
                          1f - Mathf.Exp(-18f * Time.deltaTime));

        /// <summary>Finger state over each face, polled per frame by GameRoot.</summary>
        public void SetPressed(bool shakeBtn, bool clearBtn)
        {
            pressShake = shakeBtn;
            pressClear = clearBtn;
        }

        /// <summary>A shake that actually fired — jolt the button.</summary>
        public void FireShake(float now) => shakeFiredAt = now;

        /// <summary>A clearance that actually fired — jolt that button too.</summary>
        public void FireClearance(float now) => clearFiredAt = now;

        /// <summary>World centre of the shake face, for sparkles that land on the drawing.</summary>
        public Vector3 ButtonWorldCenter
            => button != null ? button.bounds.center : transform.position;

        /// <summary>World centre of the clearance face.</summary>
        public Vector3 ClearanceWorldCenter
            => clearFace != null ? clearFace.bounds.center : transform.position;

        /// <summary>Affordability + availability drive the clearance button's read.</summary>
        public void SyncClearance(int coins, int cost, bool hasTargets, float now)
        {
            if (!IsBuilt) return;

            if (cost != shownCost)
            {
                shownCost = cost;
                if (priceLabel != null) priceLabel.text = $"${cost}";
            }

            bool canBuy = coins >= cost && hasTargets;
            if (canBuy != shownCanBuy)
            {
                shownCanBuy = canBuy;
                float alpha = canBuy ? 1f : 0.55f;
                if (clearFace != null)
                    clearFace.color = Palette.WithAlpha(canBuy ? readyTint : idleTint, alpha);
                if (clearLabel != null) clearLabel.alpha = alpha;
            }

            // Same deny grammar as the shake button: a decaying side-shake, off clearHome —
            // and the same press squash and fire jolt, off clearScaleHome.
            float wiggle = DenyWiggle(ref clearDenyAt, now);
            pressKClear = Squash(pressKClear, pressClear);
            float fired = Punch(ref clearFiredAt, now, FirePunchSec, 0.15f);
            if (clearRoot != null)
            {
                clearRoot.localPosition = clearHome
                                        + new Vector3(wiggle * StageCoords.PX,
                                                      -(1f - pressKClear) / (1f - PressScale)
                                                       * PressDropPx * StageCoords.PX, 0f);
                clearRoot.localScale = clearScaleHome * pressKClear * (1f + fired);
            }
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

        /// <summary>
        /// The stage-px rect a face renderer actually occupies, for hit testing without a
        /// Canvas. Read from the renderer, not rebuilt from the §8.7 constants: the buttons
        /// are hand-placed (and hand-scaled) in the scene, sometimes under folder nodes, and
        /// a constant-based rect drifts off the drawing the moment the designer moves it —
        /// taps were landing a full button-height below the face. The view's parent frame is
        /// the stage frame, the same assumption StageOffset always made.
        /// </summary>
        Rect FaceStageRect(SpriteRenderer face, float fallbackX, float fallbackW)
        {
            if (face == null)
            {
                Vector2 o = StageOffset;
                return new Rect(fallbackX + ButtonShiftX + o.x, 833f + ButtonShiftY + o.y,
                                fallbackW, 46f);
            }

            Transform space = transform.parent != null ? transform.parent : transform;
            Vector3 c = space.InverseTransformPoint(face.transform.position);
            Vector3 span = space.lossyScale, own = face.transform.lossyScale;
            float w = face.size.x * Mathf.Abs(span.x > 0f ? own.x / span.x : own.x) / StageCoords.PX;
            float h = face.size.y * Mathf.Abs(span.y > 0f ? own.y / span.y : own.y) / StageCoords.PX;
            return new Rect(c.x / StageCoords.PX - w * 0.5f,
                            -c.y / StageCoords.PX - h * 0.5f, w, h);
        }

        /// <summary>Stage-px rect of the shake button face.</summary>
        public Rect ButtonRect => FaceStageRect(button, 12f, 226f);

        /// <summary>Stage-px rect of the clearance button face.</summary>
        public Rect ClearanceRect => FaceStageRect(clearFace, 250f, 168f);
    }
}
