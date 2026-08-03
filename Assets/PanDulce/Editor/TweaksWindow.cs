using System.Collections.Generic;
using System.Text;
using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Window ▸ Pan Dulce ▸ Tweaks — the design tool (§10).
    ///
    /// IMGUI rather than UI Toolkit: schema-driven rows are trivial in it, and it survives
    /// domain reloads with no UXML plumbing. GameRoot reads the TuningConfig asset every
    /// frame, which is what makes every edit apply on the NEXT frame with no restart.
    ///
    /// Domain reload is OFF in this project, so EditorApplication.update is subscribed in
    /// OnEnable and unsubscribed in OnDisable — never from a static constructor — and
    /// GameRoot.Current is resolved fresh on each repaint rather than cached (§10.4).
    /// </summary>
    public sealed class TweaksWindow : EditorWindow
    {
        const string TuningPath = "Assets/PanDulce/Config/Tuning.asset";

        [MenuItem("Window/Pan Dulce/Tweaks")]
        public static void Open()
        {
            var w = GetWindow<TweaksWindow>("Pan Dulce · Tweaks");
            w.minSize = new Vector2(340f, 400f);
            w.Show();
        }

        TuningConfig config;
        Vector2 scroll;
        string toast;
        double toastUntil, lastRepaint;
        string json = "";
        int orderTierChoice = 3, dropTierChoice = 0;
        readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            // Mandatory with domain reload off — otherwise this double-subscribes.
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnPlayModeChanged(PlayModeStateChange _) => Repaint();

        void Tick()
        {
            // ~8 Hz repaint so the live stats move without burning the Editor.
            if (!Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup - lastRepaint < 0.125) return;
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
                EditorGUILayout.HelpBox("No Tuning.asset found.\nRun Pan Dulce ▸ Rebuild Stage first.",
                                        MessageType.Warning);
                if (GUILayout.Button("Rebuild Stage")) StageBuilder.Rebuild();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            Header(cfg);
            LiveStats();
            Presets(cfg);
            Knobs(cfg);
            Actions();
            JsonBlock(cfg);
            Footer(cfg);

            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(toast) && EditorApplication.timeSinceStartup < toastUntil)
                EditorGUILayout.HelpBox(toast, MessageType.Info);
        }

        // ---------------------------------------------------------------- blocks

        void Header(TuningConfig cfg)
        {
            EditorGUILayout.LabelField("Pan Dulce · Tweaks", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Edits write to Tuning.asset and persist after exiting play mode — a tuning " +
                "session is a committable diff.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Ping config asset")) EditorGUIUtility.PingObject(cfg);
            Divider();
        }

        void LiveStats()
        {
            if (!Application.isPlaying) return;
            var g = GameRoot.Current;                      // resolved fresh, never cached
            if (g == null) return;

            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
            float fps = 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime);

            using (new EditorGUILayout.HorizontalScope())
            {
                Stat("FPS", fps.ToString("F0"), fps < 45f);
                Stat("bodies", g.Sim.Bodies.Count.ToString(), false);
                Stat("phys ms", g.PhysicsMs.ToString("F2"), g.PhysicsMs > 8f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                Stat("charge", $"{g.Boost.Charge * 100f:F0}%", false);
                Stat("next cust", $"{g.Shop.SecondsShown}s", false);
                Stat("combo", g.Sim.ComboN.ToString(), false);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                Stat("tiers", $"{g.Sim.HighestDiscovered + 1}/11", false);
                Stat("score", g.Score.Score.ToString(), false);
                Stat("danger", g.TopOut.DangerT.ToString("F1"), g.TopOut.Blinking);
            }
            if (g.GameOver) EditorGUILayout.HelpBox("Run ended — the bakery is closed.", MessageType.Warning);
            Divider();
        }

        static void Stat(string label, string value, bool warn)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = warn ? new Color(0.9f, 0.4f, 0.3f) : EditorStyles.miniLabel.normal.textColor }
            };
            EditorGUILayout.LabelField($"{label} {value}", style, GUILayout.MinWidth(60f));
        }

        void Presets(TuningConfig cfg)
        {
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (name, diff) in KnobSchema.Presets)
                {
                    bool active = MatchesPreset(cfg, diff);
                    var prev = GUI.backgroundColor;
                    if (active) GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
                    if (GUILayout.Button(name, EditorStyles.miniButton)) ApplyPreset(cfg, diff, name);
                    GUI.backgroundColor = prev;
                }
            }
            Divider();
        }

        bool MatchesPreset(TuningConfig cfg, (string key, float value)[] diff)
        {
            foreach (var knob in KnobSchema.All())
            {
                float want = knob.def;
                foreach (var d in diff) if (d.key == knob.key) want = d.value;
                if (Mathf.Abs(knob.Get(cfg.Data) - want) > 0.0001f) return false;
            }
            return true;
        }

        void ApplyPreset(TuningConfig cfg, (string key, float value)[] diff, string name)
        {
            Undo.RecordObject(cfg, "Apply preset " + name);
            foreach (var knob in KnobSchema.All())
            {
                float want = knob.def;
                foreach (var d in diff) if (d.key == knob.key) want = d.value;
                knob.Set(cfg.Data, want);
            }
            Save(cfg, $"Preset '{name}' applied");
        }

        void Knobs(TuningConfig cfg)
        {
            foreach (var section in KnobSchema.Sections)
            {
                if (!foldouts.TryGetValue(section.title, out bool open))
                    open = !section.beyondMock;   // first sections open, "Beyond the mock" collapsed

                open = EditorGUILayout.Foldout(open, $"{section.icon}  {section.title}", true);
                foldouts[section.title] = open;
                if (!open) continue;

                EditorGUI.indentLevel++;
                foreach (var knob in section.knobs) Row(cfg, knob);
                EditorGUI.indentLevel--;
            }
            Divider();
        }

        void Row(TuningConfig cfg, Knob knob)
        {
            float current = knob.Get(cfg.Data);
            bool changed = Mathf.Abs(current - knob.def) > 0.0001f;

            using (new EditorGUILayout.HorizontalScope())
            {
                string label = changed ? $"• {knob.label}" : knob.label;
                string suffix = knob.unit != null ? $" {knob.unit}" : "";

                float next = current;
                switch (knob.kind)
                {
                    case KnobKind.Bool:
                        next = EditorGUILayout.Toggle(label, current > 0.5f) ? 1f : 0f;
                        break;
                    case KnobKind.Enum:
                        next = EditorGUILayout.Popup(label, Mathf.RoundToInt(current), knob.options);
                        break;
                    case KnobKind.Int:
                        next = EditorGUILayout.IntSlider(new GUIContent($"{label}{suffix}"),
                                                         Mathf.RoundToInt(current),
                                                         Mathf.RoundToInt(knob.min),
                                                         Mathf.RoundToInt(knob.max));
                        break;
                    default:
                        next = EditorGUILayout.Slider(new GUIContent($"{label}{suffix}"),
                                                       current, knob.min, knob.max);
                        next = Mathf.Round(next / knob.step) * knob.step;   // snap to the design step
                        break;
                }

                // ↺ restores just this knob.
                using (new EditorGUI.DisabledScope(!changed))
                    if (GUILayout.Button("↺", GUILayout.Width(24f)))
                        next = knob.def;

                if (Mathf.Abs(next - current) > 0.0000001f)
                {
                    Undo.RecordObject(cfg, "Tune " + knob.label);
                    knob.Set(cfg.Data, next);
                    EditorUtility.SetDirty(cfg);
                }
            }

            if (!string.IsNullOrEmpty(knob.help))
                EditorGUILayout.LabelField(" ", knob.help, EditorStyles.miniLabel);
        }

        void Actions()
        {
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            var g = Application.isPlaying ? GameRoot.Current : null;

            using (new EditorGUI.DisabledScope(g == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Restart run")) { g.Restart(); Toast("Run restarted"); }
                    if (GUILayout.Button("Step one frame")) { g.Sim.Tick(1f / 60f); Toast("Stepped 1 frame"); }
                }

                EditorGUILayout.LabelField("Cloth", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Empty")) { g.Sim.EmptyCloth(); Toast("Cloth emptied"); }
                    if (GUILayout.Button("Fill ×8"))
                    {
                        for (int i = 0; i < 8; i++)
                            g.Sim.MakeBody(60f + Random.value * 300f, 60f + i * 20f, g.Sim.Pick(), 1f);
                        Toast("Filled ×8");
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    dropTierChoice = EditorGUILayout.IntSlider(dropTierChoice, 0, TierTable.Max);
                    if (GUILayout.Button("Drop this"))
                        g.Sim.MakeBody(SimField.CX, SimField.DropY, dropTierChoice, 1f);
                    // The pair spawns at x = 80 and x = 300 so they roll down the curve into
                    // each other — the fastest way to test merge behaviour.
                    if (GUILayout.Button("Drop pair"))
                    {
                        g.Sim.MakeBody(80f, SimField.DropY, dropTierChoice, 1f);
                        g.Sim.MakeBody(300f, SimField.DropY, dropTierChoice, 1f);
                    }
                }

                EditorGUILayout.LabelField("Progression", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reveal next")) g.Sim.RevealTier(g.Sim.HighestDiscovered + 1);
                    if (GUILayout.Button("Reveal all"))
                        for (int t = 0; t < TierTable.Count; t++) g.Sim.RevealTier(t);
                    if (GUILayout.Button("Re-lock")) g.Sim.RelockCase();
                }

                EditorGUILayout.LabelField("Customers", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Summon now")) g.Shop.OpenWindow();
                    orderTierChoice = EditorGUILayout.IntSlider(orderTierChoice, 0, TierTable.Max);
                    if (GUILayout.Button("Force order")) g.Shop.ForceOrder(orderTierChoice);
                    if (GUILayout.Button("Skip")) g.Shop.SkipCustomer(g.Tuning);
                }

                EditorGUILayout.LabelField("Boost & day", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fill charge")) g.Boost.Fill();
                    if (GUILayout.Button("Fire shake")) g.ForceShake();
                    if (GUILayout.Button("Empty charge")) g.Boost.Spend();
                    if (GUILayout.Button("Cloth colour")) g.CycleClothColor();
                }
            }
            Divider();
        }

        void JsonBlock(TuningConfig cfg)
        {
            EditorGUILayout.LabelField("Config JSON (diff from default)", EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(json)) json = BuildJson(cfg);
            json = EditorGUILayout.TextArea(json, GUILayout.MinHeight(60f));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh")) json = BuildJson(cfg);
                if (GUILayout.Button("Copy")) { EditorGUIUtility.systemCopyBuffer = json; Toast("Copied"); }
                if (GUILayout.Button("Apply")) ApplyJson(cfg, json);
            }
            Divider();
        }

        /// <summary>Hand-rolled: Newtonsoft is not installed, and this is a flat string→number map.</summary>
        static string BuildJson(TuningConfig cfg)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var knob in KnobSchema.All())
            {
                float v = knob.Get(cfg.Data);
                if (Mathf.Abs(v - knob.def) <= 0.0001f) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append('"').Append(knob.key).Append("\": ");
                if (knob.kind == KnobKind.Bool) sb.Append(v > 0.5f ? "true" : "false");
                else if (knob.kind == KnobKind.Enum) sb.Append('"').Append(knob.options[(int)v]).Append('"');
                else sb.Append(v.ToString("0.####"));
            }
            return sb.Append('}').ToString();
        }

        void ApplyJson(TuningConfig cfg, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Undo.RecordObject(cfg, "Apply JSON");

            int applied = 0;
            foreach (var knob in KnobSchema.All())
            {
                int i = text.IndexOf($"\"{knob.key}\"", System.StringComparison.Ordinal);
                if (i < 0) continue;
                int colon = text.IndexOf(':', i);
                if (colon < 0) continue;
                int end = text.IndexOfAny(new[] { ',', '}' }, colon);
                if (end < 0) end = text.Length;

                string raw = text.Substring(colon + 1, end - colon - 1).Trim().Trim('"');
                float value;
                if (knob.kind == KnobKind.Bool) value = raw == "true" ? 1f : 0f;
                else if (knob.kind == KnobKind.Enum) value = Mathf.Max(0, System.Array.IndexOf(knob.options, raw));
                else if (!float.TryParse(raw, out value)) continue;

                knob.Set(cfg.Data, Mathf.Clamp(value, knob.min, knob.max));   // clamp through the schema
                applied++;
            }
            Save(cfg, $"Applied {applied} value(s)");
        }

        void Footer(TuningConfig cfg)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to mock defaults"))
                {
                    Undo.RecordObject(cfg, "Reset tuning");
                    cfg.ResetToMockDefaults();
                    Save(cfg, "Reset to mock defaults");
                }
                if (GUILayout.Button("Save asset"))
                {
                    AssetDatabase.SaveAssets();
                    Toast("Tuning.asset saved");
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        void Save(TuningConfig cfg, string message)
        {
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            json = BuildJson(cfg);
            Toast(message);
        }

        void Toast(string message)
        {
            toast = message;
            toastUntil = EditorApplication.timeSinceStartup + 2.5;
            Repaint();
        }

        static void Divider()
        {
            EditorGUILayout.Space(4f);
            var r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
            EditorGUILayout.Space(4f);
        }
    }
}
