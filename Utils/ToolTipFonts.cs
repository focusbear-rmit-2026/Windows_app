using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FocusBear.Core.Ui;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// A tooltip takes its font from the element it's on, so a tooltip on an icon glyph (the AI check-in's (i),
    /// the To-Do and Day Plan icon buttons...) was drawn in the icon font and came out as boxes. For every such
    /// tooltip, app-wide, this hands the tooltip the window's text font instead, just before it opens. The look
    /// (colours, corners, padding) still comes from the app's ToolTip style.
    /// </summary>
    internal static class ToolTipFonts
    {
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.ToolTipOpeningEvent,
                new ToolTipEventHandler(OnToolTipOpening), true);
            if (E2EBuild.ExposeFonts)
                EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler(OnToolTipOpenedForTests), true);
        }

        /// <summary>
        /// E2E test seam (<see cref="E2EBuild.ExposeFontsVariable"/>): an open tooltip reports the font its text is
        /// drawn in through UI Automation, as its ItemStatus "font:&lt;family&gt;". UIA reads the same text whatever the
        /// font, so this is how a test sees an icon font on a tooltip.
        /// </summary>
        private static void OnToolTipOpenedForTests(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is ToolTip tip)) return;
                FontFamily drawn = FirstTextFont(tip) ?? tip.FontFamily;
                System.Windows.Automation.AutomationProperties.SetItemStatus(tip, "font:" + (drawn?.Source ?? ""));
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToolTipFonts expose ex - " + ex.Message);
            }
        }

        /// <summary>The font of the first TextBlock (with text) inside the tooltip, or null.</summary>
        private static FontFamily FirstTextFont(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text)) return tb.FontFamily;
                FontFamily nested = FirstTextFont(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            try
            {
                if (!(sender is FrameworkElement owner) || owner.ToolTip == null) return;
                // The font the tooltip would inherit from its owner.
                var ownerFont = (FontFamily)owner.GetValue(TextElement.FontFamilyProperty);
                if (!IconFonts.IsIconFont(ownerFont?.Source)) return;
                FontFamily text = TextFontFor(owner);
                if (owner.ToolTip is ToolTip tip)
                {
                    if (IconFonts.IsIconFont(tip.FontFamily?.Source)) tip.FontFamily = text;
                    return;
                }
                // A plain string (or element): wrap it in a ToolTip carrying the text font; the app's implicit
                // ToolTip style still applies.
                owner.ToolTip = new ToolTip { Content = owner.ToolTip, FontFamily = text };
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("ToolTipFonts ex - " + ex.Message);
            }
        }

        /// <summary>The nearest text font above the icon: an ancestor's, else the window's, else Segoe UI.</summary>
        private static FontFamily TextFontFor(FrameworkElement owner)
        {
            DependencyObject node = owner;
            while (node != null)
            {
                node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node);
                if (node is FrameworkElement fe)
                {
                    var f = (FontFamily)fe.GetValue(TextElement.FontFamilyProperty);
                    if (f != null && !IconFonts.IsIconFont(f.Source)) return f;
                }
            }
            return new FontFamily("Segoe UI");
        }
    }
}
