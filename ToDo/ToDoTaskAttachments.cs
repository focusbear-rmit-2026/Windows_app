using Focus_Bear.StatsScreens;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Focus_Bear.ToDoScreens.ToDoUi;

namespace Focus_Bear.ToDoScreens
{
    /// <summary>
    /// A task's attachments, in the editor of an existing task — the Mac's <c>ToDoAttachmentsStore</c>:
    /// the list (opened in the browser through the server's short-lived download URL), "Add file…", and delete.
    /// </summary>
    internal sealed class ToDoTaskAttachments : UserControl
    {
        private readonly string _taskId;
        private readonly ToDoStrings _s;
        private readonly StackPanel _list = new StackPanel();
        private readonly TextBlock _status;
        private readonly Button _add;
        private IList<ToDoAttachment> _attachments = new List<ToDoAttachment>();
        private bool _uploading;
        private readonly bool _isDraft;
        internal ToDoDraftAttachments Draft { get; } = new ToDoDraftAttachments();
        internal bool HasPendingFiles => Draft.Files.Count > 0;
        private const string ClipboardImageName = "Screenshot.png";
        private const string ClipboardImageType = "image/png";

        private static API.FocusBearAPIClient Api() => new API.FocusBearAPIClient();

        internal ToDoTaskAttachments(string taskId, ToDoStrings strings, bool isDraft = false)
        {
            _taskId = taskId;
            _isDraft = isDraft;
            _s = strings;
            var stack = new StackPanel();
            var header = new DockPanel { Margin = new Thickness(0, 16, 0, 6) };
            _add = SmallButton(_s.AddAttachment, async (o, e) => await PickAndUploadAsync(), glyph: Glyph(0xE723));
            _add.Margin = new Thickness(0);
            AutomationProperties.SetAutomationId(_add, "btnToDoAttachmentAdd");
            DockPanel.SetDock(_add, Dock.Right);
            header.Children.Add(_add);
            header.Children.Add(StatsUi.Text(_s.Attachments, 12, FontWeights.SemiBold, wrap: false));
            stack.Children.Add(header);
            stack.Children.Add(_list);
            _status = StatsUi.Text("", 11, secondary: true);
            AutomationProperties.SetAutomationId(_status, "txtToDoAttachmentStatus");
            stack.Children.Add(_status);
            Content = stack;
            Draw();
            Loaded += async (o, e) => { if (!_isDraft) await LoadAsync(); };
        }

        private async Task LoadAsync()
        {
            var list = await Api().GetToDoAttachments(_taskId);
            if (list != null) _attachments = list;
            // A list that never arrived is not "No attachments yet".
            else _status.Text = _s.AttachmentsLoadFailed;
            Draw();
        }

        private void Draw()
        {
            _list.Children.Clear();
            _add.IsEnabled = !_uploading;
            foreach (var file in Draft.Files)
            {
                var pending = new DockPanel();
                var remove = SmallButton(_s.DeleteAttachment, (o, e) => { Draft.Remove(file); Draw(); });
                remove.IsEnabled = !_uploading;
                DockPanel.SetDock(remove, Dock.Right);
                pending.Children.Add(remove);
                pending.Children.Add(StatsUi.Text(file.FileName, 12));
                _list.Children.Add(pending);
            }
            if (_attachments.Count == 0 && !HasPendingFiles)
            {
                _list.Children.Add(StatsUi.Text(_s.NoAttachmentsYet, 12, secondary: true));
                return;
            }
            foreach (var attachment in _attachments) _list.Children.Add(Row(attachment));
        }

