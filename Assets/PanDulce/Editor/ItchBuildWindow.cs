using System.IO;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Window ▸ Pan Dulce ▸ Build for itch.io — one button that makes the WebGL build and the
    /// upload-ready zip. Same plain-language style as DesignerWindow: every control says what it does.
    ///
    /// Builds are kicked off via EditorApplication.delayCall, never inline in OnGUI — BuildPlayer
    /// triggers a domain reload and its own progress window, which would unbalance the IMGUI
    /// layout stack and spray EndLayoutGroup errors.
    /// </summary>
    public sealed class ItchBuildWindow : EditorWindow
    {
        const string DevPrefKey = "PanDulce.ItchBuild.Development";

        [MenuItem("Window/Pan Dulce/Build for itch.io")]
        public static void Open()
        {
            var w = GetWindow<ItchBuildWindow>("Pan Dulce · itch.io");
            w.minSize = new Vector2(340f, 300f);
            w.Show();
        }

        bool development;

        void OnEnable() => development = EditorPrefs.GetBool(DevPrefKey, false);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Ship it", EditorStyles.boldLabel);

            bool nextDev = EditorGUILayout.Toggle("Development build", development);
            if (nextDev != development)
            {
                development = nextDev;
                EditorPrefs.SetBool(DevPrefKey, development);
            }
            Help("Keeps the profiler and readable stack traces. Leave OFF for anything you upload — " +
                 "it roughly doubles the download.");

            EditorGUILayout.Space(8f);

            GUI.enabled = !EditorApplication.isCompiling && !Application.isPlaying;
            var big = new GUIStyle(GUI.skin.button) { fixedHeight = 40f, fontStyle = FontStyle.Bold };
            if (GUILayout.Button("Build WebGL + Zip for itch.io", big))
                Run(build: true);
            Help("Full run: builds to Builds/WebGL, packs Builds/pandulce-webgl-itch.zip, then opens " +
                 "the folder. Takes a few minutes — Unity's own progress bar takes over.");

            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(!WebGLBuilder.HasBuild))
            {
                if (GUILayout.Button("Re-zip the existing build"))
                    Run(build: false);
            }
            Help(WebGLBuilder.HasBuild
                     ? "Skips the build and just repacks what's already in Builds/WebGL. Seconds, not minutes."
                     : "Nothing in Builds/WebGL yet — build once first.");

            GUI.enabled = true;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Where things land", EditorStyles.boldLabel);
            PathRow("Build", WebGLBuilder.OutputDir, WebGLBuilder.HasBuild);
            PathRow("Zip", WebGLBuilder.ZipPath, WebGLBuilder.HasZip);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("On itch.io", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Upload the zip, tick “This file will be played in the browser”, and set the embed " +
                $"viewport to {WebGLBuilder.CanvasWidth} × {WebGLBuilder.CanvasHeight}. index.html sits at " +
                "the zip root, which is what itch expects.",
                EditorStyles.wordWrappedMiniLabel);

            if (EditorApplication.isCompiling)
                EditorGUILayout.HelpBox("Compiling — wait for scripts to settle before building.",
                                        MessageType.Info);
            else if (Application.isPlaying)
                EditorGUILayout.HelpBox("Exit play mode before building.", MessageType.Info);
        }

        // ------------------------------------------------------------ helpers

        void Run(bool build)
        {
            bool dev = development;
            EditorApplication.delayCall += () =>       // never build from inside OnGUI
            {
                if (build && !WebGLBuilder.Build(dev)) return;
                if (!WebGLBuilder.Zip()) return;
                EditorUtility.RevealInFinder(Path.GetFullPath(WebGLBuilder.ZipPath));
            };
        }

        static void PathRow(string label, string path, bool exists)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{label}: {path}", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("Reveal", EditorStyles.miniButton, GUILayout.Width(56f)))
                        EditorUtility.RevealInFinder(Path.GetFullPath(path));
                }
            }
        }

        static void Help(string text)
            => EditorGUILayout.LabelField(" ", text, EditorStyles.wordWrappedMiniLabel);
    }
}
