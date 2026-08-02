using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The design palette (§8.8) and the cloth's shading multiply.
    ///
    /// The project renders in Linear colour space but every colour in the design is an sRGB
    /// hex string. Never build a Color by dividing hex components by 255 — in Linear that
    /// renders washed out. Everything here goes through ColorUtility, which converts.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Masa      = Hex("#f2ddb7");
        public static readonly Color Crust     = Hex("#8a5a33");
        public static readonly Color DarkCrust = Hex("#6f4a2c");
        public static readonly Color Cream     = Hex("#fff3dd");
        public static readonly Color Wood      = Hex("#a97448");
        public static readonly Color Page      = Hex("#cfa06b");

        public static readonly Color WallFill  = Hex("#e6c194");
        public static readonly Color BarTop    = Hex("#a97448");
        public static readonly Color BarBottom = Hex("#96633c");
        public static readonly Color BarBorder = Hex("#7c5231");
        public static readonly Color ChipFill  = Hex("#7c5231");
        public static readonly Color Amber     = Hex("#f0b64f");
        public static readonly Color AmberDeep = Hex("#dd9a2e");
        public static readonly Color Danger    = Hex("#d94f43");
        public static readonly Color Locked    = Hex("#c3ae93");

        /// <summary>The four furoshiki swatches (§6.2).</summary>
        public static readonly Color[] ClothSwatches =
        {
            Hex("#cf6b5c"), Hex("#6a7fb0"), Hex("#7fa864"), Hex("#d9a441")
        };

        public static Color Hex(string s)
            => ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;

        /// <summary>
        /// The cloth's channel multiply, done in GAMMA space (§8.5).
        ///
        /// The mock multiplies sRGB channels. Multiplying Linear values by the same factor
        /// gives a visibly different result — muddy in shadow, blown out in highlight — so
        /// this converts to gamma, multiplies, and converts back. Every mix(C, f) in the
        /// design goes through here.
        /// </summary>
        public static Color Mix(Color c, float f)
        {
            Color g = c.gamma;
            return new Color(Mathf.Min(1f, g.r * f),
                             Mathf.Min(1f, g.g * f),
                             Mathf.Min(1f, g.b * f), c.a).linear;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
