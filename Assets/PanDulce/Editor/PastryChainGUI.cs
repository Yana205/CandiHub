using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// The dessert-chain editing GUI — shared verbatim between the Studio window and
    /// Pastries.asset's own Inspector, so the chain (order, Play/Case sizes, merge-growth
    /// audit) is editable from either place with identical behaviour. Callers hand in their
    /// SerializedObject and hear back through onChanged(reordered) for context-specific
    /// refreshes (the Studio window rebuilds its pile preview, the Inspector needs nothing).
    /// </summary>
    static class PastryChainGUI
    {
        public const string AssetPath = "Assets/PanDulce/Config/Pastries.asset";

        /// <summary>Per-dessert size range, in percent of the authored size.</summary>
        public const float MinSizePct = 25f, MaxSizePct = 400f;

        public static void Draw(SerializedObject so, PastryDatabase db, System.Action<bool> onChanged)
        {
            Help("The merge chain, top = tier 0 (smallest). ▲▼ move a dessert to another tier " +
                 "— sprite, name, sizes and every skin variant travel together, and gameplay " +
                 "follows this order. The two sizes are isolated: Play scales the pile art AND " +
                 "the physics circle (stacking on Designer ▸ Pile size); Case scales only the " +
                 "chrome icon — glass case, order bubble, next plaque, serve flight — and " +
                 "never touches gameplay. Skin columns: a sprite + name per tier; blank = " +
                 "shares the Original (Purin and Roll Cake stay blank on purpose).");

            var pSprites = so.FindProperty("pastries");
            var pNames = so.FindProperty("names");
            var pScales = so.FindProperty("artScale");
            var pCase = so.FindProperty("caseScale");
            var pSkins = so.FindProperty("skins");
            var pActive = so.FindProperty("activeSkin");
            if (pCase.arraySize != TierTable.Count)
            {
                pCase.arraySize = TierTable.Count;
                for (int i = 0; i < pCase.arraySize; i++)
                {
                    var e = pCase.GetArrayElementAtIndex(i);
                    if (e.floatValue <= 0f) e.floatValue = 1f;   // unset = 100%
                }
            }
            if (pSprites.arraySize < TierTable.Count || pNames.arraySize < TierTable.Count
                || pScales.arraySize < TierTable.Count)
            {
                EditorGUILayout.HelpBox("Pastries.asset is missing entries — run Pan Dulce ▸ Rebuild Stage.",
                                        MessageType.Warning);
                return;
            }

            SkinBar(so, db, pSkins, pActive, onChanged);
            TrackHeader(pSkins);

            bool skinEdited = false;
            int moveFrom = -1, moveTo = -1;
            for (int i = 0; i < TierTable.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i.ToString(), GUILayout.Width(18f));

                    var sprite = pSprites.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    var thumb = sprite != null ? AssetPreview.GetAssetPreview(sprite) : null;
                    GUILayout.Label(thumb, GUILayout.Width(30f), GUILayout.Height(30f));

                    using (new EditorGUILayout.VerticalScope())
                    {
                        var nameProp = pNames.GetArrayElementAtIndex(i);
                        nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

                        // Authored as percentages — a designer thinks "make it 40% bigger",
                        // not "0.4 stage px of radius". Stored as the fraction the code wants.
                        SizeRow(pScales.GetArrayElementAtIndex(i), "Play");
                        SizeRow(pCase.GetArrayElementAtIndex(i), "Case");
                        if (i > 0) GrowthLabel(pScales, db, i);
                    }

                    skinEdited |= SkinCells(pSkins, i);

                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("▲", GUILayout.Width(24f))) { moveFrom = i; moveTo = i - 1; }
                    using (new EditorGUI.DisabledScope(i == TierTable.Count - 1))
                        if (GUILayout.Button("▼", GUILayout.Width(24f))) { moveFrom = i; moveTo = i + 1; }
                }
            }

            bool reordered = false;
            if (moveFrom >= 0)
            {
                pSprites.MoveArrayElement(moveFrom, moveTo);
                pNames.MoveArrayElement(moveFrom, moveTo);
                pScales.MoveArrayElement(moveFrom, moveTo);
                pCase.MoveArrayElement(moveFrom, moveTo);
                for (int t = 0; t < pSkins.arraySize; t++)
                {
                    var track = pSkins.GetArrayElementAtIndex(t);
                    track.FindPropertyRelative("sprites").MoveArrayElement(moveFrom, moveTo);
                    track.FindPropertyRelative("names").MoveArrayElement(moveFrom, moveTo);
                }
                reordered = true;
            }

            // A skin-art edit rebuilds the generated views too, so the pile preview and the
            // glass case repaint with the sprite that was just dropped in.
            if (so.ApplyModifiedProperties()) Changed(reordered || skinEdited, onChanged);

            GrowthSection(so, pScales, db, onChanged);
            WarnIfChainShrinks(db);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All sizes → 100%"))
                {
                    for (int i = 0; i < TierTable.Count; i++)
                    {
                        pScales.GetArrayElementAtIndex(i).floatValue = 1f;
                        pCase.GetArrayElementAtIndex(i).floatValue = 1f;
                    }
                    so.ApplyModifiedProperties();
                    Changed(false, onChanged);
                }
                if (GUILayout.Button("Save asset"))
                    AssetDatabase.SaveAssets();
            }
            Help("Changes live in Pastries.asset (undo works). \"Save asset\" writes it to disk now.");
        }

        const float SkinCellW = 68f;

        /// <summary>
        /// The skin controls above the chain: which track the game draws right now, plus
        /// add/rename/delete for the tracks themselves. The preview is a real asset field
        /// (activeSkin), so Play mode, builds and the case all follow it — it is the skin
        /// switch, not an editor-only toggle.
        /// </summary>
        static void SkinBar(SerializedObject so, PastryDatabase db, SerializedProperty pSkins,
                            SerializedProperty pActive, System.Action<bool> onChanged)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var options = new string[db.SkinCount];
                for (int i = 0; i < options.Length; i++) options[i] = db.SkinName(i);
                int now = Mathf.Clamp(pActive.intValue, 0, options.Length - 1);
                int picked = EditorGUILayout.Popup("Skin", now, options);
                if (picked != now) pActive.intValue = picked;

                if (GUILayout.Button("+ track", GUILayout.Width(60f)))
                {
                    int t = pSkins.arraySize;
                    pSkins.InsertArrayElementAtIndex(t);
                    var track = pSkins.GetArrayElementAtIndex(t);
                    track.FindPropertyRelative("trackName").stringValue = $"Skin {t + 1}";
                    var spr = track.FindPropertyRelative("sprites");
                    var nam = track.FindPropertyRelative("names");
                    spr.arraySize = TierTable.Count;
                    nam.arraySize = TierTable.Count;
                    for (int i = 0; i < TierTable.Count; i++)
                    {
                        spr.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        nam.GetArrayElementAtIndex(i).stringValue = "";
                    }
                }
            }

            for (int t = 0; t < pSkins.arraySize; t++)
            {
                var track = pSkins.GetArrayElementAtIndex(t);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18f);
                    var pName = track.FindPropertyRelative("trackName");
                    pName.stringValue = EditorGUILayout.TextField($"Track {t + 1}", pName.stringValue);
                    if (GUILayout.Button("✕", GUILayout.Width(22f))
                        && EditorUtility.DisplayDialog("Delete skin track",
                               $"Delete \"{pName.stringValue}\" and its sprite assignments? " +
                               "The sprite files themselves are untouched.", "Delete", "Cancel"))
                    {
                        pSkins.DeleteArrayElementAtIndex(t);
                        pActive.intValue = Mathf.Clamp(pActive.intValue, 0, pSkins.arraySize);
                        break;
                    }
                }
            }
        }

        /// <summary>Column labels so the per-row skin cells read as tracks.</summary>
        static void TrackHeader(SerializedProperty pSkins)
        {
            if (pSkins.arraySize == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int t = 0; t < pSkins.arraySize; t++)
                {
                    var name = pSkins.GetArrayElementAtIndex(t).FindPropertyRelative("trackName").stringValue;
                    GUILayout.Label(name, EditorStyles.miniBoldLabel, GUILayout.Width(SkinCellW));
                }
                GUILayout.Space(52f);   // over the ▲▼ column
            }
        }

        /// <summary>
        /// One row's skin cells: thumbnail, sprite slot, name override per track. Returns
        /// true when any cell changed so the caller can rebuild previews.
        /// </summary>
        static bool SkinCells(SerializedProperty pSkins, int tier)
        {
            bool changed = false;
            for (int t = 0; t < pSkins.arraySize; t++)
            {
                var track = pSkins.GetArrayElementAtIndex(t);
                var spr = track.FindPropertyRelative("sprites");
                var nam = track.FindPropertyRelative("names");
                if (spr.arraySize != TierTable.Count) spr.arraySize = TierTable.Count;
                if (nam.arraySize != TierTable.Count) nam.arraySize = TierTable.Count;

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(SkinCellW)))
                {
                    var pSprite = spr.GetArrayElementAtIndex(tier);
                    var sprite = pSprite.objectReferenceValue as Sprite;
                    var thumb = sprite != null ? AssetPreview.GetAssetPreview(sprite) : null;
                    GUILayout.Label(thumb, GUILayout.Width(30f), GUILayout.Height(30f));

                    EditorGUI.BeginChangeCheck();
                    pSprite.objectReferenceValue = EditorGUILayout.ObjectField(
                        GUIContent.none, sprite, typeof(Sprite), false, GUILayout.Width(SkinCellW));
                    var pName = nam.GetArrayElementAtIndex(tier);
                    pName.stringValue = EditorGUILayout.TextField(pName.stringValue,
                                                                  GUILayout.Width(SkinCellW));
                    changed |= EditorGUI.EndChangeCheck();
                }
            }
            return changed;
        }

        /// <summary>
        /// Everything a chain edit must refresh regardless of which editor hosted it:
        /// reorder rebuilds every generated view (order is gameplay), and the glass-case
        /// edit preview repaints so the scene mirrors the asset immediately.
        /// </summary>
        static void Changed(bool reordered, System.Action<bool> onChanged)
        {
            if (!Application.isPlaying)
            {
                if (reordered)
                    foreach (var v in Object.FindObjectsByType<GeneratedView>(FindObjectsInactive.Include))
                        v.Rebuild();
                var view = Object.FindAnyObjectByType<DisplayCaseView>(FindObjectsInactive.Include);
                if (view != null) view.EditorPreviewSeats(view.SeatTiers);
                SceneView.RepaintAll();
            }
            onChanged?.Invoke(reordered);
        }

        /// <summary>One labelled percentage slider row — Play (art+physics) or Case (chrome).</summary>
        static void SizeRow(SerializedProperty p, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(30f));
                float pct = p.floatValue * 100f;
                pct = GUILayout.HorizontalSlider(pct, MinSizePct, MaxSizePct);
                pct = EditorGUILayout.FloatField(Mathf.Round(pct), GUILayout.Width(40f));
                GUILayout.Label("%", GUILayout.Width(14f));
                p.floatValue = Mathf.Clamp(pct, MinSizePct, MaxSizePct) * 0.01f;
            }
        }

        /// <summary>
        /// Per-row readout of the merge INTO this tier: red = shrinks (a merge would hand
        /// back a smaller dessert), orange = outside the authored target band, green = in
        /// band. Reads the live slider values, so it updates while you drag.
        /// </summary>
        static void GrowthLabel(SerializedProperty pScales, PastryDatabase db, int i)
        {
            float prev = TierTable.BaseRadius[i - 1] * pScales.GetArrayElementAtIndex(i - 1).floatValue;
            float cur = TierTable.BaseRadius[i] * pScales.GetArrayElementAtIndex(i).floatValue;
            float g = cur / prev - 1f;
            float tgt = db.MergeGrowthTarget, tol = db.MergeGrowthTolerance;

            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = g <= 0f ? new Color(0.95f, 0.4f, 0.35f)
                                  : Mathf.Abs(g - tgt) > tol ? new Color(0.95f, 0.65f, 0.25f)
                                  : new Color(0.5f, 0.78f, 0.5f);
            GUILayout.Label($"merge: {(g >= 0f ? "+" : "")}{g * 100f:F0}%  " +
                            $"(target +{tgt * 100f:F0} ± {tol * 100f:F0})", style);
        }

        /// <summary>
        /// The single consistency knob: one growth percentage for every merge in the chain,
        /// audited live and enforceable — "Even out" rewrites the Play sizes so each step
        /// lands exactly on target, anchored at tier 0's current size. Tested per asset by
        /// MergeGrowthTests.
        /// </summary>
        static void GrowthSection(SerializedObject so, SerializedProperty pScales,
                                  PastryDatabase db, System.Action<bool> onChanged)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Merge growth", EditorStyles.miniBoldLabel);
            Help("One percentage rules every merge: each result should be this much bigger " +
                 "than its parent. The labels above audit each step against target ± " +
                 "tolerance; \"Even out\" rewrites all Play sizes to hit the target exactly " +
                 "(tier 0 keeps its size as the anchor). Saved in Pastries.asset and " +
                 "enforced by the MergeGrowthTests EditMode suite.");

            var pTarget = so.FindProperty("mergeGrowth");
            var pTol = so.FindProperty("mergeGrowthTolerance");
            // The 5-tier chain runs ~+35% per merge at 100% Play sizes; leave slider room
            // above that for experiments.
            pTarget.floatValue = EditorGUILayout.Slider("Target growth per merge %",
                                                        pTarget.floatValue * 100f, 5f, 100f) * 0.01f;
            pTol.floatValue = EditorGUILayout.Slider("Tolerance ±%",
                                                     pTol.floatValue * 100f, 0f, 20f) * 0.01f;
            so.ApplyModifiedProperties();

            if (GUILayout.Button("Even out growth → rewrite Play sizes"))
            {
                float g = 1f + pTarget.floatValue;
                float r = TierTable.BaseRadius[0] * pScales.GetArrayElementAtIndex(0).floatValue;
                string clamped = "";
                for (int i = 1; i < TierTable.Count; i++)
                {
                    r *= g;
                    float want = r / TierTable.BaseRadius[i];
                    float set = Mathf.Clamp(want, MinSizePct * 0.01f, MaxSizePct * 0.01f);
                    if (!Mathf.Approximately(want, set))
                        clamped += $"\n· {db.Name(i)} wanted {want * 100f:F0}% but sizes clamp at " +
                                   $"{(want > set ? MaxSizePct : MinSizePct):F0}%";
                    pScales.GetArrayElementAtIndex(i).floatValue = set;
                    r = TierTable.BaseRadius[i] * set;   // keep later steps honest after a clamp
                }
                so.ApplyModifiedProperties();
                Changed(false, onChanged);
                if (clamped.Length > 0)
                    Debug.LogWarning("Even out growth hit the size clamps — steps after these " +
                                     "stay on target but the chain's total span changed:" + clamped);
            }
        }

        /// <summary>
        /// A merge must feel like a promotion, so every tier has to be physically bigger than
        /// the one below it. The base radii guarantee that on their own; a size % steep enough
        /// can undo it, and then merging two desserts hands back something smaller. Worth
        /// catching here, where the number is being typed. (SizeScale multiplies every tier
        /// equally, so it cancels out of the comparison and is left out.)
        /// </summary>
        static void WarnIfChainShrinks(PastryDatabase db)
        {
            if (db == null) return;

            string bad = "";
            float prev = TierTable.EffectiveRadius(0, 1f, db.TierSize(0));
            for (int i = 1; i < TierTable.Count; i++)
            {
                float r = TierTable.EffectiveRadius(i, 1f, db.TierSize(i));
                if (r <= prev)
                {
                    // The size % that would put this tier just past the one below it.
                    float need = prev / TierTable.BaseRadius[i] * 100f;
                    bad += $"\n· {db.Name(i)} (tier {i}) is not bigger than {db.Name(i - 1)} " +
                           $"— needs more than {Mathf.Ceil(need)}%.";
                }
                prev = Mathf.Max(prev, r);
            }

            if (bad.Length > 0)
                EditorGUILayout.HelpBox("Sizes now move the physics circle, and this chain " +
                                        "shrinks somewhere — a merge there hands back a " +
                                        "smaller dessert:" + bad, MessageType.Warning);
        }

        static void Help(string text)
            => EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
    }
}
