using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Display case — stage (9,300) 412 × 96, five slots with a sliding window (§8.4).
    /// The window follows progress: start = clamp(highestDiscovered - 3, 0, 6).
    /// </summary>
    public sealed class DisplayCaseView : GeneratedView
    {
        const int Slots = 5;

        readonly SpriteRenderer[] icons = new SpriteRenderer[Slots];
        readonly TextMeshPro[] labels = new TextMeshPro[Slots];
        int shownStart, shownMax;

        protected override void Build()
        {
            shownStart = shownMax = -1;
            var t = Content;

            // knob on top
            ViewFactory.Panel(t, "Knob", 200f, 289f, 30f, 11f, 5, Palette.Hex("#e0c079"), "Case", 1);

            // glass: white border panel + gradient fill + rail + two rotated glare stripes (§8.4)
            ViewFactory.Panel(t, "GlassBorder", 9f, 300f, 412f, 96f, 14,
                              new Color(1f, 1f, 1f, 0.92f), "Case", 0);
            ViewFactory.Rect(t, "GlassFill", Shapes.VerticalAlphaGradient(64, 0.5f, 0.12f),
                             12f, 303f, 406f, 90f, Color.white, "Case", 1);
            ViewFactory.Rect(t, "Rail", Shapes.White, 12f, 315f, 406f, 2f,
                             new Color(1f, 1f, 1f, 0.75f), "Case", 2);
            var glareA = ViewFactory.Rect(t, "GlareA", Shapes.White, 20f, 296f, 44f, 120f,
                                          new Color(1f, 1f, 1f, 0.30f), "Case", 3);
            glareA.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var glareB = ViewFactory.Rect(t, "GlareB", Shapes.White, 74f, 296f, 16f, 120f,
                                          new Color(1f, 1f, 1f, 0.22f), "Case", 3);
            glareB.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);

            float slotW = 412f / Slots;
            for (int i = 0; i < Slots; i++)
            {
                float cx = 9f + slotW * (i + 0.5f);
                icons[i] = ViewFactory.Icon(t, $"Slot_{i}_Icon", database, i, cx, 352f, 21f, "Case", 10);
                ViewFactory.Rect(t, $"Slot_{i}_LedgeShadow", Shapes.White, cx - 26f, 377f, 52f, 2f,
                                 Palette.Hex("#b99f7c"), "Case", 11);
                ViewFactory.Rect(t, $"Slot_{i}_Ledge", Shapes.White, cx - 26f, 372f, 52f, 5f,
                                 Palette.Hex("#d8c6ac"), "Case", 12);
                labels[i] = ViewFactory.Label(t, $"Slot_{i}_Label", "?", cx - 30f, 388f, 60f, 9.5f,
                                              Palette.Hex("#7a5735"), "Case", 13);
            }
        }

        public void Sync(MergeSim sim)
        {
            if (!IsBuilt) return;
            int maxD = sim.HighestDiscovered;
            int start = Mathf.Clamp(maxD - 3, 0, TierTable.Count - Slots);
            if (start == shownStart && maxD == shownMax) return;
            shownStart = start;
            shownMax = maxD;

            for (int i = 0; i < Slots; i++)
            {
                int tier = start + i;
                bool found = sim.IsDiscovered(tier);
                if (database != null) icons[i].sprite = database.Pastry(tier);
                // Undiscovered entries render the sprite tinted flat, label '?' (§8.4).
                icons[i].color = found ? Color.white : Palette.Locked;
                labels[i].text = found ? TierTable.Names[tier] : "?";
            }
        }
    }

    /// <summary>The current regular, with the four entrance animations (§8.6).</summary>
    public sealed class CustomerView : GeneratedView
    {
        SpriteRenderer sprite;
        Transform anchor;
        float entranceStart, happyStart;
        EntranceStyle style;
        float duration = 0.7f;

        protected override void Build()
        {
            entranceStart = -1f;
            happyStart = -1f;

            anchor = ViewFactory.Node(Content, "CustomerAnchor", 210f, 342f).transform;

            var go = new GameObject("CustomerSprite") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(anchor, false);
            sprite = go.AddComponent<SpriteRenderer>();
            sprite.sharedMaterial = SpriteMaterials.Unlit;
            sprite.sortingLayerName = "Customer";
            sprite.sortingOrder = 0;
            if (database != null) sprite.sprite = database.Customer(0);
            // Authored 210×170 at 2x with a centre pivot → lift by half its height so the
            // anchor behaves as bottom-centre, which is what the entrances assume.
            go.transform.localPosition = new Vector3(0f, 85f * StageCoords.PX, 0f);

            SetVisible(false);
        }

        public void Arrive(int index, EntranceStyle entranceStyle, float entranceTime)
        {
            if (!IsBuilt) return;
            SetVisible(true);
            if (database != null) sprite.sprite = database.Customer(index);
            style = entranceStyle;
            duration = Mathf.Max(0.05f, entranceTime);
            entranceStart = Time.time;
            happyStart = -1f;
        }

        public void Celebrate() => happyStart = Time.time;

        public void Leave()
        {
            SetVisible(false);
            entranceStart = -1f;
            happyStart = -1f;
        }

        void Update()
        {
            if (!IsBuilt || anchor == null) return;

            if (happyStart >= 0f)
            {
                // bounce played twice over 0.45s
                float k = (Time.time - happyStart) / 0.45f;
                if (k <= 2f)
                {
                    float p = Mathf.Repeat(k, 1f);
                    float s = Mathf.Sin(p * Mathf.PI);
                    anchor.localPosition = Base() + new Vector3(0f, 14f * s * StageCoords.PX, 0f);
                    anchor.localScale = new Vector3(1f + 0.02f * s, 1f - 0.02f * s, 1f);
                    return;
                }
                happyStart = -1f;
            }

            if (entranceStart < 0f) return;
            float t = Mathf.Clamp01((Time.time - entranceStart) / duration);
            // cubic-bezier(0.22, 0.9, 0.3, 1) ≈ a strong ease-out
            float e = 1f - Mathf.Pow(1f - t, 3f);

            Vector3 offset = Vector3.zero;
            float scale = 1f, rot = 0f, alpha = 1f;

            switch (style)
            {
                case EntranceStyle.Walk:
                    offset.x = Mathf.Lerp(-200f, 0f, e) * StageCoords.PX;
                    offset.y = Mathf.Sin(t * Mathf.PI * 3f) * 6f * (1f - t) * StageCoords.PX;
                    alpha = Mathf.Clamp01(t / 0.12f);
                    break;
                case EntranceStyle.Hop:
                    offset.y = -Mathf.Lerp(130f, 0f, e) * StageCoords.PX;
                    alpha = Mathf.Clamp01(t / 0.3f);
                    break;
                case EntranceStyle.Pop:
                    scale = Mathf.Lerp(0.18f, 1f, e);
                    rot = Mathf.Lerp(-12f, 0f, e);
                    break;
                case EntranceStyle.Slide:
                    offset.y = -Mathf.Lerp(96f, 0f, e) * StageCoords.PX;
                    alpha = Mathf.Clamp01(t / 0.6f);
                    break;
            }

            anchor.localPosition = Base() + offset;
            anchor.localScale = Vector3.one * scale;
            anchor.localRotation = Quaternion.Euler(0f, 0f, rot);
            if (sprite != null) sprite.color = new Color(1f, 1f, 1f, alpha);
        }

        static Vector3 Base() => new Vector3(210f * StageCoords.PX, -342f * StageCoords.PX, 0f);
    }

    /// <summary>The pastry's arc from the cloth to the customer, ending at stage (182,224).</summary>
    public sealed class ServeFlightView : GeneratedView
    {
        static readonly Vector2 Target = new Vector2(182f, 224f);

        SpriteRenderer sprite;
        Vector2 from;
        float startTime, duration = 0.9f;
        System.Action onArrive;

        public bool Flying => startTime >= 0f;

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

    /// <summary>Aim guide and the held pastry, drawn at the top of the cloth (§8.5 step 4).</summary>
    public sealed class AimGuideView : GeneratedView
    {
        SpriteRenderer line, held;
        int shownTier;

        protected override void Build()
        {
            shownTier = -1;
            // Defaults to the cloth centre so the guide reads correctly in the Editor too,
            // before PointerInput has ever run.
            line = ViewFactory.Rect(Content, "AimLine", Shapes.Dashes(4, 10, 3),
                                    SimField.CX, 66f, 3f, 306f,
                                    new Color(1f, 1f, 1f, 0.8f), "PlayArea", 20);
            held = ViewFactory.Icon(Content, "HeldPastry", database, 0, SimField.CX, 42f, 20f,
                                    "PlayArea", 21);
        }

        public void Sync(bool visible, float aimX, int tier, float sizeScale)
        {
            if (!IsBuilt) return;
            SetVisible(visible);
            if (!visible) return;

            float r = TierTable.EffectiveRadius(tier, sizeScale);
            float x = Mathf.Clamp(aimX, SimField.WL + r, SimField.WR - r);

            line.transform.localPosition = new Vector3(x * StageCoords.PX, -(66f + 153f) * StageCoords.PX, 0f);
            held.transform.localPosition = new Vector3(x * StageCoords.PX, -42f * StageCoords.PX, 0f);

            if (tier == shownTier) return;
            shownTier = tier;
            if (database != null) held.sprite = database.Pastry(tier);
            ViewFactory.SetIcon(held, r);
        }
    }

    /// <summary>
    /// The danger line. Not in the mock — it comes with the top-out layer, and the cloth had
    /// no visual for it, so this is new: a dashed rule across the cloth mouth that sits at
    /// low alpha normally and blinks once the pile is actually over it.
    /// </summary>
    public sealed class DangerLineView : GeneratedView
    {
        SpriteRenderer line;

        protected override void Build()
        {
            line = ViewFactory.Rect(Content, "DangerLine", Shapes.Dashes(9, 7, 3),
                                    SimField.BL, 82f, SimField.BR - SimField.BL, 2.5f,
                                    Palette.Danger, "PlayArea", 15);
        }

        public void Sync(bool enabled, bool alwaysShow, float lineY, bool blinking, float now)
        {
            if (!IsBuilt) return;
            SetVisible(enabled && (alwaysShow || blinking));
            if (!Content.gameObject.activeSelf) return;

            float a = blinking ? 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(now * 7f)) : 0.28f;
            line.color = Palette.WithAlpha(Palette.Danger, a);
            line.transform.localPosition = new Vector3(
                (SimField.BL + (SimField.BR - SimField.BL) * 0.5f) * StageCoords.PX,
                -lineY * StageCoords.PX, 0f);
        }
    }

    /// <summary>Offsets ClothShakeRoot during a shake — desk and NEXT plaque stay still (§7.7).</summary>
    public sealed class ClothShaker : MonoBehaviour
    {
        public void Sync(Vector2 simOffset)
        {
            transform.localPosition = new Vector3(simOffset.x * StageCoords.PX,
                                                   -simOffset.y * StageCoords.PX, 0f);
        }
    }

    /// <summary>The two flaps, the knot and the "Closing time" card, driven by CloseT (§8.5).</summary>
    public sealed class FoldView : GeneratedView
    {
        Transform flapLeft, flapRight, knot;
        SpriteRenderer flapLeftSr, flapRightSr, knotA, knotB, knotC;
        TextMeshPro closing;

        protected override void Build()
        {
            var t = Content;

            flapLeft = ViewFactory.Node(t, "FlapLeft", SimField.BL + 12f, 352f).transform;
            flapLeftSr = ViewFactory.Rect(flapLeft, "Shape", Shapes.RoundedRect(64, 64, 26),
                                          -20f, -320f, 210f, 330f, Color.white, "PlayArea", 70);

            flapRight = ViewFactory.Node(t, "FlapRight", SimField.BR - 12f, 352f).transform;
            flapRightSr = ViewFactory.Rect(flapRight, "Shape", Shapes.RoundedRect(64, 64, 26),
                                           -190f, -320f, 210f, 330f, Color.white, "PlayArea", 71);

            knot = ViewFactory.Node(t, "Knot", SimField.CX, 108f).transform;
            knotA = ViewFactory.Rect(knot, "LobeL", Shapes.Circle(64), -30f, -9f, 30f, 17f,
                                     Color.white, "PlayArea", 72);
            knotB = ViewFactory.Rect(knot, "LobeR", Shapes.Circle(64), 0f, -9f, 30f, 17f,
                                     Color.white, "PlayArea", 72);
            knotC = ViewFactory.Rect(knot, "Centre", Shapes.Circle(64), -13f, -10f, 26f, 21f,
                                     Color.white, "PlayArea", 73);
            knotA.transform.localRotation = Quaternion.Euler(0, 0, 26f);
            knotB.transform.localRotation = Quaternion.Euler(0, 0, -26f);

            closing = ViewFactory.Label(t, "ClosingLabel", "Closing time", SimField.CX - 150f, 190f,
                                        300f, 20f, Palette.Cream, "PlayArea", 80);

            SetVisible(false);
        }

        public void Sync(float closeT, Color clothColor)
        {
            if (!IsBuilt) return;
            bool draw = closeT > 0.002f;
            SetVisible(draw);
            if (!draw) return;

            float p = closeT;
            float e = p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;

            flapLeft.localRotation = Quaternion.Euler(0f, 0f, -e * 1.42f * Mathf.Rad2Deg);
            flapRight.localRotation = Quaternion.Euler(0f, 0f, e * 1.42f * Mathf.Rad2Deg);

            flapLeftSr.color = Palette.Mix(clothColor, 1.06f);
            flapRightSr.color = Palette.Mix(clothColor, 0.9f);

            // Past p > 0.55 the knot scales in.
            float k = Mathf.Clamp01((p - 0.55f) / 0.45f);
            knot.gameObject.SetActive(k > 0f);
            knot.localScale = Vector3.one * k;
            knotA.color = knotB.color = Palette.Mix(clothColor, 1.1f);
            knotC.color = Palette.Mix(clothColor, 1.18f);

            closing.alpha = k;
            // Outline setters reach through renderer.material — runtime only (see Pools).
            if (Application.isPlaying)
            {
                closing.outlineWidth = 0.25f;
                closing.outlineColor = Palette.Mix(clothColor, 0.6f);
            }
        }
    }
}
