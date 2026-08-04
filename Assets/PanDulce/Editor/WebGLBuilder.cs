using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// WebGL build for itch.io. Decompression fallback stays ON so the Brotli-compressed
    /// build runs without any server header configuration — itch's CDN then needs nothing
    /// special. The default canvas matches the 446 × 900 safe box (StageCoords.SafeW/H),
    /// which is the itch embed viewport to enter; StageFitter contains any other size.
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutputDir = "Builds/WebGL";

        [MenuItem("Pan Dulce/Build WebGL (itch.io)")]
        public static void Build()
        {
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.defaultWebScreenWidth = 446;
            PlayerSettings.defaultWebScreenHeight = 900;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log($"[PanDulce] WebGL build {s.result}: {s.outputPath} " +
                      $"({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime.TotalMinutes:F1} min)");
        }
    }
}
