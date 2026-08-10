using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>The NEXT plaque at the cloth's top-right — §8.5 step 7. Does NOT shake.</summary>
    public sealed class NextPlaqueView : GeneratedView
    {
        /// <summary>Stage-px radius of the preview icon, shared by Build and Sync.</summary>
        const float IconRadius = 13f;

        SpriteRenderer icon;
        int shownTier = -1, shownSkin = -1;

        protected override void Build()
        {
            shownTier = -1;
            var t = Content;
            float x = SimField.BR - 66f;

            Sprite art = skin != null ? skin.NextPlaque : null;
            if (art != null)
            {
                // The drawing carries its own carved frame, so the separate Border panel
                // goes — a wood rim behind a wood rim only thickens it. Taller than the flat
                // plaque (54 → 60): the frame is chunky top and bottom, and the word plus the
                // icon have to clear it. 9-slicing keeps that frame an even thickness.
                //
                // Authored placement (Lital, 2026-08-08): Face at local y 0.43, Word 0.57,
                // Icon 0.35 — one base shift, the three offsets below are unchanged.
                const float y = -71f;
                ViewFactory.Rect(t, "Face", art, x - 2f, y - 2f, 70f, 60f, Color.white, "PlayArea", 63);
                ViewFactory.Label(t, "Word", "NEXT", x - 2f, y + 14f, 70f, 11f,
                                  Palette.Hex("#a58358"), "PlayArea", 64);
                icon = ViewFactory.Icon(t, "Icon", database, 0, x + 33f, y + 36f,
                                        IconRadius, "PlayArea", 64);
            }
            else
            {
                const float y = 20f;
                ViewFactory.Panel(t, "Border", x - 2f, y - 2f, 68f, 54f, 13, Palette.Wood, "PlayArea", 62);
                ViewFactory.Panel(t, "Face", x, y, 64f, 50f, 12,
                                  new Color(1f, 243f/255f, 221f/255f, 0.95f), "PlayArea", 63);
                ViewFactory.Label(t, "Word", "NEXT", x, y + 12f, 64f, 11f,
                                  Palette.Hex("#a58358"), "PlayArea", 64);
                icon = ViewFactory.Icon(t, "Icon", database, 0, x + 32f, y + 34f,
                                        IconRadius, "PlayArea", 64);
            }
        }

        public void Sync(int nextTier, int nextSkin)
        {
            if (!IsBuilt || icon == null) return;
            if (nextTier == shownTier && nextSkin == shownSkin) return;
            shownTier = nextTier;
            shownSkin = nextSkin;
            if (database != null)
            {
                icon.sprite = database.Pastry(nextTier, nextSkin);
                ViewFactory.SetIcon(icon, IconRadius, database.DisplaySize(nextTier));
            }
        }
    }
}
