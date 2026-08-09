using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The fail line in the box world (2026-08-09): a body is "over the line" only when
    /// its CENTER crosses, not its top edge. In the v2 flat box (no floor sag, sizeScale
    /// 1.59) the edge check ended runs with two desserts in the basket — a Mochi resting
    /// on a Roll Cake poked 11px over and the run died mid-aim, 3.4s after the drop.
    /// </summary>
    public class TopOutTests
    {
        // Live v2 box geometry: flat floor at 250, line at 82, grace 2.2s.
        static SimConfigData BoxCfg() => new SimConfigData
        {
            startingBodies = 0, gravity = 0f, centerPull = 0f, kinPull = 0f,
            floorSag = 0f, floorY = 250f, wallLeft = 72f, wallRight = 338f,
            sizeScale = 1.5899999f, mergeTouchSec = 999f,
            topOut = true, topOutLine = 82f, topOutGrace = 2.2f
        };

        static MergeSim Sim(SimConfigData cfg)
        {
            var sim = new MergeSim(cfg, new System.Random(1));
            sim.EmptyCloth();
            return sim;
        }

        /// <summary>Ticks sim + watch together; returns true if the run ended.</summary>
        static bool Run(MergeSim sim, TopOutWatch watch, SimConfigData cfg, float sec)
        {
            int n = (int)(sec / 0.016f);
            for (int i = 0; i < n; i++)
            {
                sim.Tick(0.016f);
                if (watch.Tick(0.016f, sim.Bodies, sim.Now, cfg)) return true;
            }
            return false;
        }

        [Test]
        public void MochiOnRollCake_PokingOverTheLine_Survives()
        {
            var cfg = BoxCfg();
            var sim = Sim(cfg);
            var watch = new TopOutWatch();

            // Roll Cake seated on the floor, Mochi parked on top of it — the two-dessert
            // basket from the bug report. The Mochi's top edge is at ~71 (11px over the
            // line) but its center is at ~92, below the line.
            float r4 = TierTable.EffectiveRadius(4, cfg);
            float r0 = TierTable.EffectiveRadius(0, cfg);
            Body cake = sim.MakeBody(200f, cfg.floorY - r4, 4, 1f);
            Body mochi = sim.MakeBody(200f, cake.y - r4 - r0, 0, 1f);

            Assert.That(mochi.y - TierTable.Er(mochi, cfg), Is.LessThan(cfg.topOutLine),
                        "precondition: the mochi's top edge pokes over the line");
            Assert.That(mochi.y, Is.GreaterThan(cfg.topOutLine),
                        "precondition: the mochi's center stays below the line");

            Assert.That(Run(sim, watch, cfg, 6f), Is.False,
                        "a dessert merely poking over the line must not end the run");
        }

        [Test]
        public void CenterOverTheLine_TopsOutAfterTheGrace()
        {
            var cfg = BoxCfg();
            var sim = Sim(cfg);
            var watch = new TopOutWatch();

            // A settled body whose CENTER sits above the line — genuinely over.
            sim.MakeBody(200f, cfg.topOutLine - 5f, 2, 1f);

            Assert.That(Run(sim, watch, cfg, TopOutWatch.SettledAge + cfg.topOutGrace + 1f),
                        Is.True, "a settled body past the line must still end the run");
        }
    }
}
