using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
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
            if (database != null)
            {
                icon.sprite = database.Pastry(nextTier);
                ViewFactory.SetIcon(icon, 13f, database.ArtScale(nextTier));
            }
        }
    }
}
