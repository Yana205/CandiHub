using System.Collections.Generic;
using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The top-out + score layer ported from Docs/web-reference. Not in the mock, so it
    /// carries no design authority from the handoff — these tests pin it to the reference.
    /// </summary>
    public class ChallengeTests
    {
        static SimConfigData Cfg() => new SimConfigData();

        static Body Settled(float y, int tier = 0) =>
            new Body { y = y, tier = tier, spawnT = 1f, bornAt = 0f, vy = 0f };

        // ---------------------------------------------------------------- score

        [Test]
        public void MergeScore_ScalesWithTierAndCombo()
        {
            var s = new ScoreKeeper();
            s.AddMerge(0, 1);                       // (0+1)*10*1
            Assert.That(s.Score, Is.EqualTo(10));

            s.ResetRun();
            s.AddMerge(4, 3);                       // (4+1)*10*3
            Assert.That(s.Score, Is.EqualTo(150));
        }

        [Test]
        public void MergeScore_TreatsComboZeroAsOne()
        {
            var s = new ScoreKeeper();
            s.AddMerge(2, 0);                       // max(1, 0) => 1
            Assert.That(s.Score, Is.EqualTo(30));
        }

        [Test]
        public void DiscoveryAndServe_AwardReferenceValues()
        {
            var s = new ScoreKeeper();
            s.AddDiscovery();
            Assert.That(s.Score, Is.EqualTo(250));

            s.ResetRun();
            s.AddServe(5);                          // 100 + 5*25
            Assert.That(s.Score, Is.EqualTo(225));
        }

        [Test]
        public void Best_OnlyRisesAndSurvivesResetRun()
        {
            var s = new ScoreKeeper();
            s.AddMerge(9, 5);                       // 500
            Assert.That(s.CommitBest(), Is.True);
            Assert.That(s.Best, Is.EqualTo(500));

            s.ResetRun();
            s.AddMerge(0, 1);                       // 10
            Assert.That(s.CommitBest(), Is.False, "a worse run must not overwrite best");
            Assert.That(s.Best, Is.EqualTo(500));
        }

        // ---------------------------------------------------------------- top-out

        [Test]
        public void TopOut_IgnoresBodiesThatAreStillGrowingIn()
        {
            var cfg = Cfg();
            var w = new TopOutWatch();
            var bodies = new List<Body> { new Body { y = 10f, tier = 0, spawnT = 0.4f, bornAt = 0f, vy = 0f } };

            w.Tick(1f, bodies, 10f, cfg);
            Assert.That(w.DangerT, Is.Zero, "a body mid-pop must not threaten the run");
        }

        [Test]
        public void TopOut_IgnoresFreshlyBornBodies()
        {
            var cfg = Cfg();
            var w = new TopOutWatch();
            var bodies = new List<Body> { Settled(10f) };

            // now - bornAt = 0.5, under the 1.2s settle age
            w.Tick(0.5f, bodies, 0.5f, cfg);
            Assert.That(w.DangerT, Is.Zero, "a just-dropped pastry must not end the run");
        }

        [Test]
        public void TopOut_IgnoresFastMovingBodies()
        {
            var cfg = Cfg();
            var w = new TopOutWatch();
            var b = Settled(10f);
            b.vy = 200f;                            // above the 90 threshold
            var bodies = new List<Body> { b };

            w.Tick(1f, bodies, 10f, cfg);
            Assert.That(w.DangerT, Is.Zero, "a body in flight must not count as topped out");
        }

        [Test]
        public void TopOut_FiresOnlyAfterGraceElapses()
        {
            var cfg = Cfg();
            cfg.topOutGrace = 2.2f;
            var w = new TopOutWatch();
            var bodies = new List<Body> { Settled(10f) };   // well above the 82 line

            bool ended = false;
            for (int i = 0; i < 21; i++) ended |= w.Tick(0.1f, bodies, 10f, cfg);
            Assert.That(ended, Is.False, "must survive 2.1s over the line");

            ended = w.Tick(0.2f, bodies, 10f, cfg);
            Assert.That(ended, Is.True, "must end once grace is exceeded");
        }

        [Test]
        public void TopOut_DrainsAtDoubleRateWhenClear()
        {
            var cfg = Cfg();
            var w = new TopOutWatch();
            var over = new List<Body> { Settled(10f) };
            var clear = new List<Body> { Settled(300f) };

            w.Tick(1f, over, 10f, cfg);
            Assert.That(w.DangerT, Is.EqualTo(1f).Within(0.0001f));

            w.Tick(0.25f, clear, 10f, cfg);
            Assert.That(w.DangerT, Is.EqualTo(0.5f).Within(0.0001f), "should drain at 2x");
        }

        [Test]
        public void TopOut_DisabledKnobNeverAccrues()
        {
            var cfg = Cfg();
            cfg.topOut = false;
            var w = new TopOutWatch();
            var bodies = new List<Body> { Settled(10f) };

            for (int i = 0; i < 100; i++)
                Assert.That(w.Tick(0.1f, bodies, 10f, cfg), Is.False);
            Assert.That(w.DangerT, Is.Zero);
        }

        [Test]
        public void TopOut_AccountsForBodyRadiusNotJustCentre()
        {
            // A big pastry whose centre is below the line can still cross it with its top.
            var cfg = Cfg();
            cfg.topOutLine = 82f;
            var w = new TopOutWatch();

            float r = TierTable.EffectiveRadius(10, cfg.SizeScale);   // 90 * 1.3 = 117
            var bodies = new List<Body> { Settled(82f + r - 5f, 10) };

            w.Tick(0.1f, bodies, 10f, cfg);
            Assert.That(w.DangerT, Is.GreaterThan(0f), "top of the body crosses the line");
        }

        // ---------------------------------------------------------------- day cycle

        [Test]
        public void DayCycle_EasesShutAndBlocksDropping()
        {
            var c = new DayCycle();
            Assert.That(c.CanDrop, Is.True);

            for (int i = 0; i < 10; i++) c.Tick(0.1f, true);   // 1.0s of 1.1s
            Assert.That(c.CloseT, Is.GreaterThan(0.12f));
            Assert.That(c.CanDrop, Is.False, "cannot drop while folding shut");
            Assert.That(c.DrawFold, Is.True);

            for (int i = 0; i < 5; i++) c.Tick(0.1f, true);
            Assert.That(c.CloseT, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(c.TimerRuns, Is.False, "customer countdown pauses when shut");
        }

        [Test]
        public void DayCycle_ForcedClosedFoldsEvenWithKnobOff()
        {
            // This is how a top-out presents: the run ends by closing the bakery.
            var c = new DayCycle { ForcedClosed = true };
            for (int i = 0; i < 15; i++) c.Tick(0.1f, false);
            Assert.That(c.CloseT, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void DayCycle_ReopensWhenKnobCleared()
        {
            var c = new DayCycle();
            for (int i = 0; i < 15; i++) c.Tick(0.1f, true);
            for (int i = 0; i < 15; i++) c.Tick(0.1f, false);
            Assert.That(c.CloseT, Is.Zero);
            Assert.That(c.CanDrop, Is.True);
        }

        // ---------------------------------------------------------------- shop loop

        [Test]
        public void Shop_OpensOnTimerAndOrdersTiersTwoToFive()
        {
            var cfg = Cfg();
            cfg.customerEverySec = 5;
            var shop = new ShopDirector(new System.Random(3));
            shop.Reset(cfg);

            for (int i = 0; i < 100; i++)
            {
                shop.Tick(0.1f, i * 0.1f, cfg, timerRuns: true);
                if (shop.State == ShopState.Open) break;
            }

            Assert.That(shop.State, Is.EqualTo(ShopState.Open));
            Assert.That(shop.OrderTier, Is.InRange(2, 5));
        }

        [Test]
        public void Shop_TimerHaltsWhileFoldedShut()
        {
            var cfg = Cfg();
            cfg.customerEverySec = 5;
            var shop = new ShopDirector(new System.Random(3));
            shop.Reset(cfg);

            for (int i = 0; i < 200; i++) shop.Tick(0.1f, i * 0.1f, cfg, timerRuns: false);
            Assert.That(shop.State, Is.EqualTo(ShopState.Closed), "no customers while closed");
        }

        [Test]
        public void Shop_ServeThenHappyThenClosesAndResetsTimer()
        {
            var cfg = Cfg();
            cfg.customerEverySec = 9;
            cfg.happyMs = 1400f;
            var shop = new ShopDirector(new System.Random(3));
            shop.Reset(cfg);
            shop.OpenWindow();
            Assert.That(shop.OrderActive, Is.True);

            shop.ServeInFlight = true;
            shop.CompleteServe(100f, cfg);
            Assert.That(shop.State, Is.EqualTo(ShopState.Happy));
            Assert.That(shop.Served, Is.EqualTo(1));

            shop.Tick(0.1f, 100f + 1.3f, cfg, timerRuns: true);
            Assert.That(shop.State, Is.EqualTo(ShopState.Happy), "holds for happyMs");

            shop.Tick(0.1f, 100f + 1.5f, cfg, timerRuns: true);
            Assert.That(shop.State, Is.EqualTo(ShopState.Closed));
            Assert.That(shop.OrderTier, Is.EqualTo(-1));
            Assert.That(shop.TimeLeft, Is.EqualTo(9f).Within(0.0001f), "timer resets after a serve");
        }

        [Test]
        public void Shop_CyclesThreeRegulars()
        {
            var cfg = Cfg();
            var shop = new ShopDirector(new System.Random(3));
            shop.Reset(cfg);

            for (int i = 0; i < 4; i++)
            {
                shop.OpenWindow();
                Assert.That(shop.CustomerIndex, Is.EqualTo(i % 3));
                shop.CompleteServe(i * 10f, cfg);
                shop.Tick(0.1f, i * 10f + 2f, cfg, timerRuns: true);
            }
        }
    }
}
