using Focus_Bear.Utils;
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Focus_Bear.LateNoMore
{
    /// <summary>
    /// The small code-built kit Late No More's screens share, in the style of <c>FocusMusicView</c> / <c>BlockListsView</c>:
    /// theme keys BackgroundColor / ForegroundColor / TrayCardBg / TrayCardBorder / TraySubtitleColor and the #DE7400 accent.
    /// </summary>
    internal static class LnmUi
    {
        public static readonly Color AccentColor = Color.FromRgb(0xDE, 0x74, 0x00);
        public static readonly Brush Accent = Frozen(new SolidColorBrush(AccentColor));
        public static readonly Brush AccentTint = Frozen(new SolidColorBrush(Color.FromArgb(0x1F, 0xDE, 0x74, 0x00)));
        public static readonly Brush AccentEdge = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xDE, 0x74, 0x00)));
        public static readonly Brush LateRed = Frozen(new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x3E)));
        public static readonly Brush OkGreen = Frozen(new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x5B)));
        public static readonly FontFamily Symbols = new FontFamily("Segoe MDL2 Assets");

        public const int GlyphCalendar = 0xE787, GlyphClock = 0xE823, GlyphClose = 0xE711, GlyphAdd = 0xE710, GlyphLink = 0xE71B,
            GlyphVideo = 0xE714, GlyphCheck = 0xE73E, GlyphSpeaker = 0xE767, GlyphInfo = 0xE946, GlyphWarning = 0xE7BA, GlyphSnooze = 0xE708;

        public static Brush Frozen(SolidColorBrush brush)
        {
            brush.Freeze();
            return brush;
        }

        public static void ApplyAppFont(Control target)
        {
            try
            {
                string font = AppSettings.GetInstance.sSelectedAppFont;
                if (string.IsNullOrEmpty(font)) return;
                if (font == "Poppins" || font == "IntelOne Mono" || font == "OpenDyslexic3 Regular")
                    target.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", font));
                else
                    target.FontFamily = new FontFamily(font);
            }
            catch (Exception ex) { LogWriter.LogWrite("LateNoMore ApplyAppFont - " + ex.Message); }
        }

        public static string S(string fallback, params string[] path)
        {
            try
            {
                var settings = AppSettings.GetInstance;
                return settings.ConfigFileStrings == null ? fallback : settings.GetConfigStringSafe(fallback, path);
            }
            catch { return fallback; }
        }

        public static TextBlock Text(string text, double size, FontWeight? weight = null, bool secondary = false, bool wrap = true)
        {
            var block = new TextBlock
            {
                Text = text ?? "",
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TraySubtitleColor" : "ForegroundColor");
            return block;
        }

        public static TextBlock Glyph(int code, double size, string colorKey = "ForegroundColor")
        {
            var block = new TextBlock { Text = ((char)code).ToString(), FontFamily = Symbols, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
            block.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
            return block;
        }

        public static Border Card(UIElement child, Thickness padding, double radius = 12)
        {
            var card = new Border { Child = child, Padding = padding, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return card;
        }

        public static Border Divider(Thickness margin)
        {
            var line = new Border { Height = 1, Margin = margin };
            line.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            return line;
        }

        /// <summary>The accent disc with a white glyph (Late No More's badge).</summary>
        public static FrameworkElement Badge(int glyph, double size = 30, Brush fill = null)
        {
            var grid = new Grid { Width = size, Height = size };
            grid.Children.Add(new Ellipse { Fill = fill ?? Accent });
            grid.Children.Add(new TextBlock { Text = ((char)glyph).ToString(), FontFamily = Symbols, FontSize = size * 0.46, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return grid;
        }

        private static ControlTemplate PlainTemplate()
        {
            var template = new ControlTemplate(typeof(ButtonBase));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            template.VisualTree = presenter;
            return template;
        }

        public static Button Clickable(UIElement content, Action onClick, string automationId, string name)
        {
            if (content is Border border && border.Background == null) border.Background = Brushes.Transparent;
            if (content is Panel panel && panel.Background == null) panel.Background = Brushes.Transparent;
            var button = new Button { Content = content, Template = PlainTemplate(), Cursor = Cursors.Hand, Background = Brushes.Transparent, FocusVisualStyle = null };
            button.Click += (s, e) => onClick?.Invoke();
            if (automationId != null) AutomationProperties.SetAutomationId(button, automationId);
            if (name != null) AutomationProperties.SetName(button, name);
            return button;
        }

        /// <summary>A rounded button: filled accent for the main action, outlined otherwise.</summary>
        public static Button Pill(string title, bool primary, Action onClick, string automationId, int glyph = 0, double fontSize = 14)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            if (glyph != 0)
            {
                var icon = new TextBlock { Text = ((char)glyph).ToString(), FontFamily = Symbols, FontSize = fontSize - 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 0) };
                if (primary) icon.Foreground = Brushes.White; else icon.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                row.Children.Add(icon);
            }
            var label = new TextBlock { Text = title, FontSize = fontSize, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            if (primary) label.Foreground = Brushes.White; else label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            row.Children.Add(label);
            var face = new Border { Child = row, CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 8, 18, 8), BorderThickness = new Thickness(primary ? 0 : 1) };
            if (primary) face.Background = Accent;
            else
            {
                face.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                face.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            }
            var button = Clickable(face, onClick, automationId, title);
            button.MouseEnter += (s, e) => face.Opacity = 0.88;
            button.MouseLeave += (s, e) => face.Opacity = 1;
            button.IsEnabledChanged += (s, e) => face.Opacity = button.IsEnabled ? 1 : 0.45;
            return button;
        }

        /// <summary>A quiet text button (secondary actions).</summary>
        public static Button TextButton(string title, Action onClick, string automationId, bool underline = false, Brush colour = null)
        {
            var label = Text(title, 13, FontWeights.SemiBold, secondary: colour == null, wrap: false);
            if (colour != null) label.Foreground = colour;
            if (underline) label.TextDecorations = TextDecorations.Underline;
            var face = new Border { Child = label, Padding = new Thickness(6, 4, 6, 4), CornerRadius = new CornerRadius(6) };
            var button = Clickable(face, onClick, automationId, title);
            button.MouseEnter += (s, e) => face.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            button.MouseLeave += (s, e) => face.Background = Brushes.Transparent;
            return button;
        }

        public static Button IconButton(int glyph, string tooltip, Action onClick, string automationId, double size = 30)
        {
            var icon = Glyph(glyph, 12, "TraySubtitleColor");
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            var face = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Child = icon, Background = Brushes.Transparent };
            var button = Clickable(face, onClick, automationId, tooltip);
            button.ToolTip = tooltip;
            button.MouseEnter += (s, e) => face.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            button.MouseLeave += (s, e) => face.Background = Brushes.Transparent;
            return button;
        }

        public static TextBox Input(string automationId, string name)
        {
            var box = new TextBox { FontSize = 13, Padding = new Thickness(8, 7, 8, 7), BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center };
            box.SetResourceReference(Control.BackgroundProperty, "BackgroundColor");
            box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            box.SetResourceReference(Control.BorderBrushProperty, "TrayCardBorder");
            box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(box, automationId);
            AutomationProperties.SetName(box, name);
            return box;
        }

        /// <summary>The app's Mac-style switch (App.xaml <c>MacStyleToggleSwitch</c>).</summary>
        public static ToggleButton Switch(bool on, string automationId, string name)
        {
            var toggle = new ToggleButton { IsChecked = on, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, Focusable = true };
            try { toggle.Style = (Style)Application.Current.FindResource("MacStyleToggleSwitch"); } catch { /* falls back to a plain toggle */ }
            AutomationProperties.SetAutomationId(toggle, automationId);
            AutomationProperties.SetName(toggle, name);
            return toggle;
        }

        /// <summary>A keyword chip with its remove ✕.</summary>
        public static FrameworkElement Chip(string text, Action remove, string automationId)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var label = Text(text, 13, wrap: false);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MaxWidth = 260;
            row.Children.Add(label);
            var x = Clickable(new Border { Padding = new Thickness(6, 2, 2, 2), Child = Glyph(GlyphClose, 9, "TraySubtitleColor") }, remove, automationId, "Remove " + text);
            x.ToolTip = "Remove";
            x.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(x);
            var chip = new Border { Child = row, CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 5, 8, 5), Margin = new Thickness(0, 0, 8, 8), BorderThickness = new Thickness(1), Background = AccentTint, BorderBrush = AccentEdge };
            AutomationProperties.SetName(chip, text);
            return chip;
        }
    }
}
