using System;

namespace FocusBear.Core.Ui
{
    /// <summary>
    /// Icon fonts: their glyphs live in the Private Use Area, so ordinary text set in one renders as boxes. A
    /// tooltip inherits its font from the element it is on, which is how an (i) glyph's tooltip came out as
    /// "□□□ □□□".
    /// </summary>
    public static class IconFonts
    {
        private static readonly string[] Families =
        {
            "Segoe MDL2 Assets", "Segoe Fluent Icons", "Segoe UI Symbol", "Segoe UI Emoji", "Marlett", "Webdings",
            "Wingdings", "FontAwesome", "Font Awesome", "Material Icons", "Material Symbols",
        };

        /// <summary>
        /// Whether a WPF FontFamily source (e.g. "Segoe MDL2 Assets", "Segoe Fluent Icons, Segoe MDL2 Assets",
        /// "pack://.../#Font Awesome 6 Free") names an icon font first - the one text would actually use.
        /// </summary>
        public static bool IsIconFont(string familySource)
        {
            if (string.IsNullOrWhiteSpace(familySource)) return false;
            // "base uri#Family, fallback": the family name follows the '#' (the uri itself may hold commas).
            string first = familySource;
            int hash = first.IndexOf('#');
            if (hash >= 0) first = first.Substring(hash + 1);
            first = first.Split(',')[0].Trim();
            foreach (string family in Families)
            {
                if (first.StartsWith(family, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
