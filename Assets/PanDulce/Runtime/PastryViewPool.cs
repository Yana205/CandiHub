using System.Collections.Generic;
using PanDulce.Core;
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
        readonly List<SpriteRenderer> rings = new List<SpriteRenderer>(64);
        Material unlit;
        bool initialised;

        // Ring sprite is baked at diameter 220 (radius 110 texture px, PPU 100).
        const float RingBakedRadius = 110f;

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

            var ringGo = new GameObject($"OrderRing_{rings.Count:00}");
            ringGo.transform.SetParent(transform, false);
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = Shapes.Ring(220, 10);
            ring.color = new Color(1f, 1f, 1f, 0.85f);
            ring.sortingLayerName = sortingLayer;
            ring.sortingOrder = sortingOrder - 1;   // just behind its pastry, so it coats the edge
            if (unlit != null) ring.sharedMaterial = unlit;
            ringGo.SetActive(false);
            rings.Add(ring);
            return sr;
        }

        /// <summary>One frame of mirroring: position, rotation, squish scale, tier sprite.
        /// <paramref name="holdTarget"/>/<paramref name="holdK"/> is the serve press in
        /// progress — that dessert swells with the hold so "this is about to fly" is
        /// unmistakable before it commits.</summary>
        public void Sync(MergeSim sim, float sizeScale, int orderTier, Body hovered,
                         Body holdTarget = null, float holdK = 0f)
        {
            var bodies = sim.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                if (i >= views.Count) Grow();
                Body b = bodies[i];
                SpriteRenderer sr = views[i];

                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                sr.sprite = database != null ? database.Pastry(b.tier, b.skin) : null;

                sr.transform.localPosition = new Vector3(b.x * StageCoords.PX,
                                                         -b.y * StageCoords.PX, 0f);
                sr.transform.localEulerAngles = new Vector3(0f, 0f, StageCoords.RotationDegrees(b.rot));

                // Sprites are authored at radius 200, so scale is simply Er / 200. The
                // per-dessert size is already inside er — multiplying it in again here is what
                // used to let the drawn size drift away from the circle the sim collides with.
                float er = TierTable.Er(b, sizeScale,
                                        database != null ? database.TierSize(b.tier) : 1f);
                float s = er / TierTable.CanonicalSpriteRadius;
                // Hover reads "you can hand this over"; the hold swell reads "it is being
                // handed over" — ease-out so most of the growth lands early in the press.
                float highlight = b == holdTarget ? 1.2f + 0.25f * (1f - (1f - holdK) * (1f - holdK))
                                : b == hovered    ? 1.2f : 1f;
                sr.transform.localScale = new Vector3((1f + b.squish * 0.6f) * s * highlight,
                                                       (1f - b.squish) * s * highlight, 1f);
                sr.color = Color.white;

                // The wanted dessert wears a pulsing ring until it is tapped (§7.6).
                SpriteRenderer ring = rings[i];
                if (orderTier >= 0 && b.tier == orderTier)
                {
                    if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
                    ring.transform.localPosition = sr.transform.localPosition;
                    float pulse = 1.18f + 0.08f * Mathf.Sin(Time.time * (2f * Mathf.PI / 0.9f));
                    float rs = er * pulse / RingBakedRadius;   // hugs the dessert, whatever size it is
                    ring.transform.localScale = new Vector3(rs, rs, 1f);
                }
                else if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
            }

            for (int i = bodies.Count; i < views.Count; i++)
            {
                if (views[i].gameObject.activeSelf) views[i].gameObject.SetActive(false);
                if (rings[i].gameObject.activeSelf) rings[i].gameObject.SetActive(false);
            }
        }

        public void HideAll()
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i].gameObject.SetActive(false);
                rings[i].gameObject.SetActive(false);
            }
        }
    }
}
