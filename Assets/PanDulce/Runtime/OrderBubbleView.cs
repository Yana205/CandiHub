using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>Order bubble — pops in beside the bear after he arrives (§8.4).</summary>
    public sealed class OrderBubbleView : GeneratedView
    {
        // Content layout inside the cream face, in stage px.
        //
        // The icon used to sit at x 212 with its drawn left edge 47 px inside the face, so the
        // bubble opened with a wide empty gap and pushed the label into the right-hand corner —
        // and the label's band ran 51 px PAST the face, so its size was capped by the overflow
        // rather than by the space. Reclaiming that gap is where the room for a bigger label
        // comes from; the font alone had nowhere to grow.
        //
        // Two measured facts drive the numbers, both taken in the FACE's own space (the stage
        // root is scaled, so world-space bounds are ~0.585× and will mislead you):
        //  · the pastry sprites draw about 1.4× past CanonicalSpriteRadius, so a SetIcon radius
        //    of 16 lands ~44 px across — IconHalf, which is what the layout has to reserve.
        //    Sized so the widest case multiplier (roll cake, 1.15) plus the ±8% breathe in
        //    Update still clears the 64 px face.
        //  · the longest order the game can ask for, "Choco Donut, please!", renders 145 px at
        //    font 17 — inside the 156 px band beside the icon, with 11 px to spare.
        // Re-measure both from MeshRenderer.bounds via Content.InverseTransformPoint if a
        // longer dessert name or a fatter sprite is ever added. TMP's preferredWidth is no use
        // here: it reports roughly three times the truth for this font.
        const float IconRadius = 18f;   // SetIcon radius, before the dessert's case-size multiplier
        const float IconHalf = 25f;     // and the drawn half-width that radius actually produces
        const float FontSize = 19f;
        const float PadX = 14f;         // face edge → icon, and label band → face edge
        const float Gap = 10f;          // icon → label

        // The cream face the two sit inside, and the slots derived from it.
        //
        // The face grew from 238 × 64 to 266 × 76 (centre held, so it opens outwards evenly)
        // because the icon and label were already at the ceiling of the old one — height is
        // the binding constraint, and trimming padding buys width the icon cannot use. Every
        // number below is sized off this face, so widening it again is the one edit needed to
        // grow the contents further.
        const float FaceX = 129f, FaceY = 157f, FaceW = 266f, FaceH = 76f;
        const float MidY = FaceY + FaceH * 0.5f;
        const float IconX = FaceX + PadX + IconHalf;
        const float TextX = IconX + IconHalf + Gap;
        const float TextW = FaceX + FaceW - PadX - TextX;

        // The chrome around the face: a 3 px border ring, and a shadow of the same plate
        // dropped 4 px. Derived from the face so the three can never drift apart.
        const float Rim = 3f, ShadowDrop = 4f;
        const float PlateX = FaceX - Rim, PlateY = FaceY - Rim;
        const float PlateW = FaceW + Rim * 2f, PlateH = FaceH + Rim * 2f;
        const float FaceRadius = 25f, PlateRadius = FaceRadius + Rim;

        // The tail hangs off the bottom edge: an 18 px square turned 45°, so half of it shows
        // below the face and the other half is buried in it.
        const float TailSize = 18f, TailCx = 205f;
        const float TailCy = FaceY + FaceH;

        /// <summary>Rendered text may run this close to the label band's end before the
        /// bubble starts growing — a little slack so a hairline overshoot doesn't resize.</summary>
        const float TextSlackPx = 8f;

        /// <summary>Widening cap, split evenly left and right of the authored centre. At the
        /// authored face (129…395 in a 430 frame) ±55 keeps both edges inside the stage.</summary>
        const float MaxExtraPx = 110f;

        SpriteRenderer icon, shadowPlate, borderPlate, boxFace;
        TextMeshPro nameLabel, hintLabel;
        Quaternion hintHomeRot = Quaternion.identity;
        Vector3 hintHomeScale = Vector3.one;
        float shownAt;
        int shownTier;
        float iconBaseScale;

        // The authored geometry, read back from the scene at the end of Build — the base the
        // per-order widening grows from and shrinks back to. Computed rects would fight the
        // designer; these follow wherever the bubble is dragged.
        Rect shadowHome, borderHome, boxHome;
        float nameHomeLeft, nameHomeY, nameHomeW;
        float iconHomeX;
        float extraShown;

        protected override void Build()
        {
            shownAt = -1f;
            shownTier = -1;
            var t = Content;

            // Radii are a third of the panel height rather than the 18/16 they were: the old
            // numbers were read through a sprite that got stretched 6× across, so they landed
            // on screen as a long lozenge. Now that Panel 9-slices, the authored radius is the
            // radius you get, and these keep the soft bubble the stretch used to fake. The
            // inner face is Rim smaller in radius as well as in rect, so the border reads the
            // same width around the corners as it does along the edges.
            shadowPlate = ViewFactory.Panel(t, "Shadow", PlateX, PlateY + ShadowDrop, PlateW, PlateH,
                                            (int)PlateRadius,
                                            new Color(122f/255f, 84f/255f, 49f/255f, 0.25f), "Overlay", 9);
            borderPlate = ViewFactory.Panel(t, "Border", PlateX, PlateY, PlateW, PlateH, (int)PlateRadius,
                                            Palette.Hex("#e0cba6"), "Overlay", 10);
            boxFace = ViewFactory.Panel(t, "Box", FaceX, FaceY, FaceW, FaceH, (int)FaceRadius,
                                        Palette.Hex("#fffaf0"), "Overlay", 11);
            ViewFactory.Panel(t, "Tail", TailCx - TailSize * 0.5f, TailCy - TailSize * 0.5f,
                              TailSize, TailSize, 3, Palette.Hex("#fffaf0"), "Overlay", 11, 45f);

            // Both sit on the face's midline. Label y is its vertical centre, not a baseline —
            // TextAlignmentOptions.Left is middle-left — so the same MidY centres both.
            icon = ViewFactory.Icon(t, "Icon", database, 2, IconX, MidY, IconRadius, "Overlay", 12);
            iconBaseScale = icon != null ? icon.transform.localScale.x : 1f;
            nameLabel = ViewFactory.Label(t, "Name", "", TextX, MidY, TextW, FontSize,
                                          Palette.Hex("#6b4a2e"), "Overlay", 12,
                                          TextAlignmentOptions.Left);
            hintLabel = ViewFactory.Label(t, "Hint", "Press & hold to give!",
                                          192f, 208f, 180f, 11f, Color.white, "Overlay", 12,
                                          TextAlignmentOptions.Left, FontStyles.Bold);
            if (hintLabel != null)
            {
                // Code owns the hint's LOOK (Yana, 2026-08-11: the old sentence was too long
                // and too beige) — re-applied every bind, unlike layout, so the scene's older
                // wording cannot linger. Position stays whatever the scene says.
                hintLabel.text = "Press & hold to give!";
                hintLabel.color = Color.white;
                hintLabel.fontStyle = FontStyles.Bold;
                hintLabel.fontSize = 11f * StageCoords.PX * 10f;   // ViewFactory's TMP mapping
                hintHomeRot = hintLabel.transform.localRotation;
                hintHomeScale = hintLabel.transform.localScale;
            }

            // The authored base the per-order widening works from — read back from the scene,
            // the way BoostBarView reads buttonHome, so a hand-dragged bubble stays the truth.
            extraShown = 0f;
            shadowHome = HomeRect(shadowPlate);
            borderHome = HomeRect(borderPlate);
            boxHome = HomeRect(boxFace);
            if (icon != null) iconHomeX = icon.transform.localPosition.x / StageCoords.PX;
            if (nameLabel != null)
            {
                nameHomeW = nameLabel.rectTransform.sizeDelta.x / StageCoords.PX;
                nameHomeLeft = nameLabel.transform.localPosition.x / StageCoords.PX - nameHomeW * 0.5f;
                nameHomeY = -nameLabel.transform.localPosition.y / StageCoords.PX;
            }
            SetVisible(false);
        }

        /// <summary>A renderer's current stage-px rect (y-down, top-left), inverse of Place.</summary>
        static Rect HomeRect(SpriteRenderer sr)
        {
            if (sr == null) return default;
            float w = sr.size.x / StageCoords.PX, h = sr.size.y / StageCoords.PX;
            return new Rect(sr.transform.localPosition.x / StageCoords.PX - w * 0.5f,
                            -sr.transform.localPosition.y / StageCoords.PX - h * 0.5f, w, h);
        }

        /// <summary>
        /// Widens the bubble by <paramref name="extra"/> stage px, split evenly around the
        /// authored centre so the tail stays put — the plates stretch, the icon rides the
        /// left edge, and the label band gains the full width. 0 restores the authored rects.
        /// </summary>
        void FitWidth(float extra)
        {
            extraShown = extra;
            float half = extra * 0.5f;
            if (shadowPlate != null)
                ViewFactory.Place(shadowPlate, shadowHome.x - half, shadowHome.y,
                                  shadowHome.width + extra, shadowHome.height);
            if (borderPlate != null)
                ViewFactory.Place(borderPlate, borderHome.x - half, borderHome.y,
                                  borderHome.width + extra, borderHome.height);
            if (boxFace != null)
                ViewFactory.Place(boxFace, boxHome.x - half, boxHome.y,
                                  boxHome.width + extra, boxHome.height);
            if (icon != null)
            {
                Vector3 p = icon.transform.localPosition;
                p.x = (iconHomeX - half) * StageCoords.PX;
                icon.transform.localPosition = p;
            }
            if (nameLabel != null)
                ViewFactory.Place(nameLabel, nameHomeLeft - half, nameHomeY, nameHomeW + extra);
        }

        public void Show(int tier, float now)
        {
            if (!IsBuilt || tier < 0) { Hide(); return; }
            SetVisible(true);
            shownAt = now;
            if (tier != shownTier) Apply(tier);
        }

        void Apply(int tier)
        {
            shownTier = tier;
            if (database != null && icon != null)
            {
                icon.sprite = database.Pastry(tier);
                // Fixed bubble radius, scaled by the dessert's case size % so it reads as the
                // same dessert; the breathing in Update pulses around that. This has to be the
                // SAME constant Build authored with — it ran at a hardcoded 16 against a scene
                // authored at 22, so the Scene view previewed an icon 1.4× the one play mode
                // actually served, and neither view could be trusted while tuning the other.
                ViewFactory.SetIcon(icon, IconRadius, database.DisplaySize(tier));
                iconBaseScale = icon.transform.localScale.x;
            }
            if (nameLabel != null)
            {
                nameLabel.text = $"{(database != null ? database.Name(tier) : TierTable.Names[tier])}, please!";

                // Fit the bubble to the order. Measured off the RENDERED mesh, never
                // GetPreferredValues — for this font it reports ~1.5× the truth (307 px for
                // a run that draws at 225). Show() has just activated Content, so the mesh
                // is buildable here; a font asset still warming up measures 0 and simply
                // keeps the authored width.
                nameLabel.ForceMeshUpdate(true, true);
                float textW = nameLabel.textBounds.size.x / StageCoords.PX;
                float extra = Mathf.Clamp(textW + TextSlackPx - nameHomeW, 0f, MaxExtraPx);
                if (!Mathf.Approximately(extra, extraShown)) FitWidth(extra);
            }
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

            // once the pop settles, the wanted dessert breathes ±8% to pull the eye
            if (icon != null)
            {
                float s = iconBaseScale;
                if (k >= 1f)
                    s *= 1f + 0.08f * Mathf.Sin((Time.time - shownAt - 0.35f) * (2f * Mathf.PI / 1.1f));
                icon.transform.localScale = new Vector3(s, s, 1f);
            }

            // ...and the hint rocks and pulses like a little shop sign. Composes off the
            // home read at bind, so a hand-turned hint keeps its authored tilt underneath.
            if (hintLabel != null)
            {
                if (k >= 1f)
                {
                    float w = Time.time - shownAt - 0.35f;
                    hintLabel.transform.localRotation =
                        hintHomeRot * Quaternion.Euler(0f, 0f, 3.5f * Mathf.Sin(w * (2f * Mathf.PI / 1.6f)));
                    hintLabel.transform.localScale =
                        hintHomeScale * (1f + 0.05f * Mathf.Sin(w * (2f * Mathf.PI / 0.8f)));
                }
                else
                {
                    hintLabel.transform.localRotation = hintHomeRot;
                    hintLabel.transform.localScale = hintHomeScale;
                }
            }
        }
    }
}
