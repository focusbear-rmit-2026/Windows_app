using Focus_Bear.API;
using Focus_Bear.Habits.DayPlan;
using Focus_Bear.Models;
using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Habits
{
    /// <summary>
    /// Windows port of the macOS "Day Plan" (Schedule) view — see Mac-App PR #1935.
    ///
    /// An agenda-card timetable of the day built from the current routine's remaining habits, with
    /// deep-work / break blocks and the evening-routine transition marker. Consecutive habits of one
    /// routine collapse into a single bordered group card; every other block (deep work, break,
    /// evening transition, "habits done") gets its own coloured card — mirroring the macOS render().
    ///
    /// This is a NEW class, kept fully independent of <see cref="HabitsWelcomeList"/> (which is
    /// intentionally left untouched). It replaces the Welcome list at routine start.
    ///
    /// Milestone 4: read-only card UI (colours/layout matched to macOS, strings from config).
    /// Selecting a habit + the Play/Done/Skip action bar (M5), the tasks panel (M6/M7), the date
    /// bar (M8) and calendar (deferred) come later.
    /// </summary>
    public partial class HabitsDayPlan : Page
    {
        private readonly AppSettings appSettings = AppSettings.GetInstance;
        private FocusBearMainWindow _parentWindow;

        // macOS system colours (light-mode canonical values), used with alpha for card fills/borders.
        private static readonly Color COrange = Color.FromRgb(0xFF, 0x95, 0x00);
        private static readonly Color CGreen = Color.FromRgb(0x34, 0xC7, 0x59);
        private static readonly Color CPurple = Color.FromRgb(0xAF, 0x52, 0xDE);
        private static readonly Color CBlue = Color.FromRgb(0x00, 0x7A, 0xFF);
        private static readonly Color CIndigo = Color.FromRgb(0x58, 0x56, 0xD6);
        private static readonly Color CGray = Color.FromRgb(0x8E, 0x8E, 0x93);   // secondary text / separator
        private static readonly Color CRed = Color.FromRgb(0xFF, 0x3B, 0x30);

        // Fixed column widths so every row's time / emoji / title line up (matches macOS layoutColumns).
        private const double TimeColWidth = 140;
        private const double EmojiColWidth = 26;

        // The habit the user tapped to reveal its Play/Done/Skip bar. Null = default to the current
        // (next-up) habit. Index is a position in the live active-routine array (arrAll*Habits).
        private int? _selectedHabitIndex;

        // The currently-rendered selected-habit card, so Done can flash it green before removing it.
        private Border _selectedUnitBorder;

        // A not-yet-done "earlier today" habit the user tapped to reveal its "I am done" action.
        private string _selectedPastHabitId;

        // The day being viewed (‹ date › navigation). Today = live routine; other days = read-only.
        private DateTime _selectedDate = DateTime.Today;
        private bool IsViewingToday => _selectedDate.Date == DateTime.Today;

        // Per-routine accordion collapse overrides (routine name → collapsed). Absent → default:
        // "earlier today" (completed) routines start collapsed; the rest start expanded.
        private readonly Dictionary<string, bool> _routineCollapsed = new Dictionary<string, bool>();

        private bool IsRoutineCollapsed(string name, bool isEarlier)
        {
            if (_routineCollapsed.TryGetValue(name, out bool collapsed)) return collapsed;
            return isEarlier;   // earlier-today routines start collapsed; others expanded
        }

        private void ToggleRoutineCollapsed(string name, bool isEarlier)
        {
            _routineCollapsed[name] = !IsRoutineCollapsed(name, isEarlier);
            RenderDayPlan();
        }

        public HabitsDayPlan()
        {
        }

        public HabitsDayPlan(FocusBearMainWindow parentWindow) : this()
        {
            InitializeComponent();

            // Match the app-wide font selection, exactly like HabitsWelcomeList does.
            if (appSettings.sSelectedAppFont == "Poppins" || appSettings.sSelectedAppFont == "IntelOne Mono" || appSettings.sSelectedAppFont == "OpenDyslexic3 Regular")
            {
                this.FontFamily = new FontFamily(new Uri("pack://application:,,,/Focus Bear"), string.Format("./fonts/#{0}", appSettings.sSelectedAppFont));
            }
            else
            {
                this.FontFamily = new FontFamily(appSettings.sSelectedAppFont);
            }

            _parentWindow = parentWindow;

            try
            {
                RenderDayPlan();
                InitTasksPanel();     // restore panel width + render the tasks list (M7)
                FetchDayProgress();   // refresh the completed-habit set from the backend, then re-render
                FetchRoutineCancelledState();   // detect a force-cancelled routine → show its Uncancel card
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("HabitsDayPlan RenderDayPlan ex - " + ex.Message);
            }
        }

        /// <summary>
        /// Re-render and refresh the Day Plan for a REUSED instance (so the page isn't recreated on
        /// every view). Resets the transient selection; keeps the accordion collapse state.
        /// </summary>
        public void Refresh()
        {
            _selectedDate = DateTime.Today;   // a re-show always lands on today, not a stale past day
            _selectedHabitIndex = null;
            _selectedPastHabitId = null;
            _routineCollapsed.Clear();   // back to defaults (active expanded, earlier collapsed) per view
            try
            {
                RenderDayPlan();
                RenderTasksPanel();   // keep the panel in sync on a re-show (M7)
                HydrateTasksFromBackend();   // reconcile done-state from the to-do list on every re-open
                FetchDayProgress();
                FetchRoutineCancelledState();   // detect a force-cancelled routine → show its Uncancel card
                FetchCalendar(force: true);   // re-sync meetings on every re-open so new/changed events show promptly
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan Refresh ex - " + ex.Message); }
        }

        // The day (yyyy-MM-dd) we last fetched routine-progress for, to fetch at most once per day.
        private string _lastProgressFetchDay = "";

        // Whether TODAY's morning/evening routine was force-cancelled (the whole routine was skipped). When
        // set, that routine's Day Plan card shows the "you cancelled … Uncancel" message instead of its
        // habits. Refreshed from current-activity-props' today_routine_progress[*].was_force_completed.
        private bool _morningRoutineCancelled;
        private bool _eveningRoutineCancelled;

        // After a local cancel/uncancel the backend's was_force_completed can lag a moment, so a refresh that
        // re-reads it would wrongly flip our just-set state back. Trust the local flags until this time.
        private DateTime _cancelStateStickyUntil = DateTime.MinValue;

        /// <summary>
        /// Fetch the selected day's completed habits from the backend so done habits strike through.
        /// Matches the macOS guard: for a PAST/other day always fetch; for TODAY only when the routine is
        /// FINISHED (while a routine is active/postponed the live state already has the completions).
        /// Throttled to once per day; the throttle resets whenever today's routine is active/postponed.
        /// </summary>
        private async void FetchDayProgress() { await FetchDayProgressAsync(); }

        private async Task FetchDayProgressAsync()
        {
            try
            {
                string dayKey = _selectedDate.ToString("yyyy-MM-dd");
                if (IsViewingToday)
                {
                    bool routineActive = appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS
                                      || appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS
                                      || appSettings.nCurrentActivityType == CurrentActivityType.BREAK_ACTIVITY
                                      || appSettings.bHabitsPostponedState;
                    if (routineActive)
                    {
                        _lastProgressFetchDay = "";   // routine in progress — reset so we refetch once it finishes
                        return;
                    }
                }

                if (_lastProgressFetchDay == dayKey) return;   // already fetched this day
                _lastProgressFetchDay = dayKey;

                var client = new FocusBearAPIClient();
                var (completed, skipped) = await client.GetDayRoutineProgress(dayKey);
                if (completed != null)
                {
                    if (!appSettings.dayPlanCompletedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> set))
                    {
                        set = new HashSet<string>();
                        appSettings.dayPlanCompletedHabitIdsByDay[dayKey] = set;
                    }
                    foreach (string id in completed) set.Add(id);

                    // Cache skipped habits separately so they render as skipped (not still-doable).
                    if (!appSettings.dayPlanSkippedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> skipSet))
                    {
                        skipSet = new HashSet<string>();
                        appSettings.dayPlanSkippedHabitIdsByDay[dayKey] = skipSet;
                    }
                    if (skipped != null) foreach (string id in skipped) skipSet.Add(id);

                    RenderDayPlan();
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan FetchDayProgress ex - " + ex.Message); }
        }

        /// <summary>
        /// Refresh whether today's morning/evening routine was force-cancelled (skipped as a whole), from
        /// current-activity-props' today_routine_progress[*].was_force_completed — matching macOS's
        /// updateUncancelRoutineButtonVisibility. Only meaningful for today; a cancelled routine shows its
        /// "you cancelled … Uncancel" card (see BuildGroupCard). Re-renders when the state changes.
        /// </summary>
        private async void FetchRoutineCancelledState()
        {
            try
            {
                if (!IsViewingToday)
                {
                    if (_morningRoutineCancelled || _eveningRoutineCancelled)
                    {
                        _morningRoutineCancelled = _eveningRoutineCancelled = false;
                        RenderDayPlan();
                    }
                    return;
                }

                // A recent local cancel/uncancel wins over a possibly-stale backend read for a short window.
                if (DateTime.Now < _cancelStateStickyUntil) return;

                var client = new FocusBearAPIClient();
                await client.GetLastActivityInfo();

                bool morning = RoutineWasForceCancelled("morning_routine", appSettings.arrAllMorningHabits);
                bool evening = RoutineWasForceCancelled("evening_routine", appSettings.arrAllEveningHabits);

                if (morning != _morningRoutineCancelled || evening != _eveningRoutineCancelled)
                {
                    _morningRoutineCancelled = morning;
                    _eveningRoutineCancelled = evening;
                    RenderDayPlan();
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan FetchRoutineCancelledState ex - " + ex.Message); }
        }

        // Reads today_routine_progress[routineKey] from the cached current-activity-props JSON and returns
        // true when that routine was force-completed for today (status completed + was_force_completed) and
        // its sequence matches the current routine's — i.e. the user cancelled the whole routine.
        private bool RoutineWasForceCancelled(string routineKey, System.Collections.Generic.List<habit> habits)
        {
            try
            {
                if (string.IsNullOrEmpty(appSettings.sLastActivityDoneInfo)) return false;
                if (habits == null || habits.Count == 0) return false;
                string sequenceId = habits[0].activity_sequence_id;
                if (string.IsNullOrEmpty(sequenceId)) return false;

                var info = Newtonsoft.Json.Linq.JObject.Parse(appSettings.sLastActivityDoneInfo);
                var progress = info["today_routine_progress"];
                var routine = progress?[routineKey];
                if (routine == null || routine.Type == Newtonsoft.Json.Linq.JTokenType.Null) return false;

                bool wasForceCompleted = routine["was_force_completed"]?.ToObject<bool>() ?? false;
                string status = routine["status"]?.ToString() ?? "";
                string todaySequenceId = routine["sequence_id"]?.ToString() ?? "";

                return wasForceCompleted
                    && status.ToLowerInvariant() == "completed"
                    && (string.IsNullOrEmpty(todaySequenceId) || todaySequenceId == sequenceId);
            }
            catch (Exception ex) { LogWriter.LogWrite("RoutineWasForceCancelled ex - " + ex.Message); return false; }
        }

        // MARK: - Render

        /// <summary>
        /// Rebuilds the agenda: consecutive same-routine habits become one group card; every other
        /// block gets its own card. Mirrors the macOS scheduleViewController.render().
        /// </summary>
        private void RenderDayPlan()
        {
            DayPlanStack.Children.Clear();
            DayPlanStack.Children.Add(BuildDateBar());

            List<ScheduleBlock> blocks = DayPlanDataSource.BuildBlocks(_selectedDate, includeDismissedMeetings: _showDismissedMeetings);

            // Deep-work sprints are task drop targets (like habit rows). Pre-compute which deep-work
            // task belongs to which sprint once, using nearest-anchor matching (M7 — deep-work targets).
            List<ScheduleBlock> dwBlocks = blocks.FindAll(b => b.Kind == ScheduleBlockKind.DeepWork);
            Dictionary<ScheduleBlock, List<DayPlanTask>> dwTasks = DeepWorkTasksByBlock(dwBlocks);

            var renderedRoutines = new HashSet<string>();
            bool eveningDoneToday = false;   // the evening routine rendered as a done/"earlier" section today
            int i = 0;
            while (i < blocks.Count)
            {
                if (blocks[i].Kind == ScheduleBlockKind.Habit)
                {
                    string name = blocks[i].RoutineName;
                    var group = new List<ScheduleBlock>();
                    while (i < blocks.Count && blocks[i].Kind == ScheduleBlockKind.Habit && blocks[i].RoutineName == name)
                    {
                        group.Add(blocks[i]);
                        i++;
                    }
                    renderedRoutines.Add(name);
                    // The "well done finishing your evening routine" shutdown card should only appear when the
                    // routine was actually done - i.e. the evening rendered as an "earlier" section AND at
                    // least one of its habits is actually done/skipped. Merely being past the evening window
                    // with nothing done is NOT "finished" and must not show the well-done prompt (#1421).
                    // (Not cancelled either — that shows the Uncancel card, which would contradict it.)
                    if (name == DayPlanText.EveningRoutineHabits && group.Exists(b => b.IsEarlier && b.IsDone) && !_eveningRoutineCancelled)
                        eveningDoneToday = true;
                    DayPlanStack.Children.Add(BuildGroupCard(group));
                }
                else if (blocks[i].Kind == ScheduleBlockKind.DeepWork)
                {
                    AddDeepWorkCardWithTasks(blocks[i], dwTasks);
                    i++;
                }
                else if (blocks[i].Kind == ScheduleBlockKind.Meeting)
                {
                    AddMeetingCardWithTasks(blocks[i]);   // meetings are task drop targets too (key event:<id>)
                    i++;
                }
                else
                {
                    DayPlanStack.Children.Add(BuildNonHabitCard(blocks[i]));
                    i++;
                }
            }

            // Safety net: a force-cancelled routine must show its Uncancel card even when BuildBlocks didn't
            // emit its group (e.g. right after cancelling, before the day's progress is re-fetched). Add a
            // standalone cancelled card for any cancelled routine not already rendered above.
            if (IsViewingToday)
            {
                if (_morningRoutineCancelled && !renderedRoutines.Contains(DayPlanText.MorningRoutineHabits))
                    DayPlanStack.Children.Add(BuildStandaloneCancelledCard(isEvening: false));
                if (_eveningRoutineCancelled && !appSettings.DidCutOffTimePassed()
                    && !renderedRoutines.Contains(DayPlanText.EveningRoutineHabits))
                    DayPlanStack.Children.Add(BuildStandaloneCancelledCard(isEvening: true));
            }

            // Evening shutdown prompt (#2005 part 4): a "well done — sleep / shut down" card at the bottom of
            // the plan once the evening routine is done (shown as an "earlier" section), before cutoff, until
            // the user dismisses it for the day. Computed live so it covers local, cross-device and skipped
            // completions without depending on a specific finish hook firing.
            if (IsViewingToday && eveningDoneToday && !appSettings.DidCutOffTimePassed()
                && !RegistryManager.IsEveningShutdownPromptDismissedToday())
                DayPlanStack.Children.Add(BuildEveningShutdownCard());

            // Empty state — only when nothing else (besides the date bar) was rendered.
            if (DayPlanStack.Children.Count <= 1)
            {
                DayPlanStack.Children.Add(new TextBlock
                {
                    Text = IsViewingToday ? DayPlanText.NoHabitsLeft : DayPlanText.NothingScheduled,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 8, 0, 0),
                    Foreground = SecBrush()
                });
            }
        }

        // A standalone cancelled routine card (header + Uncancel body) for the safety-net path in
        // RenderDayPlan, styled like BuildGroupCard's cancelled state but not tied to a habit block group.
        private Border BuildStandaloneCancelledCard(bool isEvening)
        {
            string name = isEvening ? DayPlanText.EveningRoutineHabits : DayPlanText.MorningRoutineHabits;
            var inner = new StackPanel { Orientation = Orientation.Vertical };
            inner.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = "🧩 " + name,
                FontSize = 15,
                Foreground = FgBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 4)
            });
            inner.Children.Add(BuildCancelledRoutineBody(isEvening));
            return StyledCard(
                fill: new SolidColorBrush(WithAlpha(COrange, 0.05)),
                border: new SolidColorBrush(COrange),
                hasBorder: true,
                content: inner);
        }

        // The evening shutdown prompt card: 😴 + title/subtitle + [Sleep] [Shut Down] + "Not now" (macOS #2005
        // part 4). Sleep suspends the PC, Shut Down powers it off; both (and "Not now") clear the prompt.
        private FrameworkElement BuildEveningShutdownCard()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 10) };
            panel.Children.Add(new Emoji.Wpf.TextBlock { Text = "😴", FontSize = 34, HorizontalAlignment = HorizontalAlignment.Center });
            panel.Children.Add(new TextBlock
            {
                Text = EveningShutdownString("title", "Well done on finishing your evening routine"),
                FontSize = 15, FontWeight = FontWeights.Bold, Foreground = FgBrush(),
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 2)
            });
            panel.Children.Add(new TextBlock
            {
                Text = EveningShutdownString("subtitle", "Have a restful sleep"),
                FontSize = 13, Foreground = SecBrush(),
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
            });

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(MakeShutdownButton(EveningShutdownString("buttonSleepText", "Sleep"), primary: true, onClick: () =>
            {
                RegistryManager.SetEveningShutdownPromptDismissed();
                RenderDayPlan();
                Focus_Bear.Utils.Global.SleepPc();
            }));
            row.Children.Add(MakeShutdownButton(EveningShutdownString("buttonShutDownText", "Shut Down"), primary: false, onClick: () =>
            {
                RegistryManager.SetEveningShutdownPromptDismissed();
                Focus_Bear.Utils.Global.ShutDownPc();
            }));
            panel.Children.Add(row);

            var notNow = new TextBlock
            {
                Text = EveningShutdownString("buttonNotNowText", "Not now"),
                FontSize = 13, Foreground = SecBrush(), TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0)
            };
            notNow.MouseLeftButtonUp += (s, e) => { RegistryManager.SetEveningShutdownPromptDismissed(); RenderDayPlan(); };
            panel.Children.Add(notNow);
            return panel;
        }

        // A rounded button for the shutdown prompt: orange fill + white text (primary), or white fill + grey
        // border + black text (secondary) — mirrors macOS AppOrangeButtonStyle.
        private FrameworkElement MakeShutdownButton(string text, bool primary, Action onClick)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = primary ? new SolidColorBrush(COrange) : Brushes.White,
                BorderBrush = primary ? new SolidColorBrush(COrange) : new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand,
                MinWidth = 110
            };
            border.Child = new TextBlock
            {
                Text = text, FontSize = 14, FontWeight = FontWeights.Medium,
                Foreground = primary ? Brushes.White : Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            };
            border.MouseLeftButtonUp += (s, e) => { e.Handled = true; onClick(); };
            return border;
        }

        // Reads an eveningShutdownInfo config string with an English fallback.
        private string EveningShutdownString(string key, string fallback)
        {
            try
            {
                var val = appSettings.ConfigFileStrings["eveningShutdownInfo"]?[key];
                return (val != null && val.ToString().Length > 0) ? val.ToString() : fallback;
            }
            catch { return fallback; }
        }

        // MARK: - Date navigation

        /// <summary>The ‹ date › bar with a "Back to today" link (or a "TODAY · time" indicator).</summary>
        private UIElement BuildDateBar()
        {
            var bar = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(MakeDateArrow("‹", () => ChangeDay(-1)));
            row.Children.Add(new TextBlock
            {
                Text = _selectedDate.ToString(_selectedDate.Year == DateTime.Today.Year ? "ddd d MMM" : "ddd d MMM yyyy", CultureInfo.CurrentCulture),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = FgBrush(),
                MinWidth = 150,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            });
            row.Children.Add(MakeDateArrow("›", () => ChangeDay(1)));
            bar.Children.Add(row);

            if (IsViewingToday)
            {
                bar.Children.Add(new TextBlock
                {
                    Text = DayPlanText.Today.ToUpperInvariant() + " · " + DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant(),
                    FontSize = 11,
                    Foreground = SecBrush(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            else
            {
                var back = new TextBlock
                {
                    Text = DayPlanText.BackToToday,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(COrange),
                    TextDecorations = TextDecorations.Underline,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                back.MouseLeftButtonUp += (s, e) => GoToday();
                bar.Children.Add(back);
            }
            return bar;
        }

        private FrameworkElement MakeDateArrow(string glyph, Action onClick)
        {
            var t = new TextBlock
            {
                Text = glyph,
                FontSize = 26,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEB, 0x91, 0x2E)),
                Padding = new Thickness(10, 0, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Button
            {
                Content = t,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate()
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void ChangeDay(int days)
        {
            _selectedDate = _selectedDate.AddDays(days);
            _selectedHabitIndex = null;
            _selectedPastHabitId = null;
            RenderDayPlan();
            RenderTasksPanel();   // tasks are per-day (M7)
            HydrateTasksFromBackend();   // pull that day's planned tasks (Phase 4 read)
            FetchDayProgress();
            FetchRoutineCancelledState();
            FetchCalendar(force: true);   // pull fresh meetings for the day just navigated to
        }

        private void GoToday()
        {
            _selectedDate = DateTime.Today;
            _selectedHabitIndex = null;
            _selectedPastHabitId = null;
            RenderDayPlan();
            RenderTasksPanel();   // tasks are per-day (M7)
            HydrateTasksFromBackend();   // pull that day's planned tasks (Phase 4 read)
            FetchDayProgress();
            FetchRoutineCancelledState();
            FetchCalendar(force: true);   // pull fresh meetings for today
        }

        // MARK: - Habit group card

        /// <summary>One bordered block holding all consecutive habits of a routine.</summary>
        private Border BuildGroupCard(List<ScheduleBlock> habits)
        {
            bool isActive = habits.Exists(b => b.IsActionable);
            bool isEarlier = habits.Exists(b => b.IsEarlier);
            // The active routine doesn't fit around today's meetings (#1371). It stays startable, but the
            // card defaults to collapsed ONLY in this case — its header shows a short "not enough time"
            // heading (in place of the time range), and the full explanation shows once expanded.
            bool hasOverflow = isActive && habits.Exists(b => b.DidOverflow);
            ScheduleBlock first = habits[0];
            ScheduleBlock last = habits[habits.Count - 1];
            string name = string.IsNullOrEmpty(first.RoutineName) ? "Habits" : first.RoutineName;

            // A force-cancelled routine (the whole routine was skipped) replaces its habits with a "you
            // cancelled … Uncancel" body (matches macOS #2010). The card stays collapsible but defaults to
            // expanded so the message is visible. Evening's uncancel is only offered until shut-off, after
            // which the focus-summary / evening flow takes over.
            bool isEveningGroup = name == DayPlanText.EveningRoutineHabits;
            bool routineCancelled = IsViewingToday &&
                (((name == DayPlanText.MorningRoutineHabits) && _morningRoutineCancelled)
                 || (isEveningGroup && _eveningRoutineCancelled && !appSettings.DidCutOffTimePassed()));
            bool collapsed = IsRoutineCollapsed(name, routineCancelled ? false : (isEarlier || hasOverflow));

            var inner = new StackPanel { Orientation = Orientation.Vertical };

            // Accordion header: chevron + "🧩 <routine>  ·  <start>–<end>". Clicking toggles collapse —
            // the completed "earlier today" routine starts collapsed so its struck-through habits are
            // tucked away but one tap away.
            // An active routine can be cancelled from a red ✕ at the top-left of its card (matches macOS):
            // clicking it confirms, then force-cancels the routine so the card flips to the Uncancel state.
            bool showCancelX = isActive && IsViewingToday && !routineCancelled
                               && (name == DayPlanText.MorningRoutineHabits || isEveningGroup);

            var headerGrid = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(2, 0, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                    // chevron
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title
            if (showCancelX) headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // ✕ (top-right)

            // Same chevron the left menu uses for its collapsible headers: a thin grey Path that swaps
            // between a right chevron (collapsed) and a down chevron (expanded).
            var chevron = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(collapsed ? "M6,4 L10,8 6,12" : "M4,6 L8,10 12,6"),
                Stroke = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            Grid.SetColumn(chevron, 0);
            var header = new Emoji.Wpf.TextBlock
            {
                // Overflow: show the short "not enough time" heading in place of the (misleading) packed
                // time range, tinted orange so the card reads as needing attention (#1371).
                Text = hasOverflow
                    ? string.Format("🧩 {0}  ·  {1}", name, DayPlanText.NotEnoughTimeHeading)
                    : string.Format("🧩 {0}  ·  {1}–{2}", name, TimeString(first.Start), TimeString(last.End)),
                FontSize = 15,
                Foreground = hasOverflow ? new SolidColorBrush(COrange) : FgBrush(),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(header, 1);
            headerGrid.Children.Add(chevron);
            headerGrid.Children.Add(header);
            if (showCancelX)
            {
                var cancelX = BuildCancelRoutineX(isEveningGroup);
                Grid.SetColumn(cancelX, 2);
                headerGrid.Children.Add(cancelX);
            }
            headerGrid.MouseLeftButtonUp += (s, e) => ToggleRoutineCollapsed(name, routineCancelled ? false : (isEarlier || hasOverflow));
            inner.Children.Add(headerGrid);

            // Cancelled routine → show the Uncancel body instead of the habit rows (card stays collapsible).
            if (routineCancelled)
            {
                if (!collapsed) inner.Children.Add(BuildCancelledRoutineBody(isEveningGroup));
                return StyledCard(
                    fill: new SolidColorBrush(WithAlpha(COrange, 0.05)),
                    border: new SolidColorBrush(COrange),
                    hasBorder: true,
                    content: inner);
            }

            if (!collapsed)
            {
                // The active routine's habits don't fit around today's meetings, but it stays startable —
                // the full explanation shows here once the (collapsed-by-default) card is expanded (#1371).
                if (hasOverflow)
                {
                    inner.Children.Add(new Emoji.Wpf.TextBlock
                    {
                        Text = DayPlanText.NotEnoughTimeForRoutine,
                        FontSize = 12.5,
                        Foreground = new SolidColorBrush(COrange),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(24, 0, 6, 6)
                    });
                }

                // Default selection = the next-up (current) habit; keep the user's pick if it's still valid.
                var actionableIdxs = new HashSet<int>();
                foreach (var b in habits) if (b.IsActionable && b.HabitIndex.HasValue) actionableIdxs.Add(b.HabitIndex.Value);
                int? currentIdx = habits.FirstOrDefault(b => b.IsCurrent)?.HabitIndex;
                int? selected = (_selectedHabitIndex.HasValue && actionableIdxs.Contains(_selectedHabitIndex.Value))
                    ? _selectedHabitIndex : currentIdx;

                // Past habits (today's "earlier" accordion, or any previous calendar day) can be logged
                // retroactively; a future day is a read-only preview only.
                bool isPastDay = _selectedDate.Date < DateTime.Today;
                foreach (var h in habits)
                {
                    // Each habit row is wrapped as a drop target keyed "habit:<title>", with any tasks
                    // dragged onto it nested directly beneath (AddHabitRowWithTasks — see M7 partial).
                    if (h.IsEarlier || isPastDay)
                    {
                        // Past habit: done → struck-through, read-only. Not done → selectable to reveal an
                        // "I am done" action so it can be logged retroactively.
                        if (h.IsDone)
                        {
                            AddHabitRowWithTasks(inner, BuildHabitRow(h), h);
                        }
                        else if (!string.IsNullOrEmpty(h.HabitId) && h.HabitId == _selectedPastHabitId)
                        {
                            AddHabitRowWithTasks(inner, BuildSelectedPastHabitUnit(h), h);
                        }
                        else
                        {
                            ScheduleBlock captured = h;
                            AddHabitRowWithTasks(inner, BuildHabitRow(h, onSelect: () =>
                            {
                                _selectedPastHabitId = captured.HabitId;
                                _selectedHabitIndex = null;
                                RenderDayPlan();
                            }), h);
                        }
                        continue;
                    }

                    bool isSel = h.IsActionable && h.HabitIndex.HasValue && h.HabitIndex == selected;
                    if (isSel)
                        AddHabitRowWithTasks(inner, BuildSelectedHabitUnit(h, h.HabitIndex.Value), h);
                    else
                        AddHabitRowWithTasks(inner, BuildHabitRow(h), h);
                }
            }

            return StyledCard(
                fill: new SolidColorBrush(WithAlpha(COrange, 0.05)),
                border: isActive ? new SolidColorBrush(COrange) : new SolidColorBrush(WithAlpha(CGray, 0.5)),
                hasBorder: true,
                content: inner);
        }

        // The body shown inside a cancelled routine's card: the "you cancelled … " message + an underlined
        // "Uncancel my … routine" link that restores the routine via the backend.
        private FrameworkElement BuildCancelledRoutineBody(bool isEvening)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6, 2, 6, 6) };

            string msg = isEvening
                ? HabitsStateString("cancelledEveningRoutineMessage", "You cancelled your evening routine. If you changed your mind, click \"Uncancel My Evening Routine\" to continue your evening habits.")
                : HabitsStateString("cancelledMorningRoutineMessage", "You cancelled your morning routine. If you changed your mind, click \"Uncancel My Morning Routine\" to continue your morning habits.");
            panel.Children.Add(new TextBlock
            {
                Text = msg,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Foreground = SecBrush(),
                Margin = new Thickness(0, 0, 0, 8)
            });

            string btnText = isEvening
                ? HabitsStateString("buttonUncancelEveningRoutine", "Uncancel my evening routine")
                : HabitsStateString("buttonUncancelMorningRoutine", "Uncancel my morning routine");
            var link = new TextBlock
            {
                Text = btnText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(COrange),
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand
            };
            link.MouseLeftButtonUp += (s, e) => UncancelRoutine(isEvening);
            panel.Children.Add(link);
            return panel;
        }

        // Restores a cancelled routine: undo the force-complete on the backend; it then serves that routine's
        // habits again via current-activity-props. Clears our cached progress for today and re-derives so the
        // card swaps back from the Uncancel message to the habit rows.
        private async void UncancelRoutine(bool isEvening)
        {
            try
            {
                var habits = isEvening ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                if (habits == null || habits.Count == 0 || string.IsNullOrEmpty(habits[0].activity_sequence_id))
                {
                    LogWriter.LogWrite("UncancelRoutine - no sequence id to uncancel");
                    return;
                }

                SetBusy(true);
                var client = new FocusBearAPIClient();
                int result = await client.UncompleteCurrentSequence(habits[0].activity_sequence_id);
                LogWriter.LogWrite("UncancelRoutine Uncomplete result=" + result);
                // As with cancel, we don't gate on the return code — a non-201 is typically "already
                // uncompleted", and the Day Plan reflects the backend's was_force_completed on refresh.

                // Backend no longer marks the routine cancelled. Clear the local flag + today's cached progress
                // (it's additive, so stale skipped/done entries would linger) and refetch so the restored
                // habits show. Keep the local flag rather than re-reading was_force_completed, which can lag
                // right after the write.
                if (isEvening) _eveningRoutineCancelled = false; else _morningRoutineCancelled = false;
                _cancelStateStickyUntil = DateTime.Now.AddSeconds(20);   // hold this state over a lagging backend read
                string dayKey = DateTime.Today.ToString("yyyy-MM-dd");
                appSettings.dayPlanCompletedHabitIdsByDay.Remove(dayKey);
                appSettings.dayPlanSkippedHabitIdsByDay.Remove(dayKey);
                _lastProgressFetchDay = "";

                // Undo the "done for today" mark and re-enter routine state so the app fetches and starts the
                // habits again — the routine becomes live, not just a closed card (mirrors macOS: clear the
                // shown-till flag, then re-derive the routine).
                var app = (App)Application.Current;
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;
                if (isEvening)
                {
                    RegistryManager.RemoveEveningHabitsShownTillDate();
                    app.ShowEveningRoutine();
                }
                else
                {
                    RegistryManager.RemoveMorningHabitsShownTillDate();
                    app.ShowMorningRoutine();
                }
                SetBusy(false);
            }
            catch (Exception ex) { SetBusy(false); LogWriter.LogWrite("UncancelRoutine ex - " + ex.Message); }
        }

        // Dim the main window (grid shade) as a lightweight busy indicator while an uncancel/cancel API
        // call + refresh is in flight, and to back the confirmation dialog.
        private void SetBusy(bool busy)
        {
            try { if (_parentWindow != null) _parentWindow.GridShade.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; }
            catch (Exception ex) { LogWriter.LogWrite("HabitsDayPlan SetBusy ex - " + ex.Message); }
        }

        // The red ✕ at an active routine card's top-left: confirms, then cancels the whole routine for today.
        private FrameworkElement BuildCancelRoutineX(bool isEvening)
        {
            var x = new TextBlock
            {
                Text = "✕",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CRed),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 6, 0),
                Cursor = Cursors.Hand,
                ToolTip = DayPlanText.SkipRoutineForToday
            };
            // Handle the click here so it cancels the routine instead of bubbling up to toggle the accordion.
            x.MouseLeftButtonUp += (s, e) => { e.Handled = true; CancelRoutineForToday(isEvening); };
            return x;
        }

        // Confirms ("Cancel Routine?"), then cancels (force-completes) the active routine for today with the
        // same teardown as the other skip-whole-routine flows — set the shown-till date so it isn't
        // re-prompted, close secondary windows, clear the activity type — so the Day Plan card flips to the
        // "you cancelled … Uncancel" state.
        private async void CancelRoutineForToday(bool isEvening)
        {
            try
            {
                var habits = isEvening ? appSettings.arrAllEveningHabits : appSettings.arrAllMorningHabits;
                if (habits == null || habits.Count == 0 || string.IsNullOrEmpty(habits[0].activity_sequence_id))
                {
                    LogWriter.LogWrite("CancelRoutineForToday - no sequence id to cancel");
                    return;
                }

                // The confirmation message points the user at the matching Uncancel link (<BUTTON> token).
                string uncancelLabel = isEvening
                    ? HabitsStateString("buttonUncancelEveningRoutine", "Uncancel my evening routine")
                    : HabitsStateString("buttonUncancelMorningRoutine", "Uncancel my morning routine");
                string message = DayPlanText.CancelRoutineMessage.Replace("<BUTTON>", uncancelLabel);

                // Confirmation, centred on the main window with the usual dim grid-shade behind it.
                SetBusy(true);
                var box = new FBMessageBox(message, DayPlanText.CancelRoutineTitle, MessageBoxButton.YesNo,
                    DayPlanText.CancelRoutineOk, DayPlanText.CancelRoutineCancel)
                {
                    Owner = _parentWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                box.ShowDialog();
                if (box.btnClicked != MessageBoxResult.Yes) { SetBusy(false); return; }   // Cancel → leave unchanged

                // Keep the shade up as a busy indicator while the cancel is sent and the plan refreshes.
                var client = new FocusBearAPIClient();
                int result = await client.ForceCompleteCurrentSequence(habits[0].activity_sequence_id, true);
                LogWriter.LogWrite("CancelRoutineForToday ForceComplete result=" + result);
                // Note: we do NOT gate on result. A non-201 here is typically "already force-completed" (a
                // repeat cancel), and the routine is still cancelled on the backend. The Day Plan reflects the
                // backend's was_force_completed on refresh (macOS approach); we also show it optimistically now.

                // Mark cancelled locally and render immediately so the card flips right away — the active
                // routine group is still in the blocks, so the flag makes it render as the Uncancel card. We
                // keep the local flag rather than re-reading was_force_completed (which can lag after the write).
                if (isEvening) _eveningRoutineCancelled = true; else _morningRoutineCancelled = true;
                _cancelStateStickyUntil = DateTime.Now.AddSeconds(20);
                string dayKey = DateTime.Today.ToString("yyyy-MM-dd");
                appSettings.dayPlanCompletedHabitIdsByDay.Remove(dayKey);
                appSettings.dayPlanSkippedHabitIdsByDay.Remove(dayKey);
                _lastProgressFetchDay = "";
                RenderDayPlan();

                // Tear down the running routine (each step guarded so a failure can't undo the card above):
                // mark it done for today so it isn't re-prompted, close secondary windows, clear the type.
                try { if (isEvening) RegistryManager.SetEveningHabitsShownTillDate(); else RegistryManager.SetMorningHabitsShownTillDate(); }
                catch (Exception e1) { LogWriter.LogWrite("CancelRoutineForToday SetShownTill ex - " + e1.Message); }
                try { appSettings.RemoveSecondaryWindows(); }
                catch (Exception e2) { LogWriter.LogWrite("CancelRoutineForToday RemoveSecondaryWindows ex - " + e2.Message); }
                appSettings.nCurrentActivityType = CurrentActivityType.NONE;

                // Refresh the window out of "routine mode" chrome (which we're leaving) without navigating away
                // from the Day Plan: restore the normal title-bar buttons (min/max, not just close) + resizing,
                // re-expand/enable the sidebar, and hide the routine postpone/abort ("please spare me") button.
                try
                {
                    ((App)Application.Current).BringMainWindowToFront();
                    _parentWindow.EnableAllButtons();
                    if (_parentWindow.PageHabits != null)
                        _parentWindow.PageHabits.btnPleaseSpareMe.Visibility = Visibility.Collapsed;
                }
                catch (Exception e3) { LogWriter.LogWrite("CancelRoutineForToday chrome refresh ex - " + e3.Message); }

                // Re-pull today's progress so the cancelled routine also resurfaces as an "earlier" group and
                // re-render for the final consistent state.
                await FetchDayProgressAsync();
                RenderDayPlan();
                SetBusy(false);
            }
            catch (Exception ex) { SetBusy(false); LogWriter.LogWrite("CancelRoutineForToday ex - " + ex.Message); }
        }

        // Reads a habitsStateVcInfo config string (where the uncancel/cancelled-routine strings live), English fallback.
        private string HabitsStateString(string key, string fallback)
        {
            try
            {
                var val = appSettings.ConfigFileStrings["habitsStateVcInfo"]?[key];
                return (val != null && val.ToString().Length > 0) ? val.ToString() : fallback;
            }
            catch { return fallback; }
        }

        /// <summary>
        /// A single tappable habit line: time · emoji · name. Selecting it reveals the action bar
        /// underneath (see BuildSelectedHabitUnit). Non-actionable rows (preview routines) don't select.
        /// </summary>
        private Grid BuildHabitRow(ScheduleBlock block, Action onSelect = null)
        {
            string timeText = string.Format("{0}–{1}", TimeString(block.Start), TimeString(block.End));
            // Completed "earlier today" habits render struck-through in the secondary colour. A SKIPPED
            // habit is also struck-through (so it doesn't read as still-doable) but tagged "· skipped" to
            // tell it apart from a done one.
            bool skipped = block.IsDone && !string.IsNullOrEmpty(block.HabitId)
                           && DayPlanDataSource.IsHabitSkipped(block.HabitId, _selectedDate.ToString("yyyy-MM-dd"));
            string title = skipped ? block.Title + "   ·  " + DayPlanText.SkippedTag : block.Title;
            Brush titleColor = block.IsDone ? SecBrush() : FgBrush();
            Grid row = LayoutColumns(timeText, title, SecBrush(), titleColor, vPad: 4, strikeThrough: block.IsDone);

            // A caller-supplied select handler (past-habit rows) wins; otherwise a live actionable row
            // selects itself by index.
            if (onSelect != null)
            {
                row.Background = Brushes.Transparent;   // make the whole row hit-testable
                row.Cursor = Cursors.Hand;
                row.MouseLeftButtonUp += (s, e) => onSelect();
            }
            else if (block.IsActionable && block.HabitIndex.HasValue)
            {
                int idx = block.HabitIndex.Value;
                row.Background = Brushes.Transparent;
                row.Cursor = Cursors.Hand;
                row.MouseLeftButtonUp += (s, e) =>
                {
                    _selectedHabitIndex = idx;
                    RenderDayPlan();
                };
            }
            return row;
        }

        /// <summary>
        /// The selected habit as the habit-view's connected unit: a solid-orange header (white
        /// time · emoji · name) with the Play/Done/Skip bar joined directly beneath, all inside one
        /// orange rounded border. Mirrors the macOS makeSelectedHabitUnit.
        /// </summary>
        private Border BuildSelectedHabitUnit(ScheduleBlock block, int idx)
        {
            // Orange header with white text, same columns as the plain rows so it lines up.
            var header = new Border
            {
                Background = new SolidColorBrush(COrange),
                Child = LayoutColumns(
                    string.Format("{0}–{1}", TimeString(block.Start), TimeString(block.End)),
                    block.Title, Brushes.White, Brushes.White, vPad: 8)
            };

            var unit = new StackPanel { Orientation = Orientation.Vertical };
            unit.Children.Add(header);
            unit.Children.Add(BuildActionBar(idx));

            // One orange rounded border around the whole unit; clip so the header fills the top.
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(COrange),
                Background = (Application.Current.TryFindResource("BackgroundColor") as Brush) ?? Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(0, 2, 0, 4),
                Child = unit
            };
            _selectedUnitBorder = card;   // so OnDone can flash it green before removal
            return card;
        }

        /// <summary>
        /// A not-done "earlier today" habit selected for retroactive logging: the same orange unit as a
        /// live selected habit, but with a single "I am done" (Complete) action + a reassuring hint —
        /// no Play/Skip, since an un-struck past habit is simply not-yet-logged. (macOS #2012.)
        /// </summary>
        private Border BuildSelectedPastHabitUnit(ScheduleBlock block)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(COrange),
                Child = LayoutColumns(
                    string.Format("{0}–{1}", TimeString(block.Start), TimeString(block.End)),
                    block.Title, Brushes.White, Brushes.White, vPad: 8)
            };

            var bar = new Grid { Margin = new Thickness(14, 12, 14, 12) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var circle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(COrange),
                Child = new TextBlock
                {
                    Text = "✓",
                    FontSize = 18,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var btn = new Button
            {
                Content = circle,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate()
            };
            btn.Click += (s, e) => OnPastHabitDone(block);
            Grid.SetColumn(btn, 0);
            bar.Children.Add(btn);

            var text = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            text.Children.Add(new TextBlock
            {
                Text = DayPlanText.PastHabitComplete,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = FgBrush()
            });
            text.Children.Add(new Emoji.Wpf.TextBlock
            {
                Text = DayPlanText.PastHabitCompleteHint,
                FontSize = 11,
                Foreground = SecBrush(),
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(text, 1);
            bar.Children.Add(text);

            var unit = new StackPanel { Orientation = Orientation.Vertical };
            unit.Children.Add(header);
            unit.Children.Add(bar);

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(COrange),
                Background = (Application.Current.TryFindResource("BackgroundColor") as Brush) ?? Brushes.White,
                ClipToBounds = true,
                Margin = new Thickness(0, 2, 0, 4),
                Child = unit
            };
        }

        /// <summary>The Play / Done / Skip bar shown beneath the selected habit.</summary>
        private UIElement BuildActionBar(int idx)
        {
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 14, 0, 10)
            };
            Brush idle = new SolidColorBrush(WithAlpha(CGray, 0.3));
            bar.Children.Add(ActionColumn(MakeActionIcon("play", Brushes.White), DayPlanText.Play,
                                          new SolidColorBrush(COrange), () => OnPlay(idx)));
            bar.Children.Add(ActionColumn(MakeActionIcon("done", FgBrush()), DayPlanText.Done,
                                          idle, () => OnDone(idx)));
            bar.Children.Add(ActionColumn(MakeActionIcon("skip", FgBrush()), DayPlanText.Skip,
                                          idle, () => OnSkip(idx)));
            return bar;
        }

        /// <summary>A crisp vector icon for the action circles — "play" (triangle), "done" (check),
        /// "skip" (skip-forward). Scales to fit the circle, so it stays sharp at any size.</summary>
        private static System.Windows.Shapes.Path MakeActionIcon(string kind, Brush color)
        {
            var path = new System.Windows.Shapes.Path
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            switch (kind)
            {
                case "play":   // right-pointing filled triangle, nudged right so it reads centred
                    path.Data = Geometry.Parse("M 9,6 L 9,18 L 19,12 Z");
                    path.Fill = color;
                    path.Width = 15; path.Height = 15;
                    path.Margin = new Thickness(2, 0, 0, 0);
                    break;
                case "done":   // check mark (stroked, rounded)
                    path.Data = Geometry.Parse("M 5,12.5 L 10,17.5 L 19,7");
                    path.Stroke = color;
                    path.StrokeThickness = 2.4;
                    path.StrokeStartLineCap = PenLineCap.Round;
                    path.StrokeEndLineCap = PenLineCap.Round;
                    path.StrokeLineJoin = PenLineJoin.Round;
                    path.Fill = Brushes.Transparent;
                    path.Width = 19; path.Height = 19;
                    break;
                default:       // "skip" — filled triangle + bar (skip-to-next)
                    path.Data = Geometry.Parse("M 6,6 L 6,18 L 15,12 Z M 16,6 L 18.5,6 L 18.5,18 L 16,18 Z");
                    path.Fill = color;
                    path.Width = 14; path.Height = 14;
                    break;
            }
            return path;
        }

        /// <summary>One action column: a coloured circular button with a vector icon, and a label beneath.</summary>
        private StackPanel ActionColumn(UIElement icon, string label, Brush fill, Action onClick)
        {
            var circle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = fill,
                Child = icon
            };

            var btn = new Button
            {
                Content = circle,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Template = BorderlessButtonTemplate()
            };
            btn.Click += (s, e) => onClick();

            var col = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 28, 0) };
            col.Children.Add(btn);
            col.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = SecBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            });
            return col;
        }

        /// <summary>A chrome-free Button template (just the content) so our circle isn't boxed in.</summary>
        private static ControlTemplate BorderlessButtonTemplate()
        {
            var tmpl = new ControlTemplate(typeof(Button));
            var cp = new System.Windows.FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            tmpl.VisualTree = cp;
            return tmpl;
        }

        // MARK: - Actions (Play / Done / Skip)

        /// <summary>The live to-do array backing the active routine (indices match Play/Done/Skip).</summary>
        private List<habit> ActiveArray()
        {
            if (appSettings.nCurrentActivityType == CurrentActivityType.MORNING_HABITS) return appSettings.arrAllMorningHabits;
            if (appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS) return appSettings.arrAllEveningHabits;
            return null;
        }

        private string ActiveLogType()
        {
            return appSettings.nCurrentActivityType == CurrentActivityType.EVENING_HABITS ? "evening routine" : "morning routine";
        }

        /// <summary>
        /// Whether acting (Done/Skip) on habit <paramref name="idx"/> leaves no other habit still to do
        /// in the active routine — i.e. it's the last remaining one, so the routine completes.
        /// </summary>
        private bool WillBeLastRemaining(int idx)
        {
            List<habit> arr = ActiveArray();
            if (arr == null) return false;
            string todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            int remaining = 0;
            var dbg = new StringBuilder();
            for (int i = 0; i < arr.Count; i++)
            {
                habit h = arr[i];
                bool handled = !string.IsNullOrEmpty(h.id) && DayPlanDataSource.IsHabitDoneOrSkipped(h.id, todayKey);
                if (!handled) remaining++;
                dbg.Append("[" + i + ":" + (h.name ?? "?") + ":" + (string.IsNullOrEmpty(h.id) ? "noid" : (handled ? "handled" : "REMAIN")) + "]");
            }
            bool isLast = remaining <= 1;   // only the habit being acted on is left
            LogWriter.LogWrite("WillBeLastRemaining(idx=" + idx + ") arrCount=" + arr.Count + " remaining=" + remaining + " -> " + isLast + " | " + dbg);
            return isLast;
        }

        /// <summary>
        /// Route the last remaining habit through the normal finished screen so it shows the
        /// routine-complete state with the "Unlock the machine" button (and its own log-quantity
        /// prompt for a log habit). Makes the habit the last activity so bLastActivity is detected;
        /// the finished screen posts the completion/skip itself.
        /// </summary>
        private void RouteLastHabitToFinished(int idx, bool skipped)
        {
            List<habit> arr = ActiveArray();
            if (arr == null || idx < 0 || idx >= arr.Count) { RenderDayPlan(); return; }
            habit h = arr[idx];

            // Move it to the end and point nHabitIndex at it, so the finished screen treats it as the
            // last activity (Count == nHabitIndex + 1) and shows the Unlock button.
            arr.RemoveAt(idx);
            arr.Add(h);
            appSettings.nHabitIndex = arr.Count - 1;
            appSettings.currentHabitPlaying = h;
            appSettings.dLastActivityPerformedStartTime = DateTime.Now;
            appSettings.bSkipCurrentActivity = false;
            appSettings.bSkipCurrentActivityCantDoItToday = skipped;
            // The finished screen only posts the completion/skip when NOT "showing activities from mid"
            // (that flag makes it graph-only). A Day-Plan Done/Skip IS a real completion, so clear it and
            // give it a valid start time so the completed/skip API actually fires.
            appSettings.bShowActivitiesFromMid = false;
            _parentWindow.PageHabits.ActivityStartTime = DateTime.Now;
            _selectedHabitIndex = null;

            LogWriter.LogWrite("DayPlan last habit -> finished screen: " + (h.name ?? "") + (skipped ? " (skip)" : ""));
            _parentWindow.PageHabits.ShowACtivityFinishedScreen(skipped);
        }

        /// <summary>
        /// Play: begin the routine starting at the selected habit — move it to the front (like the
        /// Welcome list's Begin) and show the standard routine-start screen.
        /// </summary>
        // Play the current/next actionable habit — the same flow as clicking Play on that card in the Day
        // Plan view (resume-from-parked, resume-if-postponed, last-remaining handling). Used by the tray
        // Day Plan card's Start button.
        public void PlayCurrentHabit()
        {
            try
            {
                var blocks = DayPlanDataSource.BuildBlocks(_selectedDate, includeDismissedMeetings: _showDismissedMeetings);
                var current = blocks?.FirstOrDefault(b => b.IsCurrent && b.HabitIndex.HasValue);
                if (current != null) OnPlay(current.HabitIndex.Value);
                else LogWriter.LogWrite("PlayCurrentHabit - no current actionable habit");
            }
            catch (Exception ex) { LogWriter.LogWrite("PlayCurrentHabit ex - " + ex.Message); }
        }

        private void OnPlay(int idx)
        {
            try
            {
                List<habit> arr = ActiveArray();
                if (arr == null || idx < 0 || idx >= arr.Count) { RenderDayPlan(); return; }
                habit item = arr[idx];

                // Issue 2: resume from THIS habit's own saved progress if it was parked (paused → back /
                // postponed). Each parked habit keeps its own remaining time, so replaying continues where
                // it left off — even after another habit was parked in between.
                int savedRemaining = 0;
                bool resuming = !string.IsNullOrEmpty(item.id)
                    && appSettings.dayPlanHabitRemainingSeconds.TryGetValue(item.id, out savedRemaining)
                    && savedRemaining > 0;
                if (resuming)
                {
                    // Point the single postpone slot at THIS habit so the activity resumes from its time.
                    RegistryManager.SetPostponedHabitData(item.id, savedRemaining.ToString());
                    appSettings.dayPlanHabitRemainingSeconds.Remove(item.id);
                }

                // If a focus session is running over a postponed routine, playing a habit must finish that
                // session the same way the postpone timer expiring does — ForceStopFocusSessionOnHabitsTime
                // (respects a teacher class-session lock, resets pause) — before resuming the routine below.
                if (appSettings.isFocusModeRunning)
                {
                    LogWriter.LogWrite("DayPlan OnPlay: focus session running — finishing it before starting the habit");
                    ((App)Application.Current).ForceStopFocusSessionOnHabitsTime();
                }

                // Issue 1: if the routine is postponed, resume it first (stop the postpone timer, clear the
                // state, re-enter routine mode) before starting — otherwise the tabs stay enabled and the
                // postpone timer keeps running. This covers BOTH the habits-postponed state AND the evening
                // focus-summary-postponed state (#1365): the latter has its own timer + tray item that must
                // be cleared, otherwise the habit runs while the app is still "postponed".
                if (appSettings.bHabitsPostponedState || appSettings.bFocusSummaryPostponedState)
                {
                    ((App)Application.Current).ResumeHabitsForDayPlanPlay();
                }

                // If this is the LAST remaining habit (all others already done/skipped), move it to the
                // END and point nHabitIndex at it, so after it runs the finished screen's own index-based
                // last-activity check (Count == nHabitIndex + 1) shows the "Unlock" / routine-complete
                // screen. This covers the out-of-order Day Plan case (habits done via a Done/Skip/Play mix)
                // without changing the finished-screen logic. Otherwise move it to the front to run next.
                bool isLastRemaining = WillBeLastRemaining(idx);
                LogWriter.LogWrite("DayPlan OnPlay: '" + (item.name ?? "") + "' idx=" + idx + " lastRemaining=" + isLastRemaining
                    + (isLastRemaining ? " -> moved to END (unlock on finish)" : " -> moved to FRONT"));
                arr.RemoveAt(idx);
                if (isLastRemaining)
                {
                    arr.Add(item);
                    appSettings.nHabitIndex = arr.Count - 1;
                }
                else
                {
                    arr.Insert(0, item);
                    appSettings.nHabitIndex = 0;
                }
                // Playing a habit from the Day Plan starts a real activity, so clear any "show finished
                // activities from mid" state (set when the app resumes mid-routine) — otherwise this
                // habit's own completion would be skipped as if it were just the resumed graph.
                appSettings.bShowActivitiesFromMid = false;
                _selectedHabitIndex = null;
                LogWriter.LogWrite("DayPlan Play habit: " + (item.name ?? "") + (resuming ? " (resume)" : ""));

                // The "set your intention" (ExtraPersonal) screen shows once, for the first habit of the
                // routine only — later plays (incl. from the Day Plan) go straight to the activity.
                if (!appSettings.bRoutineIntentionShown && !resuming)
                {
                    appSettings.bRoutineIntentionShown = true;
                    _parentWindow.PageHabits.ShowExtraPersonalFocus();
                }
                else
                {
                    _parentWindow.PageHabits.ShowActivityStartScreen(resuming);   // resume → start from saved time
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlan OnPlay ex - " + ex.Message); }
        }

        /// <summary>
        /// Done: for a log-quantity habit, ask its quantities first (native popup); then flash the
        /// card green, log the completion (backend + offline CSV, mirroring the normal path, including
        /// any quantities) and drop it from today's plan.
        /// </summary>
        private void OnDone(int idx)
        {
            try
            {
                List<habit> arr = ActiveArray();
                if (arr == null || idx < 0 || idx >= arr.Count) { RenderDayPlan(); return; }
                habit h = arr[idx];
                LogWriter.LogWrite("DayPlan OnDone: '" + (h.name ?? "") + "' idx=" + idx);

                // Last remaining habit → route to the finished screen so it shows the routine-complete
                // state with the "Unlock the machine" button (and handles its own log-quantity prompt),
                // exactly like the normal end-of-routine flow. (macOS parity.)
                if (WillBeLastRemaining(idx))
                {
                    if (!string.IsNullOrEmpty(h.id)) appSettings.dayPlanDoneOrSkippedHabitIds.Add(h.id);
                    RouteLastHabitToFinished(idx, skipped: false);
                    return;
                }

                // Log-quantity habits ask for their numbers first (writes back into h.logQuantityQuestions).
                if (h.logQuantity && h.logQuantityQuestions != null && h.logQuantityQuestions.Count > 0)
                {
                    var win = new DayPlanLogQuantityWindow(h.name ?? "", h.logQuantityQuestions, _parentWindow);
                    // Dim the main window behind the popup (app-wide convention for modal popups).
                    _parentWindow.GridShade.Visibility = Visibility.Visible;
                    bool? result;
                    try { result = win.ShowDialog(); }
                    finally { _parentWindow.GridShade.Visibility = Visibility.Collapsed; }
                    if (result != true) return;   // cancelled — don't complete
                }

                FlashGreenThen(() => CompleteHabit(idx, h));
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlan OnDone ex - " + ex.Message); }
        }

        /// <summary>Logs a habit complete (with any captured quantities) and removes it from the plan.</summary>
        private void CompleteHabit(int idx, habit h)
        {
            try
            {
                int dur = DayPlanDataSource.EstimatedDuration(h);
                string startStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                appSettings.AddEntryInCSV(ActiveLogType(), h.name ?? "", dur.ToString(), h.logQuantityQuestion ?? "", "", startStr);
                var client = new FocusBearAPIClient();
                List<QuantityQuestion> quantities = h.logQuantity ? h.logQuantityQuestions : null;
                _ = client.PostActivityCompleted(h.id, h.activity_sequence_id, DateTime.Now, dur, "", quantities, false);
                LogWriter.LogWrite("DayPlan Done habit: " + (h.name ?? ""));

                if (!string.IsNullOrEmpty(h.id)) appSettings.dayPlanDoneOrSkippedHabitIds.Add(h.id);
                List<habit> arr = ActiveArray();
                if (arr != null && idx >= 0 && idx < arr.Count) arr.RemoveAt(idx);
                _selectedHabitIndex = null;
                RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlan CompleteHabit ex - " + ex.Message); }
        }

        /// <summary>Flashes the selected card green for a moment, then runs <paramref name="then"/>.</summary>
        private void FlashGreenThen(Action then)
        {
            if (_selectedUnitBorder == null) { then(); return; }
            _selectedUnitBorder.Background = new SolidColorBrush(WithAlpha(CGreen, 0.55));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
            timer.Tick += (s, e) => { timer.Stop(); then(); };
            timer.Start();
        }

        /// <summary>
        /// Retroactively log a not-done "earlier today" habit as complete (macOS #2012): for a
        /// log-quantity habit ask its numbers first, then post the completion backdated to the habit's
        /// slot time, mark it done (so it re-renders struck-through) and clear the selection.
        /// </summary>
        private void OnPastHabitDone(ScheduleBlock block)
        {
            try
            {
                if (string.IsNullOrEmpty(block.HabitId)) return;
                if (!TryFindHabit(block.HabitId, out habit h)) return;

                if (h.logQuantity && h.logQuantityQuestions != null && h.logQuantityQuestions.Count > 0)
                {
                    var win = new DayPlanLogQuantityWindow(h.name ?? "", h.logQuantityQuestions, _parentWindow);
                    _parentWindow.GridShade.Visibility = Visibility.Visible;
                    bool? result;
                    try { result = win.ShowDialog(); }
                    finally { _parentWindow.GridShade.Visibility = Visibility.Collapsed; }
                    if (result != true) return;
                }

                int dur = DayPlanDataSource.EstimatedDuration(h);
                var client = new FocusBearAPIClient();
                List<QuantityQuestion> quantities = h.logQuantity ? h.logQuantityQuestions : null;
                // Backdate the completion to the habit's slot on the viewed day.
                _ = client.PostActivityCompleted(h.id, h.activity_sequence_id, block.Start, dur, "", quantities, false);
                string dayKey = _selectedDate.ToString("yyyy-MM-dd");
                if (!appSettings.dayPlanCompletedHabitIdsByDay.TryGetValue(dayKey, out HashSet<string> daySet))
                {
                    daySet = new HashSet<string>();
                    appSettings.dayPlanCompletedHabitIdsByDay[dayKey] = daySet;
                }
                daySet.Add(h.id);
                LogWriter.LogWrite("DayPlan past habit done: " + (h.name ?? "") + " on " + dayKey);
                _selectedPastHabitId = null;
                RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlan OnPastHabitDone ex - " + ex.Message); }
        }

        /// <summary>Finds a habit by id in the full morning/evening lists (for its activity_sequence_id).</summary>
        private bool TryFindHabit(string id, out habit found)
        {
            foreach (habit h in appSettings.arrGLOBALAllMorningHabits) if (h.id == id) { found = h; return true; }
            foreach (habit h in appSettings.arrGLOBALAllEveningHabits) if (h.id == id) { found = h; return true; }
            found = default(habit);
            return false;
        }

        /// <summary>Skip: log the habit skipped (backend) and drop it from today's plan.</summary>
        private void OnSkip(int idx)
        {
            try
            {
                List<habit> arr = ActiveArray();
                if (arr == null || idx < 0 || idx >= arr.Count) { RenderDayPlan(); return; }
                habit h = arr[idx];
                LogWriter.LogWrite("DayPlan OnSkip: '" + (h.name ?? "") + "' idx=" + idx);

                // Last remaining habit → route to the finished screen (skipped variant) so it shows the
                // routine-complete + "Unlock the machine" state. It posts the skip itself.
                // IMPORTANT: check BEFORE marking this habit handled — otherwise the remaining count is
                // one short and skipping the *second-to-last* habit would wrongly complete the routine
                // (matches how OnDone orders it).
                if (WillBeLastRemaining(idx))
                {
                    if (!string.IsNullOrEmpty(h.id)) appSettings.dayPlanDoneOrSkippedHabitIds.Add(h.id);
                    RouteLastHabitToFinished(idx, skipped: true);
                    return;
                }

                if (!string.IsNullOrEmpty(h.id)) appSettings.dayPlanDoneOrSkippedHabitIds.Add(h.id);
                var client = new FocusBearAPIClient();
                _ = client.PostActivitySkipped(h.id, h.activity_sequence_id);
                LogWriter.LogWrite("DayPlan Skip habit: " + (h.name ?? ""));
                arr.RemoveAt(idx);
                _selectedHabitIndex = null;
                RenderDayPlan();
            }
            catch (Exception ex) { LogWriter.LogWrite("DayPlan OnSkip ex - " + ex.Message); }
        }

        // MARK: - Non-habit cards (deep work / break / evening transition / all-done / meeting)

        private Border BuildNonHabitCard(ScheduleBlock block)
        {
            Color fill, border;
            bool hasBorder = true;
            switch (block.Kind)
            {
                case ScheduleBlockKind.Meeting:
                    Color mc = ColorFromHex(block.Colour, CBlue);   // per-event colour override, default blue
                    fill = WithAlpha(mc, 0.10); border = WithAlpha(mc, 0.5); break;
                case ScheduleBlockKind.DeepWork:
                    fill = WithAlpha(CGreen, 0.10); border = WithAlpha(CGreen, 0.5); break;
                case ScheduleBlockKind.BreakSprint:
                    fill = WithAlpha(COrange, 0.10); border = WithAlpha(COrange, 0.5); break;
                case ScheduleBlockKind.RoutineTransition:
                    fill = WithAlpha(CPurple, 0.10); border = WithAlpha(CPurple, 0.5); break;
                case ScheduleBlockKind.Sleep:
                    fill = WithAlpha(CIndigo, 0.10); border = WithAlpha(CIndigo, 0.4); break;
                default: // AllDone
                    fill = Colors.Transparent; border = Colors.Transparent; hasBorder = false; break;
            }

            bool startOnly = block.Kind == ScheduleBlockKind.AllDone || block.Kind == ScheduleBlockKind.RoutineTransition;
            string timeText = startOnly ? TimeString(block.Start)
                                        : string.Format("{0}–{1}", TimeString(block.Start), TimeString(block.End));
            string title = block.Kind == ScheduleBlockKind.AllDone ? "✅ " + DayPlanText.HabitsDone : block.Title;

            // Trailing accessory: duration for deep-work/break/sleep, or the "meeting" subtitle.
            string trailing = null;
            if (block.Kind == ScheduleBlockKind.DeepWork || block.Kind == ScheduleBlockKind.BreakSprint || block.Kind == ScheduleBlockKind.Sleep)
                trailing = DurationString(block.DurationSeconds);
            else if (!string.IsNullOrEmpty(block.Subtitle))
                trailing = block.Subtitle;

            Grid content = LayoutColumns(timeText, title, SecBrush(), FgBrush(), vPad: 4, trailing: trailing);

            return StyledCard(new SolidColorBrush(fill), new SolidColorBrush(border), hasBorder, content);
        }

        // MARK: - Building blocks

        /// <summary>
        /// A rounded, optionally-bordered card wrapping <paramref name="content"/>. Matches the macOS
        /// styledCard: cornerRadius 8, 1pt border, 12×8 content padding.
        /// </summary>
        private Border StyledCard(Brush fill, Brush border, bool hasBorder, UIElement content)
        {
            return new Border
            {
                Background = fill,
                BorderBrush = border,
                BorderThickness = new Thickness(hasBorder ? 1 : 0),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Child = content
            };
        }

        /// <summary>
        /// Lays [time | emoji | title (| trailing)] into a 3-column grid with fixed time/emoji widths,
        /// so the title column starts at the same x on every row (mirrors macOS layoutColumns).
        /// </summary>
        private Grid LayoutColumns(string time, string title, Brush timeColor, Brush titleColor,
                                   double vPad, string trailing = null, bool strikeThrough = false)
        {
            var grid = new Grid { Margin = new Thickness(6, vPad, 0, vPad) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(EmojiColWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var timeLabel = new TextBlock
            {
                Text = time,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                Foreground = timeColor,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeLabel, 0);
            grid.Children.Add(timeLabel);

            SplitEmoji(title, out string emoji, out string rest);
            if (!string.IsNullOrEmpty(emoji))
            {
                var emojiLabel = new Emoji.Wpf.TextBlock
                {
                    Text = emoji,
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(emojiLabel, 1);
                grid.Children.Add(emojiLabel);
            }

            var titleLabel = new TextBlock
            {
                Text = rest,
                FontSize = 16,
                Foreground = titleColor,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                TextDecorations = strikeThrough ? TextDecorations.Strikethrough : null
            };

            if (string.IsNullOrEmpty(trailing))
            {
                Grid.SetColumn(titleLabel, 2);
                grid.Children.Add(titleLabel);
            }
            else
            {
                // title + a secondary trailing label (duration / "meeting") on the same line.
                var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(titleLabel);
                row.Children.Add(new TextBlock
                {
                    Text = trailing,
                    FontSize = 13,
                    Foreground = SecBrush(),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                Grid.SetColumn(row, 2);
                grid.Children.Add(row);
            }

            return grid;
        }

        // MARK: - Helpers

        /// <summary>Peels a leading emoji run off "🧘 Meditate" so the emoji renders in its own column.</summary>
        private static void SplitEmoji(string title, out string emoji, out string rest)
        {
            emoji = "";
            rest = title ?? "";
            if (string.IsNullOrEmpty(title)) return;

            string trimmed = title.TrimStart();
            var sb = new StringBuilder();
            int consumed = 0;
            var en = StringInfo.GetTextElementEnumerator(trimmed);
            while (en.MoveNext())
            {
                string cluster = (string)en.Current;
                if (string.IsNullOrWhiteSpace(cluster)) break;
                if (!IsEmojiLike(cluster)) break;
                sb.Append(cluster);
                consumed += cluster.Length;
            }

            string peeled = sb.ToString();
            string remainder = trimmed.Substring(consumed).Trim();
            if (peeled.Length > 0 && remainder.Length > 0)
            {
                emoji = peeled;
                rest = remainder;
            }
        }

        private static bool IsEmojiLike(string cluster)
        {
            if (string.IsNullOrEmpty(cluster)) return false;
            int cp = char.ConvertToUtf32(cluster, 0);
            return cp >= 0x1F000                       // most emoji (supplementary planes)
                || (cp >= 0x2190 && cp <= 0x2BFF)      // arrows, symbols, misc technical, misc symbols/arrows
                || (cp >= 0x2600 && cp <= 0x27BF)      // misc symbols + dingbats
                || (cp >= 0xFE00 && cp <= 0xFE0F);     // variation selectors
        }

        /// <summary>Zero-padded 12-hour time with lowercase am/pm, e.g. "09:05 am" (matches macOS).</summary>
        private static string TimeString(DateTime d)
        {
            return d.ToString("hh:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        private static string DurationString(int seconds)
        {
            int m = seconds / 60;
            if (m >= 60) return string.Format("{0}h {1}m", m / 60, m % 60);
            return string.Format("{0}m", m);
        }

        private static Color WithAlpha(Color c, double alpha)
        {
            return Color.FromArgb((byte)Math.Round(alpha * 255), c.R, c.G, c.B);
        }

        /// <summary>Parse a "#RRGGBB" hex string to a Color, falling back to <paramref name="fallback"/>.</summary>
        private static Color ColorFromHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); }
            catch { return fallback; }
        }

        /// <summary>Theme-aware primary text colour (falls back to black if the resource is missing).</summary>
        private static Brush FgBrush()
        {
            return (Application.Current.TryFindResource("ForegroundColor") as Brush) ?? Brushes.Black;
        }

        /// <summary>Secondary text colour (systemGray — reads well in both light and dark).</summary>
        private static Brush SecBrush()
        {
            return new SolidColorBrush(CGray);
        }

        /// <summary>True when the dark theme is active (its foreground brush is light).</summary>
        private static bool IsDarkTheme()
        {
            var fg = Application.Current.TryFindResource("ForegroundColor") as SolidColorBrush;
            return fg != null && (fg.Color.R + fg.Color.G + fg.Color.B) / 3 > 128;
        }

        /// <summary>"All sub-tasks done" green — a muted shade that stays readable in both themes
        /// (the raw systemGreen washes out on light backgrounds).</summary>
        private static Brush DoneGreenBrush()
        {
            return new SolidColorBrush(IsDarkTheme()
                ? Color.FromRgb(0x5F, 0xD3, 0x7B)   // lighter green for dark backgrounds
                : Color.FromRgb(0x2E, 0x9E, 0x4F));  // deeper green for light backgrounds
        }
    }
}
