using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The pastry's arc from the cloth to the bear, ending at stage (215,260).</summary>
    public sealed class ServeFlightView : GeneratedView
    {
        static readonly Vector2 Target = new Vector2(215f, 260f);

        SpriteRenderer sprite;
        Vector2 from;
        float startTime, duration = 0.9f;
        System.Action onArrive;

        public bool Flying => startTime >= 0f;

        /// <summary>The active flyer sprite's transform — the serve trail follows it.</summary>
        public Transform FlyerTransform => sprite != null ? sprite.transform : null;

        protected override void Build()
        {
            startTime = -1f;
            var go = new GameObject("Flyer") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(Content, false);
            sprite = go.AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = SpriteMaterials.Unlit;
            sprite.sortingLayerName = "Overlay";
            sprite.sortingOrder = 40;
            go.SetActive(false);
        }

        public void Launch(int tier, Vector2 stageFrom, float flySec, System.Action arrived)
        {
            if (!IsBuilt) { arrived?.Invoke(); return; }
            from = stageFrom;
            duration = Mathf.Max(0.05f, flySec);
            startTime = Time.time;
            onArrive = arrived;
            sprite.gameObject.SetActive(true);
            if (database != null) sprite.sprite = database.Pastry(tier);
        }

        public void Cancel()
        {
            startTime = -1f;
            onArrive = null;
            if (sprite != null) sprite.gameObject.SetActive(false);
        }

        void Update()
        {
            if (startTime < 0f || !IsBuilt) return;
            float t = Mathf.Clamp01((Time.time - startTime) / duration);

            // position eased with a slight undershoot, scale eased smoothly (§7.6)
            Vector2 p = Vector2.LerpUnclamped(from, Target, EaseUndershoot(t));
            float s = Mathf.Lerp(1f, 0.6f, Mathf.SmoothStep(0f, 1f, t));

            sprite.transform.localPosition = StageCoords.Stage(p.x, p.y);
            ViewFactory.SetIcon(sprite, 26f * s);

            if (t < 1f) return;

            // GameRoot also runs a timeout, so a missed callback cannot strand the flyer.
            startTime = -1f;
            sprite.gameObject.SetActive(false);
            var cb = onArrive;
            onArrive = null;
            cb?.Invoke();
        }

        /// <summary>Approximates cubic-bezier(0.35, -0.15, 0.35, 1).</summary>
        static float EaseUndershoot(float t)
            => Mathf.SmoothStep(0f, 1f, t) - 0.12f * Mathf.Sin(Mathf.PI * t) * (1f - t);
    }
}
