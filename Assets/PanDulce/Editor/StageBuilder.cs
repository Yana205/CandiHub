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
    /// can be rebuilt after any layout change. Folder objects keep identity transforms and the
    /// numeric prefixes preserve ordering.
    ///
    /// A rebuild is destructive: it clears the stage, so every view writes its children out
    /// from code again and hand placements under them are lost. Use "Re-author Views From
    /// Code" to reset views alone, and PlayLayoutTool to carry placements across a rebuild.
    /// </summary>
    public static class StageBuilder
    {
        /// <summary>
        /// Puts every view's children back the way code describes them, discarding hand edits.
        /// The escape hatch after a layout edit goes wrong — narrower than a full stage
        /// rebuild, which also rewires components and re-imports art.
        /// </summary>
        [MenuItem("Pan Dulce/Re-author Views From Code")]
        public static void ReAuthorViews()
        {
            var views = Object.FindObjectsByType<GeneratedView>(FindObjectsInactive.Include);
            foreach (var v in views) v.ReAuthor();

            var active = SceneManager.GetActiveScene();
            if (active.IsValid()) EditorSceneManager.MarkSceneDirty(active);
            Debug.Log($"[PanDulce] re-authored {views.Length} views from code");
        }

        const string ConfigDir = "Assets/PanDulce/Config";
        const string TuningPath = ConfigDir + "/Tuning.asset";
        const string PastriesPath = ConfigDir + "/Pastries.asset";
        const string UiSkinPath = ConfigDir + "/UiSkin.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Pan Dulce/Rebuild Stage %#b")]
        public static void Rebuild()
        {
            var tuning = EnsureTuning();
            var db = EnsurePastryDatabase();
            var uiSkin = EnsureUiSkin();

            // Per-dessert sizes live with the sprites; Tuning reads them from here so the sim
            // sees one radius for both art and collision.
            tuning.EditorAssign(db);
            EditorUtility.SetDirty(tuning);

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

            // 21 · BACKDROP — just the paper ground behind the lineart
            var backdrop = Folder(stage.transform, "[ 21 · BACKDROP ]");
            var backdropView = backdrop.AddComponent<BackdropView>();
            backdropView.EditorAssign(db);

            // 22 · LAYOUT ART — the hand-drawn v2 layout. The prefab is the designer
            // surface (Version2ArtImport creates it once; hand edits win), so the rebuild
            // only re-instantiates it — directly under the stage wearing the folder name,
            // instead of a wrapper folder holding a single prefab.
            var layoutArt = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PanDulce/Prefabs/LayoutArt.prefab");
            if (layoutArt != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(layoutArt);
                inst.name = "[ 22 · LAYOUT ART ]";
                inst.transform.SetParent(stage.transform, false);

                // The drawn candy box is what the sim's walls and floor are measured from,
                // so it needs the asset it writes those numbers into.
                var artBounds = inst.GetComponentInChildren<PlayBoundsFromArt>(true);
                if (artBounds != null) artBounds.EditorAssign(tuning);
            }

            // 23 · CUSTOMER — the bear plus its two satellites: the speech bubble showing
            // the order, and the dessert flying over to it.
            var customerFolder = Folder(stage.transform, "[ 23 · CUSTOMER ]");
            var customer = customerFolder.AddComponent<CustomerView>();
            customer.EditorAssign(db);

            var bubbleGo = Child(customerFolder.transform, "OrderBubble");
            var bubble = bubbleGo.AddComponent<OrderBubbleView>();
            bubble.EditorAssign(db);

            var flightGo = Child(customerFolder.transform, "ServeFlight");
            var flight = flightGo.AddComponent<ServeFlightView>();
            flight.EditorAssign(db);

            // 24 · DISPLAY CASE
            var caseFolder = Folder(stage.transform, "[ 24 · DISPLAY CASE ]");
            var displayCase = caseFolder.AddComponent<DisplayCaseView>();
            displayCase.EditorAssign(db);

            // 25 · PLAY AREA — the sim origin sits at stage (6, 424)
            var play = Folder(stage.transform, "[ 25 · PLAY AREA ]");
            play.transform.localPosition = StageCoords.Stage(StageCoords.PlayOriginX,
                                                             StageCoords.PlayOriginY);

            var shakeRootGo = Child(play.transform, "ShakeRoot");
            var shaker = shakeRootGo.AddComponent<ClothShaker>();

            var aimGo = Child(shakeRootGo.transform, "AimGuide");
            var aim = aimGo.AddComponent<AimGuideView>();
            aim.EditorAssign(db);

            var dangerGo = Child(shakeRootGo.transform, "DangerLine");
            var danger = dangerGo.AddComponent<DangerLineView>();

            var bodiesGo = Child(shakeRootGo.transform, "Bodies");
            var bodies = bodiesGo.AddComponent<PastryViewPool>();

            var fx = EnsureEffectAssets();
            var effectsGo = Child(shakeRootGo.transform, "Effects");
            var effects = effectsGo.AddComponent<EffectsView>();
            effects.EditorAssign(fx.merge, fx.sparkle, fx.serve, fx.dust, fx.spawn);

            var floatsGo = Child(shakeRootGo.transform, "FloatingText");
            var floats = floatsGo.AddComponent<FloatingTextPool>();

            // FoldFlaps sits outside the shake root so the fold does not wobble
            var foldGo = Child(play.transform, "FoldFlaps");
            var fold = foldGo.AddComponent<FoldView>();

            var plaqueGo = Child(play.transform, "NextPlaque");
            var plaque = plaqueGo.AddComponent<NextPlaqueView>();
            plaque.EditorAssign(db);
            plaque.EditorAssignSkin(uiSkin);

            // Scene-view-only gizmo guides for the fall area (walls, floor curve, spawn
            // and top-out lines). Uncheck the object to hide them; they never render in
            // the Game view or in builds.
            var guidesGo = Child(play.transform, "Guides");
            var guide = guidesGo.AddComponent<PlayAreaGuide>();
            guide.EditorAssign(tuning);

            // 26 · UI
            var ui = Folder(stage.transform, "[ 26 · UI ]");

            var topBarGo = Child(ui.transform, "TopBar");
            var topBar = topBarGo.AddComponent<TopBarView>();
            topBar.EditorAssign(db);
            topBar.EditorAssignSkin(uiSkin);
            topBarGo.AddComponent<SafeAreaInset>();   // defaults to the Top edge

            // Authored placement (Yana, 2026-08-04): the bar is hand-positioned beneath the
            // pile's minimum line, stage offset (+11, -18). Deliberately NO SafeAreaInset —
            // the bar must stay exactly where it was placed, never moved at runtime.
            // BoostBarView's hit rects follow this transform offset.
            var boostGo = Child(ui.transform, "BoostBar");
            boostGo.transform.localPosition = StageCoords.Stage(11f, -18f);
            var boostBar = boostGo.AddComponent<BoostBarView>();
            boostBar.EditorAssignSkin(uiSkin);

            // The hanging "next customer in" sign is an info widget like the bars — it
            // lives with the UI rather than in a furniture folder of one.
            var signGo = Child(ui.transform, "HangingSign");
            var sign = signGo.AddComponent<SignView>();
            sign.EditorAssignSkin(uiSkin);

            var cardGo = Child(ui.transform, "GameOverCard");
            var card = cardGo.AddComponent<GameOverCard>();
            card.EditorAssignSkin(uiSkin);

            // ---- wire it up ----
            // Pools deliberately do NOT build their children here — they grow in Awake, so
            // the saved scene holds three empty parents instead of ~176 serialized objects.
            bodies.EditorAssign(db);
            pointer.Init(cam, play.transform);

            gameRoot.EditorWire(tuning, db, cam, play.transform, shaker, bodies,
                                floats, aim, danger, fold, topBar, boostBar,
                                bubble, sign, displayCase, customer, flight, card, plaque,
                                effects, pointer, sfx);

            // Hand-tuned placement captured from play mode wins over the defaults above.
            PlayLayoutTool.Apply();

            // Chrome children are authored scene objects now and are meant to be saved; only
            // the sim-driven views still hold DontSave content, and ClearForSave strips that.
            var views = Object.FindObjectsByType<GeneratedView>(FindObjectsInactive.Include);
            foreach (var v in views) v.ClearForSave();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Tuning is dirtied here (TierSize wiring) and by PlayBoundsFromArt as it
            // re-derives the walls from the box art — commit both with the scene.
            AssetDatabase.SaveAssets();

            foreach (var v in views) v.Rebuild();

            var info = new FileInfo(ScenePath);
            Debug.Log($"[PanDulce] stage rebuilt, saved to {ScenePath} ({info.Length / 1024f:F1} KB)");
        }

        // ---------------------------------------------------------------- effect assets

        const string EffectsArtDir = "Assets/PanDulce/Art/Effects";
        const string EffectsPrefabDir = "Assets/PanDulce/Prefabs/Effects";

        /// <summary>
        /// Creates the effect materials and prefabs only when missing — the prefabs are the
        /// designer-editable surface, so an existing asset always wins over the defaults here.
        /// </summary>
        static (GameObject merge, GameObject sparkle, GameObject serve, GameObject dust,
                GameObject spawn) EnsureEffectAssets()
        {
            Directory.CreateDirectory(EffectsPrefabDir);

            Material puffMat = EnsureEffectMaterial("Puff", EffectsArtDir + "/soft_disc.png");
            Material sparkMat = EnsureEffectMaterial("Spark", EffectsArtDir + "/spark.png");

            var merge = EnsureEffectPrefab("MergeBurst", puffMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.75f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.7f);   // 60–170 px/s
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                main.startColor = Palette.Cream;
                main.gravityModifier = -0.06f;                                   // gentle lift
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.12f;
            });
            var sparkle = EnsureEffectPrefab("Sparkle", sparkMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                main.startColor = Palette.Amber;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = -0.02f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.05f;
            });
            // The only system that simulates in world space: the puffs have to stay where
            // they were dropped so the flyer pulls away from them. Everything is tuned for
            // a continuous plume rather than a scatter — barely any speed, a shape narrower
            // than the puff spacing, and a ribbon threaded through the live particles.
            // EffectsView narrows and thins it as the flyer shrinks, so the plume tapers
            // toward the bear (see EffectsView.Taper).
            var serve = EnsureEffectPrefab("ServePoof", puffMat, ps =>
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.12f);      // barely drifts off the path
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.26f);
                main.startColor = Palette.Cream;
                var em = ps.emission;                                            // trail while following
                em.rateOverTime = 0f;
                em.rateOverDistance = 28f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.012f;                                           // ~1 px of jitter, under the spacing
                shape.radiusThickness = 1f;
                var sol = ps.sizeOverLifetime;                                   // settle, do not collapse
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.9f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.6f)));
                var col = ps.colorOverLifetime;                                  // a longer fade than the bursts
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0f, 1f) });
                col.color = grad;
                var tr = ps.trails;                                              // one ribbon along the whole path
                tr.enabled = true;
                tr.mode = ParticleSystemTrailMode.Ribbon;
                tr.ribbonCount = 1;
                tr.worldSpace = true;
                tr.dieWithParticles = true;
                tr.sizeAffectsWidth = true;
                tr.inheritParticleColor = true;
                tr.minVertexDistance = 0.02f;
                tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
                tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.4f);
                var ribbon = new Gradient();
                ribbon.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.5f, 1f) });
                tr.colorOverTrail = new ParticleSystem.MinMaxGradient(ribbon);
                ps.GetComponent<ParticleSystemRenderer>().trailMaterial = puffMat;
            });
            var dust = EnsureEffectPrefab("ShakeDust", puffMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startColor = new Color(232f/255f, 213f/255f, 181f/255f, 0.7f);
                main.gravityModifier = 0.05f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;      // a line along the floor
                shape.radius = 1.7f;                                            // half the cloth width in units
            });
            // The "here it comes" cue for the next held pastry. Deliberately the quietest of
            // the set — shorter-lived, slower and smaller than MergeBurst, so it reads as a
            // hint rather than a celebration.
            var spawn = EnsureEffectPrefab("SpawnPuff", puffMat, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);   // 15–50 px/s, a drift
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startColor = Palette.Cream;
                main.gravityModifier = -0.02f;                                   // barely lifts
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.1f;                                             // ring around the icon
            });
            return (merge, sparkle, serve, dust, spawn);
        }

        static Material EnsureEffectMaterial(string name, string texPath)
        {
            string path = $"{EffectsArtDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Sprites/Default"));                  // unlit — no Light2D exists
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject EnsureEffectPrefab(string name, Material mat, System.Action<ParticleSystem> configure)
        {
            string path = $"{EffectsPrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;                               // designer edits win

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;          // inherits the cloth shake
            main.maxParticles = 256;
            var emission = ps.emission;
            emission.rateOverTime = 0f;                                          // Emit()-driven
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            configure(ps);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = "PlayArea";
            r.sortingOrder = 40;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
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

            if (pastries.Length < Core.TierTable.Count)
                Debug.LogWarning($"[PanDulce] expected at least {Core.TierTable.Count} pastry sprites " +
                                 $"(chain), found {pastries.Length}. Run: node Docs/tools/bake-sprites.js");
            return db;
        }

        /// <summary>
        /// Binds the hand-drawn chrome from Art/UI by file name, after fixing the import
        /// settings 9-slicing needs. A missing file leaves its slot empty and the view falls
        /// back to the generated rounded rect, so a partial art drop never breaks the shell.
        /// </summary>
        static UiSkin EnsureUiSkin()
        {
            Directory.CreateDirectory(ConfigDir);
            SpriteImportSetup.ApplyUi();

            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(UiSkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<UiSkin>();
                AssetDatabase.CreateAsset(skin, UiSkinPath);
            }

            skin.EditorAssign(LoadUiSprite("customers counter"),
                              LoadUiSprite("coin counter"),
                              LoadUiSprite("Button"),
                              LoadUiSprite("Button 2"),
                              LoadUiSprite("Next customer"),
                              LoadUiSprite("Next pastry"),
                              LoadUiSprite("Coin"),
                              LoadUiSprite("Top Bar"),
                              LoadUiSprite("Bottom Bar"));

            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }

        /// <summary>
        /// The chrome PNGs are sliced in Multiple mode so the borders survive a reimport,
        /// and LoadAssetAtPath&lt;Sprite&gt; returns null for those — the sprite is a sub-asset.
        /// </summary>
        static Sprite LoadUiSprite(string fileName)
        {
            string path = $"{SpriteImportSetup.UiDir}/{fileName}.png";
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
                Debug.LogWarning($"[PanDulce] no sprite in {path} — that chrome falls back to " +
                                 "the generated rounded rect.");
            return sprite;
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
