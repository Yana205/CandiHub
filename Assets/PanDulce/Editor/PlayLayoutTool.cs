using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Makes play-mode layout tweaks survive.
    ///
    /// Unity throws away scene changes made during play mode. The workflow here: move
    /// things around WHILE playing until they look right, then hit
    /// Pan Dulce → Capture Layout From Play (⌘⇧L). That snapshots every persistent
    /// transform under the stage to PlayLayoutSnapshot.json. When play mode ends the
    /// snapshot is re-applied to the edit-mode scene automatically (just save with ⌘S),
    /// and StageBuilder re-applies it after every rebuild, so the placement is durable.
    ///
    /// Runtime-driven transforms are excluded on both capture and apply, because baking
    /// them would fight the code that animates them:
    ///  - ShakeRoot (boost-shake offset) and everything under Bodies/Effects/FloatingText
    ///    (pooled, positioned by the sim every frame — moving Bodies would also desync
    ///    tap-to-serve from the physics; raise Tuning.floorY instead to lift the pile)
    ///  - TopBar (SafeAreaInset moves it per device)
    ///  - HangingSign (SignView sways it)
    ///  - Guides (a truth overlay — it must stay at the sim origin)
    /// </summary>
    public static class PlayLayoutTool
    {
        const string SnapshotPath = "Assets/PanDulce/Config/PlayLayoutSnapshot.json";
        const string StageName = "[ 20 · STAGE ]";
        const string PlaySessionKey = "PanDulce.PlayStartedTicks";

        [Serializable]
        class Entry
        {
            public string path;
            public Vector3 pos;
            public Vector3 scale;
            public float rotZ;
        }

        [Serializable]
        class Snapshot
        {
            public List<Entry> entries = new List<Entry>();
        }

        static readonly string[] SkipEndsWith =
        {
            "/ShakeRoot", "/TopBar", "/HangingSign", "/Guides",
        };

        static readonly string[] SkipContains =
        {
            "/ShakeRoot/Bodies", "/ShakeRoot/Effects", "/ShakeRoot/FloatingText",
        };

        static bool Skipped(string path)
        {
            foreach (var s in SkipEndsWith) if (path.EndsWith(s, StringComparison.Ordinal)) return true;
            foreach (var s in SkipContains) if (path.Contains(s)) return true;
            return false;
        }

        // ------------------------------------------------------------------ capture

        [MenuItem("Pan Dulce/Capture Layout From Play %#l")]
        public static void Capture()
        {
            var stage = GameObject.Find(StageName);
            if (stage == null)
            {
                Debug.LogWarning("[PanDulce] no stage found — open Main.unity first.");
                return;
            }

            var snap = new Snapshot();
            Walk(stage.transform, StageName, snap);
            File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snap, true));
            AssetDatabase.Refresh();
            Debug.Log($"[PanDulce] captured {snap.entries.Count} transforms to {SnapshotPath}" +
                      (Application.isPlaying ? " — they will be re-applied when play mode ends." : ""));
        }

        static void Walk(Transform t, string path, Snapshot snap)
        {
            foreach (Transform c in t)
            {
                if ((c.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;   // generated
                string p = path + "/" + c.name;
                if (!Skipped(p))
                    snap.entries.Add(new Entry
                    {
                        path = p,
                        pos = c.localPosition,
                        scale = c.localScale,
                        rotZ = c.localEulerAngles.z,
                    });
                Walk(c, p, snap);
            }
        }

        // ------------------------------------------------------------------ apply

        [MenuItem("Pan Dulce/Apply Captured Layout")]
        public static void ApplyMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[PanDulce] exit play mode first — apply targets the saved scene.");
                return;
            }
            int n = Apply();
            if (n >= 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Applies the snapshot to the open scene. Returns entries applied, -1 if none.</summary>
        public static int Apply()
        {
            if (!File.Exists(SnapshotPath)) return -1;
            var snap = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
            if (snap?.entries == null || snap.entries.Count == 0) return -1;

            var stage = GameObject.Find(StageName);
            if (stage == null) return -1;

            int applied = 0, missing = 0;
            foreach (var e in snap.entries)
            {
                if (Skipped(e.path)) continue;      // old snapshots may hold runtime paths
                if (!e.path.StartsWith(StageName + "/", StringComparison.Ordinal)) continue;

                var t = stage.transform.Find(e.path.Substring(StageName.Length + 1));
                if (t == null) { missing++; continue; }

                t.localPosition = e.pos;
                t.localScale = e.scale;
                var eu = t.localEulerAngles; eu.z = e.rotZ;
                t.localEulerAngles = eu;
                applied++;
            }

            Debug.Log($"[PanDulce] layout snapshot applied to {applied} transforms" +
                      (missing > 0 ? $" ({missing} paths no longer exist)" : ""));
            return applied;
        }

        // -------------------------------------------------- auto-apply on play exit

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    SessionState.SetString(PlaySessionKey, DateTime.UtcNow.Ticks.ToString());

                if (state != PlayModeStateChange.EnteredEditMode) return;

                // Only auto-apply when the snapshot was captured during the play session
                // that just ended — an old file should not silently move the scene.
                if (!File.Exists(SnapshotPath)) return;
                if (!long.TryParse(SessionState.GetString(PlaySessionKey, ""), out long started)) return;
                if (File.GetLastWriteTimeUtc(SnapshotPath).Ticks < started) return;

                if (Apply() > 0)
                {
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    Debug.Log("[PanDulce] play-mode layout restored — press ⌘S to keep it.");
                }
            };
        }
    }
}
