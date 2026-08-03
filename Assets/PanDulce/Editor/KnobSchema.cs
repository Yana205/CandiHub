using System;
using System.Collections.Generic;
using PanDulce.Core;
using PanDulce.Runtime;

namespace PanDulce.Editor
{
    public enum KnobKind { Range, Int, Bool, Enum, Color }

    /// <summary>
    /// One tunable knob. Explicit Get/Set delegates rather than reflection — compile-time
    /// safe, and no GC while dragging a slider (§10.1).
    /// </summary>
    public sealed class Knob
    {
        public string key, label, unit, help;
        public float min, max, step, def;
        public KnobKind kind = KnobKind.Range;
        public string[] options;
        public bool beyondMock;
        public Func<SimConfigData, float> Get;
        public Action<SimConfigData, float> Set;   // bools 0/1, enums by index
    }

    public sealed class Section
    {
        public string title, icon;
        public bool beyondMock;
        public Knob[] knobs;
    }

    /// <summary>
    /// The whole tuning surface as data. Adding a knob is one entry here — the window builds
    /// itself from this, exactly as the web panel does.
    ///
    /// Keys match the mock's prop names so a config pastes straight into the mock's panel and
    /// back (§10.2). That round-trip is the strongest end-to-end check available.
    /// </summary>
    public static class KnobSchema
    {
        public static readonly Section[] Sections = BuildSections();

        public static IEnumerable<Knob> All()
        {
            foreach (var s in Sections)
                foreach (var k in s.knobs)
                    yield return k;
        }

        static Knob R(string key, string label, float def, float min, float max, float step,
                      Func<SimConfigData, float> get, Action<SimConfigData, float> set,
                      string unit = null, string help = null, bool beyond = false)
            => new Knob { key = key, label = label, def = def, min = min, max = max, step = step,
                          Get = get, Set = set, unit = unit, help = help, kind = KnobKind.Range,
                          beyondMock = beyond };

        static Knob I(string key, string label, float def, float min, float max,
                      Func<SimConfigData, float> get, Action<SimConfigData, float> set,
                      string unit = null, string help = null, bool beyond = false)
            => new Knob { key = key, label = label, def = def, min = min, max = max, step = 1f,
                          Get = get, Set = set, unit = unit, help = help, kind = KnobKind.Int,
                          beyondMock = beyond };

        static Knob B(string key, string label, bool def,
                      Func<SimConfigData, float> get, Action<SimConfigData, float> set,
                      string help = null, bool beyond = false)
            => new Knob { key = key, label = label, def = def ? 1f : 0f, min = 0f, max = 1f, step = 1f,
                          Get = get, Set = set, help = help, kind = KnobKind.Bool, beyondMock = beyond };

