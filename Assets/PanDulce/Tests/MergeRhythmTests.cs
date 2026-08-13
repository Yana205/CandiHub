using NUnit.Framework;
using PanDulce.Core;

namespace PanDulce.Tests
{
    /// <summary>
    /// The merge gate's rhythm knobs (2026-08-09): the squeeze LATCHES for the life of a
    /// contact, so "Merge squeeze" composes with "Touch time" — press once (a landing
    /// drop), rest together long enough, merge. Before the latch, squeeze>0 + touch>0
    /// could never both hold in the same frame and nothing ever merged.
    /// </summary>
    public class MergeRhythmTests
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

        static SimConfigData Calm() => new SimConfigData
        {
            startingBodies = 0, gravity = 0f, comboDelay = 0f,
            centerPull = 0f, kinPull = 0f
        };

        [Test]
        public void SqueezePlusTouchTime_MergesAfterOnePress()
        {
            var cfg = Calm();
            cfg.mergeOverlapPct = 0.05f;   // 5% squeeze
            cfg.mergeTouchSec = 0.3f;
            var sim = Sim(cfg);

            // Born deeply overlapped = the pressed frame; the solver then parks them at
            // exact contact, where only the latch remembers the press.
            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);
            Settle(sim, 3f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "one press + sustained touch must merge — the squeeze is latched");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));
        }

        [Test]
        public void SqueezeKeepsSettledNeighboursApart()
        {
            var cfg = Calm();
            cfg.mergeOverlapPct = 0.05f;
            cfg.mergeTouchSec = 0.3f;
            var sim = Sim(cfg);

            Body a = sim.MakeBody(180f, 200f, 0, 1f);
            Body c = sim.MakeBody(300f, 200f, 0, 1f);
            // Parked at EXACT contact — touching forever, but never pressed.
            c.x = a.x + TierTable.Er(a, cfg) + TierTable.Er(c, cfg);
            Settle(sim, 3f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(2),
                        "resting side by side is not a press — squeeze must hold them apart");
        }

        [Test]
        public void DroppedDesserts_SkipTheComboDelay()
        {
            var cfg = Calm();
            cfg.comboDelay = 5f;           // brutal chain brake...
            cfg.mergeTouchSec = 0.2f;
            var sim = Sim(cfg);

            // ...but both bodies are DROPS (grow = 1), so neither waits it out.
            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);
            Settle(sim, 2f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "a throw onto a match must merge — the combo delay only brakes chains");
            Assert.That(sim.Bodies[0].bornOfMerge, Is.True,
                        "the merge product itself is the one that waits next time");
        }

        [Test]
        public void TouchTimer_ForgivesContactChatter()
        {
            var cfg = Calm();
            cfg.mergeTouchSec = 5f;        // long enough that no merge interferes
            var sim = Sim(cfg);

            Body a = sim.MakeBody(180f, 200f, 0, 1f);
            Body c = sim.MakeBody(300f, 200f, 0, 1f);
            c.x = a.x + TierTable.Er(a, cfg) + TierTable.Er(c, cfg);   // exact contact
            Settle(sim, 1.5f);
            float served = a.kinTouchT;
            Assert.That(served, Is.GreaterThan(0.5f), "the timer must accumulate at rest");

            // A brief flicker out of the slack band — chatter, not a real separation.
            c.x += KinFlicker;
            Settle(sim, 0.1f);
            c.x -= KinFlicker;
            Settle(sim, 0.1f);

            Assert.That(a.kinTouchT, Is.GreaterThanOrEqualTo(served),
                        "a sub-quarter-second gap must pause the timer, not zero it");
        }

        const float KinFlicker = MergeSim.KinTouchSlack + 1.5f;

        static SimConfigData FlatFloor()
        {
            var cfg = Calm();
            cfg.gravity = 1500f;
            cfg.floorSag = 0f;
            cfg.floorY = 250f;
            return cfg;
        }

        [Test]
        public void ThrownPair_MergesOnTheFastLane()
        {
            var cfg = FlatFloor();
            cfg.mergeTouchSec = 0.2f;
            cfg.idleMergeSec = 6f;         // idle lane far beyond this test's horizon
            var sim = Sim(cfg);

            Body a = sim.MakeBody(200f, 100f, 0, 1f);
            Settle(sim, 1.5f);             // a lands, rests, grace passes

            sim.MakeBody(200f, 40f, 0, 1f);   // the throw — lands at impact speed
            Settle(sim, 2f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "a struck contact must merge after the plain touch time");
        }

        [Test]
        public void IdleNeighbours_TakeTheSlowLane()
        {
            var cfg = FlatFloor();
            cfg.mergeTouchSec = 0.2f;
            cfg.idleMergeSec = 3f;
            var sim = Sim(cfg);

            // Parked side by side on the floor with a hair of overlap — in contact from
            // frame one, but nothing ever strikes them.
            Body a = sim.MakeBody(200f, 100f, 0, 1f);
            Body c = sim.MakeBody(300f, 100f, 0, 1f);
            float ra = TierTable.Er(a, cfg), rc = TierTable.Er(c, cfg);
            a.x = 200f; a.y = cfg.floorY - ra;
            c.x = 200f + ra + rc - 0.5f; c.y = cfg.floorY - rc;

            Settle(sim, 2f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(2),
                        "resting neighbours must wait out the idle merge time");

            Settle(sim, 2.5f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "the idle lane still merges — just slowly");
        }

        [Test]
        public void InstantFirstMerge_PopsOnTouch_ThenGatesReturn()
        {
            var cfg = Calm();
            cfg.firstMergeInstant = true;
            cfg.mergeOverlapPct = 0.05f;
            cfg.mergeTouchSec = 5f;        // gates far beyond this test's horizon...
            cfg.idleMergeSec = 5f;
            var sim = Sim(cfg);

            // Parked at EXACT contact — never pressed, never struck. The classic gates
            // would hold this pair apart for 5s; the first-merge lane pops it on touch.
            Body a = sim.MakeBody(180f, 200f, 0, 1f);
            Body c = sim.MakeBody(300f, 200f, 0, 1f);
            c.x = a.x + TierTable.Er(a, cfg) + TierTable.Er(c, cfg);
            Settle(sim, MergeSim.StartMergeGraceSec + 0.2f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "the run's first mochi touch must merge instantly");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));

            // A second touching pair: the free lane is spent, the gates are back.
            Body d2 = sim.MakeBody(120f, 300f, 0, 1f);
            Body e2 = sim.MakeBody(320f, 300f, 0, 1f);
            e2.x = d2.x + TierTable.Er(d2, cfg) + TierTable.Er(e2, cfg);
            Settle(sim, 1f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(3),
                        "only the FIRST merge rides the free lane — gates rule after it");
        }

        [Test]
        public void InstantFirstMerge_OffByDefault()
        {
            var cfg = Calm();               // firstMergeInstant defaults to false
            cfg.mergeOverlapPct = 0.05f;
            cfg.mergeTouchSec = 5f;
            var sim = Sim(cfg);

            Body a = sim.MakeBody(180f, 200f, 0, 1f);
            Body c = sim.MakeBody(300f, 200f, 0, 1f);
            c.x = a.x + TierTable.Er(a, cfg) + TierTable.Er(c, cfg);
            Settle(sim, MergeSim.StartMergeGraceSec + 0.5f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(2),
                        "with the knob off a bare config must keep the classic gates");
        }

        [Test]
        public void TouchTimeAlone_StillMerges()
        {
            var cfg = Calm();
            cfg.mergeTouchSec = 0.3f;      // squeeze stays 0 — the classic distance rule
            var sim = Sim(cfg);

            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);
            Settle(sim, 3f);

            Assert.That(sim.Bodies.Count, Is.EqualTo(1),
                        "0% squeeze + touch time is the shipped combo and must keep merging");
        }
    }
}
