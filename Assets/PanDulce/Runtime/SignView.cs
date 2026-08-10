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
        /// <summary>Drawn sign footprint in stage px, at the art's own 822 × 606 aspect.</summary>
        const float SignW = 140f;
        const float SignH = 103f;

        TextMeshPro topLine, bigLine;
        string shownTop, shownBig;

        protected override void Build()
        {
            shownTop = shownBig = null;
            var t = Content;

            // The sign hangs still. It used to swing ±1.2°, which the drawing's own rail
            // swung with — a rail bolted to the wall cannot tilt. Cleared when the view is
            // first written out, so a scene saved mid-swing does not keep the stale tilt;
            // only then, because past that point the tilt is the designer's to set.
            if (Authoring) transform.localRotation = Quaternion.identity;

            // The drawing holds rail, ropes and board in one piece, so it replaces all five
            // primitives the sign used to be built from. Drawn at its own aspect (822×606) so
            // the ropes stay round, and a size up from the flat board's 118 × 76: the carved
            // frame and the rail eat most of the sprite, leaving a cream centre only 65% × 50%
            // of it, and the countdown has to keep its old presence inside that.
            Sprite art = skin != null ? skin.Sign : null;
            if (art != null)
            {
                ViewFactory.Rect(t, "Board", art, 8f, 54f, SignW, SignH, Color.white, "Furniture", 10);
            }
            else
            {
                ViewFactory.Rect(t, "RopeLeft", Shapes.White, 40f, 54f, 3f, 26f,
                                 Palette.ChipFill, "Furniture", 10, 16f);
                ViewFactory.Rect(t, "RopeRight", Shapes.White, 88f, 54f, 3f, 26f,
                                 Palette.ChipFill, "Furniture", 10, -16f);

                // drop shadow, border, face — three stacked panels fake border+shadow (§8.4)
                ViewFactory.Panel(t, "BoardShadow", 8f, 80f, 118f, 76f, 10, Palette.Crust, "Furniture", 10);
                ViewFactory.Panel(t, "BoardBorder", 8f, 76f, 118f, 76f, 10, Palette.Wood, "Furniture", 11);
                ViewFactory.Panel(t, "Board", 11f, 79f, 112f, 70f, 8, Palette.Hex("#fffaf0"), "Furniture", 12);
            }

            // The writing area is the cream oval, which sits low and inset — the rail and
            // ropes own the top third — so both lines move onto it. Kept off the oval's
            // extremes, where it narrows and the longer line would clip the frame.
            // Authored placement (Lital, 2026-08-08): local y -1.051 and -1.271.
            float top = art != null ? 105.1f : 98f;
            float big = art != null ? 127.1f : 130f;
            float x = art != null ? 8f + SignW * 0.19f : 8f;
            float w = art != null ? SignW * 0.62f : 118f;

            topLine = ViewFactory.Label(t, "TopLabel", "next customer in", x, top, w, 12f,
                                        Palette.Hex("#a58358"), "Furniture", 13,
                                        TextAlignmentOptions.Center, FontStyles.Normal);
            bigLine = ViewFactory.Label(t, "BigLabel", "18s", x, big, w, art != null ? 26f : 30f,
                                        Palette.Crust, "Furniture", 13);
        }

        public void Sync(ShopState state, int secondsShown, bool arriving)
        {
            if (!IsBuilt) return;
            string top = state == ShopState.Closed ? "next customer in"
                       : arriving                  ? "here they come!" : "now serving";
            string big = state == ShopState.Closed ? $"{secondsShown}s"
                       : arriving                  ? "…" : "♥";
            if (top != shownTop && topLine != null) { shownTop = top; topLine.text = top; }
            if (big != shownBig && bigLine != null) { shownBig = big; bigLine.text = big; }
        }
    }
}
