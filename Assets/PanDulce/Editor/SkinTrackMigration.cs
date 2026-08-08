using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// One-shot restructure of Pastries.asset from the flat 11-tier chain (skins interleaved
    /// as tiers) to the v2 layout: 5 gameplay tiers × 3 tracks. Deterministic — it binds by
    /// baked filename, so re-running just rewrites the same layout. Play/Case sizes reset to
    /// 100% because the tier radii were respaced; press Studio ▸ Even out growth to retune.
    /// </summary>
    public static class SkinTrackMigration
    {
        [MenuItem("Pan Dulce/Migrate To Skin Tracks (5-tier)")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<PastryDatabase>(PastryChainGUI.AssetPath);
            if (db == null) { Debug.LogError("[PanDulce] no Pastries.asset — run Rebuild Stage first."); return; }

            var so = new SerializedObject(db);

            SetTrack(so.FindProperty("pastries"), so.FindProperty("names"),
                     new[] { "pastry_00_mochi", "pastry_03_purin", "pastry_10_melonpan",
                             "pastry_06_choco-donut", "pastry_07_rollcake" },
                     new[] { "Mochi", "Purin", "Melon Pan", "Choco Donut", "Roll Cake" });

            var ones = so.FindProperty("artScale"); ones.arraySize = 5;
            var cases = so.FindProperty("caseScale"); cases.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                ones.GetArrayElementAtIndex(i).floatValue = 1f;
                cases.GetArrayElementAtIndex(i).floatValue = 1f;
            }

            var pSkins = so.FindProperty("skins");
            pSkins.arraySize = 2;
            Skin(pSkins.GetArrayElementAtIndex(0), "Matcha",
                 new[] { "pastry_01_matcha-mochi", null, "pastry_09_honey-pan", "pastry_05_matcha-donut", null },
                 new[] { "Matcha Mochi", "", "Honey Pan", "Matcha Donut", "" });
            Skin(pSkins.GetArrayElementAtIndex(1), "Berry",
                 new[] { "pastry_02_mango-mochi", null, "pastry_08_sakura-pan", "pastry_04_berry-donut", null },
                 new[] { "Mango Mochi", "", "Sakura Pan", "Berry Donut", "" });

            so.FindProperty("activeSkin").intValue = 0;
            // Radii are geometric ×1.35 per tier, so at 100% Play sizes every merge grows +35%.
            so.FindProperty("mergeGrowth").floatValue = 0.35f;
            so.FindProperty("mergeGrowthTolerance").floatValue = 0.05f;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[PanDulce] Pastries.asset migrated: 5 tiers, tracks Original / Matcha / Berry.");
        }

        static void Skin(SerializedProperty track, string name, string[] files, string[] names)
        {
            track.FindPropertyRelative("trackName").stringValue = name;
            SetTrack(track.FindPropertyRelative("sprites"), track.FindPropertyRelative("names"),
                     files, names);
        }

        static void SetTrack(SerializedProperty sprites, SerializedProperty names,
                             string[] files, string[] displayNames)
        {
            sprites.arraySize = 5;
            names.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    files[i] == null ? null : Load(files[i]);
                names.GetArrayElementAtIndex(i).stringValue = displayNames[i];
            }
        }

        static Sprite Load(string file)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteImportSetup.PastryDir}/{file}.png");
            if (s == null) Debug.LogWarning($"[PanDulce] baked sprite missing: {file}.png — run the sprite bake.");
            return s;
        }
    }
}
