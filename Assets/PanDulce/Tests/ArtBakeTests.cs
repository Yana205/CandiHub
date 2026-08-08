using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace PanDulce.Tests
{
    /// <summary>
    /// Guards the bake's size normalization: every baked dessert must occupy the same
    /// perceived mass — the radius of a circle with its opaque-pixel area — so no tier
    /// ever reads bigger than its physics circle says it is (the roll cake bug, 2026-08-08).
    /// Reads the PNGs off disk, so import settings can't hide a bad bake.
    /// </summary>
    public class ArtBakeTests
    {
        const string PastryDir = "Assets/PanDulce/Art/Pastries";
        const float TargetEffR = 181f;
        const float Tolerance = 9f;      // ±5% — resampling and alpha edges eat a pixel or two

        [Test]
        public void EveryBakedDessert_ReadsTheSameSize()
        {
            string[] files = Directory.GetFiles(PastryDir, "pastry_*.png");
            Assert.That(files, Is.Not.Empty, "no baked desserts found — run Pan Dulce ▸ Import V2 Art");

            foreach (string path in files)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                long area = 0;
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                    if (px[i].a > 8) area++;
                float effR = Mathf.Sqrt(area / Mathf.PI);
                Object.DestroyImmediate(tex);

                Assert.That(effR, Is.EqualTo(TargetEffR).Within(Tolerance),
                            $"{Path.GetFileName(path)} bakes at effective radius {effR:F1} — " +
                            "rerun Pan Dulce ▸ Rebake Desserts");
            }
        }
    }
}
