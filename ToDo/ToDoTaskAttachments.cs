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

        private static API.FocusBearAPIClient Api() => new API.FocusBearAPIClient();

        internal ToDoTaskAttachments(string taskId, ToDoStrings strings)
        {
            _taskId = taskId;
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
            Loaded += async (o, e) => await LoadAsync();
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
            if (_attachments.Count == 0)
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
            var dialog = new OpenFileDialog { Filter = _s.Attachments + " (" + patterns + ")|" + patterns + "|*.*|*.*", Multiselect = false };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            await UploadAsync(dialog.FileName);
        }

        /// <summary>Also the harness's entry point, so a test upload needs no file dialog.</summary>
        internal async Task UploadAsync(string path)
        {
            long? length = null;
            try { length = new FileInfo(path).Length; } catch (Exception ex) { LogWriter.LogWrite("Attachment size - " + ex.Message); }
            switch (ToDoAttachments.Evaluate(!string.IsNullOrEmpty(_taskId), _uploading, _attachments.Count, length))
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

            _uploading = true;
            _status.Text = _s.AttachmentUploading;
            Draw();
            string fileName = Path.GetFileName(path);
            var created = await Api().UploadToDoAttachment(_taskId, fileName, ToDoAttachments.ContentType(Path.GetExtension(path)), bytes);
            _uploading = false;
            _status.Text = created == null ? _s.AttachmentFailed : "";
            await LoadAsync();
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
