using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The opening moments of a run: the start delay pads only the first customer's
    /// countdown, and the "known at start" knob decides how much of the chain exists —
    /// everything past it is a silhouette that cannot spawn and cannot be ordered.
    /// </summary>
    public class StartTests
    {
        static SimConfigData Cfg() => new SimConfigData();

        [Test]
        public void StartDelay_PadsOnlyTheFirstCountdown()
        {
            var cfg = Cfg();
            cfg.startDelaySec = 6f;
            var shop = new ShopDirector(new System.Random(1));

            shop.Reset(cfg);
            Assert.That(shop.TimeLeft, Is.EqualTo(cfg.customerEverySec + 6f).Within(0.001f));

            shop.OpenWindow();
            shop.SkipCustomer(cfg);
            Assert.That(shop.TimeLeft, Is.EqualTo((float)cfg.customerEverySec).Within(0.001f),
                        "later visits must use the plain cadence");
        }

        [Test]
        public void StartDelay_DefaultsToZero_MockParity()
        {
            var shop = new ShopDirector(new System.Random(1));
            shop.Reset(Cfg());
            Assert.That(shop.TimeLeft, Is.EqualTo((float)Cfg().customerEverySec).Within(0.001f));
        }

        [Test]
        public void StartDiscovered_HidesTheChainPastIt()
        {
            var cfg = Cfg();
            cfg.startDiscovered = 3;
            var sim = new MergeSim(cfg, new System.Random(2));

            Assert.That(sim.IsDiscovered(2), Is.True);
            Assert.That(sim.IsDiscovered(3), Is.False, "Purin must start as a silhouette");
            Assert.That(sim.HighestDiscovered, Is.EqualTo(2));
        }

        [Test]
        public void UndiscoveredTiers_NeverSpawn_SeatMenuOrClassic()
        {
            var cfg = Cfg();
            cfg.startDiscovered = 3;
            var sim = new MergeSim(cfg, new System.Random(3));

            // Authored seat menu including undiscovered seats (Purin tier 3, Melon Pan 10).
            sim.SetSpawnPool(new[] { 0, 1, 3, 4, 10 });
            for (int i = 0; i < 200; i++)
                Assert.That(sim.Pick(), Is.LessThan(3));

            // Classic 4:3:2:1 menu must respect discovery too.
            sim.SetSpawnPool(null);
            for (int i = 0; i < 200; i++)
                Assert.That(sim.Pick(), Is.LessThan(3));
        }

        [Test]
        public void Orders_OnlyAskForDiscoveredDesserts()
        {
            var shop = new ShopDirector(new System.Random(4));
            shop.Orderable = t => t <= 2;
            for (int i = 0; i < 50; i++)
            {
                shop.OpenWindow();
                Assert.That(shop.OrderTier, Is.EqualTo(2),
                            "band 2..5 filtered by discovery leaves only tier 2");
            }
        }

        [Test]
        public void Orders_FallBackToBestMakeable_WhenBandIsAllSilhouettes()
        {
            var shop = new ShopDirector(new System.Random(5));
            shop.Orderable = t => t <= 1;
            shop.OpenWindow();
            Assert.That(shop.OrderTier, Is.EqualTo(1));
        }
    }
}
