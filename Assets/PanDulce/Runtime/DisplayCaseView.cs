using PanDulce.Core;
using TMPro;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Display case — five slots. By default a sliding window follows progress (§8.4):
    /// the window opens at clamp(highestDiscovered - 1, 0, 6) and then holds still while
    /// its silhouettes are revealed one by one; it slides (by 3, keeping 2 for context)
    /// only once the player merges past the whole window. A scene-authored seat order
    /// (followProgress off) pins each seat to a chosen dessert instead.
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

        // Seat order is scene-authored (Studio ▸ Glass case seats). With followProgress on,
        // play mode ignores it and slides the classic progress window; with it off, each seat
        // keeps its authored dessert for the whole run — discovery still decides whether it
        // renders in colour or as a silhouette.
        [SerializeField] bool followProgress = true;
        [SerializeField] int[] seatTiers = { 0, 1, 2, 3, 4 };

        public bool FollowProgress => followProgress;
        public int[] SeatTiers => seatTiers;

        readonly SpriteRenderer[] icons = new SpriteRenderer[Slots];
        readonly TextMeshPro[] labels = new TextMeshPro[Slots];
        int shownStart, shownMax, shownProgress;

        protected override void Build()
        {
            shownStart = shownMax = -1;

            // One Desserts node holds a Slot_i folder per dessert, pivoted at the icon
            // centre, so the whole shelf — or a single slot — moves as one unit.
            float slotW = CaseWidth / Slots;
            var desserts = ViewFactory.NodeTransform(Content, "Desserts");
            for (int i = 0; i < Slots; i++)
            {
                float cx = CaseLeft + slotW * (i + 0.5f);
                var slot = ViewFactory.NodeTransform(desserts, $"Slot_{i}", cx, IconY);
                icons[i] = ViewFactory.Icon(slot, "Icon", database, i, 0f, 0f, IconRadius, "Case", 10);
                // The name band lands on the case's dark wooden lip now that the art is
                // painted rather than lineart — brown-on-brown vanished, so it reads cream.
                labels[i] = ViewFactory.Label(slot, "Label", "?", -30f, 32f, 60f, 9f,
                                              Palette.Cream, "Case", 13);
            }
        }

        public void Sync(MergeSim sim)
        {
            if (!IsBuilt) return;
            int maxD = sim.HighestDiscovered;

            // The window is patient: it holds still while its silhouettes fill in one by one,
            // and only slides once the player merges PAST it — never right after the first
            // new merge, and never before the last reveal has had its moment on the shelf.
            // Each slide keeps two known desserts for context and teases three new ones.
            int start = shownStart >= 0 ? shownStart
                                        : Mathf.Clamp(maxD - 1, 0, TierTable.Count - Slots);
            while (maxD > start + Slots - 1 && start < TierTable.Count - Slots)
                start = Mathf.Min(start + Slots - 2, TierTable.Count - Slots);

            // Silhouette seats count up toward their reveal, so a repaint is also due
            // whenever any tier's merge tally moves — not only on a discovery.
            int progress = 0;
            for (int t = 0; t < TierTable.Count; t++) progress += sim.MergeCount(t);

            if (start == shownStart && maxD == shownMax && progress == shownProgress) return;
            shownStart = start;
            shownMax = maxD;
            shownProgress = progress;

            for (int i = 0; i < Slots; i++)
            {
                int tier = followProgress
                    ? start + i
                    : Mathf.Clamp(seatTiers != null && i < seatTiers.Length ? seatTiers[i] : i,
                                  0, TierTable.Count - 1);
                bool found = sim.IsDiscovered(tier);
                if (database != null && icons[i] != null)
                {
                    icons[i].sprite = database.Pastry(tier);
                    ViewFactory.SetIcon(icons[i], IconRadius, database.DisplaySize(tier));
                }
                // Undiscovered entries render the sprite as a dark silhouette; the label
                // teases '?' until the first merge, then counts up ("1/3") to the reveal.
                if (icons[i] != null) icons[i].color = found ? Color.white : Silhouette;
                if (labels[i] != null)
                    labels[i].text = found ? (database != null ? database.Name(tier) : TierTable.Names[tier])
                        : sim.MergeCount(tier) > 0 ? $"{sim.MergeCount(tier)}/{sim.DiscoverNeed}"
                        : "?";
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
                ViewFactory.SetIcon(icons[i], IconRadius, database.DisplaySize(tier));
                labels[i].text = database.Name(tier);
            }
        }
#endif
    }
}