        private FrameworkElement Row(ToDoAttachment attachment)
        {
            var row = new DockPanel { LastChildFill = true };
            var delete = new Button { Content = Glyph(0xE74D), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = _s.DeleteAttachment, Margin = new Thickness(8, 0, 0, 0), Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            delete.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
            delete.Click += async (o, e) => await DeleteAsync(attachment);
            AutomationProperties.SetAutomationId(delete, "btnToDoAttachmentDelete_" + attachment.Id);
            DockPanel.SetDock(delete, Dock.Right);
            row.Children.Add(delete);
            var size = StatsUi.Text(ToDoAttachments.SizeLabel(attachment.FileSize), 11, secondary: true, wrap: false);
            size.Margin = new Thickness(8, 0, 0, 0);
            size.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(size, Dock.Right);
            row.Children.Add(size);
            var icon = StatsUi.Text(Glyph(attachment.IsImage ? 0xEB9F : 0xE8A5), 14, secondary: true, wrap: false);
            icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            icon.Margin = new Thickness(0, 0, 8, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            var name = StatsUi.Text(attachment.FileName, 12, wrap: false);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name);

            var open = new Button { Content = row, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            open.Click += (o, e) => Open(attachment);
            var card = new Border { Child = open, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetName(card, attachment.FileName);
            AutomationProperties.SetAutomationId(open, "btnToDoAttachmentOpen_" + attachment.Id);
            return card;
        }

        private void Open(ToDoAttachment attachment)
        {
            if (!Uri.TryCreate(attachment.DownloadUrl ?? "", UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) { LogWriter.LogWrite("Open attachment - " + ex.Message); }
        }

        private async Task PickAndUploadAsync()
        {
            if (_uploading) return;
            var patterns = string.Join(";", ToDoAttachments.PickableExtensions.Select(x => "*." + x));
            var dialog = new OpenFileDialog { Filter = _s.Attachments + " (" + patterns + ")|" + patterns + "|*.*|*.*", Multiselect = true };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            foreach (var path in dialog.FileNames) await UploadAsync(path);
        }

        /// <summary>Also the harness's entry point, so a test upload needs no file dialog.</summary>
        internal async Task UploadAsync(string path)
        {
            long? length = null;
            try { length = new FileInfo(path).Length; } catch (Exception ex) { LogWriter.LogWrite("Attachment size - " + ex.Message); }
            switch (ToDoAttachments.Evaluate(!string.IsNullOrEmpty(_taskId), _uploading, _attachments.Count + Draft.ConfirmedUploadCount + Draft.Files.Count, length))
            {
                case ToDoAttachmentUploadCheck.Unavailable: return;
                case ToDoAttachmentUploadCheck.LimitReached: _status.Text = _s.AttachmentLimitReached; return;
                case ToDoAttachmentUploadCheck.TooLarge: _status.Text = _s.AttachmentTooLarge; return;
                case ToDoAttachmentUploadCheck.Unreadable: _status.Text = _s.AttachmentUnreadable; return;
                case ToDoAttachmentUploadCheck.Empty: _status.Text = _s.AttachmentEmpty; return;
            }
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception ex) { LogWriter.LogWrite("Attachment read - " + ex.Message); _status.Text = _s.AttachmentUnreadable; return; }

            await AddOrUploadAsync(new ToDoDraftAttachment(Path.GetFileName(path),
                ToDoAttachments.ContentType(Path.GetExtension(path)), bytes));
        }

        private async Task AddOrUploadAsync(ToDoDraftAttachment file)
        {
            if (_isDraft)
            {
                var check = Draft.Add(file, _attachments.Count);
                _status.Text = check == ToDoAttachmentUploadCheck.Ready ? _s.AttachmentPending : _s.AttachmentLimitReached;
                Draw();
                return;
            }
            _uploading = true;
            _status.Text = _s.AttachmentUploading;
            Draw();
            try
            {
                var created = await Api().UploadToDoAttachment(_taskId, file);
                _status.Text = created == null ? _s.AttachmentFailed : "";
                await LoadAsync();
            }
            finally { _uploading = false; Draw(); }
        }

        /// <summary>
        /// Whether Ctrl+V should become an attachment. Excel and Word put a bitmap on the clipboard beside the
        /// text they copy, so a focused text box keeps any paste that carries text; files copied in Explorer
        /// carry no text and still attach. Another process can hold the clipboard open, so a failed read
        /// leaves the paste to the control.
        /// </summary>
        internal static bool ClaimsPaste(IInputElement focused)
        {
            try
            {
                if (!Clipboard.ContainsImage() && !Clipboard.ContainsFileDropList()) return false;
                return !(focused is System.Windows.Controls.Primitives.TextBoxBase && Clipboard.ContainsText());
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Attachment paste check - " + ex.Message);
                return false;
            }
        }

        internal async Task<bool> PasteAsync()
        {
            if (_uploading || Draft.IsSaving) return false;
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    foreach (string path in Clipboard.GetFileDropList()) await UploadAsync(path);
                    return true;
                }
                if (!Clipboard.ContainsImage()) return false;
                var image = Clipboard.GetImage();
                if (image == null) return false;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    var check = ToDoAttachments.Evaluate(true, false, _attachments.Count + Draft.ConfirmedUploadCount + Draft.Files.Count, stream.Length);
                    if (check != ToDoAttachmentUploadCheck.Ready)
                    {
                        _status.Text = check == ToDoAttachmentUploadCheck.TooLarge ? _s.AttachmentTooLarge : _s.AttachmentLimitReached;
                        return true;
                    }
                    await AddOrUploadAsync(new ToDoDraftAttachment(ClipboardImageName, ClipboardImageType, stream.ToArray()));
                }
                return true;
            }
            catch (Exception ex)
            {
                LogWriter.LogWrite("Attachment paste - " + ex.Message);
                _status.Text = _s.AttachmentUnreadable;
                return true;
            }
        }

        internal async Task<bool> SaveDraftAsync(Func<Task<bool>> saveTask)
        {
            _uploading = true;
            Draw();
            try
            {
                var saved = await Draft.SaveAsync(saveTask, async file => await Api().UploadToDoAttachment(_taskId, file) != null);
                if (!saved && Draft.HasSavedTask)
                    _status.Text = Draft.Files.Any(file => file.RegistrationUnconfirmed) ? _s.AttachmentUnconfirmed : _s.AttachmentRetry;
                return saved;
            }
            finally { _uploading = false; Draw(); }
        }

        private async Task DeleteAsync(ToDoAttachment attachment)
        {
            var before = _attachments;
            _attachments = _attachments.Where(a => a.Id != attachment.Id).ToList();
            Draw();
            if (await Api().DeleteToDoAttachment(_taskId, attachment.Id))
            {
                _status.Text = "";
                return;
            }
            _attachments = before;
            _status.Text = _s.AttachmentDeleteFailed;
            Draw();
        }
    }
}
