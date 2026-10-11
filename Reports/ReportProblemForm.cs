using Focus_Bear.Utils;
using FocusBear.Core;
using FocusBear.Core.Reports;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Focus_Bear.Reports
{
    /// <summary>
    /// The Report Problem tab as one form (the Mac's <c>ReportProblemForm</c>): what happened, screenshots
    /// (drag, paste or click to choose), and a single Send. For the tester account it also picks where the
    /// report goes — Focus Bear support (the existing log upload) or a local Claude task — and lists the
    /// local reports. Screenshots stay in <c>AppSettings.droppedFiles</c>, which the upload reads.
    /// </summary>
    internal sealed class ReportProblemForm : UserControl
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
        private static readonly SolidColorBrush Accent = new SolidColorBrush(Color.FromRgb(0xDE, 0x74, 0x00));

        private readonly AppSettings _settings = AppSettings.GetInstance;
        private readonly Func<Task<bool>> _upload;
        private readonly TextBox _feedback = new TextBox();
        private readonly WrapPanel _thumbnails = new WrapPanel();
        private readonly TextBlock _dropHint;
        private readonly Border _dropZone;
        private readonly StackPanel _destination = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        private readonly RadioButton _toSupport, _toLocal;
        private readonly Button _send;
        private readonly TextBlock _sendLabel;
        private readonly TextBlock _status;
        private readonly ContentControl _localList = new ContentControl();
        private LocalReportsView _localReports;
        private readonly StackPanel _testsPaused = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        private bool _sending;

        /// <param name="upload">The support upload of the note + screenshots + logs; true when it went through.</param>
        internal ReportProblemForm(Func<string, Task<bool>> upload)
        {
            _upload = () => upload(_feedback.Text);
            Resources[typeof(Grid)] = new Style(typeof(Grid));   // the app's page-background Grid style would show inside the cards
            var root = new StackPanel { Margin = new Thickness(24, 20, 24, 24) };
            root.Children.Add(Text(S("titleReportForm", "Report a problem or share an idea"), 18, FontWeights.SemiBold));
            var subtitle = Text(S("subtitleReportForm", "Screenshots and your logs help us understand what happened."), 13, FontWeights.Normal, secondary: true);
            subtitle.Margin = new Thickness(0, 4, 0, 14);
            root.Children.Add(subtitle);

            // What happened?
            _feedback.AcceptsReturn = true;
            _feedback.TextWrapping = TextWrapping.Wrap;
            _feedback.MinHeight = 110;
            _feedback.FontSize = 14;
            _feedback.Padding = new Thickness(8);
            _feedback.VerticalContentAlignment = VerticalAlignment.Top;
            _feedback.SetResourceReference(TextBox.BackgroundProperty, "TextBoxBackgroundColor");
            _feedback.SetResourceReference(TextBox.ForegroundProperty, "ForegroundColor");
            _feedback.SetResourceReference(TextBox.CaretBrushProperty, "ForegroundColor");
            _feedback.SetResourceReference(TextBox.BorderBrushProperty, "TrayCardBorder");
            _feedback.TextChanged += (s, e) => Refresh();
            AutomationProperties.SetAutomationId(_feedback, "txtBoxFeedback");
            AutomationProperties.SetName(_feedback, S("whatHappened", "What happened?"));
            var hint = Text(S("whatHappened", "What happened?"), 14, FontWeights.Normal, secondary: true);
            hint.Margin = new Thickness(11, 9, 0, 0);
            hint.IsHitTestVisible = false;
            _feedback.TextChanged += (s, e) => hint.Visibility = _feedback.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var feedbackWell = new Grid();
            feedbackWell.Children.Add(_feedback);
            feedbackWell.Children.Add(hint);
            root.Children.Add(feedbackWell);

            // Screenshots: thumbnails, then a dashed drop zone that also opens the picker; Ctrl+V pastes.
            _thumbnails.Margin = new Thickness(0, 12, 0, 0);
            root.Children.Add(_thumbnails);
            _dropHint = Text("➕  " + S("dragPasteOrChoose", "Drag, paste, or click to choose"), 13, FontWeights.Normal, secondary: true);
            _dropHint.HorizontalAlignment = HorizontalAlignment.Center;
            _dropHint.VerticalAlignment = VerticalAlignment.Center;
            var dashed = new Rectangle { StrokeDashArray = new DoubleCollection { 4, 3 }, StrokeThickness = 1, RadiusX = 8, RadiusY = 8 };
            dashed.SetResourceReference(Shape.StrokeProperty, "TraySubtitleColor");
            var zone = new Grid { MinHeight = 60 };
            zone.Children.Add(dashed);
            zone.Children.Add(_dropHint);
            _dropZone = new Border { Child = zone, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand, AllowDrop = true, Background = new SolidColorBrush(Color.FromArgb(0x0F, 0x80, 0x80, 0x80)), CornerRadius = new CornerRadius(8) };
            _dropZone.MouseLeftButtonUp += (s, e) => ChooseScreenshots();
            _dropZone.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            _dropZone.DragEnter += (s, e) => _dropZone.Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xDE, 0x74, 0x00));
            _dropZone.DragLeave += (s, e) => _dropZone.Background = new SolidColorBrush(Color.FromArgb(0x0F, 0x80, 0x80, 0x80));
            _dropZone.Drop += (s, e) => { _dropZone.Background = new SolidColorBrush(Color.FromArgb(0x0F, 0x80, 0x80, 0x80)); AddFrom(e.Data); };
            AutomationProperties.SetAutomationId(_dropZone, "zoneScreenshots");
            root.Children.Add(_dropZone);
            var paste = new Button { Content = Text("📋  " + S("pasteFromClipboard", "Paste from clipboard"), 12, FontWeights.Normal), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(0) };
            ((TextBlock)paste.Content).Foreground = Accent;
            paste.Click += (s, e) => AddFrom(Clipboard.GetDataObject());
            AutomationProperties.SetAutomationId(paste, "btnPasteScreenshot");
            root.Children.Add(paste);
            PreviewKeyDown += (s, e) =>
            {
                // Ctrl+V anywhere but the text box pastes screenshots (in the text box it pastes text, unless it's an image).
                if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && (!_feedback.IsKeyboardFocusWithin || !Clipboard.ContainsText()))
                {
                    if (AddFrom(Clipboard.GetDataObject())) e.Handled = true;
                }
            };

            // Where it goes (tester only).
            _toSupport = Radio(S("sendToSupport", "Focus Bear support"), "rbSendToSupport", true);
            _toLocal = Radio(S("sendToLocalClaude", "Local Claude task"), "rbSendToLocal", false);
            var sendTo = Text(S("sendTo", "Send to:") + "  ", 13, FontWeights.Normal);
            sendTo.VerticalAlignment = VerticalAlignment.Center;
            _destination.Children.Add(sendTo);
            _destination.Children.Add(_toSupport);
            _destination.Children.Add(_toLocal);
            root.Children.Add(_destination);

            // Send.
            var sendRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            _sendLabel = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(26, 9, 26, 9) };
            var face = new Border { Child = _sendLabel, Background = Accent, CornerRadius = new CornerRadius(8) };
            _send = new Button { Content = face, Cursor = Cursors.Hand, Template = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) } };
            _send.IsEnabledChanged += (s, e) => face.Opacity = _send.IsEnabled ? 1 : 0.45;
            _send.Click += async (s, e) => await Send();
            AutomationProperties.SetAutomationId(_send, "btnSendReport");
            sendRow.Children.Add(_send);
            _status = Text("", 13, FontWeights.Normal, secondary: true);
            _status.VerticalAlignment = VerticalAlignment.Center;
            _status.Margin = new Thickness(12, 0, 0, 0);
            AutomationProperties.SetAutomationId(_status, "txtReportStatus");
            sendRow.Children.Add(_status);
            root.Children.Add(sendRow);

            // Tester only, while the E2E UI tests on this PC are paused (Ctrl+Alt+Shift+P): start them again.
            var pausedNote = Text(S("uiTestsPaused", "UI tests are paused.") + "  ", 13, FontWeights.Normal, secondary: true);
            pausedNote.VerticalAlignment = VerticalAlignment.Center;
            _testsPaused.Children.Add(pausedNote);
            var resume = new Button { Content = S("resumeUiTests", "Resume UI tests"), Padding = new Thickness(12, 4, 12, 4), Cursor = Cursors.Hand };
            resume.Click += (s, e) => ResumeUiTests();
            AutomationProperties.SetAutomationId(resume, "btnResumeUiTests");
            _testsPaused.Children.Add(resume);
            root.Children.Add(_testsPaused);

            _localList.Margin = new Thickness(0, 18, 0, 0);
            root.Children.Add(_localList);

            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SetResourceReference(BackgroundProperty, "SoftCreamColor");
            // Built with the main window, before the account has loaded — decide the tester extras when shown.
            IsVisibleChanged += (s, e) => { if (IsVisible) Refresh(); };
            Refresh();
        }

        private bool IsTester => LocalReportCapability.IsEnabled(_settings.userAccount?.userID);

        private string S(string key, string fallback) => _settings.GetConfigStringSafe(fallback, "helpVcInfo", key);

        private void Refresh()
        {
            bool tester = IsTester;
            _destination.Visibility = tester ? Visibility.Visible : Visibility.Collapsed;
            if (tester && _localReports == null)
            {
                _localReports = new LocalReportsView(() => AppSettings.GetInstance.userAccount?.userID);
                _localList.Content = _localReports;
            }
            _localList.Visibility = tester ? Visibility.Visible : Visibility.Collapsed;
            _testsPaused.Visibility = tester && !AppInstance.IsE2E && E2ETestPause.IsPaused(LocalReportFolders.DefaultRoot) ? Visibility.Visible : Visibility.Collapsed;
            _sendLabel.Text = _sending ? S("sending", "Sending…") : S("send", "Send");
            _send.IsEnabled = !_sending && LocalReportTaskFile.HasContent(_feedback.Text, _settings.droppedFiles.Select(f => f.FilePath));
            RenderThumbnails();
        }

        private void RenderThumbnails()
        {
            _thumbnails.Children.Clear();
            foreach (var file in _settings.droppedFiles.ToList())
            {
                var captured = file;
                var image = new Image { Source = file.Thumbnail, Width = 64, Height = 64, Stretch = Stretch.UniformToFill };
                var frame = new Border { Child = image, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), ClipToBounds = true };
                frame.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
                var remove = new Button { Content = "✕", FontSize = 9, Width = 18, Height = 18, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand, Margin = new Thickness(0, -6, -6, 0), ToolTip = S("removeScreenshot", "Remove") };
                remove.Click += (s, e) => { _settings.droppedFiles.Remove(captured); Refresh(); };
                AutomationProperties.SetAutomationId(remove, "btnRemoveScreenshot");
                var cell = new Grid { Margin = new Thickness(0, 0, 10, 6), ToolTip = System.IO.Path.GetFileName(file.FilePath) };
                cell.Children.Add(frame);
                cell.Children.Add(remove);
                _thumbnails.Children.Add(cell);
            }
            _thumbnails.Visibility = _thumbnails.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChooseScreenshots()
        {
            var dialog = new OpenFileDialog
            {
                Title = S("buttonSelectScreenshots", "Select Screenshots"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*",
                Multiselect = true,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true) Add(dialog.FileNames);
        }

        /// <summary>Image files, or raw image data (a screenshot on the clipboard) saved as a temp PNG.</summary>
        private bool AddFrom(IDataObject data)
        {
            if (data == null) return false;
            try
            {
                if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    var images = files.Where(f => ImageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())).ToList();
                    if (images.Count > 0) { Add(images); return true; }
                }
                if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap)
                {
                    string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pasted-screenshot-" + Guid.NewGuid().ToString("N") + ".png");
                    using (var stream = File.Create(path))
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(stream);
                    }
                    Add(new[] { path });
                    return true;
                }
            }
            catch (Exception ex) { LogWriter.LogWrite("ReportProblemForm add screenshots - " + ex.Message); }
            return false;
        }

        private void Add(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (_settings.droppedFiles.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;
                try { _settings.droppedFiles.Add(new DroppedFile(path)); }
                catch (Exception ex) { LogWriter.LogWrite("ReportProblemForm thumbnail - " + ex.Message); }
            }
            Refresh();
        }

        private async Task Send()
        {
            if (_sending || !LocalReportTaskFile.HasContent(_feedback.Text, _settings.droppedFiles.Select(f => f.FilePath))) return;
            _sending = true;
            _status.Text = "";
            Refresh();
            bool ok;
            if (IsTester && _toLocal.IsChecked == true)
            {
                LogWriter.Flush();
                string folder = LocalReportTaskFile.Write(LocalReportFolders.DefaultRoot, _feedback.Text, _settings.droppedFiles.Select(f => f.FilePath).ToList(),
                    AppSettings.AppDataDirLogsPath, _settings.AppVersion?.ToString() ?? "", _settings.userAccount?.userID, DateTime.Now);
                ok = folder != null;
                if (ok) LogWriter.LogWrite("ReportProblemForm - local report written to " + folder);
            }
            else ok = await _upload();
            _sending = false;
            _status.Text = ok ? S("sent", "Sent ✓") : S("sendFailed", "Failed — please try again");
            if (ok)
            {
                // Only a report that went through clears the form; a failure keeps everything for a retry.
                _feedback.Text = "";
                _settings.droppedFiles.Clear();
                _localReports?.Refresh();
            }
            Refresh();
        }

        /// <summary>The live app has to be closed for the tests to drive the E2E build, so this quits it.</summary>
        private void ResumeUiTests()
        {
            var answer = MessageBox.Show(Window.GetWindow(this), S("resumeUiTestsConfirm", "Resume the UI tests? Focus Bear will close so the tests can run."),
                "Focus Bear", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;
            if (!E2ETestPause.RequestResume(LocalReportFolders.DefaultRoot, DateTime.Now))
            {
                _status.Text = S("resumeUiTestsFailed", "Couldn't resume the UI tests");
                return;
            }
            LogWriter.LogWrite("ReportProblemForm - UI tests resumed, quitting so they can run");
            Application.Current.Shutdown(0);
        }

        private RadioButton Radio(string text, string id, bool isChecked)
        {
            var radio = new RadioButton { Content = text, GroupName = "ReportDestination", IsChecked = isChecked, Margin = new Thickness(0, 0, 18, 0), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13 };
            radio.SetResourceReference(ForegroundProperty, "ForegroundColor");
            AutomationProperties.SetAutomationId(radio, id);
            return radio;
        }

        private static TextBlock Text(string text, double size, FontWeight weight, bool secondary = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TraySubtitleColor" : "ForegroundColor");
            return block;
        }
    }
}
