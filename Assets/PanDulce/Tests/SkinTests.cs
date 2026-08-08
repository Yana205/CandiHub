using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// Color tracks in the pile (2026-08-08): after the donut is discovered, spawns roll a
    /// color track; merging matches tier AND color; the merge child keeps its parents'
    /// color where the next tier has variant art and folds to Original where it doesn't.
    /// </summary>
    public class SkinTests
    {
        /// <summary>Mirrors the shipped art: tracks 1 (Matcha) and 2 (Berry) have variants
        /// for mochi (0), melon pan (2) and choco donut (3); purin (1) and roll cake (4)
        /// are shared by every track.</summary>
        static bool ShippedArt(int tier, int track)
            => track >= 1 && track <= 2 && (tier == 0 || tier == 2 || tier == 3);

        static MergeSim Sim(int seed = 1)
        {
            var cfg = new SimConfigData { startingBodies = 0, gravity = 0f, comboDelay = 0f };
            var sim = new MergeSim(cfg, new System.Random(seed));
            sim.SkinTrackCount = 3;
            sim.SkinHasArt = ShippedArt;
            sim.EmptyCloth();
            return sim;
        }

        static void Settle(MergeSim sim, float sec)
        {
            int n = (int)(sec / 0.016f);
            for (int i = 0; i < n; i++) sim.Tick(0.016f);
        }

        [Test]
        public void DifferentColors_NeverMerge()
        {
            var sim = Sim();
            sim.MakeBody(200f, 200f, 0, 1f).skin = 1;
            sim.MakeBody(205f, 200f, 0, 1f).skin = 2;
            Settle(sim, 2f);   // well past the start grace — touching the whole time
            Assert.That(sim.Bodies.Count, Is.EqualTo(2),
                        "a matcha mochi must not merge with a mango mochi");
        }

        [Test]
        public void SameColor_Merges_AndFoldsToOriginalAtSharedTiers()
        {
            var sim = Sim();
            sim.MakeBody(200f, 200f, 0, 1f).skin = 1;
            sim.MakeBody(205f, 200f, 0, 1f).skin = 1;
            Settle(sim, 2f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1), "same color must merge as usual");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));
            Assert.That(sim.Bodies[0].skin, Is.Zero,
                        "purin has no color variants — every color converges there");
        }

        [Test]
        public void SameColor_Merge_KeepsColorWhereTheNextTierHasArt()
        {
            var sim = Sim();
            sim.MakeBody(200f, 200f, 2, 1f).skin = 2;   // two sakura pans...
            sim.MakeBody(210f, 200f, 2, 1f).skin = 2;
            Settle(sim, 2f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1));
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(3));
            Assert.That(sim.Bodies[0].skin, Is.EqualTo(2),
                        "...make a berry donut, not a plain choco donut");
        }

        [Test]
        public void ColoredSpawns_OnlyAfterTheDonutIsDiscovered()
        {
            var cfg = new SimConfigData { startingBodies = 0, startDiscovered = 1 };
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.SkinTrackCount = 3;
            sim.SkinHasArt = ShippedArt;

            for (int i = 0; i < 40; i++)
            {
                sim.EmptyCloth();          // keep the pile trivial — this tests the rolls
                Assert.That(sim.CurSkin, Is.Zero, "no colored spawns before the donut");
                Assert.That(sim.NextSkin, Is.Zero, "no colored spawns before the donut");
                sim.Tick(1f);              // clear the drop cooldown
                sim.Drop(SimField.CX, true);
            }

            sim.RevealTier(MergeSim.SkinUnlockTier);
            Assert.That(sim.SkinsLive, Is.True);

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 300; i++)
            {
                sim.EmptyCloth();
                sim.Tick(1f);
                if (sim.Drop(SimField.CX, true)) seen.Add(sim.NextSkin);
            }
            Assert.That(seen.Contains(0), Is.True, "Original must still appear among spawns");
            Assert.That(seen.Contains(1), Is.True, "Matcha must appear after the donut");
            Assert.That(seen.Contains(2), Is.True, "Berry must appear after the donut");
        }

        [Test]
        public void SkinRolls_ResetWithTheRun()
        {
            var cfg = new SimConfigData { startingBodies = 0, startDiscovered = 1 };
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.SkinTrackCount = 3;
            sim.SkinHasArt = ShippedArt;

            sim.RevealTier(MergeSim.SkinUnlockTier);
            Assert.That(sim.SkinsLive, Is.True);

            sim.ResetRun();
            Assert.That(sim.SkinsLive, Is.False, "the donut milestone is per-run");
            Assert.That(sim.CurSkin, Is.Zero);
            Assert.That(sim.NextSkin, Is.Zero);
        }
    }
}
