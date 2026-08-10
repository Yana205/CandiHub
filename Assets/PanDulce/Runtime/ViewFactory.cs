using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Builders for the chrome. Views construct their own internals through this.
    ///
    /// Every builder is find-or-create: it reuses the child already sitting under the parent
    /// when one carries the name, and only makes a fresh object when none does. That is what
    /// lets a generated hierarchy live in Main.unity as ordinary, hand-editable objects — a
    /// view can re-run Build() to re-link the state only code can supply, without disturbing
    /// anything a designer moved (see GeneratedView).
    ///
    /// The split that makes it work:
    ///   - Re-applied on every pass: the procedural sprite, the Unlit material, sorting.
    ///     None of these survive serialization, so they must be handed back each load.
    ///   - Written once, at creation: position, size, colour, text. These belong to whoever
    ///     authored the scene from that point on.
    ///
    /// All rects are given in stage px, y-down from the frame's top-left, matching §8.
    /// </summary>
    public static class ViewFactory
    {
        /// <summary>
        /// Bind pass: reuse what exists, create nothing. A name with no object behind it is an
        /// element that was deliberately deleted from the scene, so the builder returns null
        /// rather than putting it back, and the view copes with the gap.
        /// </summary>
        public static bool BindOnly;

        /// <summary>
        /// The child owning this name, or a new one. <paramref name="created"/> tells the
        /// caller whether it may write layout — see the class note on what is authored once.
        /// </summary>
        static GameObject Acquire(Transform parent, string name, out bool created)
        {
            created = false;
            if (parent == null) return null;

            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            if (BindOnly) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            created = true;
            return go;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>
        /// <paramref name="rotationDeg"/> is part of the initial rect, not a runtime tweak —
        /// pass it here rather than rotating the transform afterwards, or the rotation is
        /// re-applied on every bind and a hand-turned element snaps back.
        /// </summary>
        public static SpriteRenderer Rect(Transform parent, string name, Sprite sprite,
                                          float x, float y, float w, float h,
                                          Color color, string layer, int order,
                                          float rotationDeg = 0f)
        {
            var go = Acquire(parent, name, out bool created);
            if (go == null) return null;

            var sr = Ensure<SpriteRenderer>(go);

            // Assigning a sprite RESETS size on a sliced renderer, and Shapes rebuilds its
            // cache into new Sprite instances after every domain reload — so without this the
            // authored size is silently wiped on the first bind of each session. Read it
            // before the assignment, hand it back after.
            Vector2 authoredSize = sr.size;

            // Shapes builds its sprites in memory and SpriteMaterials.Unlit is HideAndDontSave,
            // so a scene-saved renderer wakes up pointing at neither. Handed back every pass.
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = sprite != null ? sprite : Shapes.White;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;

            if (created)
            {
                sr.color = color;
                Place(sr, x, y, w, h);
                if (rotationDeg != 0f)
                    sr.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
            }
            else sr.size = authoredSize;

            return sr;
        }

        /// <summary>
        /// Moves and resizes a sprite already built by Rect onto a new stage-px rect. The one
        /// place that turns a y-down rect into a centred local position, so a view that
        /// resizes itself at runtime cannot drift from where the same rect was built.
        ///
        /// Callers are content-driven fits (a badge that grows with its label), not layout —
        /// an element that is Placed at runtime is owned by code and will not hold a hand
        /// placement. Move its parent node instead.
        /// </summary>
        public static void Place(SpriteRenderer sr, float x, float y, float w, float h)
        {
            if (sr == null) return;
            sr.size = new Vector2(w * StageCoords.PX, h * StageCoords.PX);
            sr.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                     -(y + h * 0.5f) * StageCoords.PX, 0f);
        }

        /// <summary>
        /// The same for a label built by Label. Height comes from the font size it was built
        /// with, so only the horizontal band and the baseline are given here.
        /// </summary>
        public static void Place(TextMeshPro t, float x, float y, float w)
        {
            if (t == null) return;
            t.rectTransform.sizeDelta = new Vector2(w * StageCoords.PX, t.rectTransform.sizeDelta.y);
            t.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                    -y * StageCoords.PX, 0f);
        }

        public static SpriteRenderer Panel(Transform parent, string name,
                                           float x, float y, float w, float h, int radius,
                                           Color color, string layer, int order,
                                           float rotationDeg = 0f)
            => Rect(parent, name, Shapes.Panel(radius), x, y, w, h, color, layer, order, rotationDeg);

        /// <summary>
        /// A chrome plate: the hand-drawn art when the skin carries it, otherwise the
        /// generated rounded rect that art replaced. Same rect either way, so a missing
        /// sprite changes the look and never the layout.
        ///
        /// Pass Color.white for a face — the drawing already holds its colour. A tint is
        /// still honoured, which is how the fake drop shadows reuse the button's own
        /// silhouette instead of a rounded rect that would not match its corners.
        /// </summary>
        public static SpriteRenderer Plate(Transform parent, string name, Sprite art,
                                           float x, float y, float w, float h, int radius,
                                           Color color, string layer, int order,
                                           float rotationDeg = 0f)
            => art != null
               ? Rect(parent, name, art, x, y, w, h, color, layer, order, rotationDeg)
               : Panel(parent, name, x, y, w, h, radius, color, layer, order, rotationDeg);

        public static TextMeshPro Label(Transform parent, string name, string text,
                                        float x, float y, float w, float sizeStagePx,
                                        Color color, string layer, int order,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center,
                                        FontStyles style = FontStyles.Bold)
        {
            var go = Acquire(parent, name, out bool created);
            if (go == null) return null;

            var t = Ensure<TextMeshPro>(go);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = layer;
                mr.sortingOrder = order;
            }

            if (created)
            {
                t.text = text;
                t.alignment = align;
                t.fontStyle = style;
                t.color = color;

                // TMP world units: 1 stage px = 0.01 units, and TMP's font size is in units.
                t.fontSize = sizeStagePx * StageCoords.PX * 10f;
                t.rectTransform.sizeDelta =
                    new Vector2(w * StageCoords.PX, sizeStagePx * 1.6f * StageCoords.PX);
                t.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                        -y * StageCoords.PX, 0f);
            }
            return t;
        }

        /// <summary>
        /// A pastry icon at a given stage-px radius, from the tier sprites. Chrome-flavoured:
        /// the initial size folds in the dessert's CASE size (DisplaySize); play-area users
        /// re-SetIcon with a TierTable radius before ever being shown.
        /// </summary>
        public static SpriteRenderer Icon(Transform parent, string name, PastryDatabase db, int tier,
                                          float cx, float cy, float radius, string layer, int order)
        {
            var go = Acquire(parent, name, out bool created);
            if (go == null) return null;

            var sr = Ensure<SpriteRenderer>(go);
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = db != null ? db.Pastry(tier) : null;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;

            if (created)
            {
                SetIcon(sr, radius, db != null ? db.DisplaySize(tier) : 1f);
                sr.transform.localPosition = new Vector3(cx * StageCoords.PX,
                                                         -cy * StageCoords.PX, 0f);
            }
            return sr;
        }

        /// <summary>
        /// Sprites are authored at radius 200, so any icon size is a uniform scale.
        ///
        /// Pass tierSize ONLY when radiusStagePx is a fixed chrome radius (a case seat, the
        /// plaque, the order bubble) — and there pass the dessert's CASE size (DisplaySize),
        /// which keeps the icon proportional without tying it to gameplay. A radius that came
        /// from TierTable already has the PLAY size folded in; passing that again would
        /// square it.
        /// </summary>
        public static void SetIcon(SpriteRenderer sr, float radiusStagePx, float tierSize = 1f)
        {
            if (sr == null) return;
            float s = radiusStagePx / Core.TierTable.CanonicalSpriteRadius * tierSize;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        public static GameObject Node(Transform parent, string name, float x = 0f, float y = 0f)
        {
            var go = Acquire(parent, name, out bool created);
            if (go == null) return null;

            if (created)
                go.transform.localPosition = new Vector3(x * StageCoords.PX,
                                                         -y * StageCoords.PX, 0f);
            return go;
        }

        /// <summary>The transform of a Node, or null when the node was deleted from the scene.</summary>
        public static Transform NodeTransform(Transform parent, string name,
                                              float x = 0f, float y = 0f)
        {
            var go = Node(parent, name, x, y);
            return go != null ? go.transform : null;
        }
    }
}
