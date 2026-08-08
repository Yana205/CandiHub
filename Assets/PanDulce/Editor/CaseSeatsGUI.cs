using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// The glass-case seat GUI — shared between the Studio window and DisplayCaseView's
    /// own Inspector, so seats are editable from either place with identical behaviour.
    /// </summary>
    static class CaseSeatsGUI
    {
        public static void Draw(DisplayCaseView view, PastryDatabase db, System.Action onChanged)
        {
            EditorGUILayout.LabelField(
                "Choose which dessert sits in each of the 5 seats. This is saved on the " +
                "DisplayCaseView in the scene, so play mode shows the same order (desserts " +
                "the player has not discovered yet still play as silhouettes). Untick the " +
                "box below to use the classic window that follows the player's progress.",
                EditorStyles.wordWrappedMiniLabel);

            var so = new SerializedObject(view);
            var pFollow = so.FindProperty("followProgress");
            var pSeats = so.FindProperty("seatTiers");
            if (pSeats.arraySize != DisplayCaseView.Slots) pSeats.arraySize = DisplayCaseView.Slots;

            var options = new GUIContent[TierTable.Count];
            for (int i = 0; i < TierTable.Count; i++)
                options[i] = new GUIContent($"{i} · {db.Name(i)}");

            for (int seat = 0; seat < DisplayCaseView.Slots; seat++)
            {
                var p = pSeats.GetArrayElementAtIndex(seat);
                p.intValue = EditorGUILayout.Popup(new GUIContent($"Seat {seat + 1}"),
                                                   Mathf.Clamp(p.intValue, 0, TierTable.Max), options);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to 1–5"))
                    for (int i = 0; i < DisplayCaseView.Slots; i++)
                        pSeats.GetArrayElementAtIndex(i).intValue = i;
                if (GUILayout.Button("Top 5"))
                    for (int i = 0; i < DisplayCaseView.Slots; i++)
                        pSeats.GetArrayElementAtIndex(i).intValue = TierTable.Count - DisplayCaseView.Slots + i;
            }

            pFollow.boolValue = !EditorGUILayout.ToggleLeft(
                "Use this order during play (off = follow the player's progress)",
                !pFollow.boolValue);

            if (so.ApplyModifiedProperties())
            {
                if (!Application.isPlaying)
                {
                    view.EditorPreviewSeats(view.SeatTiers);
                    SceneView.RepaintAll();
                }
                onChanged?.Invoke();
            }
        }
    }
}
