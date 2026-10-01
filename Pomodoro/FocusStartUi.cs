using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Pomodoro
{
    /// <summary>The start screens' colours — the Mac's scoped <c>focusStart*</c> tokens, light and dark.</summary>
    internal sealed class FocusStartPalette
    {
        internal FocusStartPalette(bool dark)
        {
            Dark = dark;
            Accent = Solid(0xDE, 0x74, 0x00);
            Border = dark ? Solid(0x3A, 0x3A, 0x40) : Solid(0xE4, 0xE4, 0xE7);
            Surface = dark ? Solid(0x1F, 0x1F, 0x23) : Solid(0xFF, 0xFF, 0xFF);
            Field = dark ? Solid(0x16, 0x16, 0x1A) : Solid(0xFA, 0xFA, 0xFA);
            Halo = dark ? Solid(0x26, 0x26, 0x2B) : Solid(0xFF, 0xF7, 0xEB);
            Selection = dark ? Solid(0xFF, 0xED, 0xC9) : Solid(0xFF, 0xEB, 0xD3);
            SelectionText = Solid(0x55, 0x55, 0x55);
            Speech = dark ? Solid(0xFF, 0xED, 0xC9) : Solid(0xFF, 0xFF, 0xFF);
            SpeechText = Solid(0x10, 0x10, 0x14);
            SecondaryAccent = Solid(0x48, 0x29, 0xAD);
            Metadata = new SolidColorBrush(Color.FromArgb(0x26, 0xE4, 0x8E, 0x24));
            Metadata.Freeze();
            Track = dark ? Solid(0x3F, 0x3F, 0x46) : Solid(0xE3, 0xE3, 0xE6);
            Warning = Solid(0xD9, 0x3A, 0x2B);
            Primary = dark ? Solid(0xF4, 0xF4, 0xF5) : Solid(0x18, 0x18, 0x1B);
            // Light: 5E5E66 is 6.2:1 on the fields (71717A measured 4.4:1 there).
            Secondary = dark ? Solid(0xA1, 0xA1, 0xAA) : Solid(0x5E, 0x5E, 0x66);
        }

        internal bool Dark { get; }
        internal SolidColorBrush Accent, Border, Surface, Field, Halo, Selection, SelectionText, Speech, SpeechText, SecondaryAccent, Metadata, Track, Warning, Primary, Secondary;

        internal static FocusStartPalette Current() => new FocusStartPalette(string.Equals(AppSettings.GetInstance.sTheme, "Dark", StringComparison.OrdinalIgnoreCase));

        private static SolidColorBrush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Design measurements (Figma 2× pixels) to WPF units at the current width — the Mac's <c>FocusStartScale</c>:
    /// the design is 1675.5pt wide; the screen shrinks with the window down to 62%, never grows past 100%.
    /// </summary>
    internal struct FocusStartScale
    {
        private const double DesignWidth = 3351 / 2.0;

        internal FocusStartScale(double contentWidth)
        {
            Factor = contentWidth <= 0 ? 1 : Math.Min(1, Math.Max(0.62, contentWidth / DesignWidth));
        }

        internal double Factor { get; }

        internal double Pt(double designPixels) => designPixels / 2 * Factor;
    }

    internal static class FocusStartUi
    {
        internal static readonly FontFamily Symbols = new FontFamily("Segoe MDL2 Assets");
        internal static readonly FontFamily Emoji = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI");

        internal static TextBlock Text(string text, double size, Brush color, FontWeight? weight = null, bool wrap = true)
        {
            return new TextBlock
            {
                Text = text ?? "",
                FontSize = Math.Max(9, size),
                Foreground = color,
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
        }

        internal static TextBlock Glyph(int code, double size, Brush color) =>
            new TextBlock { Text = ((char)code).ToString(), FontFamily = Symbols, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center };

        internal static ControlTemplate Plain()
        {
            var template = new ControlTemplate(typeof(ButtonBase));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            template.VisualTree = presenter;
            return template;
        }

        /// <summary>A borderless click target around <paramref name="content"/>.</summary>
        internal static Button Clickable(UIElement content, Action onClick, string automationId = null, string tooltip = null)
        {
            // A panel or border with no background is only hit-testable where its text is; give it a transparent
            // one so clicking anywhere on the row (e.g. an accordion's header bar) counts.
            if (content is Panel panel && panel.Background == null) panel.Background = Brushes.Transparent;
            else if (content is Border border && border.Background == null) border.Background = Brushes.Transparent;
            var button = new Button { Content = content, Template = Plain(), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = tooltip, FocusVisualStyle = null };
            button.Click += (s, e) => onClick();
            if (automationId != null) AutomationProperties.SetAutomationId(button, automationId);
            if (tooltip != null) AutomationProperties.SetName(button, tooltip);
            return button;
        }

        /// <summary>The design's big orange action button. <paramref name="busy"/> adds a turning spinner beside the title.</summary>
        internal static Button Primary(string title, FocusStartPalette p, FocusStartScale s, Action onClick, string automationId, bool busy = false)
        {
            // White on the orange face, at the size of the app's other big buttons. White on #DE7400 is 3.2:1, which
            // passes for large bold text (3:1) at this size; near-black read badly against the orange.
            double size = Math.Max(16, s.Pt(52));
            var label = Text(title, size, OnAccent, FontWeights.SemiBold, wrap: false);
            label.VerticalAlignment = VerticalAlignment.Center;
            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            if (busy) content.Children.Add(Spinner(size, OnAccent, new Thickness(0, 0, s.Pt(20), 0)));
            content.Children.Add(label);
            var face = new Border { Background = p.Accent, CornerRadius = new CornerRadius(s.Pt(20)), Height = Math.Max(36, s.Pt(137)), Child = content };
            var button = Clickable(face, onClick, automationId);
            button.IsEnabledChanged += (o, e) => face.Opacity = button.IsEnabled ? 1 : 0.45;
            button.Tag = label;   // for SetPrimaryTitle
            return button;
        }

        /// <summary>
        /// Changes the title of a button made by <see cref="Primary"/>. Callers used to reach into the button for
        /// its text block and cast what they found; the title sits one panel deeper since the spinner was added,
        /// so that cast threw (the simple start screen crashed when the duration was changed).
        /// </summary>
        internal static void SetPrimaryTitle(Button button, string title)
        {
            if (button?.Tag is TextBlock label) label.Text = title ?? "";
        }

        /// <summary>A small ring that turns while something is being waited on.</summary>
        internal static FrameworkElement Spinner(double size, Brush color, Thickness margin)
        {
            var ring = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = color,
                StrokeThickness = Math.Max(2, size / 8),
                StrokeDashArray = new DoubleCollection { 2.2, 1.4 },
                StrokeDashCap = PenLineCap.Round,
                Margin = margin,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var turn = new RotateTransform();
            ring.RenderTransform = turn;
            Utils.LoadedAnimation.Run(ring, turn, RotateTransform.AngleProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
            AutomationProperties.SetAutomationId(ring, "spnBusy");
            return ring;
        }

        /// <summary>"Top Secret/ Extra Powerful Options ›" or "‹ Go back/ Regular Options" — switches the app's mode.</summary>
        internal static Button ModeLink(string top, string bottom, bool pointsBack, FocusStartPalette p, FocusStartScale s, Action onClick)
        {
            var lines = new StackPanel();
            lines.Children.Add(Text(top, s.Pt(48), p.Accent, FontWeights.SemiBold, wrap: false));
            lines.Children.Add(Text(bottom, s.Pt(48), p.Primary, FontWeights.SemiBold, wrap: false));
            var chevron = Glyph(pointsBack ? 0xE76B : 0xE76C, s.Pt(40), p.Primary);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (pointsBack) { chevron.Margin = new Thickness(0, 0, s.Pt(24), 0); row.Children.Add(chevron); row.Children.Add(lines); }
            else { chevron.Margin = new Thickness(s.Pt(24), 0, 0, 0); row.Children.Add(lines); row.Children.Add(chevron); }
            var face = new Border { Child = row, Padding = new Thickness(s.Pt(48), s.Pt(28), s.Pt(48), s.Pt(28)), CornerRadius = new CornerRadius(s.Pt(34)), BorderBrush = p.Border, BorderThickness = new Thickness(1), Background = p.Surface };
            return Clickable(face, onClick, pointsBack ? "btnFocusStartGoSimple" : "btnFocusStartGoAdvanced");
        }

        /// <summary>
        /// Draws <paramref name="image"/> with the high-quality scaler. The art is 2x to 4x the size it is shown at,
        /// and WPF's default scaler (bilinear) skips source pixels when shrinking that far, which looked pixelated.
        /// </summary>
        internal static Image Sharp(Image image)
        {
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            image.SnapsToDevicePixels = true;
            image.UseLayoutRounding = true;
            return image;
        }

        /// <summary>The bear in its disc with a speech bubble — above it (advanced) or tucked in on the left (simple).</summary>
        internal static FrameworkElement Bear(string image, string bubble, FocusStartPalette p, FocusStartScale s, bool bubbleBeside, double bearWidthPx, string bubbleAutomationId = null)
        {
            var disc = new Grid { Width = s.Pt(706), Height = s.Pt(706) };
            disc.Children.Add(new Ellipse { Fill = p.Halo });
            disc.Children.Add(Sharp(new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Focus Bear;component/Images/FocusStart/" + image + ".png")),
                Width = s.Pt(bearWidthPx),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }));
            var text = Text(bubble, s.Pt(40), p.SpeechText);
            if (bubbleAutomationId != null) AutomationProperties.SetAutomationId(text, bubbleAutomationId);
            var speech = new Border
            {
                Child = text,
                Background = p.Speech,
                CornerRadius = new CornerRadius(s.Pt(40)),
                Padding = new Thickness(s.Pt(64), s.Pt(44), s.Pt(64), s.Pt(44)),
                MaxWidth = s.Pt(682),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 0, Opacity = 0.12 },
            };
            if (bubbleBeside)
            {
                // The bear sits on the page's centre line, under the title and over the dial; the bubble hangs
                // off its left side. Bubble and bear used to be centred as one block, which pushed the bear
                // to the right of everything else on the page.
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                disc.HorizontalAlignment = HorizontalAlignment.Center;
                Grid.SetColumnSpan(disc, 2);
                row.Children.Add(disc);
                // In the left half, its right edge tucked 120 into the disc. The negative left margin lets it
                // reach past the column of controls into the page's free space instead of being squeezed.
                speech.HorizontalAlignment = HorizontalAlignment.Right;
                speech.VerticalAlignment = VerticalAlignment.Center;
                speech.Margin = new Thickness(-s.Pt(682), 0, s.Pt(706) / 2 - s.Pt(120), 0);
                Panel.SetZIndex(speech, 1);
                row.Children.Add(speech);
                return row;
            }
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            speech.HorizontalAlignment = HorizontalAlignment.Center;
            speech.Margin = new Thickness(0, 0, 0, -s.Pt(40));
            Panel.SetZIndex(speech, 1);
            stack.Children.Add(speech);
            stack.Children.Add(disc);
            return stack;
        }

        private static readonly Brush OnAccent = Frozen(Color.FromRgb(0xFF, 0xFF, 0xFF));

        private static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>The design's pill toggle.</summary>
        internal static ToggleButton Toggle(bool isOn, FocusStartPalette p, FocusStartScale s, Action<bool> changed, string automationId)
        {
            double width = Math.Max(34, s.Pt(105)), height = Math.Max(20, s.Pt(57)), knob = Math.Max(14, s.Pt(40));
            var toggle = new ToggleButton { IsChecked = isOn, Width = width, Height = height, Cursor = Cursors.Hand, FocusVisualStyle = null };
            var track = new Border { CornerRadius = new CornerRadius(height / 2), BorderThickness = new Thickness(1.5), BorderBrush = p.Border };
            var dot = new Ellipse { Width = knob, Height = knob, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness((height - knob) / 2) };
            var grid = new Grid();
            grid.Children.Add(track);
            grid.Children.Add(dot);
            Action paint = () =>
            {
                bool on = toggle.IsChecked == true;
                track.Background = on ? p.Accent : p.Field;
                dot.Fill = on ? Brushes.White : p.Primary;
                dot.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                grid.Opacity = toggle.IsEnabled ? 1 : 0.5;
            };
            var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
            toggle.Template = template;
            toggle.Content = grid;
            paint();
            toggle.Checked += (o, e) => { paint(); changed(true); };
            toggle.Unchecked += (o, e) => { paint(); changed(false); };
            toggle.IsEnabledChanged += (o, e) => paint();
            AutomationProperties.SetAutomationId(toggle, automationId);
            return toggle;
        }

        /// <summary>A rounded field well with a magnifier and a placeholder.</summary>
        /// <summary>A number in <see cref="NumberPicker"/>; the app's RoundedComboBoxStyle shows <c>name</c>.</summary>
        public sealed class NumberChoice
        {
            public NumberChoice(int value, string name) { Value = value; this.name = name; }
            public int Value { get; }
            // Lower-case: RoundedComboBoxStyle binds SelectedItem.name (its first user shows focus modes).
            public string name { get; }
            public override string ToString() => name;
        }

        /// <summary>
        /// A dropdown of whole numbers in the app's rounded, theme-aware picker (RoundedComboBoxStyle). WPF's default
        /// ComboBox kept its white chrome under the theme's white text, so the value could not be read in dark mode.
        /// </summary>
        internal static ComboBox NumberPicker(IEnumerable<int> values, int selected, Action<int> onSelected, double width, string automationId, Func<int, string> label = null)
        {
            var picker = new ComboBox { Width = width, Height = 32, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            if (Application.Current?.TryFindResource("RoundedComboBoxStyle") is Style rounded) picker.Style = rounded;
            NumberChoice current = null;
            foreach (int value in values)
            {
                var choice = new NumberChoice(value, label != null ? label(value) : value.ToString());
                picker.Items.Add(choice);
                if (value == selected) current = choice;
            }
            picker.SelectedItem = current ?? (picker.Items.Count > 0 ? picker.Items[0] : null);
            picker.SelectionChanged += (o, e) => { if (picker.SelectedItem is NumberChoice c) onSelected(c.Value); };
            AutomationProperties.SetAutomationId(picker, automationId);
            return picker;
        }

        internal static Border SearchField(TextBox box, string placeholder, FocusStartPalette p, FocusStartScale s, string automationId)
        {
            box.BorderThickness = new Thickness(0);
            box.Background = Brushes.Transparent;
            box.Foreground = p.Primary;
            box.CaretBrush = p.Primary;
            box.FontSize = Math.Max(11, s.Pt(40));
            box.VerticalContentAlignment = VerticalAlignment.Center;
            AutomationProperties.SetAutomationId(box, automationId);
            AutomationProperties.SetName(box, placeholder);
            var hint = Text(placeholder, Math.Max(11, s.Pt(40)), p.Secondary, wrap: false);
            hint.IsHitTestVisible = false;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(3, 0, 0, 0);
            Action sync = () => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (o, e) => sync();
            sync();
            var well = new Grid();
            well.Children.Add(hint);
            well.Children.Add(box);
            var row = new DockPanel();
            var icon = Glyph(0xE721, Math.Max(11, s.Pt(34)), p.Secondary);
            icon.Margin = new Thickness(0, 0, s.Pt(16), 0);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            row.Children.Add(well);
            return new Border
            {
                Child = row,
                Background = p.Field,
                BorderBrush = p.Border,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(s.Pt(34)),
                Padding = new Thickness(s.Pt(32), 0, s.Pt(32), 0),
                Height = Math.Max(30, s.Pt(97)),
            };
        }

        /// <summary>An accordion card: icon, title and one-line summary, a chevron, and the body when open.</summary>
        internal static Border AccordionCard(int icon, string title, string summary, bool expanded, UIElement body, FocusStartPalette p, FocusStartScale s, Action toggle, string automationId)
        {
            var header = new DockPanel();
            // As in the left menu: ">" when collapsed, a down arrow when expanded.
            var chevron = Glyph(expanded ? 0xE70D : 0xE76C, s.Pt(40) + 1, p.Primary);
            DockPanel.SetDock(chevron, Dock.Right);
            header.Children.Add(chevron);
            var titles = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = Glyph(icon, s.Pt(46), p.Accent);
            glyph.Margin = new Thickness(0, 0, s.Pt(16), 0);
            titleRow.Children.Add(glyph);
            titleRow.Children.Add(Text(title, s.Pt(48), p.Primary, FontWeights.SemiBold, wrap: false));
            titles.Children.Add(titleRow);
            if (!expanded)
            {
                var line = Text(summary, s.Pt(36), p.Primary, FontWeights.Medium, wrap: false);
                line.Margin = new Thickness(0, s.Pt(8), 0, 0);
                AutomationProperties.SetAutomationId(line, automationId + "Summary");
                titles.Children.Add(line);
            }
            header.Children.Add(titles);
            var stack = new StackPanel();
            stack.Children.Add(Clickable(header, toggle, automationId, title));
            if (expanded && body != null)
            {
                var holder = new Border { Child = body, Padding = new Thickness(s.Pt(42), s.Pt(41), s.Pt(42), 0) };
                stack.Children.Add(holder);
            }
            return new Border
            {
                Child = stack,
                Background = p.Surface,
                BorderBrush = expanded ? p.Accent : p.Border,
                BorderThickness = new Thickness(expanded ? 1.5 : 1),
                CornerRadius = new CornerRadius(s.Pt(60)),
                Padding = new Thickness(s.Pt(47), s.Pt(47), s.Pt(47), s.Pt(expanded ? 64 : 48)),
            };
        }

        /// <summary>A small rounded tag ("AI Enabled", a TOP score, a duration).</summary>
        internal static Border Badge(string text, Brush background, Brush foreground, FocusStartScale s)
        {
            return new Border
            {
                Child = Text(text, Math.Max(10, s.Pt(28)), foreground, FontWeights.Medium, wrap: false),
                Background = background,
                CornerRadius = new CornerRadius(Math.Max(6, s.Pt(16))),
                Padding = new Thickness(s.Pt(14), s.Pt(5), s.Pt(14), s.Pt(5)),
                Margin = new Thickness(0, 0, s.Pt(10), 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }

    /// <summary>
    /// The session-length dial: − / + circles around the big "00 hr 25 min" readout, and a 0–8 HRS slider
    /// snapped to 5 minutes underneath (the Mac's <c>FocusStartDurationDial</c>).
    /// </summary>
    internal sealed class FocusStartDurationDial : UserControl
    {
        private readonly FocusStartStrings _strings;
        private readonly FocusStartPalette _p;
        private readonly FocusStartScale _s;
        private readonly StackPanel _readout = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly Slider _slider;
        private readonly Button _minus, _plus;
        private bool _syncing;
        private FocusStartDuration _duration;

        internal event Action<FocusStartDuration> DurationChanged;

        internal FocusStartDurationDial(FocusStartDuration duration, FocusStartStrings strings, FocusStartPalette p, FocusStartScale s)
        {
            _duration = duration;
            _strings = strings;
            _p = p;
            _s = s;
            var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _minus = Stepper(0xE738, -1, "btnFocusStartMinus");
            _plus = Stepper(0xE710, 1, "btnFocusStartPlus");
            _readout.Margin = new Thickness(s.Pt(42), 0, s.Pt(42), 0);
            AutomationProperties.SetAutomationId(_readout, "txtFocusStartDuration");
            top.Children.Add(_minus);
            top.Children.Add(_readout);
            top.Children.Add(_plus);
            root.Children.Add(top);

            var sliderRow = new DockPanel { Margin = new Thickness(0, s.Pt(52), 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
            var min = FocusStartUi.Text(strings["labelSliderMinimum"], s.Pt(40), p.Secondary, FontWeights.Medium, wrap: false);
            min.VerticalAlignment = VerticalAlignment.Center;
            min.Margin = new Thickness(0, 0, s.Pt(31), 0);
            DockPanel.SetDock(min, Dock.Left);
            var max = FocusStartUi.Text(strings["labelSliderMaximum"], s.Pt(40), p.Secondary, FontWeights.Medium, wrap: false);
            max.VerticalAlignment = VerticalAlignment.Center;
            max.Margin = new Thickness(s.Pt(31), 0, 0, 0);
            DockPanel.SetDock(max, Dock.Right);
            sliderRow.Children.Add(min);
            sliderRow.Children.Add(max);
            _slider = new Slider
            {
                Minimum = 0,
                Maximum = FocusStartDuration.MaximumMinutes / FocusStartDuration.StepMinutes,
                SmallChange = 1,
                LargeChange = 6,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                Width = Math.Max(200, s.Pt(868)),
                VerticalAlignment = VerticalAlignment.Center,
                Template = SliderTemplate(p, s),
                Focusable = true,
            };
            AutomationProperties.SetAutomationId(_slider, "sldFocusStartDuration");
            AutomationProperties.SetName(_slider, strings["labelSetSessionLength"]);
            _slider.ValueChanged += (o, e) =>
            {
                if (_syncing) return;
                int minutes = FocusStartDuration.Clamp((int)Math.Round(_slider.Value) * FocusStartDuration.StepMinutes);
                Set(new FocusStartDuration(minutes, _duration.IsUntimed));
            };
            sliderRow.Children.Add(_slider);
            root.Children.Add(sliderRow);
            Content = root;
            Render();
            IsEnabledChanged += (o, e) => Opacity = IsEnabled ? 1 : 0.5;
        }

        internal FocusStartDuration Duration => _duration;

        internal void Set(FocusStartDuration duration)
        {
            if (duration.Equals(_duration)) return;
            _duration = duration;
            Render();
            DurationChanged?.Invoke(duration);
        }

        private Button Stepper(int glyph, int steps, string id)
        {
            double size = Math.Max(30, _s.Pt(105));
            var circle = new Grid { Width = size, Height = size };
            circle.Children.Add(new Ellipse { Stroke = _p.Accent, StrokeThickness = 2, Fill = _p.Field });
            var icon = FocusStartUi.Glyph(glyph, size * 0.36, _p.Accent);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            circle.Children.Add(icon);
            var button = FocusStartUi.Clickable(circle, () => Set(_duration.Stepped(steps)), id, steps < 0 ? "−5 min" : "+5 min");
            button.VerticalAlignment = VerticalAlignment.Center;
            // Centred on the digits, not on their line box: a digit's middle sits below the box's middle
            // (there is more room above the cap height than below the baseline).
            button.Margin = new Thickness(0, 2 * DigitCentreDrop * Math.Max(9, _s.Pt(NumberSize)), 0, 0);
            return button;
        }

        private void Render()
        {
            _readout.Children.Clear();
            var clock = FocusStartStrings.ClockComponents(_duration);
            double number = _s.Pt(NumberSize), unit = _s.Pt(UnitSize);
            if (clock == null)
            {
                _readout.Children.Add(FocusStartUi.Text("∞", number, _p.Accent, FontWeights.SemiBold, wrap: false));
                AutomationProperties.SetName(_readout, "∞");
            }
            else
            {
                AddEditable(clock.Item1, number, hours: true);
                Add(" " + _strings["unitHour"] + " ", unit, _p.Secondary, FontWeights.Normal);
                AddEditable(clock.Item2, number, hours: false);
                Add(" " + _strings["unitMinute"], unit, _p.Secondary, FontWeights.Normal);
                AutomationProperties.SetName(_readout, clock.Item1 + " " + _strings["unitHour"] + " " + clock.Item2 + " " + _strings["unitMinute"]);
            }
            _syncing = true;
            _slider.Value = _duration.Minutes / (double)FocusStartDuration.StepMinutes;
            _syncing = false;
            _minus.IsEnabled = _plus.IsEnabled = _slider.IsEnabled = !_duration.IsUntimed;
            _minus.Opacity = _plus.Opacity = _duration.IsUntimed ? 0.4 : 1;
        }

        private bool? _editingHours;

        /// <summary>
        /// The hours or minutes number: click it to type a value. Enter or leaving the box keeps it (clamped to
        /// 1 min – 8 h), Escape puts the old value back.
        /// </summary>
        private void AddEditable(string text, double size, bool hours)
        {
            if (_editingHours == hours && IsEnabled && !_duration.IsUntimed)
            {
                var box = new TextBox
                {
                    Text = text,
                    FontSize = Math.Max(9, size),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _p.Accent,
                    CaretBrush = _p.Accent,
                    Background = _p.Field,
                    BorderBrush = _p.Accent,
                    BorderThickness = new Thickness(1.5),
                    Padding = new Thickness(0),
                    MaxLength = 3,
                    MinWidth = size * 1.25,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                AutomationProperties.SetAutomationId(box, hours ? "txtFocusStartHours" : "txtFocusStartMinutes");
                bool done = false;
                Action<bool> finish = keep =>
                {
                    if (done) return;
                    done = true;
                    _editingHours = null;
                    int value;
                    if (keep && int.TryParse(box.Text.Trim(), out value) && value >= 0)
                    {
                        int total = hours ? value * 60 + _duration.MinutesComponent : _duration.HoursComponent * 60 + value;
                        var next = new FocusStartDuration(FocusStartDuration.Clamp(total), _duration.IsUntimed);
                        if (!next.Equals(_duration)) { Set(next); return; }
                    }
                    Render();
                };
                box.PreviewTextInput += (o, e) => e.Handled = !e.Text.All(char.IsDigit);
                box.KeyDown += (o, e) =>
                {
                    if (e.Key == Key.Enter) { e.Handled = true; finish(true); }
                    else if (e.Key == Key.Escape) { e.Handled = true; finish(false); }
                    else if (e.Key == Key.Tab) { e.Handled = true; bool toMinutes = hours; finish(true); if (toMinutes) { _editingHours = false; Render(); } }
                };
                box.LostKeyboardFocus += (o, e) => finish(true);
                box.Loaded += (o, e) => { box.Focus(); box.SelectAll(); };
                _readout.Children.Add(box);
                return;
            }
            var block = FocusStartUi.Text(text, size, _p.Accent, FontWeights.SemiBold, wrap: false);
            var target = FocusStartUi.Clickable(block, () =>
            {
                if (!IsEnabled || _duration.IsUntimed) return;
                _editingHours = hours;
                Render();
            }, hours ? "btnFocusStartEditHours" : "btnFocusStartEditMinutes",
               // Read as the value and its unit ("25 min"), not just the unit.
               text.Trim() + " " + (hours ? _strings["unitHour"] : _strings["unitMinute"]).Trim());
            target.Cursor = Cursors.IBeam;
            target.VerticalAlignment = VerticalAlignment.Bottom;
            _readout.Children.Add(target);
        }

        private const double NumberSize = 160, UnitSize = 80;

        // Segoe UI's metrics, as fractions of the font size: the space under the baseline, and how far a digit's
        // middle (half the cap height above the baseline) sits below the middle of its line box.
        private const double DescentRatio = 0.25, DigitCentreDrop = 0.064;

        /// <summary>A unit ("hr", "min") beside the big numbers, sitting on their baseline.</summary>
        private void Add(string text, double size, Brush color, FontWeight weight)
        {
            var block = FocusStartUi.Text(text, size, color, weight, wrap: false);
            block.VerticalAlignment = VerticalAlignment.Bottom;
            // Bottom-aligned boxes put the smaller text's baseline lower than the numbers' (its descent is
            // shorter), so the units hung below the digits. Lift it by the difference.
            double lift = (Math.Max(9, _s.Pt(NumberSize)) - block.FontSize) * DescentRatio;
            block.Margin = new Thickness(0, 0, 0, Math.Max(0, lift));
            _readout.Children.Add(block);
        }

        /// <summary>A capsule track, filled in orange up to the thumb, with a cream rounded thumb.</summary>
        private static ControlTemplate SliderTemplate(FocusStartPalette p, FocusStartScale s)
        {
            double trackHeight = Math.Max(10, s.Pt(47)), thumbWidth = Math.Max(22, s.Pt(89)), thumbHeight = Math.Max(24, s.Pt(99));
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>" +
                "<Grid Height='" + F(thumbHeight + 4) + "' Background='Transparent'>" +
                "<Border Height='" + F(trackHeight) + "' CornerRadius='" + F(trackHeight / 2) + "' Background='" + Hex(p.Track) + "' VerticalAlignment='Center'/>" +
                "<Track x:Name='PART_Track' Orientation='Horizontal'>" +
                "<Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'>" +
                "<Border Height='" + F(trackHeight) + "' CornerRadius='" + F(trackHeight / 2) + "' Background='" + Hex(p.Accent) + "' Margin='0,0,-" + F(thumbWidth / 2) + ",0'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>" +
                "<Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'>" +
                "<Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>" +
                "<Track.Thumb><Thumb Width='" + F(thumbWidth) + "' Height='" + F(thumbHeight) + "'><Thumb.Template><ControlTemplate TargetType='Thumb'>" +
                "<Border CornerRadius='" + F(s.Pt(26)) + "' Background='" + Hex(p.Selection) + "' BorderBrush='" + Hex(p.Accent) + "' BorderThickness='1.5'>" +
                "<StackPanel Orientation='Horizontal' HorizontalAlignment='Center' VerticalAlignment='Center'>" +
                "<Rectangle Width='2' Height='" + F(thumbHeight * 0.42) + "' Fill='" + Hex(p.Accent) + "' Margin='0,0,3,0'/><Rectangle Width='2' Height='" + F(thumbHeight * 0.42) + "' Fill='" + Hex(p.Accent) + "'/>" +
                "</StackPanel></Border></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>" +
                "</Track></Grid></ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        private static string F(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private static string Hex(SolidColorBrush brush) => "#" + brush.Color.A.ToString("X2") + brush.Color.R.ToString("X2") + brush.Color.G.ToString("X2") + brush.Color.B.ToString("X2");
    }
}
