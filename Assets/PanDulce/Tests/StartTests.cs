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
            Assert.That(sim.IsDiscovered(3), Is.False, "Choco Donut must start as a silhouette");
            Assert.That(sim.HighestDiscovered, Is.EqualTo(2));
        }

        [Test]
        public void UndiscoveredTiers_NeverSpawn_SeatMenuOrClassic()
        {
            var cfg = Cfg();
            cfg.startDiscovered = 3;
            var sim = new MergeSim(cfg, new System.Random(3));

            // Authored seat menu including undiscovered seats (Choco Donut tier 3, Roll Cake 4).
            sim.SetSpawnPool(new[] { 0, 1, 3, 4 });
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
            for (int i = 0; i < 200; i++)
            {
                shop.OpenWindow();
                Assert.That(shop.OrderTier, Is.LessThanOrEqualTo(2),
                            "a silhouette must never be ordered");
                Assert.That(shop.OrderTier, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void Orders_AskForSimpleDessertsToo()
        {
            // The band covers the WHOLE revealed chain (2026-08-10) — mochi and purin are
            // orders in their own right, not just merge fodder.
            var shop = new ShopDirector(new System.Random(4));
            shop.Orderable = t => true;
            var seen = new bool[TierTable.Count];
            for (int i = 0; i < 400; i++) { shop.OpenWindow(); seen[shop.OrderTier] = true; }

            for (int t = 0; t < TierTable.Count; t++)
                Assert.That(seen[t], Is.True, $"tier {t} must be orderable");
        }

        [Test]
        public void Orders_IgnoreThePile_OnlyTheCase()
        {
            // Orders are gated on discovery, never on what is currently on the cloth: a
            // revealed dessert with none in the box is exactly the ask to go build one.
            var shop = new ShopDirector(new System.Random(9));
            shop.Orderable = t => t == 0 || t == 3;
            for (int i = 0; i < 100; i++)
            {
                shop.OpenWindow();
                Assert.That(shop.OrderTier == 0 || shop.OrderTier == 3, Is.True,
                            $"tier {shop.OrderTier} is not in the case");
            }
        }

        [Test]
        public void Orders_FallBackToBestMakeable_WhenNothingIsRevealed()
        {
            var shop = new ShopDirector(new System.Random(5));
            shop.Orderable = t => false;
            shop.OpenWindow();
            Assert.That(shop.OrderTier, Is.EqualTo(0));
        }
    }
}
