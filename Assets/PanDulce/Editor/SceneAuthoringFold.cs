using PanDulce.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Makes EDIT-mode drags of generated nodes stick.
    ///
    /// GeneratedView children are DontSave, so grabbing one in the Hierarchy in edit mode
    /// used to be a silent no-op: the drag looked right until save or play rebuilt the view
    /// from code. Just before every scene save and before entering play mode this folds the
    /// designer's intent into data the scene actually owns:
    ///
    ///  · a nudged "Content" node → its persistent parent folder (same rule the play-mode
    ///    capture in PlayLayoutTool uses), and
    ///  · a dragged Bear / BearAnchor → CustomerView.anchor, the serialized field the
    ///    runtime walk animation reads.
    /// </summary>
    [InitializeOnLoad]
    static class SceneAuthoringFold
    {
        static SceneAuthoringFold()
        {
            EditorSceneManager.sceneSaving += (scene, path) => FoldAll();
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) FoldAll();
            };
        }

        static void FoldAll()
        {
            if (Application.isPlaying) return;

            bool changed = false;

            // The bear first: its drag is expressed against Content, so capture it before
            // Content itself gets zeroed by the fold below.
            var customer = Object.FindAnyObjectByType<CustomerView>(FindObjectsInactive.Include);
            if (customer != null) changed |= CaptureBearAnchor(customer);

            // The danger line too, and for the same reason — but its Y is not scene data,
            // it IS the lose height (Tuning.topOutLine). Captured before the Content fold
            // below, which would otherwise swallow a Content nudge into the root.
            var danger = Object.FindAnyObjectByType<DangerLineView>(FindObjectsInactive.Include);
            if (danger != null) changed |= CaptureDangerLine(danger);

            foreach (var view in Object.FindObjectsByType<GeneratedView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= PlayLayoutTool.FoldGeneratedContent(view.transform);

            if (changed && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Reads where the bear actually stands in the scene and writes it back into the
        /// serialized anchor. Handles a drag of BearAnchor, of the Bear sprite (whose rest
        /// pose is the half-height lift above the anchor), or both.
        /// </summary>
        static bool CaptureBearAnchor(CustomerView view)
        {
            var anchorT = view.transform.Find("Content/BearAnchor");
            if (anchorT == null) return false;

            var stage = new Vector2(anchorT.localPosition.x / StageCoords.PX,
                                    -anchorT.localPosition.y / StageCoords.PX);

            var bearT = anchorT.Find("Bear");
            if (bearT != null)
            {
                var rest = new Vector3(0f, CustomerView.SpriteLift * StageCoords.PX, 0f);
                Vector3 d = bearT.localPosition - rest;
                if (d.sqrMagnitude > 1e-10f)
                {
                    stage += new Vector2(d.x / StageCoords.PX, -d.y / StageCoords.PX);
                    bearT.localPosition = rest;
                }
            }

            if ((stage - view.Anchor).sqrMagnitude < 0.01f) return false;   // < 0.1 stage px — noise

            var so = new SerializedObject(view);
            so.FindProperty("anchor").vector2Value = stage;
            so.ApplyModifiedPropertiesWithoutUndo();
            anchorT.localPosition = StageCoords.Stage(stage);
            Debug.Log($"[PanDulce] bear position captured from the scene → stage ({stage.x:F0}, {stage.y:F0})");
            return true;
        }

        /// <summary>
        /// Reads where the danger line actually sits in the scene and writes that height
        /// into Tuning.topOutLine — the value TopOutWatch ends runs with — so the line the
        /// designer sees is always the line the game enforces. A drag of the root, Content
        /// or the dashed sprite all fold to the same Y; root and Content then snap back to
        /// zero, because a leftover offset there is exactly the "line quietly lying about
        /// the lose height" bug this project has now hit twice.
        /// </summary>
        static bool CaptureDangerLine(DangerLineView view)
        {
            Transform root = view.transform;
            Transform content = root.Find("Content");
            Transform lineT = content != null ? content.Find("DangerLine") : null;
            if (lineT == null) return false;

            var tuning = Object.FindAnyObjectByType<GameRoot>(FindObjectsInactive.Include)?.Tuning;
            if (tuning == null) return false;

            float simY = -(root.localPosition.y + content.localPosition.y + lineT.localPosition.y)
                         / StageCoords.PX;
            // Keep the line inside the box: above the floor with room for one mochi, below
            // the mouth — a stray drag into the sky must not create an unreachable line.
            float clamped = Mathf.Clamp(simY, 10f, tuning.FloorY - 30f);

            bool offsetStray = root.localPosition.sqrMagnitude > 1e-10f
                            || content.localPosition.sqrMagnitude > 1e-10f;
            if (Mathf.Abs(clamped - tuning.TopOutLine) < 0.1f && !offsetStray) return false;

            if (Mathf.Abs(clamped - tuning.TopOutLine) >= 0.1f)
            {
                var so = new SerializedObject(tuning);
                so.FindProperty("data.topOutLine").floatValue = clamped;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(tuning);
                Debug.Log($"[PanDulce] danger line captured from the scene → topOutLine {clamped:F0}"
                          + (Mathf.Abs(clamped - simY) > 0.1f ? $" (clamped from {simY:F0})" : ""));
            }

            root.localPosition = Vector3.zero;
            content.localPosition = Vector3.zero;
            lineT.localPosition = new Vector3(lineT.localPosition.x,
                                              -clamped * StageCoords.PX, 0f);
            return true;
        }
    }
}
