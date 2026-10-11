using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// App-wide defaults that make the WPF controls read properly in Narrator / NVDA and behave with the keyboard,
    /// found by the E2E suite's accessibility checks (windows-app-e2e-tests, tests/test_accessibility.py).
    ///
    /// <list type="bullet">
    /// <item>A button whose content is a panel of TextBlocks (the app's usual pattern) has no UI Automation Name,
    /// so a screen reader says only "button". Such a button, when it has no Name of its own, gets one bound to its
    /// text, so it follows later text changes (localisation, state). A button with no text uses its ToolTip.</item>
    /// <item>A list item (ListBox / ListView / ComboBox) bound to an object without a ToString is read as its type
    /// name ("Focus_Bear.Preferences.Settings+LeftButtonItem"). Such an item is named after the first text its
    /// template shows.</item>
    /// <item>A Hyperlink inside a collapsed TextBlock stays a Tab stop, so focus disappears onto something that
    /// isn't on screen. When Tab lands on one, focus moves straight on to the next control (or the previous one,
    /// with Shift+Tab).</item>
    /// <item>Most of the app's button, list item and sidebar styles set FocusVisualStyle to null (to hide WPF's
    /// dotted rectangle), so a keyboard user can't see where focus is. Such a control gets a solid ring in the
    /// theme's text colour instead. WPF draws a FocusVisualStyle only when focus arrives from the keyboard, so mouse
    /// users see no change.</item>
    /// <item>A plain ContentControl used as a content host is taken out of the Tab order (see OnContentHostSizeChanged).</item>
    /// </list>
    /// An explicit AutomationProperties.Name, and a string Content (which WPF already announces), are left alone.
    ///
    /// Buttons are named from a SizeChanged class handler, which runs when a button is first laid out (and again
    /// when a collapsed one is shown). A Loaded class handler would be simpler, but WPF only raises Loaded on
    /// elements under an instance Loaded handler, so it misses most buttons.
    /// </summary>
    internal static class AccessibilityDefaults
    {
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            EventManager.RegisterClassHandler(typeof(ButtonBase), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnButtonSizeChanged));
            EventManager.RegisterClassHandler(typeof(ListBoxItem), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnItemSizeChanged));
            EventManager.RegisterClassHandler(typeof(Hyperlink), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnHyperlinkGotFocus));
            EventManager.RegisterClassHandler(typeof(TreeViewItem), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnTreeItemSizeChanged));
            EventManager.RegisterClassHandler(typeof(TextBoxBase), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnTextBoxSizeChanged));
            EventManager.RegisterClassHandler(typeof(Control), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnControlFocusChanged));
            EventManager.RegisterClassHandler(typeof(Control), Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnControlFocusChanged));
            EventManager.RegisterClassHandler(typeof(ContentControl), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnContentHostSizeChanged));
        }

        // ── Content hosts ────────────────────────────────────────────────────────

        /// <summary>
        /// A bare <c>new ContentControl()</c> used to swap a screen's content (the To-do tabs and page, the Stats
        /// tab) is focusable and a Tab stop by default: Tab landed on an invisible element that UI Automation reports
        /// as the window, and Tab never got past the To-do / Stats tab strip. Only the exact type is changed;
        /// buttons, list items and other ContentControl subclasses keep their own focus behaviour.
        /// </summary>
        private static void OnContentHostSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ContentControl host && host.GetType() == typeof(ContentControl) && host.Focusable)
            {
                host.Focusable = false;
                host.IsTabStop = false;
            }
        }

        // ── Focus rings ──────────────────────────────────────────────────────────

        private static Style _focusRing;

        /// <summary>A 2px ring just outside the control, in the theme's text colour (black / near-white).</summary>
        internal static Style FocusRing
        {
            get
            {
                if (_focusRing != null) return _focusRing;
                var ring = new FrameworkElementFactory(typeof(System.Windows.Shapes.Rectangle));
                ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "ForegroundColor");
                ring.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 2.0);
                ring.SetValue(System.Windows.Shapes.Rectangle.RadiusXProperty, 4.0);
                ring.SetValue(System.Windows.Shapes.Rectangle.RadiusYProperty, 4.0);
                ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-2));
                ring.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
                var style = new Style(typeof(Control));
                style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = ring }));
                style.Seal();
                return _focusRing = style;
            }
        }

        /// <summary>
        /// A template can carry its own ring, a part named PART_KeyboardFocusRing (the sidebar rows: their selected
        /// state already draws a border, so WPF's focus visual was not visible on it). It is shown only while the
        /// control has keyboard focus that came from the keyboard, like a FocusVisualStyle.
        /// </summary>
        private static void OnControlFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
        {
            try
            {
                if (!(sender is Control control) || !ReferenceEquals(e.OriginalSource, control) || control.Template == null) return;
                if (!(control.Template.FindName("PART_KeyboardFocusRing", control) is UIElement ring)) return;
                bool show = control.IsKeyboardFocused && InputManager.Current.MostRecentInputDevice is KeyboardDevice;
                ring.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults focus ring ex - " + ex.Message);
            }
        }

        /// <summary>Gives a focusable control whose style removed its focus visual the ring instead.</summary>
        internal static void EnsureFocusRing(FrameworkElement element)
        {
            if (element.Focusable && element.FocusVisualStyle == null) element.FocusVisualStyle = FocusRing;
        }

        /// <summary>Text boxes whose style removed the focus visual and draw no focused border (the rounded search fields).</summary>
        private static void OnTextBoxSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox box && !(box.TemplatedParent is ComboBox)) EnsureFocusRing(box);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults text box ex - " + ex.Message);
            }
        }

        private static void OnTreeItemSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // A group's ring would surround all its children; only leaf items (the sidebar's screens) get one.
                if (sender is TreeViewItem item && !item.HasItems) EnsureFocusRing(item);
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults tree item ex - " + ex.Message);
            }
        }

        // ── Button names ─────────────────────────────────────────────────────────

        private static void OnButtonSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (sender is RepeatButton part && part.TemplatedParent is ScrollBar)
                {
                    // A scroll bar's page / line buttons are mouse parts (the keyboard scrolls the host); a custom
                    // scroll bar template made them Tab stops ("PageDown" between Focus start's cards).
                    part.Focusable = false;
                    return;
                }
                if (sender is ButtonBase button)
                {
                    NameFromContent(button);
                    EnsureFocusRing(button);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults button name ex - " + ex.Message);
            }
        }

        internal static void NameFromContent(ButtonBase button)
        {
            if (BindingOperations.IsDataBound(button, AutomationProperties.NameProperty)) return;
            if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))) return;
            if (button.Content is string label)
            {
                // WPF reads string content itself, unless it is only a glyph ("⚙", "＋", "‹"): then the tooltip.
                if (!IsGlyphOnly(label)) return;
                if (button.ToolTip is string)
                    button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(FrameworkElement.ToolTip)) { Source = button });
                return;
            }

            // Words only: an icon-font TextBlock or a lone symbol ("⋯", "✎") says nothing to a screen reader.
            List<TextBlock> texts = TextBlocksIn(button.Content as DependencyObject)
                .Where(t => !IsGlyphFont(t) && !IsGlyphOnly(t.Text))
                .ToList();
            if (texts.Count == 1)
            {
                button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(TextBlock.Text)) { Source = texts[0] });
                return;
            }
            if (texts.Count > 1)
            {
                var multi = new MultiBinding { Converter = JoinTextsConverter.Instance };
                foreach (TextBlock t in texts) multi.Bindings.Add(new Binding(nameof(TextBlock.Text)) { Source = t });
                button.SetBinding(AutomationProperties.NameProperty, multi);
                return;
            }
            if (button.ToolTip is string)
            {
                button.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(FrameworkElement.ToolTip)) { Source = button });
            }
        }

        /// <summary>TextBlocks in the logical content (not the template), in order.</summary>
        private static IEnumerable<TextBlock> TextBlocksIn(DependencyObject root)
        {
            if (root == null) yield break;
            if (root is TextBlock tb)
            {
                yield return tb;
                yield break;
            }
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject d)
                {
                    foreach (TextBlock t in TextBlocksIn(d)) yield return t;
                }
            }
        }

        /// <summary>No letter or digit at all: a symbol, an arrow, an emoji.</summary>
        private static bool IsGlyphOnly(string text) => !string.IsNullOrWhiteSpace(text) && !text.Any(char.IsLetterOrDigit);

        /// <summary>Icon fonts (Segoe MDL2 / Fluent icons) hold a glyph, not words.</summary>
        private static bool IsGlyphFont(TextBlock t)
        {
            string family = t.FontFamily?.Source ?? "";
            return family.IndexOf("MDL2", StringComparison.OrdinalIgnoreCase) >= 0
                   || family.IndexOf("Fluent Icons", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class JoinTextsConverter : IMultiValueConverter
        {
            internal static readonly JoinTextsConverter Instance = new JoinTextsConverter();

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
                string.Join(" ", values.OfType<string>().Select(s => s.Trim()).Where(s => s.Length > 0));

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
                throw new NotSupportedException();
        }

        // ── List item names ──────────────────────────────────────────────────────

        private static void OnItemSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (sender is ListBoxItem item)
                {
                    NameFromTemplate(item);
                    EnsureFocusRing(item);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults item name ex - " + ex.Message);
            }
        }

        internal static void NameFromTemplate(ListBoxItem item)
        {
            if (BindingOperations.IsDataBound(item, AutomationProperties.NameProperty)) return;
            if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(item))) return;
            object data = item.Content;
            if (data == null || data is string || data is UIElement) return; // WPF already reads these
            if (!ReadsAsTypeName(data)) return;
            TextBlock text = FirstVisualText(item);
            if (text != null)
                item.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(TextBlock.Text)) { Source = text });
        }

        /// <summary>True when the object's ToString is object's own (its type name), which is what UIA would read.</summary>
        private static bool ReadsAsTypeName(object data)
        {
            string shown = data.ToString();
            return shown == data.GetType().FullName || shown == data.GetType().ToString();
        }

        private static TextBlock FirstVisualText(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                // A button's label names the button, not the item (a note card's "✎" came first).
                if (child is ButtonBase) continue;
                if (child is TextBlock tb && tb.Text.Any(char.IsLetterOrDigit) && !IsGlyphFont(tb)) return tb;
                TextBlock nested = FirstVisualText(child);
                if (nested != null) return nested;
            }
            return null;
        }

        // ── Hyperlinks in hidden text ────────────────────────────────────────────

        private static void OnHyperlinkGotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!(sender is Hyperlink link)) return;
            try
            {
                if (IsShown(link)) return;
                var direction = (Keyboard.Modifiers & ModifierKeys.Shift) != 0
                    ? FocusNavigationDirection.Previous
                    : FocusNavigationDirection.Next;
                link.Dispatcher.BeginInvoke((Action)(() => link.MoveFocus(new TraversalRequest(direction))));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("AccessibilityDefaults hyperlink ex - " + ex.Message);
            }
        }

        /// <summary>Whether the TextBlock (or other element) that hosts the link is on screen.</summary>
        private static bool IsShown(Hyperlink link)
        {
            DependencyObject parent = link.Parent;
            while (parent is ContentElement ce) parent = LogicalTreeHelper.GetParent(ce);
            return !(parent is UIElement host) || host.IsVisible;
        }
    }
}
