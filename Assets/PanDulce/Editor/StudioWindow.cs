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

            // Shared with DisplayCaseView's Inspector; the window adds its pile preview,
            // which mirrors the seat order and so must follow edits.
            CaseSeatsGUI.Draw(view, Db, RebuildPreview);
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
            // Shared with Pastries.asset's Inspector; the window adds its pile preview,
            // which packs by Play sizes and so must follow edits.
            PastryChainGUI.Draw(new SerializedObject(Db), Db, _ => RebuildPreview());
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
            // the centre, and a second row nestled into its gaps. Values are SEAT indices,
            // mapped through the glass case's authored order so the pile only ever shows
            // desserts that are actually seated.
            int[] bottom = { 2, 0, 3, 1, 4, 0 };
            int[] top = { 1, 2, 0, 1 };

            var caseView = FindAnyObjectByType<DisplayCaseView>(FindObjectsInactive.Include);
            var seats = caseView != null ? caseView.SeatTiers : null;
            for (int i = 0; i < bottom.Length; i++) bottom[i] = SeatTier(seats, bottom[i]);
            for (int i = 0; i < top.Length; i++) top[i] = SeatTier(seats, top[i]);

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
                x[i] = Mathf.Clamp(cursor,
                                   (Tuning != null ? Tuning.WallLeft : SimField.WL) + r[i],
                                   (Tuning != null ? Tuning.WallRight : SimField.WR) - r[i]);
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

        static int SeatTier(int[] seats, int seat)
            => seats != null && seat < seats.Length
                ? Mathf.Clamp(seats[seat], 0, TierTable.Max)
                : seat;

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
            var layout = stage.transform.Find("[ 22 · LAYOUT ART ]");
            if (layout != null) ButtonGrid(layout, "LayoutArt");

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
