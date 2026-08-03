using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Scales the stage root to fit the screen — the ONLY object in the game that scales (§5.1).
    ///
    /// The design frame is 430 × 880 inside a 446 × 900 safe box, an aspect of 0.495. Real
    /// phones are taller, so width is the limiting dimension and the slack appears as
    /// horizontal bars — which is why the backdrop bleeds beyond the frame.
    /// </summary>
    [ExecuteAlways]
    public sealed class StageFitter : MonoBehaviour
    {
        public static float CurrentScale { get; private set; } = 1f;

        int lastW, lastH;

        void OnEnable() { lastW = lastH = 0; Fit(); }

        void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            Fit();
        }

        void Fit()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            if (lastW <= 0 || lastH <= 0) return;

            float scale = Mathf.Min(lastH / StageCoords.SafeH, lastW / StageCoords.SafeW);
            if (scale <= 0f || float.IsNaN(scale)) scale = 1f;

            CurrentScale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);

            // Keep the stage centred: the frame's top-left sits at the stage root's origin.
            transform.localPosition = new Vector3(
                -StageCoords.StageW * StageCoords.PX * scale * 0.5f,
                 StageCoords.StageH * StageCoords.PX * scale * 0.5f, 0f);
        }
    }

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
