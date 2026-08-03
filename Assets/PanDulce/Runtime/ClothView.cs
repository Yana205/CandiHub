using System.Collections.Generic;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Draws the furoshiki as a procedural mesh from the drape curve in §8.5.
    ///
    /// Every shade is derived from clothColor through Palette.Mix — none are hard-coded —
    /// which is what lets all four swatches shade correctly. The sag the pastries actually
    /// collide with lives in Core (SimField.FloorAt); this is only its visual counterpart.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ClothView : MonoBehaviour
    {
        const int SamplesPerSegment = 20;
        const float BottomY = 440f;     // sim px, below the hem so the drape reads as fabric

        [SerializeField] Color clothColor = new Color(0.81f, 0.42f, 0.36f);

        MeshFilter filter;
        MeshRenderer meshRenderer;
        Mesh mesh;
        LineRenderer outline;
        SpriteRenderer hemBand, hemLine, stitch, dots;
        Color lastColor;
        readonly List<Vector2> top = new List<Vector2>(128);

        public Color ClothColor
        {
            get => clothColor;
            set { clothColor = value; Repaint(); }
        }

        // [ExecuteAlways] so the drape is visible for layout work in the Editor, not just in
        // play mode. Everything it creates is DontSave and rebuilt on every enable, so the
        // scene file never accumulates generated children or references a runtime mesh.
        void OnEnable() => Build();

        public void Build()
        {
            ClearGenerated();
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            mesh = new Mesh { name = "Furoshiki" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            // The drape is a mesh, so it needs its own material instance with a white base
            // map — but the same unlit shader as everything else (trap 2).
            var mat = new Material(SpriteMaterials.Unlit) { name = "ClothUnlit" };
            mat.mainTexture = Texture2D.whiteTexture;
            meshRenderer.sharedMaterial = mat;
            meshRenderer.sortingLayerName = "PlayArea";
            meshRenderer.sortingOrder = 10;

            outline = GetComponent<LineRenderer>();
            if (outline == null) outline = gameObject.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.widthMultiplier = 2.5f * StageCoords.PX;
            outline.numCornerVertices = 4;
            outline.numCapVertices = 4;
            outline.material = mat;
            outline.sortingLayerName = "PlayArea";
            outline.sortingOrder = 11;
            outline.textureMode = LineTextureMode.Stretch;

            hemBand = MakeSprite("HemBand", Shapes.White, 60);
            hemLine = MakeSprite("HemLine", Shapes.White, 61);
            stitch = MakeSprite("HemStitch", Shapes.Dashes(9, 8, 2), 62);
            dots = MakeSprite("ClothDots", DotTexture(), 12);

            BuildGeometry();
            Repaint();
        }

        /// <summary>Drops generated children so the scene can be saved without them.</summary>
        public void ClearForSave() => ClearGenerated();

        /// <summary>Removes previously generated children so a rebuild cannot duplicate them.</summary>
        void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingLayerName = "PlayArea";
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A 42 × 42 dot cell — the polka pattern from §8.5 step 3, tiled over the cloth.</summary>
        static Sprite DotTexture()
        {
            const int cell = 42;
            var tex = new Texture2D(cell, cell, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[cell * cell];
            // Two dots per cell, the second offset half a cell — that is the "alternate rows
            // offset 21" rule expressed as a single tileable texture.
            AddDot(px, cell, 10.5f, 10.5f, 4f);
            AddDot(px, cell, 31.5f, 31.5f, 4f);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, cell, cell), new Vector2(0.5f, 0.5f),
                                  Shapes.PPU, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static void AddDot(Color32[] px, int cell, float cx, float cy, float r)
        {
            for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                if (a <= 0f) continue;
                int i = y * cell + x;
                if (px[i].a >= a * 255f) continue;
                px[i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        // ---------------------------------------------------------------- geometry

        void BuildGeometry()
        {
            const float BL = SimField.BL, BR = SimField.BR, cx = SimField.CX;

            top.Clear();
            Vector2 p0 = new Vector2(BL - 16f, 40f);
            top.Add(p0);
            p0 = Quad(p0, new Vector2(BL + 4f, 88f), new Vector2(BL + 36f, 100f));
            p0 = Quad(p0, new Vector2(cx - 62f, 124f), new Vector2(cx, 110f));
            p0 = Quad(p0, new Vector2(cx + 62f, 124f), new Vector2(BR - 36f, 100f));
            Quad(p0, new Vector2(BR - 4f, 88f), new Vector2(BR + 16f, 40f));

            int n = top.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];

            for (int i = 0; i < n; i++)
            {
                Vector2 t = top[i];
                verts[i] = new Vector3(t.x * StageCoords.PX, -t.y * StageCoords.PX, 0f);
                verts[i + n] = new Vector3(t.x * StageCoords.PX, -BottomY * StageCoords.PX, 0f);
                uvs[i] = new Vector2(i / (float)(n - 1), 1f);
                uvs[i + n] = new Vector2(i / (float)(n - 1), 0f);
            }

            for (int i = 0, k = 0; i < n - 1; i++)
            {
                tris[k++] = i;      tris[k++] = i + n;     tris[k++] = i + 1;
                tris[k++] = i + 1;  tris[k++] = i + n;     tris[k++] = i + n + 1;
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            outline.positionCount = n;
            for (int i = 0; i < n; i++) outline.SetPosition(i, verts[i]);
        }

        /// <summary>Samples a quadratic bezier into `top`, returning its end point.</summary>
        Vector2 Quad(Vector2 a, Vector2 ctrl, Vector2 end)
        {
            for (int i = 1; i <= SamplesPerSegment; i++)
            {
                float t = i / (float)SamplesPerSegment;
                float u = 1f - t;
                top.Add(u * u * a + 2f * u * t * ctrl + t * t * end);
            }
            return end;
        }

        // ---------------------------------------------------------------- colour

        void Repaint()
        {
            if (mesh == null) return;

            var colors = new Color[mesh.vertexCount];
            // Slight vertical shading keeps the drape from reading flat, still derived from C.
            //
            // .linear is required here and NOWHERE else: SpriteRenderer.color is a gamma-space
            // property Unity converts on assignment, but mesh vertex colours are uploaded raw.
            // Without it the drape renders visibly washed out against its own hem.
            Color topC = Palette.Mix(clothColor, 1.02f).linear;
            Color botC = Palette.Mix(clothColor, 0.86f).linear;
            int half = mesh.vertexCount / 2;
            for (int i = 0; i < half; i++) { colors[i] = topC; colors[i + half] = botC; }
            mesh.colors = colors;

            Color stroke = Palette.Mix(clothColor, 0.72f);
            outline.startColor = outline.endColor = stroke;

            // Hem: solid band from y 372 down, a darker line at 373, a stitch at 386 (§8.5.6).
            PlaceRect(hemBand, SimField.BL, 372f, SimField.BR - SimField.BL, BottomY - 372f);
            hemBand.color = Palette.Mix(clothColor, 0.88f);

            PlaceRect(hemLine, SimField.BL, 373f, SimField.BR - SimField.BL, 2.5f);
            hemLine.color = Palette.Mix(clothColor, 0.68f);

            PlaceRect(stitch, SimField.BL + 8f, 386f, SimField.BR - SimField.BL - 16f, 2f);
            stitch.color = Palette.WithAlpha(Palette.Hex("#fff6e8"), 0.5f);

            PlaceRect(dots, SimField.BL + 6f, 140f, SimField.BR - SimField.BL - 12f, 232f);
            // Tiled + Continuous repeats the 42×42 dot cell; Sliced would stretch it into blobs.
            dots.drawMode = SpriteDrawMode.Tiled;
            dots.tileMode = SpriteTileMode.Continuous;
            dots.size = new Vector2((SimField.BR - SimField.BL - 12f) * StageCoords.PX,
                                     232f * StageCoords.PX);
            dots.color = Palette.WithAlpha(Palette.Hex("#fff6e8"), 0.34f);

            lastColor = clothColor;
        }

        /// <summary>Positions a sprite over a stage-space rect given in sim px, y-down.</summary>
        static void PlaceRect(SpriteRenderer sr, float x, float y, float w, float h)
        {
            if (sr == null || sr.sprite == null) return;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w * StageCoords.PX, h * StageCoords.PX);
            sr.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                      -(y + h * 0.5f) * StageCoords.PX, 0f);
        }

        void LateUpdate()
        {
            if (clothColor != lastColor) Repaint();
        }
    }
}
