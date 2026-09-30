using System;
using System.Windows.Media;
using Focus_Bear.StatsScreens;
using Focus_Bear.ToDoScreens;
using Focus_Bear.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    /// <summary>
    /// To-do chips drew their text in the accent itself: #E88800 is 2.6:1 on white, the grey #8A8F98 3.2:1, both
    /// under the 4.5:1 an 11 px label needs (the E2E suite's contrast check, 27 Sep). TextTint keeps the hue and
    /// darkens it (light theme) or lightens it (dark theme) until it reads.
    /// </summary>
    [TestClass]
    public class ToDoTextTintTests
    {
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

        private static Color Of(Brush b) => ((SolidColorBrush)b).Color;

        [TestMethod]
        public void Accents_read_at_4_5_to_1_on_a_light_card()
        {
            string before = AppSettings.GetInstance.sTheme;
            try
            {
                AppSettings.GetInstance.sTheme = "Light";
                foreach (Brush tint in new[] { ToDoUi.Accent, ToDoUi.Neutral, ToDoUi.Good, ToDoUi.Info, ToDoUi.Overdue })
                    Assert.IsTrue(Contrast(Of(ToDoUi.TextTint(tint)), Colors.White) >= 4.5, Of(tint).ToString());
            }
            finally { AppSettings.GetInstance.sTheme = before; }
        }

        [TestMethod]
        public void Accents_read_at_4_5_to_1_on_a_dark_card()
        {
            string before = AppSettings.GetInstance.sTheme;
            try
            {
                AppSettings.GetInstance.sTheme = "Dark";
                var card = Color.FromRgb(0x2E, 0x2E, 0x30);
                foreach (Brush tint in new[] { ToDoUi.Accent, ToDoUi.Neutral, ToDoUi.Good, ToDoUi.Info, ToDoUi.Overdue })
                    Assert.IsTrue(Contrast(Of(ToDoUi.TextTint(tint)), card) >= 4.5, Of(tint).ToString());
            }
            finally { AppSettings.GetInstance.sTheme = before; }
        }

        [TestMethod]
        public void A_readable_tint_is_left_as_it_is()
        {
            string before = AppSettings.GetInstance.sTheme;
            try
            {
                AppSettings.GetInstance.sTheme = "Light";
                var dark = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
                Assert.AreSame(dark, ToDoUi.TextTint(dark));
            }
            finally { AppSettings.GetInstance.sTheme = before; }
        }
    }
}
