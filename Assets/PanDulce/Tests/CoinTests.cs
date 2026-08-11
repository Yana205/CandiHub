using NUnit.Framework;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Tests
{
    /// <summary>
    /// The coin economy and the Day-old clearance despawn path
    /// (spec docs/superpowers/specs/2026-08-04-purchasable-boosts-design.md).
    /// </summary>
    public class CoinTests
    {
        static SimConfigData Cfg() => new SimConfigData();

        // ---------------------------------------------------------------- CoinPurse

        [Test]
        public void Purse_AddsAndSpends()
        {
            var p = new CoinPurse();
            p.Add(40);
            Assert.That(p.TrySpend(30), Is.True);
            Assert.That(p.Coins, Is.EqualTo(10));
        }

        [Test]
        public void Purse_DeniesWhenBroke_AndKeepsBalance()
        {
            var p = new CoinPurse();
            p.Add(10);
            Assert.That(p.TrySpend(30), Is.False);
            Assert.That(p.Coins, Is.EqualTo(10), "a denied spend must not touch the balance");
        }

        [Test]
        public void Purse_ResetZeroes()
        {
            var p = new CoinPurse();
            p.Add(25);
            p.Reset();
            Assert.That(p.Coins, Is.Zero);
        }

        [Test]
        public void ServePay_MatchesFormula_ForOrderableTiers()
        {
            var cfg = Cfg();
            // Linear below the top of the chain; the top pays double — a roll cake can't
            // merge on, so selling it is its whole payoff.
            for (int tier = 2; tier <= 5; tier++)
            {
                int linear = cfg.coinBase + tier * cfg.coinPerTier;
                Assert.That(CoinPurse.ServePay(cfg, tier),
                            Is.EqualTo(tier >= TierTable.Max
                                       ? linear * CoinPurse.TopTierPayMult : linear));
            }
        }

        // ---------------------------------------------------------------- clearance

        static MergeSim EmptySim(SimConfigData cfg)
        {
            cfg.startingBodies = 0;
            return new MergeSim(cfg, new System.Random(7));
        }

        [Test]
        public void RemoveUpToTier_RemovesOnlyLowTiers_AndReportsThem()
        {
            var sim = EmptySim(Cfg());
            sim.MakeBody(100f, 300f, 0, 1f);
            sim.MakeBody(140f, 300f, 1, 1f);
            sim.MakeBody(180f, 300f, 2, 1f);
            sim.MakeBody(220f, 300f, 5, 1f);

            var removed = new System.Collections.Generic.List<(Vector2 pos, int tier)>();
            Assert.That(sim.RemoveUpToTier(1, removed), Is.EqualTo(2));
            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(sim.Bodies.Count, Is.EqualTo(2));
            foreach (var b in sim.Bodies)
                Assert.That(b.tier, Is.GreaterThan(1), "tiers above the cutoff must survive");
        }

        [Test]
        public void RemoveUpToTier_FiresNoMergedEvent()
        {
            var sim = EmptySim(Cfg());
            sim.MakeBody(100f, 300f, 0, 1f);
            sim.MakeBody(140f, 300f, 0, 1f);

            int merges = 0;
            sim.Merged += (_, _, _) => merges++;
            sim.RemoveUpToTier(1);
            Assert.That(merges, Is.Zero, "clearance is a despawn, never a merge — no score, no boost charge");
        }

        [Test]
        public void HasAnyUpToTier_TracksTheBoard()
        {
            var sim = EmptySim(Cfg());
            Assert.That(sim.HasAnyUpToTier(1), Is.False);
            sim.MakeBody(100f, 300f, 2, 1f);
            Assert.That(sim.HasAnyUpToTier(1), Is.False);
            sim.MakeBody(140f, 300f, 1, 1f);
            Assert.That(sim.HasAnyUpToTier(1), Is.True);
            sim.RemoveUpToTier(1);
            Assert.That(sim.HasAnyUpToTier(1), Is.False);
        }
    }
}
