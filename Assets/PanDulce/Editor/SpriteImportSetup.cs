using System.IO;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Applies the import settings from §8.1 to the baked art.
    ///
    /// The platform override matters: uncompressed 512² RGBA is 1 MB per sprite, so 11
    /// pastries plus shell art would burn ~20 MB of texture memory for no benefit. Editor
    /// and Standalone stay uncompressed so the result can be compared against the mock
    /// pixel-for-pixel; Android and iOS use ASTC 6×6, which flat art with hard outlines
    /// takes cleanly.
    /// </summary>
    public static class SpriteImportSetup
    {
        public const string PastryDir = "Assets/PanDulce/Art/Pastries";
        public const string CustomerDir = "Assets/PanDulce/Art/Customers";
        public const string ShellDir = "Assets/PanDulce/Art/Shell";
        public const string EffectsDir = "Assets/PanDulce/Art/Effects";
        public const string UiDir = "Assets/PanDulce/Art/UI";

        [MenuItem("Pan Dulce/Apply Sprite Import Settings")]
        public static void ApplyAll()
        {
            int n = Apply(PastryDir) + Apply(CustomerDir) + Apply(ShellDir) + Apply(EffectsDir)
                  + ApplyUi();
            Debug.Log($"[PanDulce] sprite import settings applied to {n} textures");
        }

        /// <summary>
        /// Pixels-per-unit for the 9-sliced chrome art, keyed by file name.
        ///
        /// A sliced SpriteRenderer draws its border at borderPx/PPU world units, so at the
        /// stock PPU 100 the Button's 73 px cap is 73 stage px tall — on a 46 px button the
        /// two caps overlap and the sprite collapses. These values shrink each drawing to
        /// roughly the rect it fills, which lands the corner radius where the drawing puts
        /// it. Unlisted files keep PPU 100, which is right for the unsliced chips.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, float> UiPixelsPerUnit =
            new System.Collections.Generic.Dictionary<string, float>
            {
                { "Button",        400f },   // faces, drawn 46 px tall
                { "Button 2",      800f },   // badges, drawn 21 px tall
                { "Next customer", 700f },   // the sign, drawn 140 × 103
                { "Next pastry",   500f },   // the NEXT plaque, drawn 70 × 60
            };

        /// <summary>
        /// The chrome art is hand-sliced in the Sprite Editor, so this deliberately leaves
        /// the import mode and the sprite sheet alone — it only fixes what slicing needs:
        /// a FullRect mesh (a tight mesh silently disables sliced drawing) and a PPU that
        /// keeps the borders inside the rect the sprite is drawn at.
        /// </summary>
        [MenuItem("Pan Dulce/Apply UI Sprite Import Settings")]
        public static int ApplyUi()
        {
            if (!Directory.Exists(UiDir)) return 0;
            int count = 0;

            foreach (string path in Directory.GetFiles(UiDir, "*.png", SearchOption.TopDirectoryOnly))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                float ppu = UiPixelsPerUnit.TryGetValue(
                    Path.GetFileNameWithoutExtension(assetPath), out float v) ? v : 100f;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool unchanged = Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                                 && settings.spriteMeshType == SpriteMeshType.FullRect
                                 && importer.textureType == TextureImporterType.Sprite
                                 && !importer.mipmapEnabled
                                 && importer.alphaIsTransparency;
                if (unchanged) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;

                // Applied through the settings block, not importer.spritePixelsPerUnit:
                // SetTextureSettings writes the whole block back and would restore the old
                // value it was read with.
                settings.spritePixelsPerUnit = ppu;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        public static int Apply(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int count = 0;

            foreach (string path in Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;          // 1 sim px = 0.01 world units
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
                    ps.maxTextureSize = 512;
                    ps.format = TextureImporterFormat.ASTC_6x6;
                    ps.compressionQuality = 100;
                    importer.SetPlatformTextureSettings(ps);
                }

                importer.SaveAndReimport();
                count++;
            }
            return count;
        }
    }
}
