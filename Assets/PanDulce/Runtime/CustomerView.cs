using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The customer, rising into the window behind the counter to place their order.</summary>
    public sealed class CustomerView : GeneratedView
    {
        /// <summary>
        /// How tall the customer stands, in stage px.
        ///
        /// The drawing is FITTED to this rather than scaled by a fixed factor. The old
        /// hardcoded 0.5 was only ever right for the 460 × 400 placeholder — a replacement
        /// drawn at a different pixel size silently changed the character's height with it.
        /// Fitting means any future art drops in at the same presence behind the counter.
        /// </summary>
        public const float RenderHeight = 200f;

        /// <summary>Sprite pivot is centred; the anchor is bottom-centre, so the sprite sits
        /// half its rendered height above it.</summary>
        public const float SpriteLift = RenderHeight * 0.5f;

        /// <summary>The uniform scale that renders <paramref name="s"/> at RenderHeight.</summary>
        static float FitScale(Sprite s)
        {
            if (s == null) return 1f;
            float drawnWorldH = s.rect.height / s.pixelsPerUnit;
            return drawnWorldH > 0f ? RenderHeight * StageCoords.PX / drawnWorldH : 1f;
        }

        // Entrance and exit are the same move played in opposite directions: fade in standing
        // at the window and settle forward onto the glass, then step back and fade out.
        //
        // Neither end can duck behind scenery. Nothing draws in front of the Customer layer at
        // window height — WindowFrame is on Background — so a walk-in has nothing to emerge
        // from and the exit has to end itself. Rising into frame from below the sill was worse
        // still: the climb crossed the whole display case in plain view, which read as the bear
        // sliding up from behind the glass rather than stepping up to the counter.
        const float EnterScale = 0.94f;   // how far back they stand as they appear
        const float EnterDriftPx = 6f;    // and the small settle down onto their feet
        const float ExitScale = 0.92f;    // how far back they step
        const float ExitDriftPx = 6f;     // slight lift as they turn away

        // The waiting nudge: after NudgeDelay with nobody handing anything over, a small hop,
        // repeating on NudgeGap, to pull the eye back to the window. Deliberately half the
        // celebrate bounce's height and a single hop rather than two, so it reads as "still
        // waiting" and never as the reward for a serve.
        //
        // The gap matters more than it looks: a customer has no patience timer and stands at
        // the window until served, so this is a loop that can run for a whole game. Five
        // seconds keeps it a slow heartbeat instead of a nag.
        const float NudgeDelay = 5f;
        const float NudgeGap = 5f;
        const float NudgeRisePx = 7f;
        const float NudgeTime = 0.4f;

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
        float entranceStart = -1f, happyStart = -1f, departStart = -1f, duration = 0.7f;
        float idleStart = -1f;          // idle breathing phase-zeroes when a walk/bounce ends
        bool present;

        /// <summary>
        /// The bear is walked from the sim every frame and is created here directly rather than
        /// through ViewFactory, so authoring it into the scene would duplicate it on each bind.
        /// </summary>
        protected override bool Authorable => false;

        protected override void Build()
        {
            entranceStart = happyStart = departStart = idleStart = -1f;
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
            // Fitted to RenderHeight whatever the art was drawn at. Centre pivot → lift half
            // the RENDERED height so the anchor is bottom-centre; the lift is in the anchor's
            // space, unaffected by the child scale.
            go.transform.localScale = Vector3.one * FitScale(bear.sprite);
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
            happyStart = departStart = idleStart = -1f;
            // Our Update may not run again this frame, so seed frame zero of the fade here —
            // otherwise the previous exit's leftover transform flashes for one frame, fully
            // opaque, before the entrance takes over.
            FadeIn(0f);
        }

        public void Celebrate()
        {
            // A serve can land mid-move; finish the entrance so the bounce plays at the window.
            // That can interrupt a fade too, so the exit's alpha is cleared here as well.
            entranceStart = departStart = idleStart = -1f;
            happyStart = Time.time;
            SetAlpha(1f);
        }

        /// <summary>The visit is over: sink back down the way they came up.</summary>
        public void Depart(float walkTime)
        {
            if (!IsBuilt || !present) { Leave(); return; }
            duration = Mathf.Max(0.05f, walkTime);
            departStart = Time.time;
            entranceStart = happyStart = idleStart = -1f;
        }

        public void Leave()
        {
            present = false;
            entranceStart = happyStart = departStart = idleStart = -1f;
            SetAlpha(1f);
            if (bearAnchor != null) bearAnchor.localScale = Vector3.one;
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

            if (departStart >= 0f)
            {
                float k = Mathf.Clamp01((Time.time - departStart) / duration);
                if (k >= 1f) { Leave(); return; }
                StepBack(k);
                return;
            }

            if (entranceStart >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - entranceStart) / duration);
                if (t >= 1f)
                {
                    entranceStart = -1f;
                    bearAnchor.localPosition = Base();
                    bearAnchor.localRotation = Quaternion.identity;
                    bearAnchor.localScale = Vector3.one;
                    SetAlpha(1f);
                    return;
                }
                FadeIn(t);
                return;
            }

            // Waiting at the counter: a slow squash-breathe so the bear never sits frozen.
            // Phase starts at zero when the idle begins, so there is no pop out of a walk
            // or bounce; the amplitude stays under the happy bounce's so it reads as rest.
            if (idleStart < 0f) idleStart = Time.time;
            float waited = Time.time - idleStart;
            float br = Mathf.Sin(waited * (2f * Mathf.PI / 3.4f));
            float hop = Nudge(waited);

            bearAnchor.localPosition = Base() + new Vector3(0f, hop * NudgeRisePx * StageCoords.PX, 0f);
            bearAnchor.localRotation = Quaternion.identity;
            // Breathe and hop share the squash axis rather than multiplying: the hop is the
            // louder of the two and would otherwise beat against the breathe's own rhythm.
            float squash = 0.012f * br + 0.02f * hop;
            bearAnchor.localScale = new Vector3(1f + squash, 1f - squash, 1f);
        }

        /// <summary>
        /// The waiting hop, as a 0→1→0 arc. <paramref name="waited"/> is seconds of unbroken
        /// idle; the result is 0 until the first nudge is due, so a customer served promptly
        /// never hops at all.
        /// </summary>
        static float Nudge(float waited)
        {
            if (waited < NudgeDelay) return 0f;
            float since = Mathf.Repeat(waited - NudgeDelay, NudgeGap);
            return since < NudgeTime ? Mathf.Sin(since / NudgeTime * Mathf.PI) : 0f;
        }

        /// <summary>
        /// The exit: step back from the glass and fade out on the spot, at window height.
        /// k runs 0 (standing) → 1 (gone).
        /// </summary>
        void StepBack(float k)
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            bearAnchor.localPosition = StageCoords.Stage(anchor.x, anchor.y - e * ExitDriftPx);
            bearAnchor.localRotation = Quaternion.identity;
            bearAnchor.localScale = Vector3.one * Mathf.Lerp(1f, ExitScale, e);
            SetAlpha(1f - e);
        }

        /// <summary>The fade is on the renderer, so it has to be undone before the next visit.</summary>
        void SetAlpha(float a)
        {
            if (bear == null) return;
            var c = bear.color;
            c.a = Mathf.Clamp01(a);
            bear.color = c;
        }

        /// <summary>
        /// The entrance, the exit run backwards: t goes 0 (invisible, standing back from the
        /// glass) → 1 (opaque, settled at the anchor).
        ///
        /// Position, scale and alpha all land on their rest values at exactly t = 1, so the
        /// idle breathe picks up from rest with nothing to pop out of. Alpha leads the move
        /// slightly — squared easing on the transform, plain on the fade — so the bear is
        /// already readable while the last of the settle plays.
        /// </summary>
        void FadeIn(float t)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            float settling = 1f - e;

            bearAnchor.localPosition = StageCoords.Stage(anchor.x, anchor.y - settling * EnterDriftPx);
            bearAnchor.localRotation = Quaternion.identity;
            bearAnchor.localScale = Vector3.one * Mathf.Lerp(EnterScale, 1f, e);
            SetAlpha(t);
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
