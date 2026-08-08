using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The hanging sign (§8.4). Reads "next customer in / {n}s" when closed,
    /// "here they come!" while the bear is still walking in, "now serving / ♥" once
    /// they reach the counter — the sign never gets ahead of the scene.
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

        public void Sync(ShopState state, int secondsShown, bool arriving)
        {
            if (!IsBuilt) return;
            string top = state == ShopState.Closed ? "next customer in"
                       : arriving                  ? "here they come!" : "now serving";
            string big = state == ShopState.Closed ? $"{secondsShown}s"
                       : arriving                  ? "…" : "♥";
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
}
