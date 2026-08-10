using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>Aim guide and the held pastry, drawn at the top of the cloth (§8.5 step 4).</summary>
    public sealed class AimGuideView : GeneratedView
    {
        SpriteRenderer line, held;
        int shownTier, shownSkin;

        /// <summary>
        /// Resting local positions read back from the scene. Only x tracks the finger, so the
        /// height of the guide and the held pastry stay wherever they were placed.
        /// </summary>
        Vector3 lineHome, heldHome;

        /// <summary>Sim px, y-down: where the held pastry currently sits. Stale while hidden.</summary>
        public Vector2 HeldSimPos { get; private set; } = new Vector2(SimField.CX, HeldY);

        /// <summary>The held icon's centre line above the cloth.</summary>
        public const float HeldY = 42f;

        protected override void Build()
        {
            shownTier = shownSkin = -1;
            // Defaults to the cloth centre so the guide reads correctly in the Editor too,
            // before PointerInput has ever run.
            // A SOLID white line, not the dashed sprite this used to pass. The dashes never
            // reached the screen as dashes: Rect draws Sliced, the dash sprite carries no
            // 9-slice border, so a 14 × 3 pattern was stretched across a 3 × 306 rect. Only
            // 4 px in every 14 are opaque, so squeezing the pattern into 3 px of width
            // averaged the alpha down to ~29%, and 0.8 × 0.29 left the guide at roughly a
            // fifth of the opacity it was written for — a washed-out grey smear rather than
            // the white line the tint asks for. A plain white sprite has no pattern to lose.
            line = ViewFactory.Rect(Content, "AimLine", Shapes.White,
                                    SimField.CX, 66f, 3f, 306f,
                                    new Color(1f, 1f, 1f, 0.8f), "PlayArea", 20);
            held = ViewFactory.Icon(Content, "HeldPastry", database, 0, SimField.CX, HeldY, 20f,
                                    "PlayArea", 21);

            lineHome = line != null ? line.transform.localPosition : Vector3.zero;
            heldHome = held != null ? held.transform.localPosition : Vector3.zero;
        }

        public void Sync(bool visible, float aimX, int tier, int skin, float sizeScale,
                         float wallL = SimField.WL, float wallR = SimField.WR)
        {
            if (!IsBuilt) return;
            SetVisible(visible);
            if (!visible) return;

            float r = TierTable.EffectiveRadius(tier, sizeScale,
                                                database != null ? database.TierSize(tier) : 1f);
            float x = Mathf.Clamp(aimX, wallL + r, wallR - r);

            // Only x follows the finger; y and z stay at the authored height.
            if (line != null)
                line.transform.localPosition = new Vector3(x * StageCoords.PX, lineHome.y, lineHome.z);
            if (held != null)
                held.transform.localPosition = new Vector3(x * StageCoords.PX, heldHome.y, heldHome.z);
            HeldSimPos = new Vector2(x, HeldY);

            if (tier == shownTier && skin == shownSkin) return;
            shownTier = tier;
            shownSkin = skin;
            if (database != null && held != null) held.sprite = database.Pastry(tier, skin);
            ViewFactory.SetIcon(held, r);   // r already carries the dessert's size
        }
    }
}
