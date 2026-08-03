using System.Collections.Generic;
using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
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
