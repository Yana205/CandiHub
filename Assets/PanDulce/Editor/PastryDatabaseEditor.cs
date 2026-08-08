using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Pastries.asset's Inspector = the Studio window's chain section, drawn by the shared
    /// PastryChainGUI — same reorder arrows, Play/Case sliders and merge-growth audit, so
    /// nothing is Studio-only. The non-chain art bindings follow as plain fields.
    /// </summary>
    [CustomEditor(typeof(PastryDatabase))]
    sealed class PastryDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (PastryDatabase)target;
            EditorGUILayout.LabelField("Dessert chain — order & size", EditorStyles.boldLabel);
            PastryChainGUI.Draw(serializedObject, db, null);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Other art", EditorStyles.boldLabel);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "pastries", "names",
                                    "artScale", "caseScale", "mergeGrowth", "mergeGrowthTolerance",
                                    "skins", "activeSkin");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
