using System.IO;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// One-shot import for the version-2 hand-drawn art.
    ///
    /// The finished layout lives in Art/BG: one Procreate painting exported a layer at a
    /// time, every layer on the same 1376 × 2560 canvas, so the pieces self-align when
    /// stacked at the same position. That canvas is the grey lineart's 860 × 1600 at
    /// exactly 1.6×, which is why nothing had to be re-aligned when the paint landed —
    /// importing at PPU 320 instead of 200 puts every piece back where the lineart was
    /// (430 × 800 stage px at scale 1). The layer PNGs already carry their own alpha, so
    /// unlike the grey scans there is nothing to key out: the tool only stamps import
    /// settings on them and assembles LayoutArt.prefab. The prefab is the designer
    /// surface: it is created only when missing, so hand-tuned positions survive
    /// re-imports and stage rebuilds — delete it to regenerate.
    ///
    /// The final desserts replace placeholder pastry sprites in place, renormalized to the
    /// §8.1 convention (content fits a 400 px box centered in a 512² canvas) so
    /// ViewFactory.SetIcon's radius/200 scaling stays honest.
    /// </summary>
    public static class Version2ArtImport
    {
        const string LayoutDir = "Assets/PanDulce/Art/BG";
        const string WideDir = "Assets/PanDulce/Art/Layout";   // generated, never hand-edited
        const string DessertDir = "Assets/PanDulce/Art/final-desserts";
        const string PrefabPath = "Assets/PanDulce/Prefabs/LayoutArt.prefab";

        const string Src = LayoutDir + "/";
        const string Wide = WideDir + "/";

        /// <summary>
        /// The painting, back to front — the Procreate stack read bottom-up (see
        /// BG/full-layers.png), grouped into the three things the scene cares about:
        /// the room behind the customer, the case the customer stands behind, and the
        /// counter the player plays on.
        ///
        /// Two of Yana's filenames say something other than what they draw, so the node
        /// name is the truth here: `glass-container-blush1` is the 菓子パン lettering that
        /// belongs on the noren (its alpha sits at canvas y 280–558, up at the curtain),
        /// and `IMG_1392` is the hairline seam where the wall meets the desk.
        /// `IMG_1377` — the four red registration ticks — is deliberately not imported.
        ///
        /// Sorting is where filled paint differs from lineart: you could see the bear
        /// through the grey outlines, so everything could sit on Background. Now the case
        /// has to occlude the bear, which means Furniture (above Customer) for its body,
        /// and Case+20 for the glass so the wash reads as glass OVER the shelf desserts.
        /// </summary>
        static readonly (string asset, string group, string node, string layer, int order)[] LayoutPieces =
        {
            // The room — all of it behind the bear.
            (Wide + "wall-wide.png",                   "Room",        "Wall",        "Background", 0),
            (Src + "streen-window.png",                "Room",        "WindowView",  "Background", 2),
            (Src + "street-window-wood.png",           "Room",        "WindowFrame", "Background", 3),
            (Src + "entrance-windows.png",             "Room",        "Noren",       "Background", 6),
            (Src + "glass-container-blush1.png",       "Room",        "NorenSign",   "Background", 7),

            // The counter the bear stands behind: desk, seam, then the case body on top.
            (Wide + "desk-wide.png",                   "Counter",     "Desk",        "Furniture",  2),
            (Src + "IMG_1392.PNG",                     "Counter",     "DeskSeam",    "Furniture",  3),
            (Src + "glass-container.png",              "GlassCase",   "CaseBody",    "Furniture",  4),

            // Shelf desserts draw at Case/10 with their labels at 13 — the glass goes over
            // both, so the pastries are seen THROUGH it.
            (Src + "glass-container-galsseffect2.png", "GlassCase",   "CaseGlass",   "Case",      20),

            // The play surface. Pastries fall at PlayArea/30, in front of the box.
            (Src + "furoshiki.png",                    "PlaySurface", "Furoshiki",   "PlayArea",   2),
            (Src + "candybox.png",                     "PlaySurface", "CandyBox",    "PlayArea",   5),
        };

        /// <summary>Group nodes, in the order they should appear in the hierarchy.</summary>
        static readonly string[] LayoutGroups = { "Room", "GlassCase", "Counter", "PlaySurface" };

        /// <summary>
        /// The cloth and the box are one prop, and they are the only pieces that need to be
        /// bigger than Yana painted them: the canvas is exactly the 430 px frame width, so
        /// the furoshiki's left and right points get sliced off by the canvas edge a few px
        /// inside the screen. This node scales the pair about the desk line (so the cloth
        /// stays seated on the counter) until the slice is safely off-screen — 430 × 1.10
        /// spans 473 px against the 446 px the widest supported phone shows.
        ///
        /// Enlarging the box is now free: <see cref="PanDulce.Runtime.PlayBoundsFromArt"/>
        /// on CandyBox re-derives the sim's walls and floor from wherever the art ends up,
        /// so this number can be changed in the Inspector without touching Tuning.
        /// </summary>
        const float PlaySurfaceScale = 1.10f;

        /// <summary>
        /// Pieces that must reach past the canvas into letterbox slack. The wall and the
        /// floor are vertical gradients, so no flat backdrop colour meets their edge without
        /// a seam — but neither has horizontal detail worth keeping, so widening the canvas
        /// and repeating the outermost column outwards extends them exactly. (The first cut
        /// at this stretched the whole sprite behind itself, which smeared the desk's centre
        /// shading across the bleed and read as the desk drawn twice.)
        /// </summary>
        static readonly (string src, string dst)[] WidenedPieces =
        {
            ("background.png", "wall-wide.png"),
            ("desk.png", "desk-wide.png"),
        };

        /// <summary>How much wider than the source canvas the widened pieces are baked.</summary>
        const float WidenFactor = 1.6f;

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

        // The painted desk line sits at canvas y 1520 of 2560 — stage y 475 once the canvas
        // is mapped onto its 800 px height. The sim's counter top is stage y 424, so the
        // canvas rides 51 px up and the two coincide. (Same number as the grey lineart:
        // its desk line was at canvas y 950 of 1600, which is the identical fraction.)
        const float CanvasOffsetY = -51f;

        [MenuItem("Pan Dulce/Import V2 Art")]
        public static void Run()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PanDulce] compiling — run Import V2 Art again when idle.");
                return;
            }

            WidenPieces();
            ProcessDesserts();
            AssetDatabase.Refresh();
            ApplyLayoutImportSettings();
            SpriteImportSetup.Apply(SpriteImportSetup.PastryDir);
            EnsureLayoutArtPrefab();
            StageBuilder.Rebuild();
            Debug.Log("[PanDulce] V2 art imported.");
        }

        // ------------------------------------------------------------- layout art

        /// <summary>
        /// PPU for the finished painting: the 1376 × 2560 canvas renders 430 × 800 stage
        /// px, the same footprint the 860 × 1600 lineart had at PPU 200.
        /// </summary>
        const float LayoutPPU = 320f;

        /// <summary>
        /// Re-bake the widened wall and floor: the source canvas centred in a wider one, with
        /// its outermost column repeated outwards. Centring is what keeps the piece's pivot
        /// on the canvas centre, so the widened sprite drops straight into the same position
        /// at the same PPU as every other piece.
        /// </summary>
        static void WidenPieces()
        {
            Directory.CreateDirectory(WideDir);
            foreach (var (src, dst) in WidenedPieces)
            {
                var tex = LoadTex(Path.Combine(LayoutDir, src));
                int w = tex.width, h = tex.height;
                int ow = Mathf.RoundToInt(w * WidenFactor) & ~1;   // keep it even so the centring is exact
                int ox = (ow - w) / 2;

                var px = tex.GetPixels32();
                var outPx = new Color32[ow * h];
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    Color32 left = px[row], right = px[row + w - 1];
                    int orow = y * ow;
                    for (int x = 0; x < ox; x++) outPx[orow + x] = left;
                    System.Array.Copy(px, row, outPx, orow + ox, w);
                    for (int x = ox + w; x < ow; x++) outPx[orow + x] = right;
                }

                var wide = new Texture2D(ow, h, TextureFormat.RGBA32, false);
                wide.SetPixels32(outPx);
                File.WriteAllBytes(Path.Combine(WideDir, dst), wide.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(wide);
            }
        }

        static void ApplyLayoutImportSettings()
        {
            foreach (var piece in LayoutPieces)
            {
                string assetPath = piece.asset;
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[PanDulce] layout piece missing: {assetPath}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = LayoutPPU;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                // Uncompressed while authoring — every piece is a smooth watercolour wash,
                // and block compression bands the pink wall badly. Mobile still ships ASTC.
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;   // matches the platform overrides below

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

        /// <summary>Re-bake only the desserts — for sizing/normalization changes. Files are
        /// overwritten in place so sprite GUIDs (and every Pastries.asset slot, Original and
        /// skin alike) survive without an EditorAssign pass.</summary>
        [MenuItem("Pan Dulce/Rebake Desserts")]
        public static void RebakeDesserts()
        {
            if (EditorApplication.isCompiling)
            {
                Debug.LogWarning("[PanDulce] compiling — run Rebake Desserts again when idle.");
                return;
            }
            ProcessDesserts();
            AssetDatabase.Refresh();
            SpriteImportSetup.Apply(SpriteImportSetup.PastryDir);
            Debug.Log("[PanDulce] desserts re-baked.");
        }

        static void ProcessDesserts()
        {
            // Only files that fell OUT of the table (a dessert renamed or cut) are deleted;
            // everything in the table is overwritten in place, which keeps its GUID and so
            // every sprite reference in Pastries.asset — Original slots and skin slots alike.
            foreach (string path in Directory.GetFiles(SpriteImportSetup.PastryDir, "pastry_*.png"))
            {
                string name = Path.GetFileName(path);
                if (System.Array.FindIndex(Desserts, d => d.dst == name) < 0)
                    AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
            }

            foreach (var (src, dst) in Desserts)
                BakeDessert(Path.Combine(DessertDir, src),
                            $"{SpriteImportSetup.PastryDir}/{dst}");
        }

        /// <summary>
        /// Every dessert should READ as the same size at the same tier radius, but the
        /// drawings fill their boxes very differently — a chunky roll cake swirl versus an
        /// airy melon pan. So the bake normalizes by perceived mass, not bounding box: the
        /// content is scaled until the radius of a circle with its opaque-pixel area hits
        /// TargetEffR, centered on the 512² canvas.
        /// </summary>
        const float TargetEffR = 181f;   // the mochi trio's historical size — the eye's anchor
        const float MaxContent = 460f;   // safety cap so no bake can spill the canvas

        static void BakeDessert(string srcPath, string dstPath)
        {
            var src = LoadTex(srcPath);

            int minX = src.width, minY = src.height, maxX = -1, maxY = -1;
            long area = 0;
            var pixels = src.GetPixels32();
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                    if (pixels[y * src.width + x].a > 8)
                    {
                        area++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
            if (maxX < 0) { Object.DestroyImmediate(src); return; }

            float w = maxX - minX + 1, h = maxY - minY + 1;
            float effR = Mathf.Sqrt(area / Mathf.PI);
            float scale = Mathf.Min(TargetEffR / effR, MaxContent / Mathf.Max(w, h));
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

                var groups = new System.Collections.Generic.Dictionary<string, Transform>();
                foreach (string g in LayoutGroups) groups[g] = Node(root.transform, g);

                // PlaySurface pivots on the desk line and scales the cloth + box together.
                // Its children sit at the canvas centre relative to that pivot, so the pair
                // grows about the counter edge rather than drifting off it.
                var surface = groups["PlaySurface"];
                surface.localPosition = StageCoords.Stage(215f, 424f - CanvasOffsetY);
                surface.localScale = new Vector3(PlaySurfaceScale, PlaySurfaceScale, 1f);

                // Table order is paint order, so siblings read back-to-front top-to-bottom
                // in the hierarchy the same way the Procreate stack does.
                foreach (var (asset, group, node, layer, order) in LayoutPieces)
                    Piece(groups[group], node, asset, layer, order);

                // The box is the play area's authority now — see PlayBoundsFromArt.
                var candyBox = surface.Find("CandyBox");
                if (candyBox != null) candyBox.gameObject.AddComponent<PlayBoundsFromArt>();

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

        static void Piece(Transform parent, string name, string asset, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Every piece shares the canvas, so every piece sits at the canvas centre —
            // expressed relative to its group, which is what lets PlaySurface pivot
            // somewhere else entirely without shifting what it holds.
            go.transform.localPosition = StageCoords.Stage(215f, 400f) - parent.localPosition;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            // The unlit material is HideAndDontSave and cannot be serialized into the
            // prefab — UnlitSprite re-links it on load instead.
            go.AddComponent<UnlitSprite>();
        }
    }
}
