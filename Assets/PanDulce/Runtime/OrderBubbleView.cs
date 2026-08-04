using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>Order bubble — pops in beside the bear after he arrives (§8.4).</summary>
    public sealed class OrderBubbleView : GeneratedView
    {
        SpriteRenderer icon;
        TextMeshPro nameLabel;
        float shownAt;
        int shownTier;
        float iconBaseScale;

        protected override void Build()
        {
            shownAt = -1f;
            shownTier = -1;
            var t = Content;

            ViewFactory.Panel(t, "Shadow", 140f, 164f, 244f, 70f, 18,
                              new Color(122f/255f, 84f/255f, 49f/255f, 0.25f), "Overlay", 9);
            ViewFactory.Panel(t, "Border", 140f, 160f, 244f, 70f, 18, Palette.Hex("#e0cba6"), "Overlay", 10);
            ViewFactory.Panel(t, "Box", 143f, 163f, 238f, 64f, 16, Palette.Hex("#fffaf0"), "Overlay", 11);
            var tail = ViewFactory.Panel(t, "Tail", 196f, 218f, 18f, 18f, 3, Palette.Hex("#fffaf0"), "Overlay", 11);
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            icon = ViewFactory.Icon(t, "Icon", database, 2, 170f, 195f, 16f, "Overlay", 12);
            iconBaseScale = icon.transform.localScale.x;
            nameLabel = ViewFactory.Label(t, "Name", "", 192f, 188f, 180f, 15f,
                                          Palette.Hex("#6b4a2e"), "Overlay", 12,
                                          TextAlignmentOptions.Left);
            ViewFactory.Label(t, "Hint", "tap it in the cloth to hand it over",
                              192f, 208f, 180f, 9f, Palette.Hex("#a58358"), "Overlay", 12,
                              TextAlignmentOptions.Left, FontStyles.Normal);
            SetVisible(false);
        }

        public void Show(int tier, float now)
        {
            if (!IsBuilt || tier < 0) { Hide(); return; }
            SetVisible(true);
            shownAt = now;
            if (tier == shownTier) return;
            shownTier = tier;
            if (database != null) icon.sprite = database.Pastry(tier);
            nameLabel.text = $"{TierTable.Names[tier]}, please!";
        }

        public void Hide()
        {
            SetVisible(false);
            shownTier = -1;
            shownAt = -1f;
        }

        void Update()
        {
            if (shownAt < 0f || !IsBuilt) return;
            // pop: .35s back-out, matching cubic-bezier(.34,1.56,.64,1)
            float k = Mathf.Clamp01((Time.time - shownAt) / 0.35f);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            Content.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);

            // once the pop settles, the wanted dessert breathes ±8% to pull the eye
            float s = iconBaseScale;
            if (k >= 1f)
                s *= 1f + 0.08f * Mathf.Sin((Time.time - shownAt - 0.35f) * (2f * Mathf.PI / 1.1f));
            icon.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
