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
}
