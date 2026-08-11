using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// Color tracks in the pile (2026-08-08; cosmetic-only since 2026-08-11): spawns roll
    /// a color track — mochi from the very first deal, everything else after the donut is
    /// discovered — with Original carrying double weight so colors stay a bit rare.
    /// Merging matches TIER ONLY: any colors merge (the same-shade gate "made a junk of
    /// skins" — Yana). Same-color parents pass the color on where the next tier has
    /// variant art (folding to Original where it doesn't); mixed-color parents roll a
    /// surprise color for the child.
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
        public void DifferentColors_Merge_AboveMochiToo()
        {
            var sim = Sim();
            sim.MakeBody(200f, 200f, 2, 1f).skin = 1;
            sim.MakeBody(210f, 200f, 2, 1f).skin = 2;
            Settle(sim, 2f);   // well past the start grace — touching the whole time
            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "colors are cosmetic — a matcha pan merges with a berry one");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(3));
        }

        [Test]
        public void Mochi_MergesAcrossColors()
        {
            var sim = Sim();
            sim.MakeBody(200f, 200f, 0, 1f).skin = 1;
            sim.MakeBody(205f, 200f, 0, 1f).skin = 2;
            Settle(sim, 2f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "all mochi merge with all mochi, whatever they wear");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));
            Assert.That(sim.Bodies[0].skin, Is.Zero,
                        "purin has no color variants — the surprise roll folds to Original");
        }

        [Test]
        public void MixedColorParents_RollASurpriseColorForTheChild()
        {
            // Give purin variant art (test-only) so the surprise roll is observable.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 30; seed++)
            {
                var sim = Sim(seed);
                sim.SkinHasArt = (tier, track) => track >= 1 && track <= 2;
                sim.MakeBody(200f, 200f, 0, 1f).skin = 1;
                sim.MakeBody(205f, 200f, 0, 1f).skin = 2;
                Settle(sim, 2f);
                Assert.That(sim.Bodies.Count, Is.EqualTo(1));
                seen.Add(sim.Bodies[0].skin);
            }
            Assert.That(seen.Count, Is.GreaterThan(1),
                        "mixed parents must not always hand back the same color");
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
        public void BeforeTheDonut_OnlyMochiWearsColors()
        {
            var cfg = new SimConfigData { startingBodies = 0, startDiscovered = 3 };
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.SkinTrackCount = 3;
            sim.SkinHasArt = ShippedArt;

            var mochiSkins = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                sim.EmptyCloth();          // keep the pile trivial — this tests the rolls
                sim.Tick(2f);              // clear the drop cooldown
                if (!sim.Drop(SimField.CX, true)) continue;
                if (sim.NextTier == 0) mochiSkins.Add(sim.NextSkin);
                else Assert.That(sim.NextSkin, Is.Zero,
                                 "no colored deals above mochi before the donut");
            }
            Assert.That(mochiSkins.Contains(1), Is.True, "matcha mochi deals from the start");
            Assert.That(mochiSkins.Contains(2), Is.True, "berry mochi deals from the start");
        }

        [Test]
        public void ColoredDeals_AreABitMoreRareThanOriginal()
        {
            var cfg = new SimConfigData { startingBodies = 0, startDiscovered = 1 };
            var sim = new MergeSim(cfg, new System.Random(11));
            sim.SkinTrackCount = 3;
            sim.SkinHasArt = ShippedArt;

            var counts = new int[3];
            for (int i = 0; i < 2000; i++)
            {
                sim.EmptyCloth();
                sim.Tick(2f);
                if (sim.Drop(SimField.CX, true)) counts[sim.NextSkin]++;
            }
            // Original carries double weight: ~50% plain, ~25% each color.
            int total = counts[0] + counts[1] + counts[2];
            Assert.That(counts[0] / (float)total, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(counts[1], Is.GreaterThan(0));
            Assert.That(counts[2], Is.GreaterThan(0));
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
            // Mochi keeps its colors across runs; anything colored above mochi would
            // mean the milestone leaked.
            if (sim.CurSkin != 0) Assert.That(sim.CurTier, Is.Zero);
            if (sim.NextSkin != 0) Assert.That(sim.NextTier, Is.Zero);
        }

        [Test]
        public void MixedColors_ChildRollsASurprise_AtEveryTier()
        {
            // Not just mochi: a matcha pan + berry pan child must be a legal donut color,
            // and across seeds the surprise roll must actually vary.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 30; seed++)
            {
                var sim = Sim(seed);
                sim.MakeBody(200f, 200f, 2, 1f).skin = 1;
                sim.MakeBody(210f, 200f, 2, 1f).skin = 2;
                Settle(sim, 2f);
                Assert.That(sim.Bodies.Count, Is.EqualTo(1));
                Assert.That(sim.Bodies[0].skin, Is.InRange(0, 2));
                seen.Add(sim.Bodies[0].skin);
            }
            Assert.That(seen.Count, Is.GreaterThan(1),
                        "mixed pans must not always hand back the same donut color");
        }
    }
}
