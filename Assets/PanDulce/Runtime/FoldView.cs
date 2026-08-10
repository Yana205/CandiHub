using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The two flaps, the knot and the "Closing time" card, driven by CloseT (§8.5).</summary>
    public sealed class FoldView : GeneratedView
    {
        Transform flapLeft, flapRight, knot;
        SpriteRenderer flapLeftSr, flapRightSr, knotA, knotB, knotC;
        TextMeshPro closing;

        protected override void Build()
        {
            var t = Content;

            flapLeft = ViewFactory.NodeTransform(t, "FlapLeft", SimField.BL + 12f, 352f);
            flapLeftSr = ViewFactory.Rect(flapLeft, "Shape", Shapes.RoundedRect(64, 64, 26),
                                          -20f, -320f, 210f, 330f, Color.white, "PlayArea", 70);

            flapRight = ViewFactory.NodeTransform(t, "FlapRight", SimField.BR - 12f, 352f);
            flapRightSr = ViewFactory.Rect(flapRight, "Shape", Shapes.RoundedRect(64, 64, 26),
                                           -190f, -320f, 210f, 330f, Color.white, "PlayArea", 71);

            knot = ViewFactory.NodeTransform(t, "Knot", SimField.CX, 108f);
            knotA = ViewFactory.Rect(knot, "LobeL", Shapes.Circle(64), -30f, -9f, 30f, 17f,
                                     Color.white, "PlayArea", 72, 26f);
            knotB = ViewFactory.Rect(knot, "LobeR", Shapes.Circle(64), 0f, -9f, 30f, 17f,
                                     Color.white, "PlayArea", 72, -26f);
            knotC = ViewFactory.Rect(knot, "Centre", Shapes.Circle(64), -13f, -10f, 26f, 21f,
                                     Color.white, "PlayArea", 73);

            closing = ViewFactory.Label(t, "ClosingLabel", "Closing time", SimField.CX - 150f, 190f,
                                        300f, 20f, Palette.Cream, "PlayArea", 80);

            SetVisible(false);
        }

        public void Sync(float closeT, Color clothColor)
        {
            if (!IsBuilt) return;
            bool draw = closeT > 0.002f;
            SetVisible(draw);
            if (!draw) return;

            float p = closeT;
            float e = p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

            if (flapLeft != null)
                flapLeft.localRotation = Quaternion.Euler(0f, 0f, -e * 1.42f * Mathf.Rad2Deg);
            if (flapRight != null)
                flapRight.localRotation = Quaternion.Euler(0f, 0f, e * 1.42f * Mathf.Rad2Deg);

            if (flapLeftSr != null) flapLeftSr.color = Palette.Mix(clothColor, 1.06f);
            if (flapRightSr != null) flapRightSr.color = Palette.Mix(clothColor, 0.9f);

            // Past p > 0.55 the knot scales in.
            float k = Mathf.Clamp01((p - 0.55f) / 0.45f);
            if (knot != null)
            {
                knot.gameObject.SetActive(k > 0f);
                knot.localScale = Vector3.one * k;
            }
            if (knotA != null && knotB != null)
                knotA.color = knotB.color = Palette.Mix(clothColor, 1.1f);
            if (knotC != null) knotC.color = Palette.Mix(clothColor, 1.18f);

            if (closing == null) return;
            closing.alpha = k;
            // Outline setters reach through renderer.material — runtime only (see FloatingTextPool).
            if (Application.isPlaying)
            {
                closing.outlineWidth = 0.25f;
                closing.outlineColor = Palette.Mix(clothColor, 0.6f);
            }
        }
    }
}
