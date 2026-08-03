using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Fits the 430 × 880 design frame onto the screen by widening the camera (§5.1).
    ///
    /// The design frame sits inside a 446 × 900 safe box, an aspect of 0.495. Real phones are
    /// taller, so width is the limiting dimension and the slack appears as horizontal bars —
    /// which is why the backdrop bleeds beyond the frame.
    ///
    /// The stage itself never scales. It is authored at 1 stage px = 0.01 world units and
    /// stays there; the orthographic camera is what adapts. Scaling the stage transform
    /// instead double-applies the stage → screen mapping the camera already performs, because
    /// a screen-px/stage-px ratio is not a world-space scale factor. That was a real bug: with
    /// the camera pinned at size 4.4 it showed a fixed 880 stage px, so on an iPhone 15 the
    /// 2.62 ratio cropped away 62% of the stage.
    /// </summary>
    [ExecuteAlways]
    public sealed class StageFitter : MonoBehaviour
    {
        /// <summary>
        /// Screen px per stage px. <see cref="SafeAreaInset"/> divides by this to convert
        /// <c>Screen.safeArea</c> into stage px, which is the ratio's honest meaning.
        /// </summary>
        public static float CurrentScale { get; private set; } = 1f;

        [SerializeField] Camera targetCamera;

        int lastW, lastH;

        /// <summary>Editor-time wiring, so the reference is committed rather than resolved
        /// by tag lookup on first frame.</summary>
        public void EditorAssign(Camera cam) { targetCamera = cam; Fit(); }

        void OnEnable() { lastW = lastH = 0; Fit(); }

        void Update()
        {
            ResolveViewport(out int w, out int h);
            if (w == lastW && h == lastH) return;
            Fit();
        }

        void Fit()
        {
            ResolveViewport(out lastW, out lastH);
            if (lastW <= 0 || lastH <= 0) return;

            CurrentScale = StageFit.ScreenPxPerStagePx(lastW, lastH);

            // Fixed placement: the frame's top-left sits at the stage root's origin, so the
            // 430 × 880 frame lands centred on a camera positioned at x=0, y=0.
            transform.localScale = Vector3.one;
            transform.localPosition = new Vector3(
                -StageCoords.StageW * StageCoords.PX * 0.5f,
                 StageCoords.StageH * StageCoords.PX * 0.5f, 0f);

            var cam = ResolveCamera();
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = StageFit.OrthographicSize(lastW, lastH);
        }

        Camera ResolveCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            return targetCamera;
        }

        /// <summary>
        /// The dimensions to fit against. In a build this is simply the screen.
        ///
        /// Not so in the Editor: outside play mode <c>Screen.width/height</c> report whichever
        /// EditorWindow is currently repainting, not the Game view. That is how a 578 × 956
        /// inspector panel once produced the committed 1.0622 stage scale. Ask the Game view
        /// directly instead, and fall back to Screen if the internal call ever disappears.
        /// </summary>
        static void ResolveViewport(out int w, out int h)
        {
            w = Screen.width;
            h = Screen.height;
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            if (TryGetGameViewSize(out int gw, out int gh)) { w = gw; h = gh; }
#endif
        }

#if UNITY_EDITOR
        static System.Reflection.MethodInfo gameViewSizeMethod;
        static bool gameViewSizeResolved;

        static bool TryGetGameViewSize(out int w, out int h)
        {
            w = h = 0;

            if (!gameViewSizeResolved)
            {
                gameViewSizeResolved = true;
                gameViewSizeMethod = typeof(UnityEditor.Handles).GetMethod(
                    "GetMainGameViewSize",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
            }

            if (gameViewSizeMethod == null) return false;

            var size = (Vector2)gameViewSizeMethod.Invoke(null, null);
            w = Mathf.RoundToInt(size.x);
            h = Mathf.RoundToInt(size.y);
            return w > 0 && h > 0;
        }
#endif
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
