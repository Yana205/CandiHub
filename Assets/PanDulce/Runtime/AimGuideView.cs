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
            line = ViewFactory.Rect(Content, "AimLine", Shapes.Dashes(4, 10, 3),
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
