using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// Text colours of the two themes against the backgrounds they are drawn on, at WCAG AA (4.5:1 for body text,
    /// 3:1 for large). The E2E suite's contrast check (28 Sep) found, in the Dark theme: white labels on the orange
    /// selected Settings row and the expanded Focus Tools card (2.4:1), the #B35C00 headings on the dark cards (2.3:1)
    /// and the grey hint text (4.4:1); and in the Light theme the ratio description on its cream card.
    /// Read from Theme/LightTheme.xaml and Theme/DarkTheme.xaml themselves.
    /// </summary>
    [TestClass]
    public class ThemeContrastTests
    {
        // The orange fills text sits on: the Settings / Focus list selection and the tray's expanded card.
        private static readonly Color SelectionOrange = Color.FromRgb(0xE9, 0x91, 0x2C);
        private static readonly Color ExpandedCardOrange = Color.FromRgb(0xF0, 0x93, 0x2A);

        private static string ThemeDir([CallerFilePath] string here = "") =>
            Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(here)), "Theme");

        private static Dictionary<string, Color> Brushes(string file)
        {
            var map = new Dictionary<string, Color>(StringComparer.Ordinal);
            string xaml = File.ReadAllText(Path.Combine(ThemeDir(), file));
            foreach (Match m in Regex.Matches(xaml, "<SolidColorBrush\\s+x:Key=\"([^\"]+)\"\\s+Color=\"([^\"]+)\""))
                map[m.Groups[1].Value] = (Color)ColorConverter.ConvertFromString(m.Groups[2].Value);
            return map;
        }

        private static double Luminance(Color c)
        {
            double Ch(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static void AssertReads(Dictionary<string, Color> theme, string text, Color background, double needed, string where)
        {
            Assert.IsTrue(theme.ContainsKey(text), $"{text} is missing from the theme");
            double ratio = Contrast(theme[text], background);
            Assert.IsTrue(ratio >= needed, $"{text} {theme[text]} on {where} {background}: {ratio:F2}:1, needs {needed}:1");
        }

        [DataTestMethod]
        [DataRow("LightTheme.xaml")]
        [DataRow("DarkTheme.xaml")]
        public void Text_on_the_orange_selection_reads(string file)
        {
            var t = Brushes(file);
            foreach (var fill in new[] { SelectionOrange, ExpandedCardOrange })
            {
                AssertReads(t, "OnOrangeText", fill, 4.5, "the orange fill");
                AssertReads(t, "OnOrangeSubtitle", fill, 4.5, "the orange fill");
            }
        }

        [DataTestMethod]
        [DataRow("LightTheme.xaml")]
        [DataRow("DarkTheme.xaml")]
        public void Orange_headings_read_on_the_page_and_the_cards(string file)
        {
            var t = Brushes(file);
            AssertReads(t, "OrangeTextColor", t["BackgroundColor"], 4.5, "BackgroundColor");
            AssertReads(t, "OrangeTextColor", t["SoftCreamColor"], 4.5, "SoftCreamColor");
            AssertReads(t, "OrangeTextColor", t["TrayCardBg"], 4.5, "TrayCardBg");
        }

        [DataTestMethod]
        [DataRow("LightTheme.xaml")]
        [DataRow("DarkTheme.xaml")]
        public void Secondary_text_reads_on_the_cream_cards(string file)
        {
            var t = Brushes(file);
            // A margin over 4.5 for 12 px text: anti-aliasing lightens thin strokes (the check samples the screen).
            AssertReads(t, "CardSubtitleColor", t["SoftCreamColor"], 5.0, "SoftCreamColor");
            AssertReads(t, "ForegroundGrey", t["SoftCreamColor"], 5.0, "SoftCreamColor");
        }
    }
}
