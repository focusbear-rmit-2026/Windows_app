using Focus_Bear.Utils;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace Focus_Bear.StatsScreens
{
    /// <summary>
    /// The Stats screen's shared building blocks — the Mac's <c>StatsCard</c>, <c>StatTile</c>, <c>StatsTabButton</c>,
    /// empty / error / loading states — made from the app's theme resources, so every tab looks alike and all
    /// of them follow light and dark mode.
    /// </summary>
    internal static class StatsUi
    {
        // The Mac's StatsPalette accents. Orange is the app's own; the rest are the system colours it uses.
        internal static readonly Color OrangeColor = (Color)ColorConverter.ConvertFromString("#E88800");
        internal static readonly Brush Orange = Freeze(new SolidColorBrush(OrangeColor));
        /// <summary>Text and icons on an Orange fill: white, as on the focus start screen's Start button.</summary>
        internal static readonly Brush OnOrange = Brushes.White;
        /// <summary>
        /// Orange for text on the card background: #B35C00 is 4.7:1 on white (#E88800 was 2.6:1). On the dark card
        /// (#2E2E30) #B35C00 is only 2.9:1, so dark mode uses #F09A3E (6.1:1).
        /// </summary>
        internal static Brush OrangeText => IsDark ? OrangeTextDark : OrangeTextLight;

        /// <summary>
        /// The orange text colour as a theme resource (OrangeTextColor), so a screen built in one theme follows a switch
        /// to the other. <see cref="OrangeText"/> is read once, when the text is made: the Stats streak and percentages
        /// stayed #B35C00 (2.9:1) after switching to Dark.
        /// </summary>
        internal static T WithOrangeText<T>(T text) where T : TextBlock
        {
            text.SetResourceReference(TextBlock.ForegroundProperty, "OrangeTextColor");
            return text;
        }
        private static readonly Brush OrangeTextLight = Freeze(new SolidColorBrush(Color.FromRgb(0xB3, 0x5C, 0x00)));
        private static readonly Brush OrangeTextDark = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0x9A, 0x3E)));
        internal static readonly Brush Info = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F80ED")));
        internal static readonly Brush Positive = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));
        internal static readonly Brush Warning = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2A400")));
        internal static readonly Brush Highlight = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B51E0")));
        internal static readonly Brush Danger = Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EB5757")));

        private static Brush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

        internal static bool IsDark => AppSettings.GetInstance.sTheme != null && AppSettings.GetInstance.sTheme != "Light";

        /// <summary>Web <c>bg-gray-200</c> / <c>dark:bg-gray-600</c>: a day with no completions still reads as a cell.</summary>
        internal static Brush EmptyCell => Freeze(new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDark ? "#4B5563" : "#E5E7EB")));

        internal static Brush Tint(Brush brush, double opacity)
        {
            var solid = brush as SolidColorBrush;
            var color = solid != null ? solid.Color : OrangeColor;
            return Freeze(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B)));
        }

        internal static T Themed<T>(this T element, DependencyProperty property, string resourceKey) where T : FrameworkElement
        {
            element.SetResourceReference(property, resourceKey);
            return element;
        }

        internal static TextBlock Text(string text, double size = 13, FontWeight? weight = null, bool secondary = false, bool wrap = true)
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

        internal static TextBlock Heading(string text) => Text(text, 15, FontWeights.SemiBold);

        /// <summary>A rounded card on the tray menu's palette, with an optional title.</summary>
        internal static Border Card(UIElement content, string title = null, Thickness? padding = null)
        {
            var stack = new StackPanel();
            if (!string.IsNullOrEmpty(title))
            {
                var heading = Heading(title);
                heading.Margin = new Thickness(0, 0, 0, 12);
                stack.Children.Add(heading);
            }
            if (content != null) stack.Children.Add(content);
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = padding ?? new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16),
                Child = stack,
            };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return card;
        }

        /// <summary>A labelled number, the Mac's <c>StatTile</c>.</summary>
        internal static Border Tile(string label, string value, Brush color, bool wrapValue = false)
        {
            var stack = new StackPanel();
            var caption = Text(label, 12, secondary: true);
            caption.MaxHeight = 36;
            stack.Children.Add(caption);
            var number = new TextBlock
            {
                Text = value,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = wrapValue ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = value,
            };
            if (wrapValue) number.FontSize = 18;
            stack.Children.Add(number);
            var tile = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 12, 12),
                Child = stack,
                MinHeight = 86,
            };
            tile.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            tile.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return tile;
        }

        /// <summary>Tiles in equal columns that fill the width, like the Mac's adaptive grid.</summary>
        internal static UniformGrid Tiles(params UIElement[] tiles)
        {
            var grid = new UniformGrid { Columns = Math.Min(Math.Max(tiles.Length, 1), 4), Margin = new Thickness(0, 0, -12, 4) };
            foreach (var tile in tiles) grid.Children.Add(tile);
            return grid;
        }

        /// <summary>A capsule toggle — the tab strip, the tone chips, the level headers.</summary>
        internal static Button Pill(string text, bool selected, RoutedEventHandler onClick, string icon = null, bool enabled = true)
        {
            var label = new TextBlock
            {
                Text = string.IsNullOrEmpty(icon) ? text : icon + "  " + text,
                FontSize = 13,
                FontWeight = selected ? FontWeights.SemiBold : FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (selected) label.Foreground = OnOrange;
            else label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var border = new Border
            {
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 0, 12, 0),
                Height = 30,
                Child = label,
            };
            if (selected)
            {
                border.Background = Orange;
                border.BorderBrush = Orange;
            }
            else
            {
                border.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                border.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            }
            var button = new Button { Content = border, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 6, 6), IsEnabled = enabled };
            button.Template = PlainTemplate();
            if (!enabled) button.Opacity = 0.55;
            button.Click += onClick;
            return button;
        }

        private static ControlTemplate _plainTemplate;

        /// <summary>A button that is only its content (the content draws the chrome).</summary>
        internal static ControlTemplate PlainTemplate()
        {
            if (_plainTemplate != null) return _plainTemplate;
            var template = new ControlTemplate(typeof(Button));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            template.VisualTree = presenter;
            template.Seal();
            _plainTemplate = template;
            return template;
        }

        /// <summary>The app's orange buttons. Their templates ignore Padding, so the label carries the margin.</summary>
        internal static Button Button(string text, RoutedEventHandler onClick, bool primary = false, double height = 32)
        {
            var button = new Button
            {
                // The orange button faces are light in both themes, so the label is always dark (as the app's
                // ButtonTextBlockStyle is). Inheriting instead picked up Preferences' light dark-mode text.
                Content = new TextBlock { Text = text, Margin = new Thickness(16, 0, 16, 0), FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black },
                Height = height,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, primary ? "BtnStyleLightOrangeBordered" : "BtnStyleClearOrangeBordered");
            button.Click += onClick;
            return button;
        }

        internal static FrameworkElement Loading()
        {
            var bar = new ProgressBar { IsIndeterminate = true, Height = 3, Width = 220, Foreground = Orange, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            return new Border { Child = bar, Padding = new Thickness(0, 60, 0, 60), HorizontalAlignment = HorizontalAlignment.Center };
        }

        internal static FrameworkElement Error(StatsStrings strings, Action retry)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40) };
            stack.Children.Add(Centered(Text("⚠", 28, secondary: true)));
            var message = Centered(Text(strings.LoadFailed, 13, secondary: true));
            message.Margin = new Thickness(0, 8, 0, 12);
            message.MaxWidth = 420;
            stack.Children.Add(message);
            var button = Button(strings.Retry, (s, e) => retry());
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.Margin = new Thickness(0);
            stack.Children.Add(button);
            return stack;
        }

        internal static FrameworkElement Empty(string icon, string title, string description = null)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40), MaxWidth = 480 };
            stack.Children.Add(Centered(Text(icon, 34, secondary: true)));
            var heading = Centered(Text(title, 15, FontWeights.SemiBold));
            heading.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(heading);
            if (!string.IsNullOrEmpty(description))
            {
                var body = Centered(Text(description, 13, secondary: true));
                body.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(body);
            }
            return stack;
        }

        private static TextBlock Centered(TextBlock block)
        {
            block.TextAlignment = TextAlignment.Center;
            block.HorizontalAlignment = HorizontalAlignment.Center;
            return block;
        }

        /// <summary>A segmented Daily / Monthly / Yearly control.</summary>
        internal static FrameworkElement Segmented<T>(T[] options, T selected, Func<T, string> title, Action<T> onSelect)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var option in options)
            {
                var captured = option;
                bool isSelected = Equals(option, selected);
                var label = new TextBlock { Text = title(option), FontSize = 13, FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
                // The selected segment reads by its orange text and outline, not only by the tinted fill: in dark mode
                // that fill (#4A3B28) on the control's #3A3A3C barely showed which one was chosen.
                label.SetResourceReference(TextBlock.ForegroundProperty, isSelected ? "OrangeTextColor" : "ForegroundColor");
                var segment = new Border { CornerRadius = new CornerRadius(7), Padding = new Thickness(13, 4, 13, 4), Margin = new Thickness(2), Child = label, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
                if (isSelected)
                {
                    segment.SetResourceReference(Border.BackgroundProperty, "TrayIconSquareBg");
                    segment.BorderBrush = Orange;
                }
                var button = new Button { Content = segment, Template = PlainTemplate(), Cursor = System.Windows.Input.Cursors.Hand };
                button.Click += (s, e) => onSelect(captured);
                row.Children.Add(button);
            }
            var frame = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(2), Child = row, HorizontalAlignment = HorizontalAlignment.Left };
            frame.SetResourceReference(Border.BackgroundProperty, "TrayCircleBtnBg");
            return frame;
        }

        internal static void OpenExternal(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("Stats open link ex - " + ex.Message); }
        }

        internal static Hyperlink Link(string text, string url)
        {
            var link = new Hyperlink(new Run(text)) { Foreground = Info, Cursor = System.Windows.Input.Cursors.Hand };
            link.Click += (s, e) => OpenExternal(url);
            return link;
        }
    }
}
