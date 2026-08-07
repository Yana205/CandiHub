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
    ///   · Glass case seats — put any dessert in any of the 5 seats as an edit preview.
    ///   · Dessert chain — reorder which dessert is tier 0..10 (sprite + name move together
    ///     in Pastries.asset, so this changes the real merge order) and set a per-dessert
    ///     visual size. Physics radii never change.
    ///   · Scene preview — a bear at the counter and a resting pile, as DontSave objects
    ///     that are destroyed before any scene save or play-mode entry.
    ///   · Selected object — uniform scale slider for whatever is picked in the Hierarchy.
    /// </summary>
    public sealed class StudioWindow : EditorWindow
    {
        const string PastriesPath = "Assets/PanDulce/Config/Pastries.asset";
        const string TuningPath = "Assets/PanDulce/Config/Tuning.asset";
        const string StageName = "[ 20 · STAGE ]";
        const string PreviewName = "[ STUDIO PREVIEW ]";
        const string SeatsKey = "PanDulce.Studio.Seats";
        const string BearKey = "PanDulce.Studio.ShowBear";
        const string PileKey = "PanDulce.Studio.ShowPile";

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
        int[] seats;
        bool showBear, showPile;
        GameObject previewRoot;
        bool building;   // guards hierarchyChanged re-entry while we create preview objects

        bool foldSeats = true, foldChain = true, foldPreview = true, foldScale = true;

        // ------------------------------------------------------------ lifecycle

        void OnEnable()
        {
            seats = SessionState.GetIntArray(SeatsKey, new[] { 0, 1, 2, 3, 4 });
            if (seats.Length != DisplayCaseView.Slots) seats = new[] { 0, 1, 2, 3, 4 };
            showBear = SessionState.GetBool(BearKey, false);
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
            if ((showBear || showPile) && FindPreviewRoot() == null) RebuildPreview();
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
            Help("Preview any dessert in any seat of the glass case. Edit-mode only — during " +
                 "play the case follows the player's progress as always.");

            var options = new GUIContent[TierTable.Count];
            for (int i = 0; i < TierTable.Count; i++)
                options[i] = new GUIContent($"{i} · {Db.Name(i)}");

            bool changed = false;
            for (int seat = 0; seat < seats.Length; seat++)
            {
                int next = EditorGUILayout.Popup(new GUIContent($"Seat {seat + 1}"),
                                                 Mathf.Clamp(seats[seat], 0, TierTable.Max), options);
                if (next != seats[seat]) { seats[seat] = next; changed = true; }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to 1–5"))
                {
                    seats = new[] { 0, 1, 2, 3, 4 };
                    changed = true;
                }
                if (GUILayout.Button("Top 5"))
                {
                    seats = new[] { 6, 7, 8, 9, 10 };
                    changed = true;
                }
            }

            if (changed)
            {
                SessionState.SetIntArray(SeatsKey, seats);
                ApplySeats();
            }
        }

        void ApplySeats()
        {
            if (Application.isPlaying) return;
            var view = FindAnyObjectByType<DisplayCaseView>(FindObjectsInactive.Include);
            if (view != null) view.EditorPreviewSeats(seats);
            SceneView.RepaintAll();
        }

        // ------------------------------------------------------------ 2 · chain

        void ChainSection()
        {
            Help("The merge chain, top = tier 0 (smallest). ▲▼ move a dessert to another tier " +
                 "— sprite, name and size travel together, and gameplay follows this order. " +
                 "Size scales the art only; the physics circle stays the tier's radius.");

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

                        var scaleProp = pScales.GetArrayElementAtIndex(i);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            float v = GUILayout.HorizontalSlider(scaleProp.floatValue, 0.5f, 2f);
                            v = EditorGUILayout.FloatField(v, GUILayout.Width(44f));
                            scaleProp.floatValue = Mathf.Clamp(v, 0.1f, 4f);
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All sizes → 1.0"))
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
            Help("Stand-ins so you can judge the layout without pressing Play. They are never " +
                 "saved into the scene and are removed automatically before play mode / saving.");

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                bool bear = EditorGUILayout.ToggleLeft("Show customer (bear at the counter)", showBear);
                bool pile = EditorGUILayout.ToggleLeft("Show dessert pile (resting on the floor curve)", showPile);
                if (bear != showBear || pile != showPile)
                {
                    showBear = bear; showPile = pile;
                    SessionState.SetBool(BearKey, showBear);
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
            if ((showBear || showPile) && stage != null && Db != null)
            {
                previewRoot = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
                previewRoot.transform.SetParent(stage.transform, false);
                if (showBear) BuildBear(previewRoot.transform);
                if (showPile) BuildPile(previewRoot.transform);
            }
            SceneView.RepaintAll();
            building = false;
        }

        void BuildBear(Transform root)
        {
            var anchor = new GameObject("Bear") { hideFlags = HideFlags.DontSave };
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = StageCoords.Stage(CustomerView.AnchorX, CustomerView.AnchorY);

            var go = new GameObject("Sprite") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(anchor.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = Db.Customer(0);
            sr.sortingLayerName = "Customer";
            // Mirrors CustomerView: baked at 2x, PPU 100 → scale 0.5, lifted half the
            // rendered height so the anchor is bottom-centre.
            go.transform.localScale = Vector3.one * 0.5f;
            go.transform.localPosition = new Vector3(0f, 100f * StageCoords.PX, 0f);
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
                r[i] = TierTable.EffectiveRadius(bottom[i], sizeScale);
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
                float tr = TierTable.EffectiveRadius(top[i], sizeScale);
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
            ViewFactory.SetIcon(sr, TierTable.EffectiveRadius(tier, sizeScale), Db.ArtScale(tier));
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
                    EditorGUILayout.HelpBox("Generated object — its size resets on rebuild/play. " +
                                            "Use it for eyeballing; make lasting changes via the " +
                                            "dessert sizes above or on persistent objects (LayoutArt " +
                                            "pieces, folders).", MessageType.Info);
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
