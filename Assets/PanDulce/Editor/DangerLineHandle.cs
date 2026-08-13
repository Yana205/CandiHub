using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Scene-view authoring for the danger line, in two layers:
    ///
    ///  · A fat slider handle at the REAL lose height (Tuning.topOutLine), shown whenever
    ///    the DangerLine view or any of its children is selected. Dragging it writes the
    ///    value straight through — the dashes are a 2.5 px hairline nobody can grab.
    ///  · A LIVE fold: any raw transform drag of the root, Content or the sprite becomes
    ///    a topOutLine edit on the spot, and the transforms snap back to canonical in the
    ///    same event. Designers kept dragging the sprite itself (fair — it looks like the
    ///    thing to move) and then saw a second line "stuck" at the saved height until the
    ///    save-time fold ran. Live folding means there is only ever ONE line and it
    ///    follows the mouse; SceneAuthoringFold remains as the save-time backstop.
    ///
    /// Note: Rebuild Stage recreates the sprite at the design height (82), which the live
    /// fold then adopts — a rebuild deliberately resets the line to its default.
    ///
    /// Domain reload is OFF in this project: the static ctor subscription is the same
    /// pattern SceneAuthoringFold uses.
    /// </summary>
    [InitializeOnLoad]
    static class DangerLineHandle
    {
        static DangerLineHandle()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        static void OnSceneGui(SceneView sv)
        {
            if (Application.isPlaying) return;

            var view = Object.FindAnyObjectByType<DangerLineView>(FindObjectsInactive.Include);
            if (view == null) return;
            var root = Object.FindAnyObjectByType<GameRoot>(FindObjectsInactive.Include);
            var tuning = root != null ? root.Tuning : null;
            if (tuning == null) return;
            Transform content = view.transform.Find("Content");
            Transform lineT = content != null ? content.Find("DangerLine") : null;
            if (lineT == null) return;

            LiveFold(view, tuning, content, lineT);

            var sel = Selection.activeTransform;
            if (sel == null || sel.GetComponentInParent<DangerLineView>(true) != view) return;

            float y = tuning.TopOutLine;
            Vector3 left = view.transform.TransformPoint(
                new Vector3(tuning.WallLeft * StageCoords.PX, -y * StageCoords.PX, 0f));
            Vector3 right = view.transform.TransformPoint(
                new Vector3(tuning.WallRight * StageCoords.PX, -y * StageCoords.PX, 0f));
            Vector3 mid = (left + right) * 0.5f;

            Handles.color = Palette.Danger;
            Handles.DrawLine(left, right, 3f);
            Handles.Label(left + (left - right).normalized * 0.12f,
                          $"lose height · {y:F0}\n(drag the cube)");

            EditorGUI.BeginChangeCheck();
            float size = HandleUtility.GetHandleSize(mid) * 0.18f;
            Vector3 moved = Handles.Slider(mid, view.transform.up, size,
                                           Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return;

            float simY = -view.transform.InverseTransformPoint(moved).y / StageCoords.PX;
            Apply(tuning, simY, view, content, lineT);
        }

        /// <summary>
        /// Reads the line's effective height out of whatever transforms a drag touched,
        /// adopts it, and restores the canonical layout (root and Content at zero, the
        /// sprite alone carrying the height). Runs every scene-GUI event, so a drag of
        /// the sprite IS a drag of the lose height — nothing is left over to lie.
        /// </summary>
        static void LiveFold(DangerLineView view, TuningConfig tuning,
                             Transform content, Transform lineT)
        {
            float cx = (tuning.WallLeft + tuning.WallRight) * 0.5f * StageCoords.PX;
            float childY = -tuning.TopOutLine * StageCoords.PX;
            bool strayed = view.transform.localPosition.sqrMagnitude > 1e-8f
                        || content.localPosition.sqrMagnitude > 1e-8f
                        || Mathf.Abs(lineT.localPosition.x - cx) > 1e-4f
                        || Mathf.Abs(lineT.localPosition.y - childY) > 1e-4f;
            if (!strayed) return;

            float simY = -(view.transform.localPosition.y + content.localPosition.y
                           + lineT.localPosition.y) / StageCoords.PX;
            // Sub-2px deviation is authoring noise (Build centers the rect half its height
            // below Sync's placement), not a drag — snap back without touching the tuning.
            if (Mathf.Abs(simY - tuning.TopOutLine) < 2f) simY = tuning.TopOutLine;
            Apply(tuning, simY, view, content, lineT);
        }

        static void Apply(TuningConfig tuning, float simY, DangerLineView view,
                          Transform content, Transform lineT)
        {
            // Same rails as the save-time fold: inside the box, above the floor.
            float clamped = Mathf.Clamp(simY, 10f, tuning.FloorY - 30f);
            if (Mathf.Abs(clamped - tuning.TopOutLine) > 0.05f)
            {
                Undo.RecordObject(tuning, "Move danger line");
                tuning.Data.topOutLine = clamped;
                EditorUtility.SetDirty(tuning);
            }

            Undo.RecordObject(view.transform, "Move danger line");
            Undo.RecordObject(content, "Move danger line");
            Undo.RecordObject(lineT, "Move danger line");
            view.transform.localPosition = Vector3.zero;
            content.localPosition = Vector3.zero;
            lineT.localPosition = new Vector3(
                (tuning.WallLeft + tuning.WallRight) * 0.5f * StageCoords.PX,
                -clamped * StageCoords.PX, 0f);
        }
    }
}
