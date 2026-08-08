using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Window ▸ Pan Dulce ▸ Studio — the layout/art authoring panel, sibling to Designer
    /// (which owns gameplay tuning). Everything here works BEFORE play mode:
    ///
    ///   · Glass case seats — put any dessert in any of the 5 seats. Saved on the
    ///     DisplayCaseView in the scene; play mode uses the authored order too unless the
    ///     classic progress-following window is chosen.
    ///   · Dessert chain — reorder which dessert is tier 0..10 (sprite + name move together
    ///     in Pastries.asset, so this changes the real merge order) and set a per-dessert
    ///     size percentage, which scales the sprite and the physics circle together.
    ///   · Scene preview — the REAL generated bear shown at its serialized anchor (drag it;
    ///     the spot is captured on save/play), plus a resting-pile stand-in that is never
    ///     saved.
    ///   · Selected object — uniform scale slider for whatever is picked in the Hierarchy.
    /// </summary>
    public sealed class StudioWindow : EditorWindow
    {
        const string PastriesPath = "Assets/PanDulce/Config/Pastries.asset";
        const string TuningPath = "Assets/PanDulce/Config/Tuning.asset";
        const string StageName = "[ 20 · STAGE ]";
        const string PreviewName = "[ STUDIO PREVIEW ]";
        const string PileKey = "PanDulce.Studio.ShowPile";

        /// <summary>Per-dessert size range, in percent of the authored size.</summary>
        const float MinSizePct = 25f, MaxSizePct = 400f;

        [MenuItem("Window/Pan Dulce/Studio")]
        public static void Open()
        {
            var w = GetWindow<StudioWindow>("Pan Dulce · Studio");
            w.minSize = new Vector2(380f, 520f);
            w.Show();
        }

        PastryDatabase db;
        TuningConfig tuning;
        Vector2 scroll;
        bool showPile;
        GameObject previewRoot;
        bool building;   // guards hierarchyChanged re-entry while we create preview objects

        bool foldSeats = true, foldChain = true, foldPreview = true, foldScale = true;

        // ------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            showPile = SessionState.GetBool(PileKey, false);

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            Selection.selectionChanged += Repaint;
            Undo.undoRedoPerformed += RefreshAll;

            // The scene may still be loading when the window deserializes — apply late.
            EditorApplication.delayCall += RefreshAll;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            Selection.selectionChanged -= Repaint;
            Undo.undoRedoPerformed -= RefreshAll;
            DestroyPreview();
        }

        void OnHierarchyChanged()
        {
            if (building || Application.isPlaying) return;
            // Views rebuild their DontSave children on enable/domain reload, wiping the seat
            // preview — and a scene reload orphans our preview root. Re-establish both.
            ApplySeats();
            if (showPile && FindPreviewRoot() == null) RebuildPreview();
        }

        void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingEditMode) DestroyPreview();
            else if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += RefreshAll;
        }

        // DontSave objects under saved parents make Unity rewrite the scene file ("Fixing!"),
        // so previews vanish for the save and come right back — same trick as GeneratedView.
        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => DestroyPreview();
        void OnSceneSaved(UnityEngine.SceneManagement.Scene scene) { if (!Application.isPlaying) RebuildPreview(); }

        void RefreshAll()
        {
            if (Application.isPlaying) return;
            ApplySeats();
            RebuildPreview();
            Repaint();
        }

        // ------------------------------------------------------------ assets

        PastryDatabase Db => db != null ? db : db = AssetDatabase.LoadAssetAtPath<PastryDatabase>(PastriesPath);
        TuningConfig Tuning => tuning != null ? tuning : tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);

        // ------------------------------------------------------------ gui

        void OnGUI()
        {
            if (Db == null)
            {
                EditorGUILayout.HelpBox("No Pastries.asset found. Run Pan Dulce ▸ Rebuild Stage first.",
                                        MessageType.Warning);
                if (GUILayout.Button("Rebuild Stage")) StageBuilder.Rebuild();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foldSeats = Foldout(foldSeats, "Glass case seats");
            if (foldSeats) SeatsSection();

            foldChain = Foldout(foldChain, "Dessert chain — order & size");
            if (foldChain) ChainSection();

            foldPreview = Foldout(foldPreview, "Scene preview (edit mode)");
            if (foldPreview) PreviewSection();

            foldScale = Foldout(foldScale, "Selected object scale");
            if (foldScale) ScaleSection();

            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Stage")) StageBuilder.Rebuild();
                if (GUILayout.Button("Open Designer")) DesignerWindow.Open();
            }

            EditorGUILayout.EndScrollView();

            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        static bool Foldout(bool state, string title)
        {
            EditorGUILayout.Space(4f);
            bool s = EditorGUILayout.BeginFoldoutHeaderGroup(state, title);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return s;
        }

        static void Help(string text)
            => EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);

        // ------------------------------------------------------------ 1 · seats

        void SeatsSection()
        {
            var view = FindAnyObjectByType<DisplayCaseView>(FindObjectsInactive.Include);
            if (view == null) { Help("No display case in the open scene."); return; }

            Help("Choose which dessert sits in each of the 5 seats. This is saved on the " +
                 "DisplayCaseView in the scene, so play mode shows the same order (desserts " +
                 "the player has not discovered yet still play as silhouettes). Untick the " +
                 "box below to use the classic window that follows the player's progress.");

            var so = new SerializedObject(view);
            var pFollow = so.FindProperty("followProgress");
            var pSeats = so.FindProperty("seatTiers");
            if (pSeats.arraySize != DisplayCaseView.Slots) pSeats.arraySize = DisplayCaseView.Slots;

            var options = new GUIContent[TierTable.Count];
            for (int i = 0; i < TierTable.Count; i++)
                options[i] = new GUIContent($"{i} · {Db.Name(i)}");

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

            if (so.ApplyModifiedProperties()) ApplySeats();
        }

        void ApplySeats()
        {
            if (Application.isPlaying) return;
            var view = FindAnyObjectByType<DisplayCaseView>(FindObjectsInactive.Include);
            if (view != null) view.EditorPreviewSeats(view.SeatTiers);
            SceneView.RepaintAll();
        }

        // ------------------------------------------------------------ 2 · chain

        void ChainSection()
        {
            Help("The merge chain, top = tier 0 (smallest). ▲▼ move a dessert to another tier " +
                 "— sprite, name and size travel together, and gameplay follows this order. " +
                 "Size is a percentage of the dessert's authored size and moves the art AND " +
                 "the physics circle, so an enlarged dessert also takes up more room in the " +
                 "pile. It stacks on Designer ▸ Pile size, which scales all 11 at once.");

            var so = new SerializedObject(Db);
            var pSprites = so.FindProperty("pastries");
            var pNames = so.FindProperty("names");
            var pScales = so.FindProperty("artScale");
            if (pSprites.arraySize < TierTable.Count || pNames.arraySize < TierTable.Count
                || pScales.arraySize < TierTable.Count)
            {
                EditorGUILayout.HelpBox("Pastries.asset is missing entries — run Pan Dulce ▸ Rebuild Stage.",
                                        MessageType.Warning);
                return;
            }

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

                        // Authored as a percentage — a designer thinks "make it 40% bigger",
                        // not "0.4 stage px of radius". Stored as the fraction the sim wants.
                        var scaleProp = pScales.GetArrayElementAtIndex(i);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            float pct = scaleProp.floatValue * 100f;
                            pct = GUILayout.HorizontalSlider(pct, MinSizePct, MaxSizePct);
                            pct = EditorGUILayout.FloatField(Mathf.Round(pct), GUILayout.Width(40f));
                            GUILayout.Label("%", GUILayout.Width(14f));
                            scaleProp.floatValue = Mathf.Clamp(pct, MinSizePct, MaxSizePct) * 0.01f;
                        }
                    }

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
                reordered = true;
            }

            if (so.ApplyModifiedProperties())
            {
                if (reordered)
                {
                    // Order is gameplay: rebuild every generated view so the whole stage
                    // (case, plaque, bubble) reflects the new chain immediately.
                    foreach (var v in FindObjectsByType<GeneratedView>(FindObjectsInactive.Include,
                                                                       FindObjectsSortMode.None))
                        v.Rebuild();
                }
                ApplySeats();
                RebuildPreview();
            }

            WarnIfChainShrinks();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All sizes → 100%"))
                {
                    for (int i = 0; i < TierTable.Count; i++)
                        pScales.GetArrayElementAtIndex(i).floatValue = 1f;
                    so.ApplyModifiedProperties();
                    ApplySeats();
                    RebuildPreview();
                }
                if (GUILayout.Button("Save asset"))
                    AssetDatabase.SaveAssets();
            }
            Help("Changes live in Pastries.asset (undo works). \"Save asset\" writes it to disk now.");
        }

        // ------------------------------------------------------------ 3 · preview

        void PreviewSection()
        {
            Help("The bear is the REAL customer standing at its saved anchor — move it here, " +
                 "in the Inspector, or drag it in the Scene view (captured on save/play). " +
                 "Play mode walks the bear to this exact spot. The pile is a stand-in and " +
                 "is never saved.");

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var customer = FindAnyObjectByType<CustomerView>(FindObjectsInactive.Include);
                if (customer != null)
                {
                    var so = new SerializedObject(customer);
                    var pShow = so.FindProperty("editorPreview");
                    var pAnchor = so.FindProperty("anchor");
                    pShow.boolValue = EditorGUILayout.ToggleLeft("Show customer (bear at the counter)",
                                                                 pShow.boolValue);
                    pAnchor.vector2Value = EditorGUILayout.Vector2Field(
                        new GUIContent("Customer position (stage px)"), pAnchor.vector2Value);
                    if (so.ApplyModifiedProperties())
                    {
                        customer.Rebuild();
                        SceneView.RepaintAll();
                    }
                }

                bool pile = EditorGUILayout.ToggleLeft("Show dessert pile (resting on the floor curve)",
                                                       showPile);
                if (pile != showPile)
                {
                    showPile = pile;
                    SessionState.SetBool(PileKey, showPile);
                    RebuildPreview();
                }
            }

            if (Application.isPlaying)
                Help("Play mode has the real customer and pile — previews pause until you exit.");
        }

        GameObject FindPreviewRoot()
        {
            if (previewRoot != null) return previewRoot;
            var stage = GameObject.Find(StageName);
            var t = stage != null ? stage.transform.Find(PreviewName) : null;
            return previewRoot = (t != null ? t.gameObject : null);
        }

        void DestroyPreview()
        {
            building = true;
            var root = FindPreviewRoot();
            if (root != null) DestroyImmediate(root);
            previewRoot = null;
            building = false;
        }

        void RebuildPreview()
        {
            if (Application.isPlaying) return;
            building = true;
            var stale = FindPreviewRoot();
            if (stale != null) DestroyImmediate(stale);
            previewRoot = null;

            var stage = GameObject.Find(StageName);
            if (showPile && stage != null && Db != null)
            {
                previewRoot = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
                previewRoot.transform.SetParent(stage.transform, false);
                BuildPile(previewRoot.transform);
            }
            SceneView.RepaintAll();
            building = false;
        }

        /// <summary>
        /// A merge must feel like a promotion, so every tier has to be physically bigger than
        /// the one below it. The base radii guarantee that on their own; a size % steep enough
        /// can undo it, and then merging two desserts hands back something smaller. Worth
        /// catching here, where the number is being typed.
        /// </summary>
        void WarnIfChainShrinks()
        {
            if (Db == null) return;
            float sizeScale = Tuning != null ? Tuning.SizeScale : 1.3f;

            string bad = "";
            float prev = TierTable.EffectiveRadius(0, sizeScale, Db.TierSize(0));
            for (int i = 1; i < TierTable.Count; i++)
            {
                float r = TierTable.EffectiveRadius(i, sizeScale, Db.TierSize(i));
                if (r <= prev)
                {
                    // The size % that would put this tier just past the one below it.
                    float need = prev / (TierTable.BaseRadius[i] * sizeScale) * 100f;
                    bad += $"\n· {Db.Name(i)} (tier {i}) is not bigger than {Db.Name(i - 1)} " +
                           $"— needs more than {Mathf.Ceil(need)}%.";
                }
                prev = Mathf.Max(prev, r);
            }

            if (bad.Length > 0)
                EditorGUILayout.HelpBox("Sizes now move the physics circle, and this chain " +
                                        "shrinks somewhere — a merge there hands back a " +
                                        "smaller dessert:" + bad, MessageType.Warning);
        }

        void BuildPile(Transform root)
        {
            var pile = new GameObject("Pile") { hideFlags = HideFlags.DontSave };
            pile.transform.SetParent(root, false);
            // The pile lives in sim coordinates, exactly like PastryViewPool's bodies.
            pile.transform.localPosition = StageCoords.Stage(StageCoords.PlayOriginX,
                                                             StageCoords.PlayOriginY);

            float sizeScale = Tuning != null ? Tuning.SizeScale : 1.3f;
            float sag = Tuning != null ? Tuning.FloorSag : 26f;
            float floorY = Tuning != null ? Tuning.FloorY : 250f;

            // Deterministic mid-game arrangement: a bottom row shoulder-to-shoulder around
            // the centre, and a second row nestled into its gaps.
            int[] bottom = { 2, 0, 3, 1, 4, 0 };
            int[] top = { 1, 2, 0, 1 };

            var r = new float[bottom.Length];
            float total = 0f;
            for (int i = 0; i < bottom.Length; i++)
            {
                // Size % is in the radius, so the preview packs exactly like the real pile —
                // that is what makes it a fitting check and not just a picture.
                r[i] = TierTable.EffectiveRadius(bottom[i], sizeScale, Db.TierSize(bottom[i]));
                total += 2f * r[i];
            }

            var x = new float[bottom.Length];
            var y = new float[bottom.Length];
            float cursor = SimField.CX - total * 0.5f;
            for (int i = 0; i < bottom.Length; i++)
            {
                cursor += r[i];
                x[i] = Mathf.Clamp(cursor, SimField.WL + r[i], SimField.WR - r[i]);
                y[i] = SimField.FloorAt(x[i], sag, floorY) - r[i];
                Spawn(pile.transform, bottom[i], x[i], y[i], sizeScale, 30 + i);
                cursor += r[i];
            }

            for (int i = 0; i < top.Length && i + 1 < bottom.Length; i++)
            {
                float tr = TierTable.EffectiveRadius(top[i], sizeScale, Db.TierSize(top[i]));
                float tx = (x[i] + x[i + 1]) * 0.5f;
                float ty = Mathf.Min(y[i], y[i + 1]) - tr * 1.35f;
                Spawn(pile.transform, top[i], tx, ty, sizeScale, 40 + i);
            }
        }

        void Spawn(Transform parent, int tier, float simX, float simY, float sizeScale, int order)
        {
            var go = new GameObject($"Pastry_t{tier}") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = Db.Pastry(tier);
            sr.sortingLayerName = "PlayArea";
            sr.sortingOrder = order;
            go.transform.localPosition = new Vector3(simX * StageCoords.PX, -simY * StageCoords.PX, 0f);
            ViewFactory.SetIcon(sr, TierTable.EffectiveRadius(tier, sizeScale, Db.TierSize(tier)));
        }

        // ------------------------------------------------------------ 4 · scale

        void ScaleSection()
        {
            var t = Selection.activeTransform;
            if (t == null)
            {
                Help("Pick any object in the Hierarchy (or with the quick buttons below) and " +
                     "scale it with a slider or an exact number.");
            }
            else
            {
                EditorGUILayout.LabelField("Selected", t.name, EditorStyles.boldLabel);
                if (IsGenerated(t))
                    EditorGUILayout.HelpBox("Generated object. Moves/scales of a view's Content " +
                                            "node (and the bear) are folded into saved data on " +
                                            "save/play, so they stick. Leaves deeper down still " +
                                            "reset on rebuild — for desserts use the sizes above; " +
                                            "otherwise grab Content or a persistent folder.",
                                            MessageType.Info);
                if (!Mathf.Approximately(t.localScale.x, t.localScale.y))
                    Help($"Non-uniform scale ({t.localScale.x:F2}, {t.localScale.y:F2}) — the slider makes it uniform.");

                float cur = t.localScale.x;
                float next = EditorGUILayout.Slider("Scale", cur, 0.05f, 3f);
                next = EditorGUILayout.FloatField("Exact", next);
                if (!Mathf.Approximately(next, cur) && next > 0.0001f)
                {
                    Undo.RecordObject(t, "Studio scale " + t.name);
                    t.localScale = new Vector3(next, next, t.localScale.z);
                    EditorUtility.SetDirty(t);
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Quick select", EditorStyles.miniBoldLabel);
            QuickSelectButtons();
        }

        static bool IsGenerated(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if ((p.gameObject.hideFlags & HideFlags.DontSave) != 0) return true;
            return false;
        }

        void QuickSelectButtons()
        {
            var stage = GameObject.Find(StageName);
            if (stage == null) { Help("No stage in the open scene."); return; }

            // The hand-drawn layout pieces first — they are the usual scaling targets.
            var artFolder = stage.transform.Find("[ 22 · LAYOUT ART ]");
            var layout = artFolder != null && artFolder.childCount > 0 ? artFolder.GetChild(0) : null;
            if (layout != null) ButtonGrid(layout, layout.name);

            ButtonGrid(stage.transform, "Stage folders");
        }

        void ButtonGrid(Transform parent, string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == PreviewName) continue;
                if (col == 3) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); col = 0; }
                if (GUILayout.Button(child.name, EditorStyles.miniButton))
                {
                    Selection.activeGameObject = child.gameObject;
                    EditorGUIUtility.PingObject(child.gameObject);
                }
                col++;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
