using System.IO;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// One-shot import for the version-2 hand-drawn art.
    ///
    /// The layout pieces in Art/layout-gray are lineart scans on white paper, all exported
    /// on the same 860 × 1600 canvas — so once the paper is keyed out they self-align when
    /// stacked at the same position. This tool keys the white away, writes sprites to
    /// Art/Layout at PPU 200 (canvas renders 430 × 800 stage px at scale 1), and assembles
    /// them into LayoutArt.prefab. The prefab is the designer surface: it is created only
    /// when missing, so hand-tuned positions survive re-imports and stage rebuilds.
    ///
    /// The final desserts replace placeholder pastry sprites in place, renormalized to the
    /// §8.1 convention (content fits a 400 px box centered in a 512² canvas) so
    /// ViewFactory.SetIcon's radius/200 scaling stays honest.
    /// </summary>
    public static class Version2ArtImport
    {
        const string LayoutSrcDir = "Assets/PanDulce/Art/layout-gray";
        const string LayoutOutDir = "Assets/PanDulce/Art/Layout";
        const string DessertDir = "Assets/PanDulce/Art/final-desserts";
        const string PrefabPath = "Assets/PanDulce/Prefabs/LayoutArt.prefab";

        // Piece order: back to front, with the canvas stacked so every piece lands where the
        // full mock (example-layout-full.jpeg) put it.
        static readonly (string src, string name)[] LayoutPieces =
        {
            ("layout-street-window.jpeg", "street-window"),
            ("layout-hanging-entrance.jpeg", "hanging-entrance"),
            ("layout-glass-container.jpeg", "glass-container"),
            ("layout-deskline.jpeg", "deskline"),
            ("layout-furoshiki.jpeg", "furoshiki"),
            ("layout-candybox.jpeg", "candybox"),
        };

        // The full merge chain is hand-drawn: the color variants are tiers of their own,
        // so each family reads as a progression (pink → matcha → mango mochi, …). Ordered
        // smallest to largest — mochi trio, purin, donut trio, roll cake, melon pan trio.
        static readonly (string src, string dst)[] Desserts =
        {
            ("mochi_pink.PNG", "pastry_00_mochi.png"),
            ("mochi_green.PNG", "pastry_01_matcha-mochi.png"),
            ("mochi_yellow.PNG", "pastry_02_mango-mochi.png"),
            ("purin_original.PNG", "pastry_03_purin.png"),
            ("donut_pink.PNG", "pastry_04_berry-donut.png"),
            ("donut_green.PNG", "pastry_05_matcha-donut.png"),
            ("donut_brown.PNG", "pastry_06_choco-donut.png"),
            ("rollcake_strawberrymatcha.PNG", "pastry_07_rollcake.png"),
            ("melonpan_pink.PNG", "pastry_08_sakura-pan.png"),
            ("IMG_1359.PNG", "pastry_09_honey-pan.png"),   // the yellow melon pan skin
            ("melonpan_original.PNG", "pastry_10_melonpan.png"),
        };

        // The mock's desk line sits at canvas y 950 (475 at half scale); the sim's counter
        // top is stage y 424. This offset lines the two up as a starting point.
        const float CanvasOffsetY = -51f;

        [MenuItem("Pan Dulce/Import V2 Art")]
        public static void Run()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PanDulce] compiling — run Import V2 Art again when idle.");
                return;
            }

            ProcessLayout();
            ProcessDesserts();
            AssetDatabase.Refresh();
            ApplyLayoutImportSettings();
            SpriteImportSetup.Apply(SpriteImportSetup.PastryDir);
            EnsureLayoutArtPrefab();
            StageBuilder.Rebuild();
            Debug.Log("[PanDulce] V2 art imported.");
        }

        // ------------------------------------------------------------- layout lineart

        static void ProcessLayout()
        {
            Directory.CreateDirectory(LayoutOutDir);
            foreach (var (src, name) in LayoutPieces)
            {
                var tex = LoadTex(Path.Combine(LayoutSrcDir, src));
                var keyed = KeyOutPaper(tex);
                File.WriteAllBytes($"{LayoutOutDir}/{name}.png", keyed.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(keyed);
            }
        }

        /// <summary>
        /// Paper → transparent. The scans are brown ink on near-white paper; alpha comes
        /// from how far the brightest channel falls below the paper point, which keeps the
        /// anti-aliased edge of every stroke.
        /// </summary>
        static Texture2D KeyOutPaper(Texture2D tex)
        {
            const float paper = 0.90f, ink = 0.60f;
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float maxc = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
                float a = Mathf.Clamp01((paper - maxc) / (paper - ink));
                px[i].a = (byte)(a * 255f);
            }
            // LoadImage decodes JPEG into an RGB24 texture, which silently drops any alpha
            // written back into it — the keyed pixels need an RGBA32 home to survive.
            var keyed = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            keyed.SetPixels32(px);
            return keyed;
        }

        static void ApplyLayoutImportSettings()
        {
            foreach (string path in Directory.GetFiles(LayoutOutDir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 200f;   // 860 × 1600 canvas → 430 × 800 stage px
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);

                foreach (string platform in new[] { "Android", "iPhone" })
                {
                    var ps = importer.GetPlatformTextureSettings(platform);
                    ps.overridden = true;
                    ps.maxTextureSize = 2048;
                    ps.format = TextureImporterFormat.ASTC_6x6;
                    ps.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(ps);
                }

                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------- dessert bake

        static void ProcessDesserts()
        {
            // The whole chain is re-baked from source, so any pastry file not in the table
            // (old placeholders, desserts that moved tier) is stale — clear them all first.
            foreach (string path in Directory.GetFiles(SpriteImportSetup.PastryDir, "pastry_*.png"))
                AssetDatabase.DeleteAsset(path.Replace('\\', '/'));

            foreach (var (src, dst) in Desserts)
                BakeDessert(Path.Combine(DessertDir, src),
                            $"{SpriteImportSetup.PastryDir}/{dst}");
        }

        /// <summary>Fits the opaque content into a 400 px box centered on a 512² canvas.</summary>
        static void BakeDessert(string srcPath, string dstPath)
        {
            var src = LoadTex(srcPath);

            int minX = src.width, minY = src.height, maxX = -1, maxY = -1;
            var pixels = src.GetPixels32();
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                    if (pixels[y * src.width + x].a > 8)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) { Object.DestroyImmediate(src); return; }

            float w = maxX - minX + 1, h = maxY - minY + 1;
            float scale = 400f / Mathf.Max(w, h);
            int tw = Mathf.RoundToInt(w * scale), th = Mathf.RoundToInt(h * scale);
            int ox = (512 - tw) / 2, oy = (512 - th) / 2;

            var dst = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var outPx = new Color[512 * 512];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    float u = (minX + (x + 0.5f) / tw * w) / src.width;
                    float v = (minY + (y + 0.5f) / th * h) / src.height;
                    outPx[(oy + y) * 512 + ox + x] = src.GetPixelBilinear(u, v);
                }
            dst.SetPixels(outPx);
            File.WriteAllBytes(dstPath, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
        }

        static Texture2D LoadTex(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));   // decodes JPEG/PNG, always readable
            return tex;
        }

        // ------------------------------------------------------------- prefab

        static void EnsureLayoutArtPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;   // designer edits win

            var root = new GameObject("LayoutArt");
            try
            {
                root.transform.localPosition = StageCoords.Stage(0f, CanvasOffsetY);

                var backdrop = Node(root.transform, "Backdrop");
                Piece(backdrop, "StreetWindow", "street-window", "Background", 4);
                Piece(backdrop, "HangingEntrance", "hanging-entrance", "Background", 6);

                var glass = Node(root.transform, "GlassDisplay");
                Piece(glass, "GlassContainer", "glass-container", "Background", 10);

                // The customer draws on the Customer layer — above Background, below
                // Furniture — which is exactly "in front of the glass, behind the desk".
                var play = Node(root.transform, "PlayAreaArt");
                Piece(play, "DeskLine", "deskline", "Furniture", 5);
                Piece(play, "Furoshiki", "furoshiki", "PlayArea", 2);
                Piece(play, "CandyBox", "candybox", "PlayArea", 5);   // pastries draw at 30, in front

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[PanDulce] created {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Transform Node(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Piece(Transform parent, string name, string file, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Every piece shares the canvas, so every piece sits at the canvas center.
            go.transform.localPosition = StageCoords.Stage(215f, 400f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{LayoutOutDir}/{file}.png");
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            // The unlit material is HideAndDontSave and cannot be serialized into the
            // prefab — UnlitSprite re-links it on load instead.
            go.AddComponent<UnlitSprite>();
        }
    }
}
