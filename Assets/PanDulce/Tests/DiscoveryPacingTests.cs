using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// Discovery pacing (2026-08-08): a case seat colours in only after DiscoverMerges
    /// merges INTO its tier — one lucky cascade can no longer reveal the whole case.
    /// The default (1) reproduces the classic first-merge reveal, so every older test
    /// and bare config keeps its behaviour.
    /// </summary>
    public class DiscoveryPacingTests
    {
        static MergeSim Sim(SimConfigData cfg, int seed = 1)
        {
            var sim = new MergeSim(cfg, new System.Random(seed));
            sim.EmptyCloth();
            return sim;
        }

        static void Settle(MergeSim sim, float sec)
        {
            int n = (int)(sec / 0.016f);
            for (int i = 0; i < n; i++) sim.Tick(0.016f);
        }

        /// <summary>One tier-0 pair, merged and cleared, leaving one merge into tier 1.</summary>
        static void MergeOnePair(MergeSim sim)
        {
            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);
            Settle(sim, 2f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1), "the pair must merge");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));
            sim.EmptyCloth();
        }

        [Test]
        public void Default_FirstMergeStillDiscovers()
        {
            var sim = Sim(new SimConfigData
                { startingBodies = 0, gravity = 0f, comboDelay = 0f, startDiscovered = 1 });
            MergeOnePair(sim);
            Assert.That(sim.IsDiscovered(1), Is.True,
                        "discoverMerges defaults to 1 — the classic reveal must survive");
        }

        [Test]
        public void SeatRevealsOnlyAfterTheRequiredMerges()
        {
            var sim = Sim(new SimConfigData
            {
                startingBodies = 0, gravity = 0f, comboDelay = 0f,
                startDiscovered = 1, discoverMerges = 3
            });
            int fired = 0;
            sim.TierDiscovered += (tier, pos) => { if (tier == 1) fired++; };

            MergeOnePair(sim);
            Assert.That(sim.IsDiscovered(1), Is.False, "1/3 — still a silhouette");
            Assert.That(sim.MergeCount(1), Is.EqualTo(1));

            MergeOnePair(sim);
            Assert.That(sim.IsDiscovered(1), Is.False, "2/3 — still a silhouette");

            MergeOnePair(sim);
            Assert.That(sim.IsDiscovered(1), Is.True, "3/3 — revealed");
            Assert.That(fired, Is.EqualTo(1), "the discovery moment fires exactly once");
        }

        [Test]
        public void ResetRun_ClearsTheTally()
        {
            var cfg = new SimConfigData
            {
                startingBodies = 0, gravity = 0f, comboDelay = 0f,
                startDiscovered = 1, discoverMerges = 3
            };
            var sim = Sim(cfg);
            MergeOnePair(sim);
            MergeOnePair(sim);
            Assert.That(sim.MergeCount(1), Is.EqualTo(2));

            sim.ResetRun();
            Assert.That(sim.MergeCount(1), Is.Zero, "the tally is per-run");
            Assert.That(sim.IsDiscovered(1), Is.False);
        }

        [Test]
        public void StartDiscoveredTiers_NeedNoMerges()
        {
            var sim = Sim(new SimConfigData
            {
                startingBodies = 0, gravity = 0f, comboDelay = 0f,
                startDiscovered = 2, discoverMerges = 3
            });
            Assert.That(sim.IsDiscovered(0), Is.True);
            Assert.That(sim.IsDiscovered(1), Is.True,
                        "tiers known at start skip the merge requirement entirely");
        }
    }
}
