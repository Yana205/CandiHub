using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The deal knobs (2026-08-09): SpawnBias raises the pick weights to a power so the
    /// hand leans on the low tiers, and BigDealDelaySec keeps the top two tiers out of
    /// the deal for a run's opening — sized for 20–30 second games where an early dealt
    /// donut fast-tracks the roll cake. Both default to the mock (bias 1, no wait).
    ///
    /// Progressive deal (2026-08-10): seat weights rank by what is REVEALED rather than by
    /// seat index, and DealTopMargin holds the newest reveals out of the hand, so the deal
    /// starts as almost pure mochi and widens one step per unlock.
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
        public void SeatWeights_RankByWhatIsRevealed_NotBySeatIndex()
        {
            // Three desserts revealed out of five seats: the hand must be the tight 9:4:1
            // of a three-deep case, not the flat 25:16:9 tail of a full one (2026-08-10).
            var cfg = new SimConfigData { spawnBias = 2f, startDiscovered = 3 };
            var sim = new MergeSim(cfg, new System.Random(11));
            sim.SetSpawnPool(new[] { 0, 1, 2, 3, 4 });

            var counts = new int[TierTable.Count];
            const int n = 10000;
            for (int i = 0; i < n; i++) counts[sim.Pick()]++;

            Assert.That(counts[0] / (float)n, Is.EqualTo(9 / 14f).Within(0.02f));
            Assert.That(counts[1] / (float)n, Is.EqualTo(4 / 14f).Within(0.02f));
            Assert.That(counts[2] / (float)n, Is.EqualTo(1 / 14f).Within(0.02f));
        }

        [Test]
        public void ClassicMenu_TightensWhenLittleIsRevealed()
        {
            // The live scene runs "follow progress", so GameRoot passes NO seat pool and
            // this is the branch that actually deals. Two desserts revealed must be a 4:1
            // mochi hand — the old walk-down donated the blocked weight to purin and made
            // it 53/47 (2026-08-10).
            var cfg = new SimConfigData { spawnBias = 2f, startDiscovered = 2 };
            var sim = new MergeSim(cfg, new System.Random(23));

            var counts = new int[TierTable.Count];
            const int n = 10000;
            for (int i = 0; i < n; i++) counts[sim.Pick()]++;

            Assert.That(counts[0] / (float)n, Is.EqualTo(4 / 5f).Within(0.02f));
            Assert.That(counts[1] / (float)n, Is.EqualTo(1 / 5f).Within(0.02f));
        }

        [Test]
        public void ClassicMenu_NeverDealsTheTopOfTheChain()
        {
            var cfg = new SimConfigData { startDiscovered = 5 };
            var sim = new MergeSim(cfg, new System.Random(29));
            for (int i = 0; i < 500; i++)
                Assert.That(sim.Pick(), Is.LessThan(TierTable.Max),
                            "roll cake is a merge prize, never a dealt one");
        }

        [Test]
        public void DealTopMargin_KeepsTheNewestRevealMergeOnly()
        {
            var cfg = new SimConfigData { startDiscovered = 4, dealTopMargin = 1 };
            var sim = new MergeSim(cfg, new System.Random(13));
            sim.SetSpawnPool(new[] { 0, 1, 2, 3, 4 });

            for (int i = 0; i < 500; i++)
                Assert.That(sim.Pick(), Is.LessThan(3),
                            "the freshly revealed donut must be merged for, not dealt");
        }

        [Test]
        public void DealTopMargin_NeverEmptiesTheHand()
        {
            // A margin deeper than the revealed chain still leaves mochi to deal.
            var cfg = new SimConfigData { startDiscovered = 5, dealTopMargin = 9 };
            var sim = new MergeSim(cfg, new System.Random(17));
            sim.SetSpawnPool(new[] { 0, 1, 2, 3, 4 });

            for (int i = 0; i < 200; i++) Assert.That(sim.Pick(), Is.EqualTo(0));
        }

        [Test]
        public void DealTopMargin_NeverBlocksMergingUpToTheTop()
        {
            var cfg = new SimConfigData
            {
                dealTopMargin = 9, startDiscovered = 4,
                startingBodies = 0, gravity = 0f, centerPull = 0f, kinPull = 0f
            };
            var sim = new MergeSim(cfg, new System.Random(19));
            sim.EmptyCloth();

            sim.MakeBody(200f, 200f, 3, 1f);
            sim.MakeBody(205f, 200f, 3, 1f);
            for (int i = 0; i < 190; i++) sim.Tick(0.016f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1));
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(4));
            Assert.That(sim.IsDiscovered(4), Is.True);
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
