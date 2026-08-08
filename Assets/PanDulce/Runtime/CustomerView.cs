using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The bear, waddling in from the left behind the counter.</summary>
    public sealed class CustomerView : GeneratedView
    {
        /// <summary>Bear sprite pivot is centred; the anchor is bottom-centre, so the sprite
        /// sits half its rendered height (100 stage px) above it.</summary>
        public const float SpriteLift = 100f;

        // Entrance walk. StartX puts the bear's leading edge past the widest letterbox the
        // fitter can show (left visible edge bottoms out at -122, half the bear is 115).
        const float StartX = -240f;
        const float StepPx = 65f;      // stride length → hop cadence
        const float HopPx = 9f;        // hop height at mid-walk
        const float WaddleDeg = 4f;    // side-to-side tilt per step

        // Where the bear stands at the counter, in stage px. Serialized so the scene owns it:
        // edit it in the Inspector / Studio window, or drag the bear in the Scene view (the
        // editor folds the drag back into this field on save / play). The walk animation
        // reads it live, so play mode always lands on the authored spot.
        [SerializeField] Vector2 anchor = new Vector2(215f, 355f);

        // Edit-mode only: show the real generated bear standing at the anchor, so the Scene
        // and Game views preview exactly what play mode will render. Ignored during play.
        [SerializeField] bool editorPreview;

        public Vector2 Anchor => anchor;
        public bool EditorPreviewOn => editorPreview;

        Transform bearAnchor;
        SpriteRenderer bear;
        float entranceStart = -1f, happyStart = -1f, duration = 0.7f;
        bool present;

        protected override void Build()
        {
            entranceStart = happyStart = -1f;
            present = false;
            var t = Content;

            bearAnchor = ViewFactory.Node(t, "BearAnchor", anchor.x, anchor.y).transform;
            var go = new GameObject("Bear") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(bearAnchor, false);
            bear = go.AddComponent<SpriteRenderer>();
            bear.sharedMaterial = SpriteMaterials.Unlit;
            bear.sortingLayerName = "Customer";
            bear.sortingOrder = 0;
            if (database != null) bear.sprite = database.Customer(0);
            // Baked at 2x with PPU 100 → world scale 0.5 renders 230×200 stage px. Centre
            // pivot → lift half the RENDERED height (100 stage px) so the anchor is
            // bottom-centre; the lift is in the anchor's space, unaffected by the child scale.
            go.transform.localScale = Vector3.one * 0.5f;
            go.transform.localPosition = new Vector3(0f, SpriteLift * StageCoords.PX, 0f);
            go.SetActive(!Application.isPlaying && editorPreview);
        }

        public void Arrive(float entranceTime)
        {
            if (!IsBuilt) return;
            present = true;
            bear.gameObject.SetActive(true);
            duration = Mathf.Max(0.05f, entranceTime);
            entranceStart = Time.time;
            happyStart = -1f;
            // Our Update may not run again this frame — never flash a centred bear.
            bearAnchor.localPosition = StageCoords.Stage(StartX, anchor.y);
        }

        public void Celebrate()
        {
            // A serve can land mid-walk; finish the entrance so the bounce plays at the counter.
            entranceStart = -1f;
            happyStart = Time.time;
        }

        public void Leave()
        {
            present = false;
            entranceStart = -1f;
            happyStart = -1f;
            if (bear != null) bear.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsBuilt || bearAnchor == null || !present) return;

            if (happyStart >= 0f)
            {
                // happy bounce, played twice over 0.45 s each
                float k = (Time.time - happyStart) / 0.45f;
                if (k <= 2f)
                {
                    float p = Mathf.Repeat(k, 1f);
                    float s = Mathf.Sin(p * Mathf.PI);
                    bearAnchor.localPosition = Base() + new Vector3(0f, 14f * s * StageCoords.PX, 0f);
                    bearAnchor.localScale = new Vector3(1f + 0.02f * s, 1f - 0.02f * s, 1f);
                    bearAnchor.localRotation = Quaternion.identity;
                    return;
                }
                happyStart = -1f;
            }

            if (entranceStart < 0f) return;
            float t = Mathf.Clamp01((Time.time - entranceStart) / duration);
            if (t >= 1f)
            {
                entranceStart = -1f;
                bearAnchor.localPosition = Base();
                bearAnchor.localRotation = Quaternion.identity;
                bearAnchor.localScale = Vector3.one;
                return;
            }

            // Waddle: glide eases in/out; an integer step count means the hop and tilt both
            // land on zero exactly at t=1, and the envelope keeps the first/last steps small.
            float x = Mathf.SmoothStep(StartX, anchor.x, t);
            int steps = Mathf.Max(3, Mathf.RoundToInt((anchor.x - StartX) / StepPx));
            float swing = Mathf.Sin(t * steps * Mathf.PI);       // signed: flips each step
            float env = Mathf.Sin(t * Mathf.PI);
            bearAnchor.localPosition = StageCoords.Stage(x, anchor.y)
                                     + new Vector3(0f, HopPx * Mathf.Abs(swing) * env * StageCoords.PX, 0f);
            bearAnchor.localRotation = Quaternion.Euler(0f, 0f, WaddleDeg * swing * env);
            bearAnchor.localScale = Vector3.one;
        }

        Vector3 Base() => StageCoords.Stage(anchor);

#if UNITY_EDITOR
        // Inspector edits land here; SetActive is illegal inside OnValidate, so defer a frame.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying) return;
                if (bearAnchor != null) bearAnchor.localPosition = Base();
                if (bear != null && bear.gameObject.activeSelf != editorPreview)
                    bear.gameObject.SetActive(editorPreview);
            };
        }
#endif
    }
}
