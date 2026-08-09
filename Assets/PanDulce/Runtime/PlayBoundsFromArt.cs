using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Derives the sim's play bounds from the drawn candy box instead of hand-typed numbers.
    ///
    /// The walls and the floor have to sit exactly on the box the player sees, and until now
    /// that agreement was maintained by eye: measure the art off a screenshot, type
    /// wallLeft/wallRight/floorY into Tuning, repeat every time the box moved or resized.
    /// This component closes the loop — it reads the box renderer's own bounds and writes the
    /// three numbers, so scaling or nudging the box art drags the play area with it and the
    /// PlayAreaGuide's pink box line lands flush on the painted edge with no further work.
    ///
    /// The interior fractions below are the one thing measured by hand, and they only ever
    /// need re-measuring if the box is REDRAWN — they are positions within the artwork, so
    /// they survive any transform. Everything downstream is unchanged: Tuning is still the
    /// single source of truth that MergeSim, AimGuideView and PlayAreaGuide read.
    ///
    /// Edit-mode only. Play mode reads the values already written into the asset, so a run
    /// can never be perturbed by the art.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayBoundsFromArt : MonoBehaviour
    {
        [Tooltip("The asset the derived wallLeft / wallRight / floorY are written into.")]
        [SerializeField] TuningConfig tuning;

        [Tooltip("Uncheck to freeze the current Tuning numbers and move the box art freely.")]
        [SerializeField] bool drivePlayBounds = true;

        [Header("Box interior, as a fraction of the sprite")]
        // Measured off candybox.png (1376 × 2560): the box's outline stroke runs x 200–208 on
        // the left, 1162–1173 on the right, and y 2129–2136 along the bottom, so the interior
        // face the desserts rest against is x 209 … 1161 and y 2128.
        [Range(0f, 1f)] [SerializeField] float interiorLeft = 209f / 1376f;
        [Range(0f, 1f)] [SerializeField] float interiorRight = 1161f / 1376f;
        [Tooltip("Measured DOWN from the top of the sprite, like every other stage coordinate.")]
        [Range(0f, 1f)] [SerializeField] float interiorFloor = 2128f / 2560f;

        SpriteRenderer box;
        Vector3 lastPos;
        Vector3 lastScale;

        void OnEnable()
        {
            box = GetComponent<SpriteRenderer>();
            lastPos = Vector3.positiveInfinity;
            Apply();
        }

        void OnValidate() { lastPos = Vector3.positiveInfinity; }

        void Update()
        {
            if (Application.isPlaying) return;
            var p = transform.position;
            var s = transform.lossyScale;
            if (p == lastPos && s == lastScale) return;
            lastPos = p;
            lastScale = s;
            Apply();
        }

        /// <summary>The three sim-space numbers this box implies, or false if it cannot tell.</summary>
        public bool TryResolve(out float wallLeft, out float wallRight, out float floorY)
        {
            wallLeft = wallRight = floorY = 0f;
            if (box == null) box = GetComponent<SpriteRenderer>();
            if (box == null || box.sprite == null) return false;

            // Stage-local is the only space where the numbers mean anything: the fitter moves
            // and the camera resizes, but 1 stage px stays 0.01 units under the stage root.
            var fitter = GetComponentInParent<StageFitter>();
            if (fitter == null) return false;   // prefab edit stage — nothing to write against
            var stage = fitter.transform;

            var b = box.bounds;
            Vector3 lo = stage.InverseTransformPoint(new Vector3(b.min.x, b.min.y, 0f));
            Vector3 hi = stage.InverseTransformPoint(new Vector3(b.max.x, b.max.y, 0f));

            // Stage px are y-DOWN, so the sprite's world min.y is its largest stage y.
            float xL = lo.x / StageCoords.PX, xR = hi.x / StageCoords.PX;
            float yT = -hi.y / StageCoords.PX, yB = -lo.y / StageCoords.PX;
            float w = xR - xL, h = yB - yT;
            if (w <= 0f || h <= 0f) return false;

            wallLeft = xL + w * interiorLeft - StageCoords.PlayOriginX;
            wallRight = xL + w * interiorRight - StageCoords.PlayOriginX;
            floorY = yT + h * interiorFloor - StageCoords.PlayOriginY;
            return true;
        }

        /// <summary>Editor-time wiring, from StageBuilder.</summary>
        public void EditorAssign(TuningConfig cfg) { tuning = cfg; lastPos = Vector3.positiveInfinity; Apply(); }

        [ContextMenu("Snap play bounds to this box")]
        public void Apply()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || !drivePlayBounds || tuning == null) return;
            if (!TryResolve(out float wl, out float wr, out float fy)) return;

            var d = tuning.Data;
            if (Mathf.Approximately(d.wallLeft, wl) &&
                Mathf.Approximately(d.wallRight, wr) &&
                Mathf.Approximately(d.floorY, fy)) return;

            d.wallLeft = wl;
            d.wallRight = wr;
            d.floorY = fy;
            UnityEditor.EditorUtility.SetDirty(tuning);
#endif
        }
    }
}
