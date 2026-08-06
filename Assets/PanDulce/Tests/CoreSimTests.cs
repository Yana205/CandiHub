using NUnit.Framework;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Tests
{
    /// <summary>
    /// The EditMode suite from handoff §12. Core has no Unity object dependencies, so none
    /// of these need a scene — which is exactly why the sim was kept Unity-free.
    /// </summary>
    public class CoreSimTests
    {
        static SimConfigData Cfg() => new SimConfigData();

        // ---------------------------------------------------------------- Er()

        [Test]
        public void Er_AtSpawnZero_IsThirtyFivePercent()
        {
            var cfg = Cfg();
            var b = new Body { tier = 5, spawnT = 0f };
            float full = TierTable.BaseRadius[5] * cfg.SizeScale;
            Assert.That(TierTable.Er(b, cfg.SizeScale), Is.EqualTo(full * 0.35f).Within(0.001f));
        }

        [Test]
        public void Er_AtSpawnOne_IsFullRadius()
        {
            var cfg = Cfg();
            var b = new Body { tier = 5, spawnT = 1f };
            float full = TierTable.BaseRadius[5] * cfg.SizeScale;
            Assert.That(TierTable.Er(b, cfg.SizeScale), Is.EqualTo(full).Within(0.001f));
        }

        [Test]
        public void Er_AtMergeGate_HasOvershootPastFullSize()
        {
            // The back-out ease overshoots before settling: at 0.55 the body is already
            // larger than full size. This is why the merge gate uses spawnT > 0.55 —
            // it is the point where a newborn is visually "real".
            var cfg = Cfg();
            var b = new Body { tier = 5, spawnT = 0.55f };
            float full = TierTable.BaseRadius[5] * cfg.SizeScale;
            float er = TierTable.Er(b, cfg.SizeScale);
            Assert.That(er, Is.GreaterThan(full * 0.9f));
            Assert.That(er, Is.LessThan(full * 1.2f));
        }

        // ---------------------------------------------------------------- spawn distribution

        [Test]
        public void Pick_IsWeighted4321OverTiersZeroToThree()
        {
            var sim = new MergeSim(Cfg(), new System.Random(12345));
            var counts = new int[TierTable.Count];
            const int n = 10000;
            for (int i = 0; i < n; i++) counts[sim.Pick()]++;

            for (int t = 4; t < TierTable.Count; t++)
                Assert.That(counts[t], Is.Zero, $"tier {t} must never spawn");

            Assert.That(counts[0] / (float)n, Is.EqualTo(0.4f).Within(0.02f));
            Assert.That(counts[1] / (float)n, Is.EqualTo(0.3f).Within(0.02f));
            Assert.That(counts[2] / (float)n, Is.EqualTo(0.2f).Within(0.02f));
            Assert.That(counts[3] / (float)n, Is.EqualTo(0.1f).Within(0.02f));
        }

        // ---------------------------------------------------------------- merge gate

        [Test]
        public void MergeGate_RejectsBodyYoungerThanComboDelay()
        {
            var cfg = Cfg();
            cfg.startingBodies = 0;
            cfg.gravity = 0f;          // isolate the gate from the floor
            cfg.comboDelay = 0.5f;

            var sim = new MergeSim(cfg, new System.Random(1));
            sim.EmptyCloth();

            // Two overlapping, fully grown, but both newborn.
            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);

            sim.Tick(0.016f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(2), "must not merge before comboDelay elapses");

            // Age past comboDelay AND the start grace, then let them touch again.
            for (int i = 0; i < 70; i++) sim.Tick(0.016f);
            Assert.That(sim.Bodies.Count, Is.EqualTo(1), "must merge once both are older than comboDelay");
            Assert.That(sim.Bodies[0].tier, Is.EqualTo(1));
        }

        [Test]
        public void MergeGate_HoldsDuringTheStartGrace()
        {
            var cfg = Cfg();
            cfg.startingBodies = 0;
            cfg.gravity = 0f;
            cfg.comboDelay = 0f;       // isolate the grace from the per-body delay

            var sim = new MergeSim(cfg, new System.Random(1));
            sim.EmptyCloth();
            sim.MakeBody(200f, 200f, 0, 1f);
            sim.MakeBody(205f, 200f, 0, 1f);

            for (int i = 0; i < 55; i++) sim.Tick(0.016f);   // 0.88s — inside the grace
            Assert.That(sim.Bodies.Count, Is.EqualTo(2), "must not merge during the start grace");

            for (int i = 0; i < 15; i++) sim.Tick(0.016f);   // 1.12s — past it
            Assert.That(sim.Bodies.Count, Is.EqualTo(1), "must merge after the start grace");
        }

        // ---------------------------------------------------------------- the curved floor

        [Test]
        public void FloorCurve_IsFlatAtCentreAndRaisedAtEdges()
        {
            const float sag = 26f;
            Assert.That(SimField.FloorAt(SimField.CX, sag), Is.EqualTo(SimField.FY).Within(0.001f));
            Assert.That(SimField.FloorAt(SimField.BL, sag), Is.EqualTo(SimField.FY - sag).Within(0.001f));
            Assert.That(SimField.FloorAt(SimField.BR, sag), Is.EqualTo(SimField.FY - sag).Within(0.001f));
        }

        [Test]
        public void FloorCurve_RisesMonotonicallyFromCentre()
        {
            const float sag = 26f;
            float prev = SimField.FloorAt(SimField.CX, sag);
            for (float x = SimField.CX; x <= SimField.BR; x += 10f)
            {
                float fy = SimField.FloorAt(x, sag);
                Assert.That(fy, Is.LessThanOrEqualTo(prev + 0.0001f), $"floor must not dip at x={x}");
                prev = fy;
            }
        }

        [Test]
        public void CentrePull_RollsBodiesTowardTheMiddle()
        {
            // The signature feel: a body resting off-centre drifts inward, so the pile
            // settles into a bowl rather than a stack.
            var cfg = Cfg();
            cfg.startingBodies = 0;
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.EmptyCloth();

            var b = sim.MakeBody(SimField.BL + 60f, 300f, 0, 1f);
            float startX = b.x;
            for (int i = 0; i < 120; i++) sim.Tick(0.016f);

            Assert.That(b.x, Is.GreaterThan(startX), "body should roll toward the centre");
            Assert.That(b.x, Is.LessThan(SimField.CX + 1f), "should not overshoot past the centre");
        }

        [Test]
        public void RestingBody_StopsRotating()
        {
            // Centre pull leaves a settled body a few px/s of vx forever; the rolling
            // coupling used to turn that into a spin that never died.
            var cfg = Cfg();
            cfg.startingBodies = 0;
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.EmptyCloth();

            var b = sim.MakeBody(SimField.BL + 60f, 300f, 0, 1f);
            for (int i = 0; i < 240; i++) sim.Tick(0.016f);   // ~4 s to settle

            Assert.That(Mathf.Abs(b.vrot), Is.LessThan(0.001f), "a settled body must stop spinning");

            float rotBefore = b.rot;
            for (int i = 0; i < 60; i++) sim.Tick(0.016f);
            Assert.That(Mathf.Abs(b.rot - rotBefore), Is.LessThan(0.01f),
                        "rot must hold still once at rest");
        }

        /// <summary>A pile the way a player builds one — only tiers 0-3 are ever dropped.</summary>
        static MergeSim SettledPile(SimConfigData cfg, int drops)
        {
            var sim = new MergeSim(cfg, new System.Random(7));
            var rng = new System.Random(11);
            for (int d = 0; d < drops; d++)
            {
                float x = SimField.WL + 40f + (float)rng.NextDouble() * (SimField.WR - SimField.WL - 80f);
                sim.MakeBody(x, SimField.DropY, rng.Next(0, 4), 1f);
                for (int i = 0; i < 22; i++) sim.Tick(0.016f);
            }
            for (int i = 0; i < 900; i++) sim.Tick(0.016f);   // 14.4 s to settle
            return sim;
        }

        [TestCase(12)]
        [TestCase(40)]
        public void SettledPile_ComesToACompleteStop(int drops)
        {
            // The regression that matters: most of a pile rests on OTHER pastries, never
            // on the cloth, and every one of them used to turn forever.
            var cfg = Cfg();
            var sim = SettledPile(cfg, drops);

            var rot0 = new float[sim.Bodies.Count];
            for (int i = 0; i < sim.Bodies.Count; i++) rot0[i] = sim.Bodies[i].rot;

            for (int i = 0; i < 120; i++) sim.Tick(0.016f);   // 2 s

            Assert.That(sim.Bodies.Count, Is.GreaterThan(4), "expected a real pile to test");
            for (int i = 0; i < sim.Bodies.Count; i++)
            {
                Body b = sim.Bodies[i];
                Assert.That(Mathf.Abs(b.vrot), Is.LessThan(0.001f),
                            $"body {i} (tier {b.tier}) is still spinning");
                Assert.That(Mathf.Abs(b.rot - rot0[i]) * Mathf.Rad2Deg, Is.LessThan(1f),
                            $"body {i} (tier {b.tier}) is still turning");
            }
        }

        [Test]
        public void DroppedPastry_SpinsThePileItLandsOn()
        {
            // rotationAmount must still read as a scale: a body knocked by a fresh drop has
            // to spin, and only then settle.
            var cfg = Cfg();
            var sim = SettledPile(cfg, 12);
            foreach (var rested in sim.Bodies)
                Assert.That(rested.vrot, Is.Zero, "precondition: the pile is asleep");

            sim.MakeBody(SimField.CX, SimField.DropY, 0, 1f);
            float peak = 0f;
            for (int i = 0; i < 60; i++)
            {
                sim.Tick(0.016f);
                for (int k = 0; k < sim.Bodies.Count; k++)
                    peak = Mathf.Max(peak, Mathf.Abs(sim.Bodies[k].vrot));
            }
            Assert.That(peak, Is.GreaterThan(0.1f), "a landing drop must spin what it hits");

            for (int i = 0; i < 300; i++) sim.Tick(0.016f);
            for (int i = 0; i < sim.Bodies.Count; i++)
                Assert.That(Mathf.Abs(sim.Bodies[i].vrot), Is.LessThan(0.001f),
                            $"body {i} must come back to rest");
        }

        [Test]
        public void Shake_SpinsTheWholePile()
        {
            // The shake launches everything; rest damping must not swallow it.
            var cfg = Cfg();
            var sim = SettledPile(cfg, 12);

            sim.DoShake();
            sim.Tick(0.016f);

            for (int i = 0; i < sim.Bodies.Count; i++)
                Assert.That(Mathf.Abs(sim.Bodies[i].vrot), Is.GreaterThan(0f),
                            $"body {i} must be spinning after a shake");
        }

        [Test]
        public void RestDamping_DoesNotStopTheCentrePullDrift()
        {
            // The bowl is the signature of the design (§7.3) — killing spin must not also
            // pin an off-centre body in place.
            var cfg = Cfg();
            cfg.startingBodies = 0;
            var sim = new MergeSim(cfg, new System.Random(7));
            sim.EmptyCloth();

            var b = sim.MakeBody(SimField.BL + 60f, 300f, 0, 1f);
            for (int i = 0; i < 240; i++) sim.Tick(0.016f);   // settle, spin now damped
            float xAfterSettle = b.x;

            for (int i = 0; i < 120; i++) sim.Tick(0.016f);
            Assert.That(b.x, Is.GreaterThan(xAfterSettle + 1f),
                        "a resting body must keep drifting toward the middle");
        }

        // ---------------------------------------------------------------- boost meter

        [Test]
        public void ChargePerMerge_ReachesExactlyOneAfterEightMerges()
        {
            var meter = new BoostMeter();
            var cfg = Cfg();
            for (int i = 0; i < 8; i++) meter.AddMerge(cfg.ChargePerMerge);
            Assert.That(meter.Charge, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(meter.Ready, Is.True);
        }

        [Test]
        public void Charge_ClampsAtOneAndSpendEmpties()
        {
            var meter = new BoostMeter();
            for (int i = 0; i < 40; i++) meter.AddMerge(0.14f);
            Assert.That(meter.Charge, Is.EqualTo(1f).Within(0.0001f));
            meter.Spend();
            Assert.That(meter.Charge, Is.Zero);
            Assert.That(meter.Ready, Is.False);
        }
    }
}
