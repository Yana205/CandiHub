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
            ring.drawMode = SpriteDrawMode.Simple;
            ring.transform.localScale = Vector3.one;            // baked 180×80 stage px at PPU 100/2x
            ring.transform.localPosition = StageCoords.Stage(AnchorX, 318f);

            bearAnchor = ViewFactory.Node(t, "BearAnchor", AnchorX, AnchorY).transform;
            var go = new GameObject("Bear") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(bearAnchor, false);
            bear = go.AddComponent<SpriteRenderer>();
            bear.sharedMaterial = SpriteMaterials.Unlit;
            bear.sortingLayerName = "Customer";
            bear.sortingOrder = 0;
            if (database != null) bear.sprite = database.Customer(0);
            // 230×200 logical sprite, centre pivot → lift half the height so the anchor is
            // bottom-centre. Baked at 2x with PPU 100 → world scale 0.5 restores stage px.
            go.transform.localScale = Vector3.one * 0.5f;
            go.transform.localPosition = new Vector3(0f, 100f * 0.5f * StageCoords.PX, 0f);
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

    /// <summary>The desk behind the cloth — §8.5 step 1. Does NOT shake.</summary>
    public sealed class DeskView : GeneratedView
    {
        protected override void Build()
        {
            var t = Content;
            // top band + warm line
            ViewFactory.Rect(t, "TopBand", Shapes.White, 0f, 0f, 418f, 14f,
                             new Color(70f/255f, 44f/255f, 22f/255f, 0.45f), "PlayArea", 0);
            ViewFactory.Rect(t, "WarmLine", Shapes.White, 0f, 16f, 418f, 3f,
                             new Color(1f, 226f/255f, 182f/255f, 0.55f), "PlayArea", 1);
            // vertical plank lines
            foreach (float x in new[] { 58f, 150f, 268f, 390f })
                ViewFactory.Rect(t, "Plank", Shapes.White, x, 0f, 2f, 440f,
                                 new Color(92f/255f, 58f/255f, 26f/255f, 0.30f), "PlayArea", 1);
            // contact shadow under the pile
            var shadow = ViewFactory.Rect(t, "ContactShadow", Shapes.Circle(64),
                                          SimField.CX - 206f, 62f, 412f, 60f,
                                          new Color(64f/255f, 38f/255f, 16f/255f, 0.22f), "PlayArea", 2);
            shadow.drawMode = SpriteDrawMode.Simple;
            shadow.transform.localScale = new Vector3(412f / 64f, 60f / 64f, 1f);
            shadow.transform.localPosition = StageCoords.Stage(SimField.CX, 92f);
        }
    }

    /// <summary>The NEXT plaque at the cloth's top-right — §8.5 step 7. Does NOT shake.</summary>
    public sealed class NextPlaqueView : GeneratedView
    {
        SpriteRenderer icon;
        int shownTier = -1;

        protected override void Build()
        {
            shownTier = -1;
            var t = Content;
            float x = SimField.BR - 66f, y = 20f;
            ViewFactory.Panel(t, "Border", x - 2f, y - 2f, 68f, 54f, 13, Palette.Wood, "PlayArea", 62);
            ViewFactory.Panel(t, "Face", x, y, 64f, 50f, 12,
                              new Color(1f, 243f/255f, 221f/255f, 0.95f), "PlayArea", 63);
            ViewFactory.Label(t, "Word", "NEXT", x, y + 12f, 64f, 11f,
                              Palette.Hex("#a58358"), "PlayArea", 64);
            icon = ViewFactory.Icon(t, "Icon", database, 0, x + 32f, y + 34f, 13f, "PlayArea", 64);
        }

        public void Sync(int nextTier)
        {
            if (!IsBuilt || nextTier == shownTier) return;
            shownTier = nextTier;
            if (database != null) icon.sprite = database.Pastry(nextTier);
        }
    }
}
