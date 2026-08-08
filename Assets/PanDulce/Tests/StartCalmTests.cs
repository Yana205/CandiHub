using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The opening calm: the run's first seconds play slowed and ease back to full tempo.
    /// The ramp is a pure function of elapsed time, so the tests pin its whole contract.
    /// </summary>
    public class StartCalmTests
    {
        [Test]
        public void OffByDefault_AndWhenDisabled()
        {
            Assert.That(new SimConfigData().StartCalmSec, Is.EqualTo(0f),
                        "a bare config must reproduce the mock — no calm");
            Assert.That(StartCalm.Scale(0f, 0f, 0.55f), Is.EqualTo(1f));
            Assert.That(StartCalm.Scale(0f, 10f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void StartsAtCalmScale_EndsAtFullSpeed()
        {
            Assert.That(StartCalm.Scale(0f, 10f, 0.55f), Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(StartCalm.Scale(10f, 10f, 0.55f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(StartCalm.Scale(60f, 10f, 0.55f), Is.EqualTo(1f),
                        "past the calm the ramp must hold exactly 1");
        }

        [Test]
        public void RampIsMonotonic_AndStaysInsideItsBand()
        {
            float prev = 0f;
            for (int i = 0; i <= 40; i++)
            {
                float s = StartCalm.Scale(i * 0.25f, 10f, 0.55f);
                Assert.That(s, Is.GreaterThanOrEqualTo(prev), "tempo must never dip back down");
                Assert.That(s, Is.InRange(0.55f, 1f));
                prev = s;
            }
        }

        [Test]
        public void WildConfigCannotFreezeTheShop()
        {
            Assert.That(StartCalm.Scale(0f, 10f, 0f), Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(StartCalm.Scale(0f, 10f, -3f), Is.GreaterThanOrEqualTo(0.1f));
        }
    }
}
