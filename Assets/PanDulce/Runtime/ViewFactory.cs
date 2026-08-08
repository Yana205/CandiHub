using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Builders for the chrome. Views construct their own internals through this, which
    /// keeps Main.unity thin (§4) — the scene holds folders and named nodes, not 200 leaves.
    ///
    /// All rects are given in stage px, y-down from the frame's top-left, matching §8.
    /// </summary>
    public static class ViewFactory
    {
        public static SpriteRenderer Rect(Transform parent, string name, Sprite sprite,
                                          float x, float y, float w, float h,
                                          Color color, string layer, int order)
        {
            // DontSave: generated children must never enter the scene file (see GeneratedView).
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = sprite != null ? sprite : Shapes.White;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = color;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            Place(sr, x, y, w, h);
            return sr;
        }

        /// <summary>
        /// Moves and resizes a sprite already built by Rect onto a new stage-px rect. The one
        /// place that turns a y-down rect into a centred local position, so a view that
        /// resizes itself at runtime cannot drift from where the same rect was built.
        /// </summary>
        public static void Place(SpriteRenderer sr, float x, float y, float w, float h)
        {
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
            t.rectTransform.sizeDelta = new Vector2(w * StageCoords.PX, t.rectTransform.sizeDelta.y);
            t.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                    -y * StageCoords.PX, 0f);
        }

        public static SpriteRenderer Panel(Transform parent, string name,
                                           float x, float y, float w, float h, int radius,
                                           Color color, string layer, int order)
            => Rect(parent, name, Shapes.RoundedRect(Mathf.Max(4, radius * 2 + 4),
                                                     Mathf.Max(4, radius * 2 + 4), radius),
                    x, y, w, h, color, layer, order);

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
                                           Color color, string layer, int order)
            => art != null
               ? Rect(parent, name, art, x, y, w, h, color, layer, order)
               : Panel(parent, name, x, y, w, h, radius, color, layer, order);

        public static TextMeshPro Label(Transform parent, string name, string text,
                                        float x, float y, float w, float sizeStagePx,
                                        Color color, string layer, int order,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center,
                                        FontStyles style = FontStyles.Bold)
        {
            // DontSave: generated children must never enter the scene file (see GeneratedView).
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.alignment = align;
            t.fontStyle = style;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;

            // TMP world units: 1 stage px = 0.01 units, and TMP's font size is in units.
            t.fontSize = sizeStagePx * StageCoords.PX * 10f;
            t.rectTransform.sizeDelta = new Vector2(w * StageCoords.PX, sizeStagePx * 1.6f * StageCoords.PX);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = layer;
            mr.sortingOrder = order;

            go.transform.localPosition = new Vector3((x + w * 0.5f) * StageCoords.PX,
                                                      -y * StageCoords.PX, 0f);
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
            // DontSave: generated children must never enter the scene file (see GeneratedView).
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterials.Unlit;
            sr.sprite = db != null ? db.Pastry(tier) : null;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            SetIcon(sr, radius, db != null ? db.DisplaySize(tier) : 1f);
            go.transform.localPosition = new Vector3(cx * StageCoords.PX, -cy * StageCoords.PX, 0f);
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
            float s = radiusStagePx / Core.TierTable.CanonicalSpriteRadius * tierSize;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        public static GameObject Node(Transform parent, string name, float x = 0f, float y = 0f)
        {
            // DontSave: generated children must never enter the scene file (see GeneratedView).
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x * StageCoords.PX, -y * StageCoords.PX, 0f);
            return go;
        }
    }
}
