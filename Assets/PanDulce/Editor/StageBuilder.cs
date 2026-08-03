using System.IO;
using System.Linq;
using PanDulce.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PanDulce.Editor
{
    /// <summary>
    /// Builds Main.unity's hierarchy from §4, reproducibly.
    ///
    /// The scene is authored by this script rather than by hand so it stays merge-safe and
    /// can be rebuilt after any layout change. Folder objects keep identity transforms; the
    /// numeric prefixes preserve ordering; view components construct their own internals so
    /// the committed scene stays thin.
    /// </summary>
    public static class StageBuilder
    {
        const string ConfigDir = "Assets/PanDulce/Config";
        const string TuningPath = ConfigDir + "/Tuning.asset";
        const string PastriesPath = ConfigDir + "/Pastries.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Pan Dulce/Rebuild Stage %#b")]
        public static void Rebuild()
        {
            var tuning = EnsureTuning();
            var db = EnsurePastryDatabase();

            var scene = EnsureScene();
            ClearStage(scene);

            // ---- [ 00 · SYSTEMS ] ----
            var systems = Folder(null, "[ 00 · SYSTEMS ]");
            var gameRootGo = new GameObject("GameRoot");
            gameRootGo.transform.SetParent(systems.transform, false);
            var gameRoot = gameRootGo.AddComponent<GameRoot>();
            var sfx = gameRootGo.AddComponent<SfxPlayer>();
            var pointer = gameRootGo.AddComponent<PointerInput>();

            var es = new GameObject("EventSystem");
            es.transform.SetParent(systems.transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // ---- [ 10 · CAMERA ] ----
            var cameraFolder = Folder(null, "[ 10 · CAMERA ]");
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.transform.SetParent(cameraFolder.transform, false);
            cam.orthographic = true;
            // A baseline only: StageFitter widens this at runtime to contain the safe box.
            // 4.5 is the height-limited case, where 900 stage px exactly fill the view.
            cam.orthographicSize = StageCoords.SafeH * StageCoords.PX * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Page;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            // ---- [ 20 · STAGE ] — fixed scale; the camera is what adapts (§5.1) ----
            var stage = Folder(null, "[ 20 · STAGE ]");
            var fitter = stage.AddComponent<StageFitter>();
            fitter.EditorAssign(cam);

            // 21 · BACKDROP
            var backdrop = Folder(stage.transform, "[ 21 · BACKDROP ]");
            var backdropView = backdrop.AddComponent<BackdropView>();
            backdropView.EditorAssign(db);

            // 22 · CUSTOMER
            var customerFolder = Folder(stage.transform, "[ 22 · CUSTOMER ]");
            var customer = customerFolder.AddComponent<CustomerView>();
            customer.EditorAssign(db);

            // 23 · FURNITURE
            var furniture = Folder(stage.transform, "[ 23 · FURNITURE ]");
            var signGo = Child(furniture.transform, "HangingSign");
            var sign = signGo.AddComponent<SignView>();

            // 24 · DISPLAY CASE
            var caseFolder = Folder(stage.transform, "[ 24 · DISPLAY CASE ]");
            var displayCase = caseFolder.AddComponent<DisplayCaseView>();
            displayCase.EditorAssign(db);

            // 25 · PLAY AREA — the sim origin sits at stage (6, 424)
            var play = Folder(stage.transform, "[ 25 · PLAY AREA ]");
            play.transform.localPosition = StageCoords.Stage(StageCoords.PlayOriginX,
                                                             StageCoords.PlayOriginY);

            var shakeRootGo = Child(play.transform, "ClothShakeRoot");
            var shaker = shakeRootGo.AddComponent<ClothShaker>();

            var clothGo = Child(shakeRootGo.transform, "Cloth");
            clothGo.AddComponent<MeshFilter>();
            clothGo.AddComponent<MeshRenderer>();
            var cloth = clothGo.AddComponent<ClothView>();

            var aimGo = Child(shakeRootGo.transform, "AimGuide");
            var aim = aimGo.AddComponent<AimGuideView>();
            aim.EditorAssign(db);

            var dangerGo = Child(shakeRootGo.transform, "DangerLine");
            var danger = dangerGo.AddComponent<DangerLineView>();

            var bodiesGo = Child(shakeRootGo.transform, "Bodies");
            var bodies = bodiesGo.AddComponent<PastryViewPool>();

            var particlesGo = Child(shakeRootGo.transform, "Particles");
            var particles = particlesGo.AddComponent<ParticleViewPool>();

            var floatsGo = Child(shakeRootGo.transform, "FloatingText");
            var floats = floatsGo.AddComponent<FloatingTextPool>();

            // FoldFlaps sits outside the shake root so the fold does not wobble
            var foldGo = Child(play.transform, "FoldFlaps");
            var fold = foldGo.AddComponent<FoldView>();

            // 26 · UI
            var ui = Folder(stage.transform, "[ 26 · UI ]");

            var topBarGo = Child(ui.transform, "TopBar");
            var topBar = topBarGo.AddComponent<TopBarView>();
            topBar.EditorAssign(db);
            topBarGo.AddComponent<SafeAreaInset>();   // defaults to the Top edge

            var boostGo = Child(ui.transform, "BoostBar");
            var boostBar = boostGo.AddComponent<BoostBarView>();
            var bottomInset = boostGo.AddComponent<SafeAreaInset>();
            SetPrivate(bottomInset, "edge", SafeAreaInset.Edge.Bottom);

            var bubbleGo = Child(ui.transform, "OrderBubble");
            var bubble = bubbleGo.AddComponent<OrderBubbleView>();
            bubble.EditorAssign(db);

            var flightGo = Child(ui.transform, "ServeFlight");
            var flight = flightGo.AddComponent<ServeFlightView>();
            flight.EditorAssign(db);

            var cardGo = Child(ui.transform, "GameOverCard");
            var card = cardGo.AddComponent<GameOverCard>();

            // ---- wire it up ----
            // Pools deliberately do NOT build their children here — they grow in Awake, so
            // the saved scene holds three empty parents instead of ~176 serialized objects.
            bodies.EditorAssign(db);
            pointer.Init(cam, play.transform);

            gameRoot.EditorWire(tuning, db, cam, play.transform, shaker, cloth, bodies,
                                particles, floats, aim, danger, fold, topBar, boostBar,
                                bubble, sign, displayCase, customer, flight, card, pointer, sfx);

            // Strip generated content, save a clean scene, then put the preview back.
            var views = Object.FindObjectsByType<GeneratedView>(FindObjectsInactive.Include);
            foreach (var v in views) v.ClearForSave();
            if (cloth != null) cloth.ClearForSave();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            foreach (var v in views) v.Rebuild();
            if (cloth != null) cloth.Build();

            var info = new FileInfo(ScenePath);
            Debug.Log($"[PanDulce] stage rebuilt, saved to {ScenePath} ({info.Length / 1024f:F1} KB)");
        }

        // ---------------------------------------------------------------- assets

        static TuningConfig EnsureTuning()
        {
            Directory.CreateDirectory(ConfigDir);
            var t = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningPath);
            if (t != null) return t;

            t = ScriptableObject.CreateInstance<TuningConfig>();
            AssetDatabase.CreateAsset(t, TuningPath);
            AssetDatabase.SaveAssets();
            return t;
        }

        static PastryDatabase EnsurePastryDatabase()
        {
            Directory.CreateDirectory(ConfigDir);
            var db = AssetDatabase.LoadAssetAtPath<PastryDatabase>(PastriesPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<PastryDatabase>();
                AssetDatabase.CreateAsset(db, PastriesPath);
            }

            // Bind the baked art by filename order — pastry_00..pastry_10, customer_0..2.
            var pastries = LoadSprites(SpriteImportSetup.PastryDir, "pastry_");
            var customers = LoadSprites(SpriteImportSetup.CustomerDir, "customer_");
            db.EditorAssign(pastries, customers);
            db.EditorAssignShell(
                AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportSetup.ShellDir + "/window_scene.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(SpriteImportSetup.ShellDir + "/counter_opening.png"));

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            if (pastries.Length != Core.TierTable.Count)
                Debug.LogWarning($"[PanDulce] expected {Core.TierTable.Count} pastry sprites, found {pastries.Length}. " +
                                 "Run: node Docs/tools/bake-sprites.js");
            return db;
        }

        static Sprite[] LoadSprites(string dir, string prefix)
        {
            if (!Directory.Exists(dir)) return new Sprite[0];
            return Directory.GetFiles(dir, prefix + "*.png", SearchOption.TopDirectoryOnly)
                            .Select(p => p.Replace('\\', '/'))
                            .OrderBy(p => p)
                            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                            .Where(s => s != null)
                            .ToArray();
        }

        // ---------------------------------------------------------------- scene helpers

        static Scene EnsureScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path == ScenePath) return scene;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Removes everything the builder owns, leaving the camera to be re-parented.</summary>
        static void ClearStage(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.GetComponent<Camera>() != null) { go.transform.SetParent(null, true); continue; }
                Object.DestroyImmediate(go);
            }
        }

        static GameObject Folder(Transform parent, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;   // a stray scale here silently breaks layout
            return go;
        }

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void SetPrivate<T>(Object target, string field, T value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p != null)
            {
                p.enumValueIndex = (int)(object)value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
