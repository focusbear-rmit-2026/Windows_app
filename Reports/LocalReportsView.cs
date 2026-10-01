using Focus_Bear.Utils;
using FocusBear.Core.Reports;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Focus_Bear.Reports
{
    /// <summary>
    /// Strings for the local reports list, from the shared config's <c>localReportStatus</c> section (the keys the
    /// Mac's <c>LocalReportStatusStrings</c> reads), with its English defaults.
    /// </summary>
    internal sealed class LocalReportStrings
    {
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { "title", "Your local reports" }, { "explanationWindows", "Progress from the reports saved on this PC." },
            { "refresh", "Refresh" }, { "loading", "Loading reports…" }, { "empty", "No local reports for this account yet." },
            { "readFailed", "Couldn't read your local reports. Try refreshing." },
            { "untitled", "Report without a description" }, { "queued", "Queued" },
            { "investigating", "Investigating" }, { "investigatingDetail", "This report is being investigated." },
            { "readyForQA", "Ready for your review" },
            { "needsClarification", "Blocked or needs input" }, { "queuedDetail", "This report is waiting to be investigated." },
            { "readyForQADetail", "Work has been prepared for you to check. This does not confirm a released fix." },
            { "needsClarificationDetail", "This report is waiting on a decision, more information, or another change." },
            { "blockedOnBackend", "Waiting on a backend release" },
            { "blockedOnBackendDetail", "The app side is done, but this cannot be tested until a backend change is merged and deployed." },
            { "openNotes", "Read review notes" }, { "showingRecent", "Showing the most recent local reports." },
            { "reportedAt", "Reported" }, { "noteSummary", "From review notes" },
            { "addInstructions", "Add feedback or instructions" }, { "instructionsPrompt", "What else should we know?" },
            { "saveInstructions", "Save instructions" }, { "cancel", "Cancel" }, { "instructionsSaved", "Instructions saved to this report." },
            { "instructionsFailed", "Couldn't save your instructions. Refresh the reports and try again." },
            { "instructionsLimit", "Use up to 4,000 characters." },
            { "instructionsFull", "There's no room for another image — the instructions are limited to 4,000 characters. Shorten them first." }, { "yourInstructions", "Your latest instructions" },
            { "confirmedFixed", "Confirmed fixed" }, { "confirmedFixedDetail", "You confirmed that this issue is fixed." },
            { "markConfirmedFixed", "Mark as confirmed fixed" }, { "undoConfirmation", "Undo confirmation" },
            { "confirmationFailed", "Couldn't update this report's status. Refresh and try again." }, { "confirmedAt", "Confirmed" },
            { "areaFilterAll", "All areas" }, { "areaFilterLabel", "Part of the app" },
            { "areaDayPlan", "Day Plan" }, { "areaFocusSession", "Focus session" }, { "areaTasksProjects", "Tasks & projects" },
            { "areaBlocking", "Blocking" }, { "areaRoutinesBreaks", "Routines & breaks" }, { "areaReportsFeedback", "Reports" },
            { "areaStats", "Stats" }, { "areaMenuBar", "Menu bar" }, { "areaOther", "Other" },
            { "reworkNeeded", "Rework needed" },
            { "reworkNeededDetail", "You added feedback after the last review, so this is back with us." },
            { "notesReadFailed", "Couldn't read these notes. Refresh the reports and try again." }, { "close", "Close" },
            { "screenshots", "Screenshots" }, { "screenshotOpenFailed", "Couldn't open that screenshot. Refresh the reports and try again." },
            { "searchPlaceholder", "Search reports" }, { "noSearchMatches", "No reports match your search." },
            { "pasteImage", "Paste image" }, { "pasteImageHint", "Paste a screenshot, drop image files here, or choose them." },
            { "chooseImage", "Choose images…" }, { "chooseImageTitle", "Choose screenshots" },
            { "openInPreview", "Open in viewer" }, { "pasteImageFailed", "Couldn't attach that image. Copy it again and retry." },
            { "noteOriginal", "Original note" }, { "noteUpdateHeading", "Update <NUMBER> of <COUNT>" },
            { "noteLatest", "Latest" }, { "noteNewestFirst", "Newest update first." },
            { "latestUpdate", "Latest update" }, { "lastAIUpdate", "Last update from AI" },
            // Windows form
            { "sendToLocal", "Create a local Claude task" }, { "localSaved", "Saved to your local reports ✓" },
            { "localFailed", "Couldn't save the report — please try again" },
        };

        private readonly JObject _config;

        internal LocalReportStrings()
        {
            _config = AppSettings.GetInstance.ConfigFileStrings?["localReportStatus"] as JObject;
        }

        internal string this[string key]
        {
            get
            {
                var value = _config?[key] as JValue;
                if (value != null && value.Type == JTokenType.String && !string.IsNullOrEmpty((string)value)) return (string)value;
                return Defaults.TryGetValue(key, out var text) ? text : key;
            }
        }

        internal string StateKey(LocalReportReviewState state)
        {
            switch (state)
            {
                case LocalReportReviewState.Investigating: return "investigating";
                case LocalReportReviewState.ReadyForQA: return "readyForQA";
                case LocalReportReviewState.NeedsClarification: return "needsClarification";
                case LocalReportReviewState.ReworkNeeded: return "reworkNeeded";
                case LocalReportReviewState.ConfirmedFixed: return "confirmedFixed";
                case LocalReportReviewState.BlockedOnBackend: return "blockedOnBackend";
                default: return "queued";
            }
        }

        internal string Area(LocalReportArea area) => this["area" + area];
    }

    /// <summary>
    /// "Your local reports" — the Mac's <c>LocalReportStatusView</c>: every report this account filed, grouped by
    /// where it stands (attention first), searchable and filterable by area, with the reviewer's newest update,
    /// the screenshots, the full notes, and the two things the user does here: add feedback, and confirm a fix.
    /// Rescans every 15 seconds while it is on screen.
    /// </summary>
    internal sealed class LocalReportsView : UserControl
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
        private static readonly Brush Accent = Frozen("#E88800");
        private static readonly Brush Danger = Frozen("#EB5757");

        private readonly LocalReportStrings _s = new LocalReportStrings();
        private readonly Func<string> _account;
        private readonly string _root;
        private readonly DispatcherTimer _timer;
        private readonly TextBox _search = new TextBox();
        private readonly StackPanel _list = new StackPanel();
        private readonly WrapPanel _areas = new WrapPanel { Margin = new Thickness(0, 6, 0, 4) };
        private readonly Dictionary<LocalReportReviewState, bool> _expanded = new Dictionary<LocalReportReviewState, bool>();
        private readonly HashSet<string> _pending = new HashSet<string>();
        private readonly Dictionary<string, string> _rowMessages = new Dictionary<string, string>();
        private IList<LocalReport> _reports = new List<LocalReport>();
        private LocalReportArea? _area;
        private bool _loading, _failed, _limited;
        private int _generation;

        internal LocalReportsView(Func<string> account, string root = null)
        {
            _account = account;
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards
            _root = root ?? LocalReportFolders.DefaultRoot;
            var stack = new StackPanel { Margin = new Thickness(20, 0, 20, 20) };
            var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 12, 0, 0) };
            var title = Text(_s["title"], 16, FontWeights.SemiBold);
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);
            var refresh = SmallButton(_s["refresh"], (s, e) => Refresh());
            AutomationProperties.SetAutomationId(refresh, "btnLocalReportsRefresh");
            DockPanel.SetDock(refresh, Dock.Right);
            header.Children.Add(refresh);
            stack.Children.Add(header);
            var explanation = Text(_s["explanationWindows"], 12, secondary: true);
            explanation.Margin = new Thickness(0, 2, 0, 8);
            stack.Children.Add(explanation);
            _search.SetResourceReference(StyleProperty, "RoundedTextBoxStyle");
            _search.Height = 32;
            _search.Padding = new Thickness(5, 0, 5, 0);
            _search.VerticalContentAlignment = VerticalAlignment.Center;
            _search.ToolTip = _s["searchPlaceholder"];
            AutomationProperties.SetAutomationId(_search, "txtLocalReportsSearch");
            _search.TextChanged += (s, e) => Draw();
            stack.Children.Add(WithPlaceholder(_search, _s["searchPlaceholder"]));
            stack.Children.Add(_areas);
            stack.Children.Add(_list);
            Content = stack;

            _timer = new DispatcherTimer { Interval = RefreshInterval };
            _timer.Tick += (s, e) => { if (!_loading) Refresh(); };
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue) { Refresh(); _timer.Start(); }
                else _timer.Stop();
            };
            Draw();
        }

        private static Brush Frozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        // ------------------------------------------------------------------ loading

        /// <summary>Scans off the UI thread; a scan overtaken by a newer one, or by an account change, lands nowhere.</summary>
        internal async void Refresh()
        {
            int token = ++_generation;
            string account = _account();
            _loading = true;
            Draw();
            LocalReportSnapshot snapshot = null;
            bool failed = false;
            try { snapshot = await Task.Run(() => LocalReportReader.Scan(_root, account)); }
            catch (Exception ex)
            {
                failed = true;
                LogWriter.LogWrite("LocalReportsView scan ex - " + ex.Message);
            }
            if (token != _generation || account != _account()) return;
            _loading = false;
            _failed = failed;
            _reports = snapshot?.Reports ?? new List<LocalReport>();
            _limited = snapshot?.IsLimited ?? false;
            Draw();
        }

        // ------------------------------------------------------------------ drawing

        private void Draw()
        {
            DrawAreas();
            _list.Children.Clear();
            if (_failed) { _list.Children.Add(Text(_s["readFailed"], 12, secondary: true)); return; }
            if (_loading && _reports.Count == 0) { _list.Children.Add(Text(_s["loading"], 12, secondary: true)); return; }
            if (_reports.Count == 0) { _list.Children.Add(Text(_s["empty"], 12, secondary: true)); return; }
            var visible = LocalReportSearch.Matching(LocalReportAreas.Matching(_reports, _area), _search.Text);
            if (visible.Count == 0) { _list.Children.Add(Text(_s["noSearchMatches"], 12, secondary: true)); return; }
            bool searching = _search.Text.Trim().Length > 0;
            foreach (var group in LocalReportGrouping.Groups(visible)) _list.Children.Add(Group(group.Key, group.Value, searching));
            if (_limited) _list.Children.Add(Text(_s["showingRecent"], 12, secondary: true));
        }

        private void DrawAreas()
        {
            _areas.Children.Clear();
            var present = LocalReportAreas.Present(_reports);
            if (present.Count < 2) return;
            var label = Text(_s["areaFilterLabel"], 11, secondary: true);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 8, 6);
            _areas.Children.Add(label);
            _areas.Children.Add(Pill(_s["areaFilterAll"], !_area.HasValue, () => { _area = null; Draw(); }));
            foreach (var area in present)
            {
                var captured = area;
                _areas.Children.Add(Pill(_s.Area(area), _area == area, () => { _area = captured; Draw(); }, "pillReportArea" + area));
            }
        }

        /// <summary>One status accordion. A search opens every group that still has matches.</summary>
        private FrameworkElement Group(LocalReportReviewState state, IList<LocalReport> reports, bool searching)
        {
            bool open = searching || (_expanded.TryGetValue(state, out var chosen) ? chosen : LocalReportGrouping.ExpandedByDefault.Contains(state));
            var header = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold };
            header.Inlines.Add(new System.Windows.Documents.Run((open ? "▾  " : "▸  ") + _s[_s.StateKey(state)]));
            header.Inlines.Add(new System.Windows.Documents.Run("  (" + reports.Count + ")") { FontWeight = FontWeights.Normal });
            header.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var toggle = new Button { Content = header, Template = PlainTemplate(), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 6), Focusable = false };
            toggle.Click += (s, e) => { _expanded[state] = !open; Draw(); };
            AutomationProperties.SetAutomationId(toggle, "grpLocalReports_" + state);
            var stack = new StackPanel();
            stack.Children.Add(toggle);
            if (open) foreach (var report in reports) stack.Children.Add(Row(report));
            return stack;
        }

        private FrameworkElement Row(LocalReport report)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

            var left = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            left.Children.Add(Text(report.Title.Length == 0 ? _s["untitled"] : report.Title, 13, FontWeights.SemiBold));
            left.Children.Add(Text(_s["reportedAt"] + " " + report.CreatedAt.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture), 11, secondary: true));
            var age = LocalReportUpdateAge.Describe(report.LastAIUpdate, DateTime.Now);
            if (age != null) left.Children.Add(Text(_s["lastAIUpdate"] + "  " + LocalReportUpdateAge.Combined(age), 11, secondary: true));
            grid.Children.Add(left);

            var right = new StackPanel();
            Grid.SetColumn(right, 1);
            var state = Text(_s[_s.StateKey(report.State)], 12, FontWeights.SemiBold);
            if (report.State == LocalReportReviewState.ReworkNeeded) state.Foreground = Danger;
            right.Children.Add(state);
            right.Children.Add(Text(_s[_s.StateKey(report.State) + "Detail"], 11, secondary: true));
            if (report.State == LocalReportReviewState.ConfirmedFixed && report.UserStatus?.UpdatedDate != null)
                right.Children.Add(Text(_s["confirmedAt"] + " " + report.UserStatus.UpdatedDate.Value.ToString("d MMM yyyy", CultureInfo.CurrentCulture), 11, secondary: true));
            bool confirmed = report.State == LocalReportReviewState.ConfirmedFixed;
            var confirm = Link(_pending.Contains(report.Id) ? _s[confirmed ? "undoConfirmation" : "markConfirmedFixed"] + " …" : _s[confirmed ? "undoConfirmation" : "markConfirmedFixed"],
                               () => SetConfirmed(report, !confirmed), enabled: !_pending.Contains(report.Id));
            AutomationProperties.SetAutomationId(confirm, "lnkReportConfirm_" + report.ReportCreated);
            right.Children.Add(confirm);
            grid.Children.Add(right);

            var body = new StackPanel();
            body.Children.Add(grid);
            if (!string.IsNullOrEmpty(report.NotesExcerpt))
            {
                string label = report.LatestUpdate != null ? _s["latestUpdate"] + (report.LatestUpdate.Date.HasValue ? " · " + report.LatestUpdate.Date.Value.ToString("d MMM HH:mm", CultureInfo.CurrentCulture) : "") : _s["noteSummary"];
                var labelText = Text(label, 11, secondary: true);
                labelText.Margin = new Thickness(0, 8, 0, 2);
                body.Children.Add(labelText);
                body.Children.Add(Text(report.NotesExcerpt, 12));
            }
            if (report.ScreenshotPaths.Count > 0)
            {
                var shotsLabel = Text(_s["screenshots"], 11, secondary: true);
                shotsLabel.Margin = new Thickness(0, 8, 0, 4);
                body.Children.Add(shotsLabel);
                var shots = new WrapPanel();
                foreach (var path in report.ScreenshotPaths) shots.Children.Add(Thumbnail(path, report));
                body.Children.Add(shots);
            }
            var links = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            if (report.NotesPath != null)
            {
                var notes = Link(_s["openNotes"], () => OpenNotes(report));
                AutomationProperties.SetAutomationId(notes, "lnkReportNotes_" + report.ReportCreated);
                links.Children.Add(notes);
            }
            var add = Link(_s["addInstructions"], () => OpenInstructions(report));
            AutomationProperties.SetAutomationId(add, "lnkReportAddInstructions_" + report.ReportCreated);
            links.Children.Add(add);
            if (report.LatestInstructions != null)
            {
                var caption = Text(_s["yourInstructions"], 11, secondary: true);
                caption.Margin = new Thickness(0, 8, 0, 2);
                body.Children.Add(caption);
                body.Children.Add(Text(report.LatestInstructions, 12));
            }
            body.Children.Add(links);
            if (_rowMessages.TryGetValue(report.Id, out var message)) body.Children.Add(Text(message, 11, secondary: true));

            var card = new Border { Child = body, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetAutomationId(card, "rowLocalReport_" + report.ReportCreated);
            AutomationProperties.SetName(card, report.Title);
            return card;
        }

        private FrameworkElement Thumbnail(string path, LocalReport report)
        {
            var image = new Image { Width = 96, Height = 64, Stretch = Stretch.UniformToFill };
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 192;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }
            catch (Exception) { }
            var frame = new Border { Child = image, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), ClipToBounds = true };
            frame.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            var button = new Button { Content = frame, Template = PlainTemplate(), Cursor = Cursors.Hand, ToolTip = Path.GetFileName(path), Focusable = false };
            button.Click += (s, e) =>
            {
                if (!LocalReportReader.MayOpenScreenshot(path, report, _account())) { RowMessage(report, _s["screenshotOpenFailed"]); return; }
                ShowLightbox(path);
            };
            return button;
        }

        // ------------------------------------------------------------------ actions

        private async void SetConfirmed(LocalReport report, bool confirmed)
        {
            if (_pending.Contains(report.Id)) return;
            _pending.Add(report.Id);
            _rowMessages.Remove(report.Id);
            Draw();
            string account = _account();
            try { await Task.Run(() => LocalReportConfirmationWriter.SetConfirmedFixed(confirmed, report, _root, account, DateTime.UtcNow)); }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LocalReportsView confirm ex - " + ex.Message);
                _rowMessages[report.Id] = _s["confirmationFailed"];
            }
            _pending.Remove(report.Id);
            Refresh();
        }

        private void RowMessage(LocalReport report, string message)
        {
            _rowMessages[report.Id] = message;
            Draw();
        }

        private Window Dialog(string title, double width)
        {
            var window = new Window
            {
                Title = title, Width = width, SizeToContent = SizeToContent.Height, MaxHeight = SystemParameters.WorkArea.Height * 0.9,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this), ShowInTaskbar = false, ResizeMode = ResizeMode.CanResize,
            };
            window.SetResourceReference(Control.BackgroundProperty, "BackgroundColor");
            Focus_Bear.Utils.WindowTheme.FollowTheme(window);
            return window;
        }

        private void OpenInstructions(LocalReport report)
        {
            var window = Dialog(_s["addInstructions"], 540);
            AutomationProperties.SetAutomationId(window, "LocalReportInstructions");
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(Text(_s["addInstructions"], 16, FontWeights.SemiBold));
            var subtitle = Text(report.Title.Length == 0 ? _s["untitled"] : report.Title, 12, secondary: true);
            subtitle.Margin = new Thickness(0, 4, 0, 10);
            stack.Children.Add(subtitle);
            // Not the app's rounded style: its template centres the text vertically, which is wrong for a
            // box you write paragraphs in.
            var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                    MaxLength = LocalReportInstructionWriter.MaximumCharacters, Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(1),
                                    VerticalContentAlignment = VerticalAlignment.Top };
            box.SetResourceReference(Control.BackgroundProperty, "TextBoxBackgroundColor");
            box.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            box.SetResourceReference(Control.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetAutomationId(box, "txtLocalReportInstructions");
            stack.Children.Add(WithPlaceholder(box, _s["instructionsPrompt"]));
            var hint = Text(_s["instructionsLimit"] + "  " + _s["pasteImageHint"], 11, secondary: true);
            hint.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(hint);
            var error = Text("", 11);
            error.Foreground = Danger;
            stack.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var paste = SmallButton(_s["pasteImage"], (s, e) =>
            {
                // The box's MaxLength stops typing, not what the app adds: check before the image is copied in.
                if (!LocalReportInstructionWriter.ReferencesFit(box.Text, 1, DateTime.Now)) { error.Text = _s["instructionsFull"]; return; }
                string reference = PasteImage(report);
                if (reference == null) { error.Text = _s["pasteImageFailed"]; return; }
                string combined = LocalReportInstructionWriter.WithReferences(box.Text, new[] { reference });
                if (combined == null) { error.Text = _s["instructionsFull"]; return; }
                error.Text = "";
                box.Text = combined;
            });
            AutomationProperties.SetAutomationId(paste, "btnLocalReportPaste");
            // Files too, not just the clipboard (report 2026-09-25_22-01-09): a picker, and dropping files on the box.
            Action<IEnumerable<string>> attachFiles = files =>
            {
                var picked = (files ?? Enumerable.Empty<string>()).ToList();
                if (!LocalReportInstructionWriter.ReferencesFit(box.Text, picked.Count, DateTime.Now)) { error.Text = _s["instructionsFull"]; return; }
                var added = new List<string>();
                foreach (string file in picked)
                {
                    string reference = AttachImageFile(report, file);
                    if (reference != null) added.Add(reference);
                }
                if (added.Count == 0) { error.Text = _s["pasteImageFailed"]; return; }
                string combined = LocalReportInstructionWriter.WithReferences(box.Text, added);
                if (combined == null) { error.Text = _s["instructionsFull"]; return; }
                error.Text = "";
                box.Text = combined;
            };
            var choose = SmallButton(_s["chooseImage"], (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = _s["chooseImageTitle"],
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*",
                    Multiselect = true,
                };
                if (dialog.ShowDialog(window) == true) attachFiles(dialog.FileNames);
            });
            AutomationProperties.SetAutomationId(choose, "btnLocalReportChooseImage");
            buttons.Children.Add(choose);
            buttons.Children.Add(paste);
            box.AllowDrop = true;
            box.PreviewDragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; }
            };
            box.PreviewDrop += (s, e) =>
            {
                if (!(e.Data.GetData(DataFormats.FileDrop) is string[] dropped)) return;
                e.Handled = true;
                attachFiles(dropped);
            };
            buttons.Children.Add(SmallButton(_s["cancel"], (s, e) => window.Close()));
            var save = SmallButton(_s["saveInstructions"], async (s, e) =>
            {
                string text = box.Text;
                string account = _account();
                try
                {
                    await Task.Run(() => LocalReportInstructionWriter.Append(text, report, _root, account, DateTime.UtcNow));
                    _rowMessages[report.Id] = _s["instructionsSaved"];
                    window.Close();
                    Refresh();
                }
                catch (Exception ex)
                {
                    LogWriter.LogWrite("LocalReportsView instructions ex - " + ex.Message);
                    error.Text = _s["instructionsFailed"];
                }
            }, primary: true);
            AutomationProperties.SetAutomationId(save, "btnLocalReportSaveInstructions");
            buttons.Children.Add(save);
            stack.Children.Add(buttons);
            // Ctrl+V with an image on the clipboard attaches it, as the Mac's editor does.
            box.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Clipboard.ContainsImage())
                {
                    e.Handled = true;
                    paste.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            };
            window.Content = stack;
            window.Loaded += (s, e) => box.Focus();
            window.ShowDialog();
        }

        /// <summary>An image file, re-encoded as PNG into the report's screenshots folder; the reference line, or null.</summary>
        private string AttachImageFile(LocalReport report, string path)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    string name = LocalReportImageWriter.Save(stream.ToArray(), report, _root, _account(), DateTime.Now);
                    return LocalReportImageWriter.Reference(name);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LocalReportsView attach file ex - " + ex.Message);
                return null;
            }
        }

        /// <summary>The clipboard image into the report's screenshots folder; the reference line, or null.</summary>
        private string PasteImage(LocalReport report)
        {
            try
            {
                if (!Clipboard.ContainsImage()) return null;
                var image = Clipboard.GetImage();
                if (image == null) return null;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    string name = LocalReportImageWriter.Save(stream.ToArray(), report, _root, _account(), DateTime.Now);
                    return LocalReportImageWriter.Reference(name);
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("LocalReportsView paste ex - " + ex.Message);
                return null;
            }
        }

        /// <summary>The notes as the Mac shows them: the newest update first, the original diagnosis below it.</summary>
        private void OpenNotes(LocalReport report)
        {
            string text = LocalReportReader.ReadNotes(report, _account());
            if (text == null) { RowMessage(report, _s["notesReadFailed"]); return; }
            var notes = LocalReportNoteParser.Parse(text);
            var window = Dialog(notes.Title.Length > 0 ? notes.Title : _s["openNotes"], 640);
            AutomationProperties.SetAutomationId(window, "LocalReportNotes");
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(Text(notes.Title.Length > 0 ? notes.Title : (report.Title.Length == 0 ? _s["untitled"] : report.Title), 16, FontWeights.SemiBold));
            if (notes.Updates.Count > 0)
            {
                var order = Text(_s["noteNewestFirst"], 11, secondary: true);
                order.Margin = new Thickness(0, 4, 0, 8);
                stack.Children.Add(order);
            }
            foreach (var update in notes.NewestFirst)
            {
                string heading = _s["noteUpdateHeading"].Replace("<NUMBER>", update.Number.ToString(CultureInfo.InvariantCulture)).Replace("<COUNT>", notes.Updates.Count.ToString(CultureInfo.InvariantCulture));
                if (update == notes.Latest) heading += " · " + _s["noteLatest"];
                if (update.Date.HasValue) heading += " · " + update.Date.Value.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture);
                stack.Children.Add(NoteCard(heading, update.Summary, update.Body, update == notes.Latest));
            }
            stack.Children.Add(NoteCard(_s["noteOriginal"], "", notes.Original, notes.Updates.Count == 0));
            var close = SmallButton(_s["close"], (s, e) => window.Close(), primary: true);
            AutomationProperties.SetAutomationId(close, "btnLocalReportNotesClose");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(close);
            window.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            window.ShowDialog();
        }

        private FrameworkElement NoteCard(string heading, string summary, string body, bool highlight)
        {
            var stack = new StackPanel();
            var head = Text(heading, 12, FontWeights.SemiBold);
            if (highlight) head.Foreground = Accent;
            stack.Children.Add(head);
            if (!string.IsNullOrEmpty(summary)) stack.Children.Add(Text(summary, 12, FontWeights.Medium));
            var text = new TextBox { Text = body, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 12, Margin = new Thickness(0, 6, 0, 0) };
            text.SetResourceReference(Control.ForegroundProperty, "ForegroundColor");
            stack.Children.Add(text);
            var card = new Border { Child = stack, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 10) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            if (highlight) card.BorderBrush = Accent;
            else card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            return card;
        }

        private void ShowLightbox(string path)
        {
            var window = Dialog(Path.GetFileName(path), 900);
            window.SizeToContent = SizeToContent.Manual;
            window.Height = 680;
            var dock = new DockPanel { Margin = new Thickness(12) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            buttons.Children.Add(SmallButton(_s["openInPreview"], (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { LogWriter.LogWrite("LocalReportsView open ex - " + ex.Message); }
            }));
            buttons.Children.Add(SmallButton(_s["close"], (s, e) => window.Close(), primary: true));
            DockPanel.SetDock(buttons, Dock.Bottom);
            dock.Children.Add(buttons);
            var image = new Image { Stretch = Stretch.Uniform };
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                image.Source = bitmap;
            }
            catch (Exception) { }
            dock.Children.Add(image);
            window.Content = dock;
            window.KeyDown += (s, e) => { if (e.Key == Key.Escape) window.Close(); };
            window.ShowDialog();
        }

        // ------------------------------------------------------------------ small parts

        private static ControlTemplate _plain;

        private static ControlTemplate PlainTemplate()
        {
            if (_plain != null) return _plain;
            var template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
            template.Seal();
            _plain = template;
            return template;
        }

        internal static TextBlock Text(string text, double size, FontWeight? weight = null, bool secondary = false)
        {
            var block = new TextBlock { Text = text ?? "", FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TraySubtitleColor" : "ForegroundColor");
            return block;
        }

        private static Button Link(string text, Action onClick, bool enabled = true)
        {
            var label = new TextBlock { Text = text, FontSize = 12, Foreground = Accent, TextDecorations = TextDecorations.Underline };
            var button = new Button { Content = label, Template = PlainTemplate(), Cursor = Cursors.Hand, IsEnabled = enabled, Margin = new Thickness(0, 4, 16, 0), HorizontalAlignment = HorizontalAlignment.Left, Focusable = false };
            if (!enabled) button.Opacity = 0.6;
            button.Click += (s, e) => onClick();
            return button;
        }

        internal static Button SmallButton(string text, RoutedEventHandler onClick, bool primary = false)
        {
            var button = new Button
            {
                // The app's orange button faces are light in both themes, so their label is always dark
                // (ButtonTextBlockStyle); inheriting the page's colour made it unreadable in dark mode.
                Content = new TextBlock { Text = text, Margin = new Thickness(14, 0, 14, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Black },
                Height = 28, Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0),
            };
            button.SetResourceReference(StyleProperty, primary ? "BtnStyleLightOrangeBordered" : "BtnStyleClearOrangeBordered");
            if (onClick != null) button.Click += onClick;
            return button;
        }

        private static Button Pill(string text, bool selected, Action onClick, string automationId = null)
        {
            var label = new TextBlock { Text = text, FontSize = 12, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal };
            if (selected) label.Foreground = Brushes.White;
            else label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var border = new Border { Child = label, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 3), BorderThickness = new Thickness(1) };
            if (selected) { border.Background = Accent; border.BorderBrush = Accent; }
            else { border.SetResourceReference(Border.BackgroundProperty, "TrayCardBg"); border.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder"); }
            var button = new Button { Content = border, Template = PlainTemplate(), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 6, 6), Focusable = false };
            button.Click += (s, e) => onClick();
            if (automationId != null) AutomationProperties.SetAutomationId(button, automationId);
            return button;
        }

        private static Grid WithPlaceholder(TextBox box, string placeholder)
        {
            var hint = new TextBlock { Text = placeholder, FontSize = 13, IsHitTestVisible = false, Opacity = 0.7, Margin = box.AcceptsReturn ? new Thickness(11, 7, 0, 0) : new Thickness(13, 0, 0, 0), VerticalAlignment = box.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "TraySubtitleColor");
            box.FontSize = 13;
            box.SetResourceReference(TextBoxBase.CaretBrushProperty, "ForegroundColor");
            Action sync = () => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => sync();
            sync();
            var grid = new Grid();
            grid.Children.Add(box);
            grid.Children.Add(hint);
            return grid;
        }
    }
}
