using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// DisplayCaseView's Inspector = the Studio window's seats section, drawn by the shared
    /// CaseSeatsGUI — the seat popups and follow-progress toggle work identically here.
    /// </summary>
    [CustomEditor(typeof(DisplayCaseView))]
    sealed class DisplayCaseViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var view = (DisplayCaseView)target;
            var db = AssetDatabase.LoadAssetAtPath<PastryDatabase>(PastryChainGUI.AssetPath);
            if (db == null)
            {
                EditorGUILayout.HelpBox("No Pastries.asset found — run Pan Dulce ▸ Rebuild Stage.",
                                        MessageType.Warning);
                DrawDefaultInspector();
                return;
            }

            EditorGUILayout.LabelField("Glass case seats", EditorStyles.boldLabel);
            CaseSeatsGUI.Draw(view, db, null);

            EditorGUILayout.Space(8f);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "seatTiers", "followProgress");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
