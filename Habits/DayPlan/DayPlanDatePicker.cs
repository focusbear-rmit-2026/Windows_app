using Focus_Bear.Utils;
using FocusBear.Core.Schedule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// The popover behind the Day Plan's date label (the Mac's <c>DayPlanDatePickerController</c>): a month
    /// calendar the user can page through, each future day underlined in the colour of its net free time
    /// (<see cref="DayPlanFreeLevel"/>: red none left, orange up to 1h, blue up to 2h, green more). Clicking a
    /// day (past days too) moves the Day Plan there and closes it.
    ///
    /// A day is badged only once its meetings have been read (<see cref="DayPlanDayBadge"/>), so the grid comes
    /// up at once with the known days coloured, then reads the shown month's missing days in the background
    /// and colours them as they land. Each day's figure is a whole Day Plan build, so it is memoised for the
    /// popover's lifetime (per read of that day's meetings) and computed a few days per frame.
    /// </summary>
    internal sealed class DayPlanDatePicker
    {
        private const double CellSize = 34;
        private const double DiscDiameter = 22;
        private static readonly Color Accent = Color.FromRgb(0xDE, 0x74, 0x00);
        // The Mac's system colours for the four levels.
        private static readonly Color CRed = Color.FromRgb(0xFF, 0x3B, 0x30);
        private static readonly Color COrange = Color.FromRgb(0xFF, 0x95, 0x00);
        private static readonly Color CBlue = Color.FromRgb(0x00, 0x7A, 0xFF);
        private static readonly Color CGreen = Color.FromRgb(0x34, 0xC7, 0x59);

        private readonly Func<DateTime, int?> _netFreeMinutes;
        private readonly Action<DateTime> _onSelect;
        private readonly DateTime _selected;
        private readonly DayOfWeek _firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        private DateTime _month;
        private DateTime _focused;
        private bool _showFocus;
        private int _generation;
        private DateTime _closedAt = DateTime.MinValue;

        // startOfDay → (meetings read at, net free minutes or null for no schedule).
        private readonly Dictionary<DateTime, KeyValuePair<DateTime, int?>> _freeCache = new Dictionary<DateTime, KeyValuePair<DateTime, int?>>();
        // Months already asked for, so the re-render a read triggers can't start the same read again.
        private readonly HashSet<DateTime> _loadRequested = new HashSet<DateTime>();
        private int _loadsRunning;

        private readonly Popup _popup;
        private readonly Border _card;
        private readonly TextBlock _title = new TextBlock();
        private readonly TextBlock _status = new TextBlock();
        private readonly Cell[] _cells = new Cell[DayPlanMonthGrid.Cells];

        private sealed class Cell
        {
            public Border Root;
            public Ellipse Disc;
            public TextBlock Number;
            public Rectangle Underline;
            public DateTime? Date;
        }

        internal DayPlanDatePicker(DateTime selected, Func<DateTime, int?> netFreeMinutes, Action<DateTime> onSelect)
        {
            _selected = selected.Date;
            _focused = _selected;
            _month = DayPlanMonthGrid.MonthStart(_selected);
            _netFreeMinutes = netFreeMinutes;
            _onSelect = onSelect;

            var root = new StackPanel { Orientation = Orientation.Vertical };
            root.Children.Add(BuildHeader());
            root.Children.Add(BuildWeekdayRow());
            root.Children.Add(BuildGrid());
            _status.FontSize = 10.5;
            _status.HorizontalAlignment = HorizontalAlignment.Center;
            _status.Margin = new Thickness(0, 4, 0, 0);
            _status.Text = DayPlanText.DatePickerLoading;
            _status.Visibility = Visibility.Hidden;
            _status.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            root.Children.Add(_status);

            _card = new Border
            {
                Child = root,
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(14, 4, 14, 14),   // room for the shadow
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.28, Direction = 270 },
                Focusable = true,
                FocusVisualStyle = null,
            };
            _card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            _card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            _card.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(_card, "DayPlanDatePicker");
            _card.PreviewKeyDown += OnKeyDown;

            // Transient, like the Mac's NSPopover: a click anywhere else closes it. Centred under the date.
            _popup = new Popup
            {
                Child = _card,
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.Custom,
                CustomPopupPlacementCallback = (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height), PopupPrimaryAxis.Horizontal)
                },
            };
            _popup.Closed += (s, e) =>
            {
                // Only a click outside (which may be on the date label itself) counts, not Esc or a pick.
                if (Mouse.LeftButton == MouseButtonState.Pressed) _closedAt = DateTime.UtcNow;
                _generation++;
            };
        }

        internal bool IsOpen => _popup.IsOpen;

        /// <summary>The click that closed the popover (it lands on the date label) must not reopen it at once.</summary>
        internal bool JustClosed => (DateTime.UtcNow - _closedAt).TotalMilliseconds < 350;

        internal void Open(UIElement target)
        {
            _popup.PlacementTarget = target;
            _popup.IsOpen = true;
            Render();
            _card.Dispatcher.BeginInvoke(new Action(() => { _card.Focus(); Keyboard.Focus(_card); }), DispatcherPriority.Input);
        }

        /// <summary>The Day Plan rebuilt its date bar while open: keep the popover anchored to the new label.</summary>
        internal void Retarget(UIElement target)
        {
            if (_popup.IsOpen) _popup.PlacementTarget = target;
        }

        internal void Close() => _popup.IsOpen = false;

        // MARK: - Layout

        private UIElement BuildHeader()
        {
            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var prev = NavButton("‹", DayPlanText.DatePickerPreviousMonth, "DayPlanDatePickerPrev", () => ChangeMonth(-1));
            var next = NavButton("›", DayPlanText.DatePickerNextMonth, "DayPlanDatePickerNext", () => ChangeMonth(1));
            _title.FontSize = 13;
            _title.FontWeight = FontWeights.SemiBold;
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _title.VerticalAlignment = VerticalAlignment.Center;
            _title.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(_title, "DayPlanDatePickerTitle");
            Grid.SetColumn(prev, 0);
            Grid.SetColumn(_title, 1);
            Grid.SetColumn(next, 2);
            header.Children.Add(prev);
            header.Children.Add(_title);
            header.Children.Add(next);
            header.Width = CellSize * DayPlanMonthGrid.Columns;
            return header;
        }

        private static Button NavButton(string glyph, string tip, string automationId, Action onClick)
        {
            var text = new TextBlock { Text = glyph, FontSize = 18, Padding = new Thickness(8, 0, 8, 2), VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var chrome = new Border { CornerRadius = new CornerRadius(5), Background = Brushes.Transparent, Child = text };
            var btn = new Button
            {
                Content = chrome,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,   // the arrow keys belong to the grid
                ToolTip = tip,
                Template = PresenterOnly(),
            };
            btn.MouseEnter += (s, e) => chrome.Background = new SolidColorBrush(Color.FromArgb(0x22, Accent.R, Accent.G, Accent.B));
            btn.MouseLeave += (s, e) => chrome.Background = Brushes.Transparent;
            AutomationProperties.SetAutomationId(btn, automationId);
            AutomationProperties.SetName(btn, tip);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static ControlTemplate PresenterOnly()
        {
            var tmpl = new ControlTemplate(typeof(Button));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            tmpl.VisualTree = cp;
            return tmpl;
        }

        /// <summary>The weekday initials, from the locale's first weekday.</summary>
        private UIElement BuildWeekdayRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            string[] names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
            foreach (DayOfWeek d in DayPlanMonthGrid.WeekdayOrder(_firstDay))
            {
                string n = names[(int)d];
                var t = new TextBlock
                {
                    Text = n.Length > 0 ? n.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture) : "",
                    FontSize = 10,
                    Width = CellSize,
                    TextAlignment = TextAlignment.Center,
                };
                t.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
                row.Children.Add(t);
            }
            return row;
        }

        private UIElement BuildGrid()
        {
            var grid = new UniformGrid { Rows = DayPlanMonthGrid.Weeks, Columns = DayPlanMonthGrid.Columns, Width = CellSize * DayPlanMonthGrid.Columns };
            for (int i = 0; i < _cells.Length; i++)
            {
                var cell = new Cell
                {
                    Disc = new Ellipse { Width = DiscDiameter, Height = DiscDiameter, StrokeThickness = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) },
                    Number = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) },
                    // The free-time badge: a short line under the number (a line, not a dot — subtler).
                    Underline = new Rectangle { Width = 14, Height = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Hidden },
                };
                var inner = new Grid { Width = CellSize, Height = CellSize };
                inner.Children.Add(cell.Disc);
                inner.Children.Add(cell.Number);
                inner.Children.Add(cell.Underline);
                cell.Root = new Border { Child = inner, CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
                Cell captured = cell;
                cell.Root.MouseEnter += (s, e) => { if (captured.Date.HasValue) captured.Root.Background = new SolidColorBrush(Color.FromArgb(0x22, Accent.R, Accent.G, Accent.B)); };
                cell.Root.MouseLeave += (s, e) => captured.Root.Background = Brushes.Transparent;
                cell.Root.MouseLeftButtonUp += (s, e) =>
                {
                    if (!captured.Date.HasValue) return;
                    e.Handled = true;
                    Choose(captured.Date.Value);
                };
                _cells[i] = cell;
                grid.Children.Add(cell.Root);
            }
            return grid;
        }

        // MARK: - Rendering

        private void ChangeMonth(int delta)
        {
            _month = _month.AddMonths(delta);
            // Keep the keyboard cursor inside the month on screen.
            int day = Math.Min(_focused.Day, DateTime.DaysInMonth(_month.Year, _month.Month));
            _focused = new DateTime(_month.Year, _month.Month, day);
            Render();
        }

        /// <summary>Lay the month out (cheap), then colour its days.</summary>
        private void Render()
        {
            int generation = ++_generation;
            _title.Text = _month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            DateTime?[] dates = DayPlanMonthGrid.CellDates(_month, _firstDay);
            DateTime today = DateTime.Today;
            for (int i = 0; i < _cells.Length; i++)
            {
                Cell c = _cells[i];
                c.Date = dates[i];
                c.Underline.Visibility = Visibility.Hidden;
                c.Root.ToolTip = null;
                c.Root.Background = Brushes.Transparent;
                if (!c.Date.HasValue)
                {
                    c.Number.Text = "";
                    c.Disc.Fill = Brushes.Transparent;
                    c.Disc.StrokeThickness = 0;
                    c.Root.Cursor = null;
                    c.Root.BorderBrush = Brushes.Transparent;
                    AutomationProperties.SetAutomationId(c.Root, "");
                    continue;
                }
                DateTime d = c.Date.Value;
                bool isSelected = d == _selected, isToday = d == today;
                c.Number.Text = d.Day.ToString(CultureInfo.CurrentCulture);
                c.Root.Cursor = Cursors.Hand;
                AutomationProperties.SetAutomationId(c.Root, "DayPlanDatePickerDay-" + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                AutomationProperties.SetName(c.Root, d.ToString("D", CultureInfo.CurrentCulture));
                // Selected = filled accent circle; today (not selected) = thin accent ring; else nothing.
                if (isSelected)
                {
                    c.Disc.Fill = new SolidColorBrush(Accent);
                    c.Disc.StrokeThickness = 0;
                    c.Number.Foreground = Brushes.White;
                    c.Number.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    c.Disc.Fill = Brushes.Transparent;
                    c.Disc.Stroke = new SolidColorBrush(Color.FromArgb(0x8C, Accent.R, Accent.G, Accent.B));
                    c.Disc.StrokeThickness = isToday ? 1 : 0;
                    c.Number.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
                    c.Number.FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal;
                }
                c.Root.BorderBrush = _showFocus && d == _focused ? new SolidColorBrush(Accent) : Brushes.Transparent;
            }
            _ = BadgeMonthAsync(generation);
        }

        /// <summary>
        /// Colour every loaded future day of the shown month, computing uncached days a few per frame so the
        /// popover stays responsive; then read the month's missing days and colour those when they land.
        /// </summary>
        private async Task BadgeMonthAsync(int generation)
        {
            var clock = Stopwatch.StartNew();
            int computed = 0, badged = 0;
            long slowest = 0;
            DateTime today = DateTime.Today;
            var sinceYield = Stopwatch.StartNew();
            bool yielded = false;
            foreach (Cell c in _cells)
            {
                if (generation != _generation) return;   // month changed or popover closed
                if (!c.Date.HasValue) continue;
                DateTime d = c.Date.Value;
                DateTime? loadedAt = DayPlanCalendarManager.Shared.DayLoadedAtUtc(d);
                KeyValuePair<DateTime, int?> cached;
                bool uncached = d > today && loadedAt.HasValue && !(_freeCache.TryGetValue(d, out cached) && cached.Key == loadedAt.Value);
                // A day's figure is a whole Day Plan build. Cached days colour at once (no flicker as the arrow
                // keys re-render); before new work, let the month paint, and then input in every ~30 ms.
                if (uncached && (!yielded || sinceYield.ElapsedMilliseconds > 30))
                {
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    yielded = true;
                    sinceYield.Restart();
                    if (generation != _generation) return;
                }
                DayPlanFreeLevel.Level? level = DayPlanDayBadge.For(d > today, loadedAt.HasValue, () =>
                {
                    KeyValuePair<DateTime, int?> hit;
                    if (_freeCache.TryGetValue(d, out hit) && hit.Key == loadedAt.Value) return hit.Value;
                    var one = Stopwatch.StartNew();
                    int? value = _netFreeMinutes(d);
                    slowest = Math.Max(slowest, one.ElapsedMilliseconds);
                    computed++;
                    _freeCache[d] = new KeyValuePair<DateTime, int?>(loadedAt.Value, value);
                    return value;
                });
                if (level.HasValue)
                {
                    badged++;
                    c.Underline.Fill = new SolidColorBrush(ColourOf(level.Value));
                    c.Underline.Visibility = Visibility.Visible;
                    int minutes = _freeCache[d].Value ?? 0;
                    c.Root.ToolTip = minutes < 0
                        ? ForecastText.Format(DayPlanText.DatePickerOver, "<DURATION>", ForecastText.Duration(-minutes))
                        : ForecastText.Format(DayPlanText.DatePickerFree, "<DURATION>", ForecastText.Duration(minutes));
                }
            }
            if (computed > 0)
                LogWriter.LogWrite("DayPlanDatePicker: " + _month.ToString("yyyy-MM", CultureInfo.InvariantCulture) + " badged " + badged + " days ("
                                   + computed + " computed, slowest " + slowest + " ms) in " + clock.ElapsedMilliseconds + " ms");
            if (generation == _generation) await LoadMonthAsync(generation);
        }

        /// <summary>Read the shown month's unread future days, once per month per popover, then re-colour it.</summary>
        private async Task LoadMonthAsync(int generation)
        {
            DateTime month = _month;
            if (!_loadRequested.Add(month)) return;
            var cal = DayPlanCalendarManager.Shared;
            DayPlanMonthLoad.Plan plan = DayPlanMonthLoad.For(_cells.Select(c => c.Date), DateTime.Today, cal.DayNeedsLoad,
                DayPlanCalendarManager.DefaultWindowDays, DayPlanCalendarManager.MaxWindowDays, DayPlanCalendarManager.MaxSingleDayReads);
            if (plan.IsEmpty) return;
            _loadsRunning++;
            UpdateStatus();
            var clock = Stopwatch.StartNew();
            try
            {
                if (plan.WindowDays.HasValue)
                {
                    await cal.EnsureWindowAsync(plan.WindowDays.Value);
                    RerenderIfShowing(month);
                }
                if (plan.SingleDays.Count > 0)
                {
                    await cal.LoadDaysAsync(plan.SingleDays);
                    RerenderIfShowing(month);
                }
                LogWriter.LogWrite("DayPlanDatePicker: loaded " + month.ToString("yyyy-MM", CultureInfo.InvariantCulture) + " (window "
                                   + (plan.WindowDays?.ToString(CultureInfo.InvariantCulture) ?? "-") + ", single days " + plan.SingleDays.Count
                                   + ") in " + clock.ElapsedMilliseconds + " ms");
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("DayPlanDatePicker.LoadMonthAsync - " + ex.Message);
            }
            finally
            {
                _loadsRunning--;
                UpdateStatus();
            }
        }

        private void RerenderIfShowing(DateTime month)
        {
            if (_popup.IsOpen && _month == month) Render();
        }

        private void UpdateStatus()
        {
            _status.Visibility = _loadsRunning > 0 ? Visibility.Visible : Visibility.Hidden;   // its line stays reserved: no jump
        }

        private static Color ColourOf(DayPlanFreeLevel.Level level)
        {
            switch (level)
            {
                case DayPlanFreeLevel.Level.None: return CRed;
                case DayPlanFreeLevel.Level.Tight: return COrange;
                case DayPlanFreeLevel.Level.Some: return CBlue;
                default: return CGreen;
            }
        }

        // MARK: - Input

        private void Choose(DateTime date)
        {
            Close();
            _onSelect?.Invoke(date.Date);
        }

        /// <summary>Esc closes; ← → a day, ↑ ↓ a week, Page Up / Down a month; Enter or Space picks.</summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            int delta = 0;
            switch (e.Key)
            {
                case Key.Escape: e.Handled = true; Close(); return;
                case Key.Enter:
                case Key.Space: e.Handled = true; Choose(_focused); return;
                case Key.Left: delta = -1; break;
                case Key.Right: delta = 1; break;
                case Key.Up: delta = -7; break;
                case Key.Down: delta = 7; break;
                case Key.PageUp: e.Handled = true; _showFocus = true; ChangeMonth(-1); return;
                case Key.PageDown: e.Handled = true; _showFocus = true; ChangeMonth(1); return;
                default: return;
            }
            e.Handled = true;
            _showFocus = true;
            _focused = _focused.AddDays(delta);
            DateTime month = DayPlanMonthGrid.MonthStart(_focused);
            if (month != _month) _month = month;
            Render();
        }
    }
}