        static Section[] BuildSections() => new[]
        {
            new Section { title = "Physics", icon = "🍩", knobs = new[]
            {
                R("gravity", "Gravity", 1500f, 600f, 3000f, 50f,
                  c => c.gravity, (c, v) => c.gravity = v, "px/s²", "How hard everything falls."),
                R("bounciness", "Bounciness", 0.08f, 0f, 0.5f, 0.01f,
                  c => c.bounciness, (c, v) => c.bounciness = v, null, "Restitution on every impact."),
                R("sizeScale", "Size", 1.3f, 0.7f, 1.6f, 0.05f,
                  c => c.sizeScale, (c, v) => c.sizeScale = v, "×", "Multiplies every pastry radius."),
                R("rotationAmount", "Spin", 0.2f, 0f, 1f, 0.05f,
                  c => c.rotationAmount, (c, v) => c.rotationAmount = v, null, "How much collisions impart spin."),
            }},

            new Section { title = "Merge", icon = "✨", knobs = new[]
            {
                R("mergeGrowTime", "Grow-in time", 0.85f, 0.2f, 2f, 0.05f,
                  c => c.mergeGrowTime, (c, v) => c.mergeGrowTime = v, "s", "How long a merge product pops in."),
                R("comboDelay", "Post-merge grace", 0.5f, 0f, 2f, 0.1f,
                  c => c.comboDelay, (c, v) => c.comboDelay = v, "s", "A newborn cannot re-merge until this passes."),
            }},

            new Section { title = "Customers", icon = "🔔", knobs = new[]
            {
                I("customerEverySec", "Customer every", 18f, 5f, 60f,
                  c => c.customerEverySec, (c, v) => c.customerEverySec = (int)v, "s",
                  "A timer, not a drop count."),
                R("entranceTime", "Entrance time", 0.7f, 0.3f, 2f, 0.05f,
                  c => c.entranceTime, (c, v) => c.entranceTime = v, "s"),
            }},

            new Section { title = "Furoshiki", icon = "🧧", knobs = new[]
            {
                B("endOfDay", "End of day", false,
                  c => c.endOfDay ? 1f : 0f, (c, v) => c.endOfDay = v > 0.5f,
                  "Folds the cloth shut and blocks dropping."),
            }},

            new Section { title = "Boost", icon = "💪", knobs = new[]
            {
                B("boostsOn", "Boosts on", true,
                  c => c.boostsOn ? 1f : 0f, (c, v) => c.boostsOn = v > 0.5f),
                R("shakePower", "Shake power", 1f, 0.3f, 2.2f, 0.1f,
                  c => c.shakePower, (c, v) => c.shakePower = v, "×"),
                R("chargePerMerge", "Charge per merge", 0.14f, 0.02f, 1f, 0.02f,
                  c => c.chargePerMerge, (c, v) => c.chargePerMerge = v, null,
                  "0.14 fills the meter in exactly 8 merges."),
            }},

            new Section { title = "Challenge", icon = "⚠️", knobs = new[]
            {
                B("topOut", "Can lose", true,
                  c => c.topOut ? 1f : 0f, (c, v) => c.topOut = v > 0.5f,
                  "If the pile settles above the line, the run ends."),
                R("topOutLine", "Danger line", 82f, 60f, 220f, 2f,
                  c => c.topOutLine, (c, v) => c.topOutLine = v, "px",
                  "Height inside the cloth that must not be crossed."),
                R("topOutGrace", "Overflow grace", 2.2f, 0.5f, 8f, 0.1f,
                  c => c.topOutGrace, (c, v) => c.topOutGrace = v, "s",
                  "Drains at 2× once the pile drops back."),
                B("showDangerLine", "Always show line", true,
                  c => c.showDangerLine ? 1f : 0f, (c, v) => c.showDangerLine = v > 0.5f),
            }},

            new Section { title = "Audio", icon = "🔊", knobs = new[]
            {
                B("soundOn", "Sound", true,
                  c => c.soundOn ? 1f : 0f, (c, v) => c.soundOn = v > 0.5f),
            }},

            new Section { title = "Beyond the mock", icon = "🔧", beyondMock = true, knobs = new[]
            {
                I("substeps", "Substeps", 3f, 1f, 8f,
                  c => c.substeps, (c, v) => c.substeps = (int)v, null, "Physics iterations per frame.", true),
                R("floorSag", "Floor sag", 26f, 0f, 80f, 1f,
                  c => c.floorSag, (c, v) => c.floorSag = v, "px",
                  "How high the cloth rises at its edges. This is the bowl.", true),
                R("centerPull", "Centre pull", 34f, 0f, 120f, 1f,
                  c => c.centerPull, (c, v) => c.centerPull = v, "px/s²",
                  "Inward roll while touching the cloth.", true),
                R("groundFriction", "Ground friction", 9f, 0f, 25f, 0.5f,
                  c => c.groundFriction, (c, v) => c.groundFriction = v, null, null, true),
                R("comboWindow", "Combo window", 1.4f, 0.2f, 4f, 0.1f,
                  c => c.comboWindow, (c, v) => c.comboWindow = v, "s", null, true),
                R("mergePopVy", "Merge pop", -70f, -400f, 100f, 10f,
                  c => c.mergePopVy, (c, v) => c.mergePopVy = v, "px/s", "Upward hop of a newborn.", true),
                R("squishAmount", "Squish", 1f, 0f, 2f, 0.05f,
                  c => c.squishAmount, (c, v) => c.squishAmount = v, "×", null, true),
                R("particleScale", "Particles", 1f, 0f, 3f, 0.1f,
                  c => c.particleScale, (c, v) => c.particleScale = v, "×", null, true),
                R("dropCooldown", "Drop cooldown", 0.5f, 0f, 2f, 0.05f,
                  c => c.dropCooldown, (c, v) => c.dropCooldown = v, "s", null, true),
                R("dropVy", "Drop speed", 60f, 0f, 600f, 10f,
                  c => c.dropVy, (c, v) => c.dropVy = v, "px/s", null, true),
                R("flySec", "Flight time", 0.9f, 0.2f, 2.5f, 0.05f,
                  c => c.flySec, (c, v) => c.flySec = v, "s", null, true),
                R("happyMs", "Thanks! hold", 1400f, 200f, 4000f, 100f,
                  c => c.happyMs, (c, v) => c.happyMs = v, "ms", null, true),
                I("startingBodies", "Starting pastries", 9f, 0f, 24f,
                  c => c.startingBodies, (c, v) => c.startingBodies = (int)v, null,
                  "Applies on restart only.", true),
                R("shakeDuration", "Shake length", 0.6f, 0.1f, 2f, 0.05f,
                  c => c.shakeDuration, (c, v) => c.shakeDuration = v, "s", null, true),
                R("timeScale", "Slow motion", 1f, 0.05f, 2f, 0.05f,
                  c => c.timeScale, (c, v) => c.timeScale = v, "×", "Editor only.", true),
                B("paused", "Pause", false,
                  c => c.paused ? 1f : 0f, (c, v) => c.paused = v > 0.5f, "Editor only.", true),
            }},
        };

        /// <summary>The named presets from §10.2, expressed as diffs from default.</summary>
        public static readonly (string name, (string key, float value)[] diff)[] Presets =
        {
            ("Mock default", new (string, float)[0]),
            ("Floaty", new[] { ("gravity", 650f), ("bounciness", 0.24f), ("mergeGrowTime", 1.3f), ("rotationAmount", 0.45f) }),
            ("Snappy", new[] { ("gravity", 2600f), ("bounciness", 0.02f), ("mergeGrowTime", 0.28f), ("comboDelay", 0.2f) }),
            ("Chaos", new[] { ("gravity", 2200f), ("bounciness", 0.45f), ("rotationAmount", 1f), ("sizeScale", 1.5f), ("shakePower", 2.2f), ("chargePerMerge", 0.5f) }),
            ("Zen", new[] { ("gravity", 900f), ("bounciness", 0.05f), ("customerEverySec", 40f), ("mergeGrowTime", 1f) }),
        };
    }
}
