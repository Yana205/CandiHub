using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Display case — five slots with a sliding window (§8.4), following progress:
    /// start = clamp(highestDiscovered - 1, 0, 6), so the slots ahead stay silhouetted
    /// (usually 3 of the 5) and each merge reveals the next tease.
    ///
    /// V2 layout: the case itself (glass, base, knob) is drawn by the LayoutArt prefab's
    /// GlassContainer piece; this view only places the desserts inside it. Undiscovered
    /// tiers render as near-black silhouettes so a new merge reveals them with a surprise.
    /// </summary>
    public sealed class DisplayCaseView : GeneratedView
    {
        public const int Slots = 5;

        // The drawn case interior, in stage px (canvas ÷2, -51 offset — see Version2ArtImport).
        const float CaseLeft = 60f, CaseWidth = 315f, IconY = 296f, IconRadius = 21f;

        /// <summary>Undiscovered desserts render as this flat silhouette.</summary>
        static readonly Color Silhouette = new Color(0.16f, 0.11f, 0.07f, 0.92f);

        readonly SpriteRenderer[] icons = new SpriteRenderer[Slots];
        readonly TextMeshPro[] labels = new TextMeshPro[Slots];
        int shownStart, shownMax;

        protected override void Build()
        {
            shownStart = shownMax = -1;

            // One Desserts node holds a Slot_i folder per dessert, pivoted at the icon
            // centre, so the whole shelf — or a single slot — moves as one unit.
            float slotW = CaseWidth / Slots;
            var desserts = ViewFactory.Node(Content, "Desserts").transform;
            for (int i = 0; i < Slots; i++)
            {
                float cx = CaseLeft + slotW * (i + 0.5f);
                var slot = ViewFactory.Node(desserts, $"Slot_{i}", cx, IconY).transform;
                icons[i] = ViewFactory.Icon(slot, "Icon", database, i, 0f, 0f, IconRadius, "Case", 10);
                labels[i] = ViewFactory.Label(slot, "Label", "?", -30f, 32f, 60f, 9f,
                                              Palette.Hex("#7a5735"), "Case", 13);
            }
        }

        public void Sync(MergeSim sim)
        {
            if (!IsBuilt) return;
            int maxD = sim.HighestDiscovered;
            int start = Mathf.Clamp(maxD - 1, 0, TierTable.Count - Slots);
            if (start == shownStart && maxD == shownMax) return;
            shownStart = start;
            shownMax = maxD;

            for (int i = 0; i < Slots; i++)
            {
                int tier = start + i;
                bool found = sim.IsDiscovered(tier);
                if (database != null)
                {
                    icons[i].sprite = database.Pastry(tier);
                    ViewFactory.SetIcon(icons[i], IconRadius, database.ArtScale(tier));
                }
                // Undiscovered entries render the sprite as a dark silhouette, label '?'.
                icons[i].color = found ? Color.white : Silhouette;
                labels[i].text = found ? (database != null ? database.Name(tier) : TierTable.Names[tier]) : "?";
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Edit-mode preview for the Studio window: put any dessert in any seat, full colour.
        /// Play mode's Sync repaints over this — shownStart is reset so it always does.
        /// </summary>
        public void EditorPreviewSeats(int[] tiers)
        {
            if (!IsBuilt || tiers == null || database == null) return;
            shownStart = shownMax = -1;
            for (int i = 0; i < Slots && i < tiers.Length; i++)
            {
                int tier = Mathf.Clamp(tiers[i], 0, TierTable.Count - 1);
                if (icons[i] == null || labels[i] == null) continue;
                icons[i].sprite = database.Pastry(tier);
                icons[i].color = Color.white;
                ViewFactory.SetIcon(icons[i], IconRadius, database.ArtScale(tier));
                labels[i].text = database.Name(tier);
            }
        }
#endif
    }
}
