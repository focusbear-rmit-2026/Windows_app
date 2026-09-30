using Focus_Bear.StatsScreens;
using Focus_Bear.Utils;
using FocusBear.Core.ToDo;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// A task's reactions and comments, in the editor of an existing task — the Mac's
    /// <c>ToDoTaskDetailsStore</c> with its sections. Reactions toggle at once and are re-read from the server;
    /// a comment can be added, and the viewer's own edited or deleted.
    /// </summary>
    internal sealed class ToDoTaskDiscussion : UserControl
    {
        private readonly string _taskId;
        private readonly ToDoStrings _s;
        private readonly string _me;
        private readonly Action<string> _status;
        private readonly StackPanel _reactions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        private readonly StackPanel _comments = new StackPanel();
        private readonly TextBox _newComment = new TextBox();
        private IList<ToDoReaction> _taskReactions = new List<ToDoReaction>();
        private IList<ToDoComment> _commentList = new List<ToDoComment>();
        private readonly Dictionary<string, IList<ToDoReaction>> _commentReactions = new Dictionary<string, IList<ToDoReaction>>();
        private string _editing;
        private bool _busy;
        /// <summary>Reactions with a request out: a second click waits for the first answer.</summary>
        private readonly HashSet<string> _toggling = new HashSet<string>();

        private static API.FocusBearAPIClient Api() => new API.FocusBearAPIClient();

        internal ToDoTaskDiscussion(string taskId, ToDoStrings strings, string userId, Action<string> status)
        {
            _taskId = taskId;
            _s = strings;
            _me = userId ?? "";
            _status = status;
            var stack = new StackPanel();
            stack.Children.Add(Title(_s.Reactions));
            stack.Children.Add(_reactions);
            stack.Children.Add(Title(_s.Comments));
            stack.Children.Add(_comments);
            var row = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var add = SmallButton(_s.Add, async (o, e) => await AddAsync(), primary: true);
            add.Margin = new Thickness(8, 0, 0, 0);
            AutomationProperties.SetAutomationId(add, "btnToDoCommentAdd");
            DockPanel.SetDock(add, Dock.Right);
            row.Children.Add(add);
            AutomationProperties.SetAutomationId(_newComment, "txtToDoCommentNew");
            _newComment.KeyDown += async (o, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; await AddAsync(); } };
            row.Children.Add(Field(_newComment, _s.AddCommentPlaceholder));
            stack.Children.Add(row);
            Content = stack;
            Draw();
            Loaded += async (o, e) => await LoadAsync();
        }

        private static TextBlock Title(string text)
        {
            var title = StatsUi.Text(text, 12, FontWeights.SemiBold, wrap: false);
            title.Margin = new Thickness(0, 16, 0, 6);
            return title;
        }

        private async Task LoadAsync()
        {
            var api = Api();
            var reactions = api.GetToDoReactions(_taskId);
            var comments = await api.GetToDoComments(_taskId);
            _taskReactions = await reactions ?? new List<ToDoReaction>();
            _commentList = comments ?? new List<ToDoComment>();
            // Each comment arrives with its reactions. They used to be fetched again one comment at a time,
            // a request and a full redraw per comment, every time the editor opened or a comment was added.
            _commentReactions.Clear();
            foreach (var comment in _commentList) _commentReactions[comment.Id] = comment.Reactions ?? new List<ToDoReaction>();
            if (comments == null) _status(_s.RequestFailed);
            Draw();
        }

        private void Draw()
        {
            _reactions.Children.Clear();
            foreach (var emoji in ToDoComments.ReactionPalette)
            {
                var captured = emoji;
                _reactions.Children.Add(ReactionButton(emoji, _taskReactions, async () => await ToggleTaskReactionAsync(captured), "btnToDoReaction" + ToDoComments.ReactionPalette.IndexOf(emoji)));
            }
            _comments.Children.Clear();
            if (_commentList.Count == 0)
            {
                _comments.Children.Add(StatsUi.Text(_s.NoCommentsYet, 12, secondary: true));
                return;
            }
            foreach (var comment in _commentList) _comments.Children.Add(CommentCard(comment));
        }

        private FrameworkElement ReactionButton(string emoji, IList<ToDoReaction> reactions, Action onClick, string automationId = null)
        {
            bool mine = ToDoComments.HasReacted(reactions, emoji, _me);
            int count = ToDoComments.Count(reactions, emoji);
            var label = new TextBlock { FontSize = 13 };
            label.Inlines.Add(new System.Windows.Documents.Run(emoji) { FontFamily = EmojiFont });
            if (count > 0) label.Inlines.Add(new System.Windows.Documents.Run(" " + count.ToString(CultureInfo.InvariantCulture)) { FontSize = 11 });
            label.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            var border = new Border { Child = label, CornerRadius = new CornerRadius(12), Padding = new Thickness(8, 2, 8, 2), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0) };
            if (mine) { border.Background = StatsUi.Tint(Accent, 0.18); border.BorderBrush = Accent; }
            else { border.SetResourceReference(Border.BackgroundProperty, "TrayCardBg"); border.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder"); }
            var button = new Button { Content = border, Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, Focusable = false };
            button.Click += (o, e) => onClick();
            if (automationId != null) AutomationProperties.SetAutomationId(button, automationId);
            return button;
        }

        private FrameworkElement CommentCard(ToDoComment comment)
        {
            var stack = new StackPanel();
            var head = new DockPanel { LastChildFill = true };
            bool mine = ToDoComments.IsMine(comment, _me);
            if (mine && _editing != comment.Id)
            {
                var delete = new Button { Content = Glyph(0xE74D), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = _s.DeleteComment, Margin = new Thickness(8, 0, 0, 0), Focusable = false };
                delete.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                delete.Click += async (o, e) => await DeleteAsync(comment);
                AutomationProperties.SetAutomationId(delete, "btnToDoCommentDelete_" + comment.Id);
                DockPanel.SetDock(delete, Dock.Right);
                head.Children.Add(delete);
                var edit = new Button { Content = Glyph(0xE70F), FontFamily = new FontFamily("Segoe MDL2 Assets"), Template = StatsUi.PlainTemplate(), Cursor = Cursors.Hand, ToolTip = _s.EditComment, Margin = new Thickness(8, 0, 0, 0), Focusable = false };
                edit.SetResourceReference(Control.ForegroundProperty, "TraySubtitleColor");
                edit.Click += (o, e) => { _editing = comment.Id; Draw(); };
                AutomationProperties.SetAutomationId(edit, "btnToDoCommentEdit_" + comment.Id);
                DockPanel.SetDock(edit, Dock.Right);
                head.Children.Add(edit);
            }
            string who = mine ? _s.You : (comment.AuthorName ?? _s.UnknownUser);
            var date = ToDoDates.Parse(comment.CreatedAt);
            head.Children.Add(StatsUi.Text(who + (date.HasValue ? "  ·  " + date.Value.ToString("d MMM HH:mm", ToDoStrings.Culture) : ""), 11, FontWeights.SemiBold, secondary: true, wrap: false));
            stack.Children.Add(head);
            if (_editing == comment.Id)
            {
                var box = new TextBox { Text = comment.Content, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
                AutomationProperties.SetAutomationId(box, "txtToDoCommentEdit");
                stack.Children.Add(Field(box, "", 60));
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
                buttons.Children.Add(SmallButton(_s.Cancel, (o, e) => { _editing = null; Draw(); }));
                var save = SmallButton(_s.Save, async (o, e) => await UpdateAsync(comment, box.Text), primary: true);
                AutomationProperties.SetAutomationId(save, "btnToDoCommentSave");
                buttons.Children.Add(save);
                stack.Children.Add(buttons);
            }
            else
            {
                var body = StatsUi.Text(comment.Content, 12);
                body.Margin = new Thickness(0, 4, 0, 6);
                stack.Children.Add(body);
                var reactions = _commentReactions.TryGetValue(comment.Id, out var list) ? list : new List<ToDoReaction>();
                var row = new WrapPanel();
                foreach (var emoji in ToDoComments.ReactionPalette)
                {
                    var captured = emoji;
                    if (ToDoComments.Count(reactions, emoji) == 0 && emoji != ToDoComments.ReactionPalette[0]) continue;
                    row.Children.Add(ReactionButton(emoji, reactions, async () => await ToggleCommentReactionAsync(comment, captured)));
                }
                stack.Children.Add(row);
            }
            var card = new Border { Child = stack, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BackgroundProperty, "TrayCardBg");
            card.SetResourceReference(Border.BorderBrushProperty, "TrayCardBorder");
            AutomationProperties.SetName(card, comment.Content);
            return card;
        }

        private async Task ToggleTaskReactionAsync(string emoji)
        {
            if (!_toggling.Add(emoji)) return;
            try { await ToggleTaskReaction(emoji); }
            finally { _toggling.Remove(emoji); }
        }

        private async Task ToggleTaskReaction(string emoji)
        {
            bool removing = ToDoComments.HasReacted(_taskReactions, emoji, _me);
            _taskReactions = ToDoComments.Toggled(_taskReactions, emoji, _me, _taskId);
            Draw();
            bool ok = removing ? await Api().RemoveToDoReaction(_taskId, emoji) : await Api().AddToDoReaction(_taskId, emoji);
            if (!ok) _status(removing ? _s.CouldNotRemoveReaction : _s.CouldNotAddReaction);
            _taskReactions = await Api().GetToDoReactions(_taskId) ?? _taskReactions;
            Draw();
        }

        private async Task ToggleCommentReactionAsync(ToDoComment comment, string emoji)
        {
            string key = comment.Id + "\n" + emoji;
            if (!_toggling.Add(key)) return;
            try { await ToggleCommentReaction(comment, emoji); }
            finally { _toggling.Remove(key); }
        }

        private async Task ToggleCommentReaction(ToDoComment comment, string emoji)
        {
            var current = _commentReactions.TryGetValue(comment.Id, out var list) ? list : new List<ToDoReaction>();
            bool removing = ToDoComments.HasReacted(current, emoji, _me);
            _commentReactions[comment.Id] = ToDoComments.Toggled(current, emoji, _me, _taskId, comment.Id);
            Draw();
            bool ok = removing ? await Api().RemoveToDoReaction(_taskId, emoji, comment.Id) : await Api().AddToDoReaction(_taskId, emoji, comment.Id);
            if (!ok) _status(removing ? _s.CouldNotRemoveReaction : _s.CouldNotAddReaction);
            var fresh = await Api().GetToDoReactions(_taskId, comment.Id);
            if (fresh != null) _commentReactions[comment.Id] = fresh;
            Draw();
        }

        private async Task AddAsync()
        {
            string text = _newComment.Text.Trim();
            if (text.Length == 0 || _busy) return;
            _busy = true;
            bool ok = await Api().AddToDoComment(_taskId, text);
            _busy = false;
            _status(ok ? _s.CommentAdded : _s.CouldNotAddComment);
            if (!ok) return;
            _newComment.Text = "";
            await LoadAsync();
        }

        private async Task UpdateAsync(ToDoComment comment, string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || _busy) return;
            _busy = true;
            bool ok = await Api().UpdateToDoComment(_taskId, comment.Id, text);
            _busy = false;
            _status(ok ? _s.CommentUpdated : _s.CouldNotUpdateComment);
            if (!ok) return;
            comment.Content = text;
            _editing = null;
            Draw();
        }

        private async Task DeleteAsync(ToDoComment comment)
        {
            // A second click while the first delete is out would send another DELETE and report its 404.
            if (_busy) return;
            _busy = true;
            bool ok = await Api().DeleteToDoComment(_taskId, comment.Id);
            _busy = false;
            _status(ok ? _s.CommentDeleted : _s.CouldNotDeleteComment);
            if (!ok) return;
            _commentList = _commentList.Where(c => c.Id != comment.Id).ToList();
            Draw();
        }
    }
}
