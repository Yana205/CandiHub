using PanDulce.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PanDulce.Editor
{
    /// <summary>
    /// Writes machine-independent fit values into the scene just before it is saved.
    ///
    /// StageFitter runs [ExecuteAlways] so the Game view previews the real fit, which means it
    /// leaves a value derived from *this* machine's Game view on the camera. Committing that
    /// makes Main.unity churn on every resize and conflict between developers — the README
    /// calls concurrent scene edits the main source of pain in Unity teams.
    ///
    /// This is also how the old 1.0622 stage scale entered the repo: it was the fit for a
    /// 578 x 956 editor panel, baked in and never noticed.
    ///
    /// So on save we reset to the canonical design-frame values. The fitter recomputes the
    /// live values on the next update, so nothing is lost in the Editor and nothing at runtime
    /// depends on what is stored here.
    /// </summary>
    [InitializeOnLoad]
    static class StageSceneNormalizer
    {
        static StageSceneNormalizer() => EditorSceneManager.sceneSaving += OnSceneSaving;

        static void OnSceneSaving(Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var fitter = root.GetComponentInChildren<StageFitter>(true);
                if (fitter == null) continue;

                // The stage never scales, and its offset is fixed. Both are already what the
                // fitter writes; restating them here makes a stray manual edit self-correcting.
                fitter.transform.localScale = Vector3.one;
                fitter.transform.localPosition = new Vector3(
                    -StageCoords.StageW * StageCoords.PX * 0.5f,
                     StageCoords.StageH * StageCoords.PX * 0.5f, 0f);
            }

            // 4.5 is the height-limited baseline: 900 stage px exactly filling the view.
            var cam = Camera.main;
            if (cam != null && cam.gameObject.scene == scene)
                cam.orthographicSize = StageCoords.SafeH * StageCoords.PX * 0.5f;
        }
    }
}
