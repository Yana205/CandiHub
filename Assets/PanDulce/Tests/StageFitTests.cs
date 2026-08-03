using NUnit.Framework;
using PanDulce.Runtime;

namespace PanDulce.Tests
{
    /// <summary>
    /// Guards the §5.1 contain-fit. These are pure arithmetic over screen dimensions, so
    /// they need no scene and no camera — which is the point of keeping the maths in
    /// <see cref="StageFit"/> rather than inside the MonoBehaviour.
    ///
    /// The bug these were written against: StageFitter scaled the stage transform by a
    /// screen-px/stage-px ratio while the camera's orthographic size stayed pinned at 4.4.
    /// The camera shows exactly 880 stage px at that size, so any ratio above 1.0 cropped
    /// the frame. On an iPhone 15 the ratio is 2.62, so 62% of the stage fell outside.
    /// </summary>
    public class StageFitTests
    {
        /// <summary>Portrait devices from §5.1, plus the landscape editor default.</summary>
        static readonly (string name, int w, int h)[] Screens =
        {
            ("iPhone 15",        1170, 2532),
            ("iPhone SE",         750, 1334),
            ("Pixel 7",          1080, 2400),
            ("iPad Pro 11",      1668, 2388),
            ("Design safe box",   446,  900),
            ("WXGA landscape",   1366,  768),
        };

        /// <summary>Visible stage px, derived from the camera the fitter would configure.</summary>
        static (float w, float h) Visible(int screenW, int screenH)
        {
            float ortho = StageFit.OrthographicSize(screenW, screenH);
            float halfH = ortho / StageCoords.PX;
            float halfW = halfH * screenW / screenH;
            return (halfW * 2f, halfH * 2f);
        }

        // ------------------------------------------------------------ containment

        [Test]
        public void Camera_ContainsTheSafeBox_OnEveryScreen()
        {
            foreach (var s in Screens)
            {
                var v = Visible(s.w, s.h);
                Assert.That(v.w, Is.GreaterThanOrEqualTo(StageCoords.SafeW - 0.5f),
                            $"{s.name}: safe box clipped horizontally");
                Assert.That(v.h, Is.GreaterThanOrEqualTo(StageCoords.SafeH - 0.5f),
                            $"{s.name}: safe box clipped vertically");
            }
        }

        [Test]
        public void Camera_NeverCropsTheDesignFrame()
        {
            foreach (var s in Screens)
            {
                var v = Visible(s.w, s.h);
                Assert.That(v.w, Is.GreaterThanOrEqualTo(StageCoords.StageW),
                            $"{s.name}: 430 px frame cropped horizontally");
                Assert.That(v.h, Is.GreaterThanOrEqualTo(StageCoords.StageH),
                            $"{s.name}: 880 px frame cropped vertically");
            }
        }

        // ------------------------------------------------------- §5.1 reference row

        [Test]
        public void IPhone15_MatchesTheHandoffFigures()
        {
            // §5.1 states the stage renders 1127 × 2306 screen px with ~226 px of vertical
            // slack. Width is the limiting dimension, so the safe box fits exactly.
            float s = StageFit.ScreenPxPerStagePx(1170, 2532);

            Assert.That(StageCoords.StageW * s, Is.EqualTo(1128f).Within(2f));
            Assert.That(StageCoords.StageH * s, Is.EqualTo(2308f).Within(3f));
            Assert.That(2532f - StageCoords.StageH * s, Is.EqualTo(226f).Within(3f));
        }

        [Test]
        public void IPhone15_WasTheRegression_MoreThanHalfTheStageWasVisible()
        {
            // The old code left the camera at 4.4, showing a fixed 880 stage px regardless
            // of screen. Against a stage scaled 2.62x that exposed 880/2.62 = 336 px.
            var v = Visible(1170, 2532);
            Assert.That(v.h, Is.GreaterThan(900f),
                        "camera is still showing a fixed height instead of adapting");
        }

        // -------------------------------------------------------------- limiting axis

        [Test]
        public void PortraitPhones_AreWidthLimited_SoSlackIsVertical()
        {
            var v = Visible(1170, 2532);
            Assert.That(v.w, Is.EqualTo(StageCoords.SafeW).Within(0.5f),
                        "width should be the limiting dimension on a tall phone");
            Assert.That(v.h, Is.GreaterThan(StageCoords.SafeH),
                        "vertical slack is what the bleed art fills");
        }

        [Test]
        public void Landscape_IsHeightLimited_SoSlackIsHorizontal()
        {
            var v = Visible(1366, 768);
            Assert.That(v.h, Is.EqualTo(StageCoords.SafeH).Within(0.5f));
            Assert.That(v.w, Is.GreaterThan(StageCoords.SafeW));
        }

        [Test]
        public void ExactDesignAspect_ShowsTheSafeBoxWithNoSlack()
        {
            var v = Visible(446, 900);
            Assert.That(v.w, Is.EqualTo(StageCoords.SafeW).Within(0.5f));
            Assert.That(v.h, Is.EqualTo(StageCoords.SafeH).Within(0.5f));
            Assert.That(StageFit.ScreenPxPerStagePx(446, 900), Is.EqualTo(1f).Within(0.001f));
        }

        // --------------------------------------------------------------- degenerate

        [Test]
        public void DegenerateScreens_FallBackInsteadOfDividingByZero()
        {
            Assert.That(StageFit.ScreenPxPerStagePx(0, 0), Is.EqualTo(1f));
            Assert.That(StageFit.OrthographicSize(0, 0),
                        Is.EqualTo(StageCoords.SafeH * StageCoords.PX * 0.5f).Within(0.001f));
            Assert.That(StageFit.OrthographicSize(-5, 100), Is.GreaterThan(0f));
        }
    }
}
