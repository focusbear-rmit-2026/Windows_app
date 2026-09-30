using Focus_Bear.StatsScreens;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// The To-Do screen's small parts — the Mac's <c>ToDoChip</c>, <c>ToDoIconButton</c> and search field —
    /// on top of the Stats screen's palette and cards, so the native screens look like one app.
    /// </summary>
    internal static class ToDoUi
    {
        internal static readonly Brush Accent = StatsUi.Orange;
        internal static readonly Brush Overdue = StatsUi.Danger;
        internal static readonly Brush TodayTint = StatsUi.Orange;
        internal static readonly Brush Good = StatsUi.Positive;
        internal static readonly Brush Info = StatsUi.Info;
        internal static readonly Brush Neutral = Frozen(Color.FromRgb(0x8A, 0x8F, 0x98));

        /// <summary>Task and project icons: the emoji font first, so a glyph the UI font lacks isn't drawn as a box.</summary>
        internal static readonly FontFamily EmojiFont = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI");

        /// <summary>A Segoe MDL2 Assets glyph by code point.</summary>
        internal static string Glyph(int code) => ((char)code).ToString();

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// A tint as text: the same hue, darkened (light theme) or lightened (dark theme) until it reads at 4.5:1
        /// on the card. The accents themselves (#E88800 orange 2.6:1, #8A8F98 grey 3.2:1 on white) are too light
        /// for 11 px text; the chips and the TOP score used them as is.
        /// </summary>
        internal static Brush TextTint(Brush tint)
        {
            if (!(tint is SolidColorBrush solid)) return tint;
            bool dark = StatsUi.IsDark;
            Color bg = dark ? Color.FromRgb(0x2E, 0x2E, 0x30) : Colors.White;
            Color c = solid.Color;
            for (int i = 0; i < 20 && Contrast(c, bg) < 4.6; i++)
                c = dark ? Color.FromRgb(Step(c.R, 255), Step(c.G, 255), Step(c.B, 255))
                         : Color.FromRgb(Step(c.R, 0), Step(c.G, 0), Step(c.B, 0));
            return c == solid.Color ? tint : Frozen(c);
        }

        private static byte Step(byte v, byte toward) => (byte)(v + (toward - v) * 0.12);

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

        /// <summary>A small rounded badge: optional glyph + text, tinted.</summary>
        internal static Border Chip(string text, Brush tint = null, bool filled = false, string glyph = null)
        {
            tint = tint ?? Neutral;
            var label = new TextBlock
            {
                Text = string.IsNullOrEmpty(glyph) ? text : glyph + " " + text,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = filled ? Brushes.White : TextTint(tint),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return new Border
            {
                Child = label,
                Background = filled ? tint : StatsUi.Tint(tint, 0.14),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 6, 0),
                MaxWidth = 220,
            };
        }

        /// <summary>A bordered square toolbar button with a glyph.</summary>
        internal static Button IconButton(string glyph, string tooltip, bool active, RoutedEventHandler onClick, string automationId = null)
        {
            var text = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (active) text.Foreground = Brushes.White;
            else text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var border = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), Child = text };
            if (active)
            {
                border.Background = Accent;
                border.BorderBrush = Accent;
            }
            else
            {
                border.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
                border.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            }
            var button = new Button { Content = border, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = tooltip, Margin = new Thickness(6, 0, 0, 0) };
            if (onClick != null) button.Click += onClick;
            if (automationId != null) System.Windows.Automation.AutomationProperties.SetAutomationId(button, automationId);
            return button;
        }

        /// <summary>A text field in the app's rounded style with a placeholder drawn over it while empty.</summary>
        internal static Grid Field(TextBox box, string placeholder, double height = 32)
        {
            // The placeholder is the field's only visible label, so it is also its accessible name (the
            // editor's title box was read as just "edit"). An explicit name set by the caller wins.
            if (!string.IsNullOrWhiteSpace(placeholder)
                && string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(box)))
                System.Windows.Automation.AutomationProperties.SetName(box, placeholder.Trim().TrimEnd('…', '.'));
            if (height > 40)
            {
                // A box for paragraphs: the rounded style's template centres its text vertically.
                box.BorderThickness = new Thickness(1);
                box.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
                box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
                box.SetResourceReference(Control.BorderBrushProperty, "TrayCardBorder");
            }
            else box.SetResourceReference(FrameworkElement.StyleProperty, "RoundedTextBoxStyle");
            box.Height = height;
            box.FontSize = 13;
            box.VerticalContentAlignment = height > 40 ? VerticalAlignment.Top : VerticalAlignment.Center;
            // The style pads 6px top and bottom, which leaves a single line less room than its own height,
            // so the text scrolled up out of sight. The padding also lands twice (the template's host and
            // the text view both take it), so half of it lines the text up with the placeholder.
            box.Padding = height > 40 ? new Thickness(8, 6, 8, 6) : new Thickness(5, 0, 5, 0);
            box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ForegroundColor");
            var hint = new TextBlock
            {
                Text = placeholder,
                FontSize = 13,
                IsHitTestVisible = false,
                Margin = new Thickness(13, 0, 0, 0),
                VerticalAlignment = height > 40 ? VerticalAlignment.Top : VerticalAlignment.Center,
            };
            if (height > 40) hint.Margin = new Thickness(11, 7, 0, 0);
            hint.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            Action sync = () => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => sync();
            sync();
            var grid = new Grid();
            grid.Children.Add(box);
            grid.Children.Add(hint);
            return grid;
        }

        /// <summary>A drop-down: a pill that opens a menu of choices.</summary>
        internal static Button MenuButton(string text, bool active, Action<ToDoMenu> fill, string automationId = null)
        {
            var button = StatsUi.Pill(text + "  ▾", active, (s, e) => { });
            button.Click += (s, e) =>
            {
                var menu = new ToDoMenu(button);
                fill(menu);
                menu.Open();
            };
            if (automationId != null) System.Windows.Automation.AutomationProperties.SetAutomationId(button, automationId);
            return button;
        }

        /// <summary>The round completion checkbox of a row or the editor's title.</summary>
        internal static Button CompletionCheck(bool completed, string tooltip, RoutedEventHandler onClick, double size = 20)
        {
            var mark = new TextBlock
            {
                Text = completed ? Glyph(0xE73E) : "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = size * 0.55,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var circle = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                BorderThickness = new Thickness(completed ? 0 : 1.6),
                Background = completed ? Good : Brushes.Transparent,
                Child = mark,
            };
            if (!completed) circle.SetResourceReference(Border.BorderBrushProperty, "TraySubtitleColor");
            var button = new Button { Content = circle, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = tooltip, VerticalAlignment = VerticalAlignment.Top };
            button.Click += onClick;
            return button;
        }

        /// <summary>A small secondary button for the action bar: "Edit", "Snooze"…</summary>
        internal static Button SmallButton(string text, RoutedEventHandler onClick, bool primary = false, string glyph = null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0) };
            if (!string.IsNullOrEmpty(glyph))
            {
                row.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Foreground = Brushes.Black });
            }
            row.Children.Add(new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black });
            var button = new Button { Content = row, Height = 26, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 0) };
            button.SetResourceReference(FrameworkElement.StyleProperty, primary ? "BtnStyleLightOrangeBordered" : "BtnStyleClearOrangeBordered");
            if (onClick != null) button.Click += onClick;
            return button;
        }
    }

    /// <summary>
    /// Keeps a To-Do / Projects dialog on its screen: no taller or wider than the work area of the monitor its
    /// owner is on (less a margin), centred on the owner but pushed back inside that work area. Called before
    /// the window is shown; it takes over from <c>CenterOwner</c>, which happily put a 720-tall editor's top —
    /// and the Save/Cancel header — above a short screen.
    /// </summary>
    internal static class DialogFit
    {
        /// <summary>For screen tests only: a pretend work area (e.g. a short laptop screen at 175%).</summary>
        internal static Func<Window, FocusBear.Core.ToDo.DialogRect> WorkAreaOverride;

        internal static void Apply(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.SourceInitialized += (s, e) => Place(window, afterLayout: false);
            window.Loaded += (s, e) => Place(window, afterLayout: true);
        }

        /// <summary>The work area, in this window's DIPs, of the monitor its owner (else itself) is on.</summary>
        internal static FocusBear.Core.ToDo.DialogRect WorkArea(Window window)
        {
            if (WorkAreaOverride != null) return WorkAreaOverride(window);
            var handle = window.Owner != null ? new System.Windows.Interop.WindowInteropHelper(window.Owner).Handle : IntPtr.Zero;
            if (handle == IntPtr.Zero) handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            var toDips = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var topLeft = toDips.Transform(new Point(area.Left, area.Top));
            var bottomRight = toDips.Transform(new Point(area.Right, area.Bottom));
            return new FocusBear.Core.ToDo.DialogRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }

        private static void Place(Window window, bool afterLayout)
        {
            var work = WorkArea(window);
            window.MaxHeight = FocusBear.Core.ToDo.DialogPlacement.MaxHeight(work);
            window.MaxWidth = FocusBear.Core.ToDo.DialogPlacement.MaxWidth(work);
            window.MinHeight = Math.Min(window.MinHeight, window.MaxHeight);
            window.MinWidth = Math.Min(window.MinWidth, window.MaxWidth);
            var owner = window.Owner;
            // A maximised owner's Left/Top are its restore position, not where it is; centre on the screen instead.
            FocusBear.Core.ToDo.DialogRect? ownerRect = owner != null && owner.WindowState == WindowState.Normal && owner.ActualWidth > 0
                ? new FocusBear.Core.ToDo.DialogRect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight)
                : (FocusBear.Core.ToDo.DialogRect?)null;
            double width = afterLayout ? window.ActualWidth : double.IsNaN(window.Width) ? window.MinWidth : window.Width;
            double height = afterLayout ? window.ActualHeight : double.IsNaN(window.Height) ? window.MinHeight : window.Height;
            var fit = FocusBear.Core.ToDo.DialogPlacement.Fit(work, ownerRect, width, height);
            if (!double.IsNaN(window.Width) && window.Width > fit.Width) window.Width = fit.Width;
            if (!double.IsNaN(window.Height) && window.Height > fit.Height) window.Height = fit.Height;
            window.Left = fit.Left;
            window.Top = fit.Top;
        }
    }

    /// <summary>
    /// A drop-down menu drawn in the app's own theme. WPF's ContextMenu keeps the system's light look in dark
    /// mode (the app has no style for it), so the To-Do screen draws its menus itself.
    /// </summary>
    internal sealed class ToDoMenu
    {
        private readonly Popup _popup;
        private readonly StackPanel _items = new StackPanel();
        private readonly ScrollViewer _scroll;

        internal ToDoMenu(UIElement target, PlacementMode placement = PlacementMode.Bottom)
        {
            _scroll = new ScrollViewer { Content = _items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 420 };
            var card = new Border { Child = _scroll, Padding = new Thickness(4), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Margin = new Thickness(0, 4, 10, 10), MinWidth = 160 };
            card.SetResourceReference(Border.BackgroundProperty, "BackgroundColor");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.25 };
            _popup = new Popup { PlacementTarget = target, Placement = placement, StaysOpen = false, AllowsTransparency = true, Child = card };
        }

        internal double MaxHeight { get { return _scroll.MaxHeight; } set { _scroll.MaxHeight = value; } }

        /// <param name="icon">Optional Segoe MDL2 Assets code point drawn in the leading column (the Mac's
        /// SF Symbol for the item), in the theme foreground. A checked item shows the check instead.</param>
        internal void Add(string header, bool isChecked, Action onClick, string automationId = null, bool enabled = true, string tooltip = null, int icon = 0)
        {
            var check = new TextBlock { Text = isChecked ? ToDoUi.Glyph(0xE73E) : "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 11, Width = 20, VerticalAlignment = VerticalAlignment.Center, Foreground = ToDoUi.Accent };
            if (icon != 0 && !isChecked)
            {
                check.Text = ToDoUi.Glyph(icon);
                check.FontSize = 14;
                check.Width = 26;
                check.Padding = new Thickness(2, 0, 0, 0);
                check.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            }
            var label = new TextBlock { Text = header, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 380 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(check);
            row.Children.Add(label);
            var highlight = new Border { Child = row, Padding = new Thickness(6, 6, 14, 6), CornerRadius = new CornerRadius(5), Background = Brushes.Transparent };
            highlight.MouseEnter += (s, e) => highlight.SetResourceReference(Border.BackgroundProperty, "TrayIconSquareBg");
            highlight.MouseLeave += (s, e) => highlight.Background = Brushes.Transparent;
            var button = new Button { Content = highlight, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, IsEnabled = enabled, ToolTip = tooltip, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            if (!enabled) button.Opacity = 0.5;
            button.Click += (s, e) => { _popup.IsOpen = false; onClick(); };
            if (automationId != null) System.Windows.Automation.AutomationProperties.SetAutomationId(button, automationId);
            _items.Children.Add(button);
        }

        internal void AddSeparator()
        {
            var line = new Border { Height = 1, Margin = new Thickness(6, 4, 6, 4) };
            line.SetResourceReference(Border.BackgroundProperty, "TrayCardBorder");
            _items.Children.Add(line);
        }

        internal void Open() => _popup.IsOpen = true;
    }
}
