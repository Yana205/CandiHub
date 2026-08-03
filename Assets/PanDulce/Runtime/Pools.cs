using System.Collections.Generic;
using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Mirrors the sim's bodies onto pooled SpriteRenderers. Bodies churn constantly, so
    /// nothing is ever Instantiated or Destroyed during play (§3.6).
    /// </summary>
    public sealed class PastryViewPool : MonoBehaviour
    {
        [SerializeField] PastryDatabase database;
        [SerializeField] int prewarm = 48;
        [SerializeField] string sortingLayer = "PlayArea";
        [SerializeField] int sortingOrder = 30;

        readonly List<SpriteRenderer> views = new List<SpriteRenderer>(64);
        Material unlit;
        bool initialised;

        /// <summary>Pooled children are created at runtime so they never bloat Main.unity (§4).</summary>
        void Awake() => Init(database);

        public void Init(PastryDatabase db, Material material = null)
        {
            if (initialised) return;
            initialised = true;
            if (db != null) database = db;
            unlit = material != null ? material : SpriteMaterials.Unlit;
            for (int i = 0; i < prewarm; i++) Grow();
        }

        /// <summary>Editor-time wiring: records the database without creating pooled children.</summary>
        public void EditorAssign(PastryDatabase db) => database = db;

        SpriteRenderer Grow()
        {
            var go = new GameObject($"Pastry_{views.Count:00}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (unlit != null) sr.sharedMaterial = unlit;
            go.SetActive(false);
            views.Add(sr);
            return sr;
        }

        /// <summary>One frame of mirroring: position, rotation, squish scale, tier sprite.</summary>
        public void Sync(MergeSim sim, float sizeScale, int orderTier, Body hovered)
        {
            var bodies = sim.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i >= views.Count) Grow();
                Body b = bodies[i];
                SpriteRenderer sr = views[i];

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                sr.sprite = database != null ? database.Pastry(b.tier) : null;

                Vector2 stage = StageCoords.StageToSim(Vector2.zero);   // no-op guard
                sr.transform.localPosition = new Vector3(b.x * StageCoords.PX,
                                                         -b.y * StageCoords.PX, 0f);
                sr.transform.localEulerAngles = new Vector3(0f, 0f, StageCoords.RotationDegrees(b.rot));

                // Sprites are authored at radius 200, so scale is simply Er / 200.
                float er = TierTable.Er(b, sizeScale);
                float s = er / TierTable.CanonicalSpriteRadius;
                float highlight = (b == hovered) ? 1.14f : 1f;
                sr.transform.localScale = new Vector3((1f + b.squish * 0.6f) * s * highlight,
                                                       (1f - b.squish) * s * highlight, 1f);
                sr.color = Color.white;
            }

            for (int i = bodies.Count; i < views.Count; i++)
                if (views[i].gameObject.activeSelf) views[i].gameObject.SetActive(false);
        }

        public void HideAll()
        {
            for (int i = 0; i < views.Count; i++) views[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// The sim owns particle motion (not Unity's ParticleSystem), so this only mirrors it.
    /// Puffs are discs; sparks draw as a '+' built from two crossed quads.
    /// </summary>
    public sealed class ParticleViewPool : MonoBehaviour
    {
        [SerializeField] int prewarm = 120;
        [SerializeField] string sortingLayer = "PlayArea";
        [SerializeField] int sortingOrder = 40;

        readonly List<SpriteRenderer> puffs = new List<SpriteRenderer>(160);
        Material unlit;
        Sprite disc, bar;
        Color puffColor, sparkColor;
        bool initialised;

        /// <summary>Pooled children are created at runtime so they never bloat Main.unity (§4).</summary>
        void Awake() => Init();

        public void Init(Material material = null)
        {
            if (initialised) return;
            initialised = true;
            unlit = material != null ? material : SpriteMaterials.Unlit;
            disc = Shapes.Circle(32);
            bar = Shapes.RoundedRect(24, 6, 3);
            puffColor = Palette.Hex("#fff3dd");
            sparkColor = Palette.Hex("#f0b64f");
            for (int i = 0; i < prewarm; i++) Grow();
        }

        SpriteRenderer Grow()
        {
            var go = new GameObject($"Particle_{puffs.Count:000}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = sortingOrder;
            if (unlit != null) sr.sharedMaterial = unlit;
            go.SetActive(false);
            puffs.Add(sr);
            return sr;
        }

        public void Sync(MergeSim sim)
        {
            var parts = sim.Particles;
            for (int i = 0; i < parts.Count; i++)
            {
                if (i >= puffs.Count) Grow();
                Particle p = parts[i];
                SpriteRenderer sr = puffs[i];
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);

                float k = 1f - Mathf.Clamp01(p.t / Mathf.Max(0.0001f, p.life));
                sr.sprite = p.star ? bar : disc;
                sr.transform.localPosition = new Vector3(p.x * StageCoords.PX, -p.y * StageCoords.PX, 0f);

                if (p.star)
                {
                    float arm = p.r * (0.5f + k) * 2f;
                    sr.transform.localScale = new Vector3(arm / 24f * 100f * StageCoords.PX,
                                                           p.r * 0.5f / 6f * 100f * StageCoords.PX, 1f);
                    sr.transform.localEulerAngles = new Vector3(0, 0, 45f);
                    sr.color = Palette.WithAlpha(sparkColor, k);
                }
                else
                {
                    float d = p.r * 2f;
                    sr.transform.localScale = Vector3.one * (d / 32f * 100f * StageCoords.PX);
                    sr.transform.localEulerAngles = Vector3.zero;
                    sr.color = Palette.WithAlpha(puffColor, k);
                }
            }

            for (int i = parts.Count; i < puffs.Count; i++)
                if (puffs[i].gameObject.activeSelf) puffs[i].gameObject.SetActive(false);
        }
    }

    /// <summary>Pooled TMP labels for "Combo 3!", "New in the case!", "Shake!".</summary>
    public sealed class FloatingTextPool : MonoBehaviour
    {
        [SerializeField] int prewarm = 8;
        [SerializeField] string sortingLayer = "PlayArea";
        [SerializeField] int sortingOrder = 50;

        readonly List<TextMeshPro> labels = new List<TextMeshPro>(16);
        bool initialised;

        /// <summary>Pooled children are created at runtime so they never bloat Main.unity (§4).</summary>
        void Awake() => Init();

        public void Init()
        {
            if (initialised) return;
            initialised = true;
            for (int i = 0; i < prewarm; i++) Grow();
        }

        TextMeshPro Grow()
        {
            var go = new GameObject($"Float_{labels.Count:00}");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 2.4f;
            t.fontStyle = FontStyles.Bold;
            t.color = Palette.Cream;
            // Outline is set at runtime only: TMP's outlineWidth setter reaches through
            // renderer.material, which leaks an instanced material if touched in edit mode.
            if (Application.isPlaying)
            {
                t.outlineWidth = 0.22f;
                t.outlineColor = Palette.Crust;
            }
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = sortingLayer;
            mr.sortingOrder = sortingOrder;
            go.SetActive(false);
            labels.Add(t);
            return t;
        }

        public void Sync(MergeSim sim)
        {
            var floats = sim.Floats;
            for (int i = 0; i < floats.Count; i++)
            {
                if (i >= labels.Count) Grow();
                FloatText f = floats[i];
                TextMeshPro t = labels[i];
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                if (t.text != f.text) t.text = f.text;
                t.transform.localPosition = new Vector3(f.x * StageCoords.PX, -f.y * StageCoords.PX, 0f);
                t.alpha = Mathf.Min(1f, 2f * (1f - f.t));
            }

            for (int i = floats.Count; i < labels.Count; i++)
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
        }
    }
}
