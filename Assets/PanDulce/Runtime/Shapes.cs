using System.Collections.Generic;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Generated primitive sprites for the shell — panels, chips, bars, the danger line.
    ///
    /// §8.2–8.4 describe the shell almost entirely as rounded rects with gradients and
    /// borders, which are trivial to rasterise in C#. Generating them keeps the skeleton
    /// free of hand-authored placeholder art that would only be thrown away, and every
    /// element stays tintable via SpriteRenderer.color.
    ///
    /// Textures are cached by key, so a hundred panels share one texture and one draw call.
    /// </summary>
    public static class Shapes
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>All generated sprites use PPU 100 so 1 texture px = 1 stage px.</summary>
        public const float PPU = 100f;

        public static Sprite White => RoundedRect(8, 8, 0);

        public static Sprite RoundedRect(int w, int h, int radius, int border = 0)
        {
            string key = $"rr:{w}x{h}:{radius}:{border}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            w = Mathf.Max(1, w); h = Mathf.Max(1, h);
            radius = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);

            var tex = NewTexture(w, h);
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = RoundedCoverage(x, y, w, h, radius);
                bool edge = border > 0 && RoundedCoverage(x, y, w, h, radius, border) < 0.5f;
                // Border is encoded in alpha-1 space: callers tint fill and border separately
                // by using two stacked sprites, so here we only need the silhouette.
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                if (edge && a > 0.5f) px[y * w + x] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Store(key, tex);
        }

        /// <summary>Antialiased coverage of a rounded rect, optionally inset.</summary>
        static float RoundedCoverage(int x, int y, int w, int h, int radius, int inset = 0)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float l = inset, t = inset, r = w - inset, b = h - inset;
            if (fx < l || fx > r || fy < t || fy > b) return 0f;

            float rad = Mathf.Max(0, radius - inset);
            if (rad <= 0f) return 1f;

            float cx = Mathf.Clamp(fx, l + rad, r - rad);
            float cy = Mathf.Clamp(fy, t + rad, b - rad);
            float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
            return Mathf.Clamp01(rad - d + 0.5f);
        }

        public static Sprite Circle(int diameter)
        {
            string key = $"circle:{diameter}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int d = Mathf.Max(2, diameter);
            var tex = NewTexture(d, d);
            var px = new Color32[d * d];
            float r = d * 0.5f;
            for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - dist + 0.5f);
                px[y * d + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Store(key, tex);
        }

        /// <summary>A vertical two-stop gradient, tinted white→black so callers can multiply.</summary>
        public static Sprite VerticalGradient(int h, float topLuma, float bottomLuma)
        {
            string key = $"vg:{h}:{topLuma:F2}:{bottomLuma:F2}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            h = Mathf.Max(2, h);
            var tex = NewTexture(1, h);
            var px = new Color32[h];
            for (int y = 0; y < h; y++)
            {
                // texture y is bottom-up; stage y is top-down
                float k = 1f - (y / (float)(h - 1));
                float l = Mathf.Lerp(topLuma, bottomLuma, k);
                byte v = (byte)(Mathf.Clamp01(l) * 255f);
                px[y] = new Color32(v, v, v, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Store(key, tex);
        }

        /// <summary>White with alpha fading topA → bottomA — for glass and glare.</summary>
        public static Sprite VerticalAlphaGradient(int h, float topA, float bottomA)
        {
            string key = $"vag:{h}:{topA:F2}:{bottomA:F2}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            h = Mathf.Max(2, h);
            var tex = NewTexture(1, h);
            var px = new Color32[h];
            for (int y = 0; y < h; y++)
            {
                float k = 1f - (y / (float)(h - 1));           // texture y is bottom-up
                px[y] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Mathf.Lerp(topA, bottomA, k)) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Store(key, tex);
        }

        /// <summary>A horizontal dashed line, for the aim guide and the danger line.</summary>
        public static Sprite Dashes(int dash, int gap, int thickness)
        {
            string key = $"dash:{dash}:{gap}:{thickness}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int w = Mathf.Max(2, dash + gap);
            int h = Mathf.Max(1, thickness);
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(x < dash ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var tx = tex;
            tx.wrapModeU = TextureWrapMode.Repeat;
            return Store(key, tx);
        }

        /// <summary>Vertical plank seams: a `line`-px stripe at the left of each `period`-px cell.</summary>
        public static Sprite VerticalStripes(int period, int line)
        {
            string key = $"vs:{period}:{line}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int w = Mathf.Max(2, period);
            var tex = NewTexture(w, 4);
            var px = new Color32[w * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(x < line ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply(false, false);
            tex.wrapModeU = TextureWrapMode.Repeat;
            return Store(key, tex);
        }

        static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return tex;
        }

        static Sprite Store(string key, Texture2D tex)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                       new Vector2(0.5f, 0.5f), PPU, 0,
                                       SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Domain reload is off, so the cache survives play sessions and would leak.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();
    }
}
