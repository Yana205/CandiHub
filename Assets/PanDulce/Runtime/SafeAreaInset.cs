using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Pushes a chrome bar clear of the notch or the gesture bar (§5.2).
    ///
    /// Runs on the TopBar and BoostBar nodes, never on the stage root — the play area stays
    /// centred in the frame; only the two bars move.
    /// </summary>
    [ExecuteAlways]
    public sealed class SafeAreaInset : MonoBehaviour
    {
        public enum Edge { Top, Bottom }

        [SerializeField] Edge edge = Edge.Top;

        /// <summary>A floor keeps the shake button clear even where no inset is reported.</summary>
        [SerializeField] float minimumStagePx = 24f;

        Vector3 basePosition;
        bool captured;
        Rect lastSafe;
        int lastW, lastH;

        void OnEnable()
        {
            if (!captured) { basePosition = transform.localPosition; captured = true; }
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea == lastSafe && Screen.width == lastW && Screen.height == lastH) return;
            Apply();
        }

        void Apply()
        {
            lastSafe = Screen.safeArea;
            lastW = Screen.width;
            lastH = Screen.height;
            if (!captured) { basePosition = transform.localPosition; captured = true; }

            float scale = Mathf.Max(0.0001f, StageFitter.CurrentScale);
            Rect safe = Screen.safeArea;

            // Converted to stage px using the FINAL fitter scale, per §5.2.
            float insetPx = edge == Edge.Top
                ? (Screen.height - (safe.y + safe.height)) / scale
                : safe.y / scale;

            insetPx = Mathf.Max(insetPx, edge == Edge.Bottom ? minimumStagePx : 0f);

            float dy = insetPx * StageCoords.PX;
            transform.localPosition = basePosition + new Vector3(0f, edge == Edge.Top ? -dy : dy, 0f);
        }
    }
}
