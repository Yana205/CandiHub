using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Window ▸ Pan Dulce ▸ Designer — the curated design panel. Dock it next to the
    /// Inspector. Every control carries a plain-language line saying what it does.
    ///
    /// Edits write to Tuning.asset, apply on the NEXT frame in play mode, and persist
    /// after play mode exits (a tuning session is a committable diff).
    ///
    /// Domain reload is OFF: EditorApplication.update is subscribed in OnEnable and
    /// unsubscribed in OnDisable, and GameRoot.Current is resolved fresh every repaint.
    /// </summary>
    public sealed class DesignerWindow : EditorWindow
    {
        const string TuningPath = "Assets/PanDulce/Config/Tuning.asset";

        [MenuItem("Window/Pan Dulce/Designer")]
        public static void Open()
        {
            var w = GetWindow<DesignerWindow>("Pan Dulce · Designer");
            w.minSize = new Vector2(320f, 420f);
            w.Show();
        }

        TuningConfig config;
        Vector2 scroll;
        int pairTier;
        double lastRepaint;

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup - lastRepaint < 0.2) return;
            lastRepaint = EditorApplication.timeSinceStartup;
            Repaint();
        }

        TuningConfig Config
        {
            get
            {
                if (config == null) config = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
                return config;
            }
        }

        void OnGUI()
        {
            var cfg = Config;
            if (cfg == null)
            {
                EditorGUILayout.HelpBox("No Tuning.asset found. Run Pan Dulce ▸ Rebuild Stage first.",
                                        MessageType.Warning);
                if (GUILayout.Button("Rebuild Stage")) StageBuilder.Rebuild();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var d = cfg.Data;

            EditorGUILayout.LabelField("Feel", EditorStyles.boldLabel);
            Slider(cfg, "Gravity", "How fast pastries fall. Higher = snappier, lower = floatier.",
                   d.gravity, 600f, 3000f, v => d.gravity = v);
            Slider(cfg, "Bounciness", "How much everything rebounds on impact.",
                   d.bounciness, 0f, 0.5f, v => d.bounciness = v);
            Slider(cfg, "Center pull",
                   "A constant sideways drift toward the middle of the box — the old cloth-" +
                   "bowl feel. This is what herds desserts together (and into idle merges) " +
                   "when you aren't touching them. 0 = they stay where they land.",
                   d.centerPull, 0f, 100f, v => d.centerPull = v);
            Slider(cfg, "Floor sag",
                   "How much the floor curves up at the edges, like a hanging cloth. " +
                   "Desserts roll downhill into the middle. 0 = the flat, straight box floor.",
                   d.floorSag, 0f, 60f, v => d.floorSag = v);
            PercentSlider(cfg, "Pile size",
                          "Scales every dessert — art and physics together — as a percentage of " +
                          "its authored size. 100% is the drawn size. Bigger fills the cloth " +
                          "faster. Per-dessert tweaks live in Studio ▸ Dessert chain.",
                          d.sizeScale, 50f, 300f, v => d.sizeScale = v);
            Slider(cfg, "Merge grow time", "How long a freshly merged pastry takes to pop in.",
                   d.mergeGrowTime, 0.2f, 2f, v => d.mergeGrowTime = v);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Rhythm & difficulty", EditorStyles.boldLabel);
            PercentSlider(cfg, "Merge squeeze",
                          "How firmly two matching desserts must press together to merge, as a " +
                          "share of the smaller one's size. 0% = touching is enough. Above 0%, " +
                          "resting neighbours stay put until something presses them — a landing " +
                          "drop, pile weight, or a shake — and one hard-enough press counts for " +
                          "the whole contact, so it composes with Touch time: press once, rest " +
                          "together long enough, merge. Above ~15% needs a full-height drop.",
                          d.mergeOverlapPct, 0f, 25f, v => d.mergeOverlapPct = v);
            Slider(cfg, "Touch time to merge",
                   "Seconds two matching desserts must stay in contact before merging. " +
                   "0 = instant. A short hold makes the pile readable — you can see a merge " +
                   "coming and still change your mind.",
                   d.mergeTouchSec, 0f, 1.5f, v => d.mergeTouchSec = v);
            Slider(cfg, "Idle merge time",
                   "Two desserts that drifted together AT REST merge only after this long " +
                   "side by side — the pile's own quiet progress, slowed. Throws, knocks " +
                   "and shakes ignore this and use the plain touch time, so your own moves " +
                   "always feel answered. 0 = no distinction.",
                   d.idleMergeSec, 0f, 8f, v => d.idleMergeSec = v);
            Slider(cfg, "Min age to merge",
                   "A dessert BORN FROM A MERGE cannot re-merge for this many seconds — the " +
                   "brake on instant chain reactions. Dropped desserts never wait: a throw " +
                   "onto a match merges as soon as the touch time is served.",
                   d.comboDelay, 0f, 2f, v => d.comboDelay = v);
            IntSlider(cfg, "Merges to reveal",
                      "How many times a dessert must be merged into before its case seat " +
                      "colours in — and it joins the spawn menu and the order pool. 1 = the " +
                      "classic first-merge reveal; higher stretches the whole discovery arc, " +
                      "since each new tier also has to be built from revealed spawns.",
                      d.discoverMerges, 1, 6, v => d.discoverMerges = v);
            Slider(cfg, "Matching pull",
                   "Matching desserts within about a diameter drift toward each other. " +
                   "0 = off. Higher makes pairs find each other on their own — a helping " +
                   "hand, or a hazard when you wanted them apart.",
                   d.kinPull, 0f, 400f, v => d.kinPull = v);
            Slider(cfg, "Drop cooldown",
                   "Seconds between drops. The base beat of the whole game.",
                   d.dropCooldown, 0.1f, 1.5f, v => d.dropCooldown = v);
            Slider(cfg, "Low-tier lean",
                   "How hard the deal leans on the small desserts. 1 = the classic " +
                   "4:3:2:1; 2 squares the weights, so mochi dominates the hand and a " +
                   "dealt donut becomes a rare treat.",
                   d.spawnBias, 1f, 3f, v => d.spawnBias = v);
            Slider(cfg, "Big deal wait (s)",
                   "Run seconds before the top two tiers can be DEALT as the next " +
                   "dessert. Merging up to them is untouched — this only keeps the " +
                   "early hand small. 0 = off.",
                   d.bigDealDelaySec, 0f, 30f, v => d.bigDealDelaySec = v);
            IntSlider(cfg, "Hold back newest",
                      "How many of the newest reveals stay OUT of the hand. 1 = the dessert " +
                      "you just unlocked must be merged for, never dealt, until the next one " +
                      "colours in — the deal opens one step behind the case. 0 = off.",
                      d.dealTopMargin, 0, 3, v => d.dealTopMargin = v);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Customers", EditorStyles.boldLabel);
            IntSlider(cfg, "Customer every (s)", "Seconds between the bear's visits.",
                      d.customerEverySec, 5, 60, v => d.customerEverySec = v);
            Slider(cfg, "Start delay (s)", "Extra calm seconds before the FIRST customer of " +
                   "a run only — a few moments to read the shop. Later visits use the plain cadence.",
                   d.startDelaySec, 0f, 20f, v => d.startDelaySec = v);
            Slider(cfg, "Opening calm (s)",
                   "The run's first seconds play in gentle slow motion, easing up to full " +
                   "speed — the starting pile settles dreamily while the case silhouettes " +
                   "tease what can come next. 0 = off.",
                   d.startCalmSec, 0f, 20f, v => d.startCalmSec = v);
            PercentSlider(cfg, "Opening tempo",
                          "How slow the very first moment of the calm runs, as a share of " +
                          "full speed. Eases back to 100% over the opening calm.",
                          d.startCalmScale, 20f, 100f, v => d.startCalmScale = v);
            IntSlider(cfg, "Known at start", "How many desserts begin discovered — in colour, " +
                      "spawnable, orderable. 3 keeps Purin a silhouette until first merged.",
                      d.startDiscovered, 1, 6, v => d.startDiscovered = v);
            IntSlider(cfg, "Starting pastries", "How many desserts already sit in the box when " +
                      "the day opens. 0 = an empty box; more = a head start to merge into. " +
                      "Takes effect on the next Restart run.",
                      d.startingBodies, 0, 20, v => d.startingBodies = v);
            Slider(cfg, "Rise time", "How long the bear takes to pop up behind the counter.",
                   d.entranceTime, 0.3f, 2f, v => d.entranceTime = v);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Shake", EditorStyles.boldLabel);
            Slider(cfg, "Shake power", "How hard a full-meter shake launches the pile.",
                   d.shakePower, 0.3f, 2.2f, v => d.shakePower = v);
            Slider(cfg, "Charge per merge", "Meter gained per merge. 0.14 = full in 8 merges.",
                   d.chargePerMerge, 0.02f, 1f, v => d.chargePerMerge = v);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Effects & sound", EditorStyles.boldLabel);
            Slider(cfg, "Particle intensity", "Scales every burst: merge puffs, sparkles, dust.",
                   d.particleScale, 0f, 3f, v => d.particleScale = v);
            Toggle(cfg, "Sound", "All six procedural cues on or off.",
                   d.soundOn, v => d.soundOn = v);
            Slider(cfg, "Time scale", "Slow-motion for inspecting physics. Editor only.",
                   d.timeScale, 0.05f, 2f, v => d.timeScale = v);

            EditorGUILayout.Space(10f);
            Buttons(cfg, d);

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Reset all to defaults"))
            {
                Undo.RecordObject(cfg, "Reset tuning");
                cfg.ResetToMockDefaults();
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.LabelField("Edits apply live in play mode and stick afterwards.",
                                       EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ rows

        void Slider(TuningConfig cfg, string label, string help, float value, float min, float max,
                    System.Action<float> set)
        {
            float next = EditorGUILayout.Slider(label, value, min, max);
            Help(help);
            Apply(cfg, label, !Mathf.Approximately(next, value), () => set(next));
        }

        /// <summary>
        /// A fraction shown as a percentage. The stored value stays the multiplier the sim
        /// consumes — only the row's units change, so "how much bigger" is a number a designer
        /// can read off the label instead of a decimal they have to translate.
        /// </summary>
        void PercentSlider(TuningConfig cfg, string label, string help, float value,
                           float minPct, float maxPct, System.Action<float> set)
        {
            float pct = Mathf.Round(EditorGUILayout.Slider(label, value * 100f, minPct, maxPct));
            Help(help);
            Apply(cfg, label, !Mathf.Approximately(pct, Mathf.Round(value * 100f)),
                  () => set(pct * 0.01f));
        }

        void IntSlider(TuningConfig cfg, string label, string help, int value, int min, int max,
                       System.Action<int> set)
        {
            int next = EditorGUILayout.IntSlider(label, value, min, max);
            Help(help);
            Apply(cfg, label, next != value, () => set(next));
        }

        void Toggle(TuningConfig cfg, string label, string help, bool value, System.Action<bool> set)
        {
            bool next = EditorGUILayout.Toggle(label, value);
            Help(help);
            Apply(cfg, label, next != value, () => set(next));
        }

        static void Help(string text)
            => EditorGUILayout.LabelField(" ", text, EditorStyles.wordWrappedMiniLabel);

        void Apply(TuningConfig cfg, string label, bool changed, System.Action write)
        {
            if (!changed) return;
            Undo.RecordObject(cfg, "Tune " + label);
            write();
            EditorUtility.SetDirty(cfg);
        }

        /// <summary>
        /// Live diagnosis of the merge gate for the CLOSEST matching pair on the cloth —
        /// every condition the sim checks, with its live numbers, so "why isn't it
        /// merging?" answers itself instead of hiding in code.
        /// </summary>
        static void MergeGateReadout(TuningConfig cfg, MergeSim sim)
        {
            var bodies = sim.Bodies;
            Body a = null, c = null;
            float bestGap = float.MaxValue, bestRmin = 0f;
            for (int i = 0; i < bodies.Count; i++)
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    Body p = bodies[i], q = bodies[j];
                    if (p.dead || q.dead) continue;
                    if (p.tier != q.tier || p.skin != q.skin || p.tier >= TierTable.Max) continue;
                    float rp = TierTable.Er(p, cfg), rq = TierTable.Er(q, cfg);
                    float dx = q.x - p.x, dy = q.y - p.y;
                    float gap = Mathf.Sqrt(dx * dx + dy * dy) - (rp + rq);
                    if (gap < bestGap) { bestGap = gap; a = p; c = q; bestRmin = Mathf.Min(rp, rq); }
                }

            EditorGUILayout.Space(2f);
            if (a == null)
            {
                EditorGUILayout.LabelField("Merge gate: no matching pair on the cloth yet.",
                                           EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"Merge gate — closest matching pair ({TierTable.Names[a.tier]} + {TierTable.Names[c.tier]}):",
                EditorStyles.miniBoldLabel);

            float needPx = cfg.MergeOverlapPct * bestRmin;
            float age = cfg.ComboDelay;
            GateRow(sim.Now >= MergeSim.StartMergeGraceSec, "past start grace",
                    $"no merges in the run's first {MergeSim.StartMergeGraceSec:F0}s");
            GateRow(bestGap < MergeSim.KinTouchSlack, "touching",
                    $"gap {Mathf.Max(0f, bestGap):F1}px — counts within {MergeSim.KinTouchSlack:F1}px");
            GateRow(a.spawnT > 0.55f && c.spawnT > 0.55f, "grown in",
                    $"{a.spawnT:P0} / {c.spawnT:P0} — both need >55% of Merge grow time");
            string AgeStr(Body b) => b.bornOfMerge ? $"{sim.Now - b.bornAt:F1}s" : "dropped";
            GateRow((!a.bornOfMerge || sim.Now - a.bornAt > age) &&
                    (!c.bornOfMerge || sim.Now - c.bornAt > age), "old enough",
                    $"{AgeStr(a)} / {AgeStr(c)} — only merge-born wait Min age {age:F1}s");
            GateRow(a.squeezed && c.squeezed, "squeezed",
                    needPx <= 0f ? "squeeze 0% — any real touch counts"
                                 : $"needs one press ≥ {needPx:F1}px during this contact (a landing drop or a shake)");
            bool struck = a.struck && c.struck;
            float needSec = struck ? cfg.MergeTouchSec : Mathf.Max(cfg.IdleMergeSec, cfg.MergeTouchSec);
            GateRow(a.kinTouchT >= needSec && c.kinTouchT >= needSec, "touch time",
                    $"{Mathf.Min(a.kinTouchT, c.kinTouchT):F2}s of {needSec:F2}s — " +
                    (struck ? "struck (thrown/knocked): fast lane"
                            : $"idle contact: slow lane (a strike ≥ {MergeSim.StrikeSpeed:F0}px/s would fast-lane it)"));
        }

        static void GateRow(bool ok, string label, string detail)
            => EditorGUILayout.LabelField($"   {(ok ? "✓" : "✗")} {label} — {detail}",
                                          EditorStyles.miniLabel);

        // ------------------------------------------------------------ buttons

        void Buttons(TuningConfig cfg, SimConfigData d)
        {
            EditorGUILayout.LabelField("Try it (play mode)", EditorStyles.boldLabel);
            var g = Application.isPlaying ? GameRoot.Current : null;   // fresh, never cached

            if (g != null)
            {
                EditorGUILayout.LabelField(
                    $"Desserts: {g.Sim.Bodies.Count}   Highest tier: {g.Sim.HighestDiscovered}" +
                    $"   Combo: {g.Sim.ComboN}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Coins: {g.Purse.Coins}   Served: {g.Shop.Served}   Physics: {g.PhysicsMs:F1} ms",
                    EditorStyles.miniLabel);
                MergeGateReadout(cfg, g.Sim);
            }

            using (new EditorGUI.DisabledScope(g == null))
            {
                if (GUILayout.Button("Restart run"))
                    g.Restart();
                Help("Clears the cloth and starts a fresh day.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    pairTier = EditorGUILayout.IntSlider(pairTier, 0, TierTable.Max, GUILayout.Width(160f));
                    if (GUILayout.Button("Drop a merge pair"))
                    {
                        g.Sim.MakeBody(80f, SimField.DropY, pairTier, 1f);
                        g.Sim.MakeBody(300f, SimField.DropY, pairTier, 1f);
                    }
                }
                Help("Two of the chosen tier roll down the curve into each other — instant merge test.");

                if (GUILayout.Button("Fill the cloth (×8)"))
                    for (int i = 0; i < 8; i++)
                        g.Sim.MakeBody(60f + Random.value * 300f, 60f + i * 20f, g.Sim.Pick(), 1f);
                Help("Drops eight random pastries to build a pile fast.");

                if (GUILayout.Button("Summon the bear now"))
                    g.Shop.OpenWindow();
                Help("Skips the countdown — the bear pops up and places an order.");

                if (GUILayout.Button("Fire the shake"))
                    g.ForceShake();
                Help("Launches the pile as if the meter were full. Ignores the charge.");

                if (GUILayout.Button(d.endOfDay ? "Reopen the bakery" : "Toggle closing time"))
                {
                    Undo.RecordObject(cfg, "Toggle end of day");
                    d.endOfDay = !d.endOfDay;
                    EditorUtility.SetDirty(cfg);
                }
                Help("Folds the cloth shut, or opens it back up.");

                if (GUILayout.Button("Screenshot the stage"))
                {
                    string file = $"stage_{System.DateTime.Now:HHmmss}.png";
                    ScreenCapture.CaptureScreenshot(file);
                    Debug.Log($"[PanDulce] screenshot → {System.IO.Path.GetFullPath(file)}");
                }
                Help("Saves a PNG of the Game view next to the project folder.");
            }
        }
    }
}
