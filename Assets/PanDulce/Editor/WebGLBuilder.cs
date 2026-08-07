using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// UnityEngine also defines a CompressionLevel (AssetBundle compression) — disambiguate.
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace PanDulce.Editor
{
    /// <summary>
    /// WebGL build for itch.io, plus the upload-ready zip. Decompression fallback stays ON so
    /// the Brotli-compressed build runs without any server header configuration — itch's CDN
    /// then needs nothing special. The default canvas matches the 446 × 900 safe box
    /// (StageCoords.SafeW/H), which is the itch embed viewport to enter; StageFitter contains
    /// any other size.
    ///
    /// itch.io requires index.html at the ROOT of the zip — never nested in a folder — so every
    /// entry is written relative to <see cref="OutputDir"/>.
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutputDir = "Builds/WebGL";
        public const string ZipPath   = "Builds/pandulce-webgl-itch.zip";

        public const int CanvasWidth  = 446;
        public const int CanvasHeight = 900;

        /// <summary>Already-compressed payloads — storing them beats re-deflating them.</summary>
        static readonly string[] StoreOnly =
            { ".br", ".gz", ".data", ".unityweb", ".png", ".jpg", ".jpeg", ".mp3", ".ogg" };

        [MenuItem("Pan Dulce/Build WebGL + Zip for itch.io", false, 100)]
        public static void BuildAndZip()
        {
            if (!Build()) return;
            if (!Zip()) return;
            EditorUtility.RevealInFinder(Path.GetFullPath(ZipPath));
        }

        [MenuItem("Pan Dulce/Zip Existing WebGL Build", false, 101)]
        public static void ZipOnly()
        {
            if (Zip()) EditorUtility.RevealInFinder(Path.GetFullPath(ZipPath));
        }

        // ------------------------------------------------------------ build

        /// <summary>Builds the player. Returns false (and logs) if the build did not succeed.</summary>
        public static bool Build(bool development = false)
        {
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.defaultWebScreenWidth  = CanvasWidth;
            PlayerSettings.defaultWebScreenHeight = CanvasHeight;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None,
            });

            var s = report.summary;
            if (s.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[PanDulce] WebGL build {s.result} — {s.totalErrors} error(s). " +
                               "See the Console above for the first failure.");
                return false;
            }

            Debug.Log($"[PanDulce] WebGL build {s.result}: {s.outputPath} " +
                      $"({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime.TotalMinutes:F1} min)");
            return true;
        }

        // ------------------------------------------------------------ zip

        /// <summary>
        /// Packs <see cref="OutputDir"/> into <see cref="ZipPath"/> with index.html at the root,
        /// overwriting any previous zip. Returns false (and logs) if there is nothing to pack.
        /// </summary>
        public static bool Zip()
        {
            string src = Path.GetFullPath(OutputDir);
            if (!Directory.Exists(src))
            {
                Debug.LogError($"[PanDulce] No build at {OutputDir} — build first.");
                return false;
            }
            if (!File.Exists(Path.Combine(src, "index.html")))
            {
                Debug.LogError($"[PanDulce] {OutputDir}/index.html is missing — itch.io needs it at " +
                               "the zip root. Rebuild before zipping.");
                return false;
            }

            string dst = Path.GetFullPath(ZipPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));

            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            try
            {
                // Write to a temp file first so a failure never leaves a half-written zip behind.
                string tmp = dst + ".tmp";
                if (File.Exists(tmp)) File.Delete(tmp);

                using (var stream = new FileStream(tmp, FileMode.CreateNew))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string full = files[i];
                        string name = full.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar,
                                                                          Path.AltDirectorySeparatorChar);
                        name = name.Replace('\\', '/');            // zip spec: forward slashes only
                        if (name.StartsWith(".") || name.EndsWith(".DS_Store")) continue;

                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Zipping for itch.io", name, (i + 1f) / files.Length))
                        {
                            EditorUtility.ClearProgressBar();
                            File.Delete(tmp);
                            Debug.LogWarning("[PanDulce] Zip cancelled.");
                            return false;
                        }

                        var level = Array.IndexOf(StoreOnly, Path.GetExtension(full).ToLowerInvariant()) >= 0
                            ? CompressionLevel.NoCompression
                            : CompressionLevel.Optimal;

                        var entry = zip.CreateEntry(name, level);
                        using var input  = File.OpenRead(full);
                        using var output = entry.Open();
                        input.CopyTo(output);
                    }
                }

                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PanDulce] Zip failed: {e.Message}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            float mb = new FileInfo(dst).Length / (1024f * 1024f);
            Debug.Log($"[PanDulce] itch.io zip ready: {ZipPath} ({mb:F1} MB, {files.Length} files). " +
                      $"Upload it as HTML, tick \"This file will be played in the browser\", and set the " +
                      $"embed viewport to {CanvasWidth} × {CanvasHeight}.");
            return true;
        }

        public static bool HasBuild => File.Exists(Path.Combine(Path.GetFullPath(OutputDir), "index.html"));
        public static bool HasZip   => File.Exists(Path.GetFullPath(ZipPath));
    }
}
