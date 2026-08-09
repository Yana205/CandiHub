using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The deal knobs (2026-08-09): SpawnBias raises the pick weights to a power so the
    /// hand leans on the low tiers, and BigDealDelaySec keeps the top two tiers out of
    /// the deal for a run's opening — sized for 20–30 second games where an early dealt
    /// donut fast-tracks the roll cake. Both default to the mock (bias 1, no wait).
    /// </summary>
    public class DealTests
    {
        [Test]
        public void SpawnBias_LeansTheClassicDealOntoMochi()
        {
            var cfg = new SimConfigData { spawnBias = 2f, startDiscovered = 4 };
            var sim = new MergeSim(cfg, new System.Random(7));
            var counts = new int[TierTable.Count];
            const int n = 10000;
            for (int i = 0; i < n; i++) counts[sim.Pick()]++;

            // Squared 4:3:2:1 → 16:9:4:1 — mochi takes over half the hand.
            Assert.That(counts[0] / (float)n, Is.EqualTo(16 / 30f).Within(0.02f));
            Assert.That(counts[3] / (float)n, Is.EqualTo(1 / 30f).Within(0.01f),
                        "a dealt donut becomes a rare treat");
        }

        [Test]
        public void BigDealWait_KeepsTopTiersOutOfTheEarlyHand()
        {
            var cfg = new SimConfigData
            {
                bigDealDelaySec = 12f, startDiscovered = 5,
                startingBodies = 0, gravity = 0f
            };
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.EmptyCloth();
            sim.SetSpawnPool(new[] { 0, 1, 2, 3, 4 });

            for (int i = 0; i < 500; i++)
                Assert.That(sim.Pick(), Is.LessThan(3),
                            "donut and roll cake must not be dealt before the wait");

            while (sim.Now < cfg.bigDealDelaySec) sim.Tick(0.1f);

            bool bigDealt = false;
            for (int i = 0; i < 500 && !bigDealt; i++) bigDealt = sim.Pick() >= 3;
            Assert.That(bigDealt, Is.True, "after the wait the big tiers rejoin the deal");
        }

        [Test]
        public void BigDealWait_NeverBlocksMergingUpToTheTop()
        {
            // The wait shapes the DEAL only — a merge into roll cake during the wait
            // must still create and discover it.
            var cfg = new SimConfigData
            {
                bigDealDelaySec = 999f, startDiscovered = 4,
                startingBodies = 0, gravity = 0f, centerPull = 0f, kinPull = 0f
            };
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.EmptyCloth();

            sim.MakeBody(200f, 200f, 3, 1f);
            sim.MakeBody(205f, 200f, 3, 1f);
            // Past the start-of-run merge grace, then time to actually merge.
            for (int i = 0; i < 190; i++) sim.Tick(0.016f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1));
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(4), "donuts must still merge into roll cake");
            Assert.That(sim.IsDiscovered(4), Is.True);
        }
    }
}
