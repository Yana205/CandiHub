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

        [MenuItem("Pan Dulce/Apply Sprite Import Settings")]
        public static void ApplyAll()
        {
            int n = Apply(PastryDir) + Apply(CustomerDir);
            Debug.Log($"[PanDulce] sprite import settings applied to {n} textures");
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
