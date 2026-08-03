using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The bear, rising from behind the counter through the opening.</summary>
    public sealed class CustomerView : GeneratedView
    {
        const float AnchorX = 215f, AnchorY = 355f, RisePx = 130f;

        Transform bearAnchor;
        SpriteRenderer bear;
        float entranceStart = -1f, happyStart = -1f, duration = 0.7f;
        bool present;

        protected override void Build()
        {
            entranceStart = happyStart = -1f;
            present = false;
            var t = Content;

            // the opening — always visible, in front of the case glass (Case > Customer)
            var ring = ViewFactory.Rect(t, "CounterOpening",
                                        database != null ? database.CounterOpening : null,
                                        0f, 0f, 1f, 1f, Color.white, "Case", 20);
            // Baked at 2x (360×160 px, PPU 100) → scale 0.5 restores the 180×80 stage-px size.
            ring.drawMode = SpriteDrawMode.Simple;
            ring.transform.localScale = Vector3.one * 0.5f;
            ring.transform.localPosition = StageCoords.Stage(AnchorX, 330f);

            bearAnchor = ViewFactory.Node(t, "BearAnchor", AnchorX, AnchorY).transform;
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
            go.transform.localPosition = new Vector3(0f, 100f * StageCoords.PX, 0f);
            go.SetActive(false);
        }

        public void Arrive(float entranceTime)
        {
            if (!IsBuilt) return;
            present = true;
            bear.gameObject.SetActive(true);
            duration = Mathf.Max(0.05f, entranceTime);
            entranceStart = Time.time;
            happyStart = -1f;
        }

        public void Celebrate() => happyStart = Time.time;

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
                    return;
                }
                happyStart = -1f;
            }

            if (entranceStart < 0f) return;
            float t = Mathf.Clamp01((Time.time - entranceStart) / duration);
            // back-out: rises past the resting spot ~10% then settles
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            float offset = (1f - e) * RisePx;                    // px still below the resting spot
            bearAnchor.localPosition = Base() + new Vector3(0f, -offset * StageCoords.PX, 0f);
            bearAnchor.localScale = Vector3.one;
        }

        static Vector3 Base() => StageCoords.Stage(AnchorX, AnchorY);
    }
}
