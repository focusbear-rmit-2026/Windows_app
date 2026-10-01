using Focus_Bear.Models;
using Focus_Bear.Services;
using Focus_Bear.Utils;
using FocusBear.Core.Focus;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Focus_Bear.Pomodoro
{
    /// <summary>
    /// The one-dial start screen (the Mac's <c>FocusStartSimpleScreen</c>, Figma "0.0/ Flow Start"): a title,
    /// the bear, the session-length dial and one Start button. Everything else lives on the advanced screen,
    /// which the "Top Secret / Extra Powerful Options ›" link switches the app into.
    /// The session runs the mode the user last used (AI or "super distracting"), as the old simple screen did.
    /// </summary>
    public sealed class FocusStartSimpleScreen : UserControl
    {
        private readonly FocusBearMainWindow _parent;
        private readonly AppSettings _settings = AppSettings.GetInstance;
        private FocusStartDuration _duration = new FocusStartDuration(FocusStartDuration.DefaultMinutes);
        private double _factor;
        private Button _start;

        public FocusStartSimpleScreen(FocusBearMainWindow parent)
        {
            _parent = parent;
            SetResourceReference(BackgroundProperty, "BackgroundColor");
            // The Preferences host paints every Grid with the page background, which showed through inside this
            // screen's surfaces (a lighter patch in dark mode); the screen draws its own.
            Resources[typeof(Grid)] = new Style(typeof(Grid));
            SizeChanged += (s, e) =>
            {
                double factor = new FocusStartScale(ActualWidth).Factor;
                if (Math.Abs(factor - _factor) > 0.03) Render();
            };
            IsVisibleChanged += (s, e) => { if (IsVisible) UpdateValues(); };
        }

        private FocusStartStrings Strings => new FocusStartStrings(_settings.ConfigFileStrings?["focusStartVcInfo"] as JObject);

        /// <summary>Called whenever the screen is shown: the saved length, and a fresh (empty) task queue.</summary>
        public void UpdateValues()
        {
            try
            {
                BlockingScheduleManager.CheckCustomBlockingScheduleTime();
                _duration = FocusStartDuration.FromStored(_settings.PomodoroMinutes);
                //Should empty the value for tasks here
                _settings.sRawTasks = "";
                Render();
            }
            catch (Exception ex) { LogWriter.LogWrite("FocusStartSimpleScreen UpdateValues ex - " + ex.Message); }
        }

        /// <summary>AI blocking unless the last session used the "super distracting" mode (or the trial lapsed).</summary>
        private bool UsesAi()
        {
            if (_settings.isExpired) return false;
            _settings.LoadMostRecentFocusMode();
            var last = _settings.arrFocusModes.FirstOrDefault(m => m.id == _settings.sMostRecentFocusmodeDoneID);
            return !(last != null && last.isDefault);
        }

        private void Render()
        {
            var p = FocusStartPalette.Current();
            var s = new FocusStartScale(ActualWidth > 0 ? ActualWidth : 1100);
            _factor = s.Factor;
            var strings = Strings;

            var column = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, s.Pt(195), 0, s.Pt(96)) };
            var title = FocusStartUi.Text(strings["titleSimple"], s.Pt(96), p.Primary, FontWeights.SemiBold);
            title.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.MaxWidth = Math.Max(s.Pt(1367) / 2, (ActualWidth > 0 ? ActualWidth : 1100) - 2 * s.Pt(760));
            AutomationProperties.SetAutomationId(title, "txtFocusStartTitle");
            column.Children.Add(title);

            var bear = FocusStartUi.Bear("focusStartBearSimple", strings["bubbleReadyToFocus"], p, s, bubbleBeside: true, bearWidthPx: 506);
            bear.Margin = new Thickness(0, s.Pt(24), 0, s.Pt(24));
            column.Children.Add(bear);

            var label = FocusStartUi.Text(strings["labelSetSessionLength"], s.Pt(64), p.Primary);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 0, 0, s.Pt(24));
            column.Children.Add(label);

            var dial = new FocusStartDurationDial(_duration, strings, p, s);
            _start = FocusStartUi.Primary(strings.StartButtonTitle(_duration), p, s, StartSession, "btnFocusStartStart");
            dial.DurationChanged += d =>
            {
                _duration = d;
                FocusStartUi.SetPrimaryTitle(_start, strings.StartButtonTitle(d));
            };
            column.Children.Add(dial);

            _start.Width = s.Pt(1191);
            _start.Margin = new Thickness(0, s.Pt(96), 0, 0);
            column.Children.Add(_start);

            if (BlockingScheduleManager.IsIronFocusScheduleActive())
            {
                // A super strict schedule is running: no manual session on top of it (as before).
                _start.IsEnabled = false;
                var notice = FocusStartUi.Text(_settings.ConfigFileStrings?["pomodoroInfo"]?["simpleSuperStrictBlockingActiveMessage"]?.ToString()
                    ?? "A super strict blocking schedule is active.", s.Pt(36), p.Warning);
                notice.TextAlignment = TextAlignment.Center;
                notice.MaxWidth = s.Pt(1191);
                notice.Margin = new Thickness(0, s.Pt(24), 0, 0);
                AutomationProperties.SetAutomationId(notice, "txtFocusStartScheduleNotice");
                column.Children.Add(notice);
            }

            var page = new Grid();
            page.Children.Add(column);
            var link = FocusStartUi.ModeLink(strings["linkAdvancedTop"], strings["linkAdvancedBottom"], false, p, s, SwitchToAdvanced);
            link.HorizontalAlignment = HorizontalAlignment.Right;
            link.VerticalAlignment = VerticalAlignment.Top;
            link.Margin = new Thickness(0, s.Pt(123), s.Pt(64), 0);
            page.Children.Add(link);
            Content = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        private void SwitchToAdvanced()
        {
            _parent.PageSettings.SwitchAppMode(geek: true);
            _parent.HandleLeafSelection(LeftButtonType.StartFocusSession);
        }

        private void StartSession()
        {
            LogWriter.LogWrite("FocusStartSimpleScreen - start session");
            int minutes = _duration.EffectiveMinutes;
            string breakMinutes = FocusSessionStarter.BreakFromRatio(minutes);
            _settings.CapturePostHog("start-focus-session-manually", string.Format("\"type\":\"simple-focus\",\"focusDuration\":\"{0}\",\"breakDuration\":\"{1}\"", minutes, breakMinutes));

            bool ai = UsesAi();
            _settings.bAiCheckUrlEnabledLastValueBeforeSimplePomodoro = _settings.bAiCheckUrlEnabled;
            _settings.sRunningFocusModeIntention = "";
            _settings.bAiCheckUrlEnabled = ai;
            _settings.sMinutesBreakPomodoro = "";
            // The simple screen never starts a class session (#1343) — a teacher here runs a personal session.
            _settings.bTeacherStartPersonalSession = true;

            var mode = FocusSessionStarter.SimpleMode(ai);
            FocusSessionStarter.WarnAllowListWithoutAi(mode, _parent);
            FocusSessionStarter.Start(mode, minutes, breakMinutes, "", personalSession: true);
            RegistryManager.SetSimplePomodoroTourShownStatus();

            _parent.Hide();
            _settings.isStartPomodoroWindowOpen = false;
        }
    }
}
