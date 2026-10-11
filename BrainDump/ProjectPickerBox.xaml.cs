using FocusBear.Core.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Focus_Bear.BrainDump
{
    /// <summary>
    /// A type-to-narrow project field: the Mac's <c>SearchableComboBoxField</c>. A flat drop-down listing
    /// every project is unusable once there are more than a handful (the reporting account has 128), so
    /// typing narrows the list and what the text means when the user moves on follows
    /// <see cref="ProjectFilterText.Resolve"/> — an exact name, or a fragment only one project matches;
    /// anything else is no project, never a guess.
    /// </summary>
    public partial class ProjectPickerBox : UserControl
    {
        private List<(string Id, string Name)> _options = new List<(string Id, string Name)>();
        private string _selectedId;
        private bool _suppress;

        public ProjectPickerBox()
        {
            InitializeComponent();
        }

        /// <summary>The first row, meaning "no project" ("All projects"); null for a field that must be chosen.</summary>
        public string ClearedTitle { get; set; }

        public string Placeholder
        {
            get { return lblPlaceholder.Text; }
            set { lblPlaceholder.Text = value; UpdatePlaceholder(); }
        }

        /// <summary>Raised when the user settles on a project (or on none).</summary>
        public event EventHandler SelectionCommitted;

        public string SelectedId
        {
            get { return _selectedId; }
            set { _selectedId = value; ShowSelection(); }
        }

        public void SetOptions(IEnumerable<(string Id, string Name)> options)
        {
            _options = options.ToList();
            if (_selectedId != null && !_options.Any(o => o.Id == _selectedId)) _selectedId = null;
            // Never rewrite the box under the user's typing.
            if (!txtQuery.IsKeyboardFocusWithin) ShowSelection();
        }

        private void ShowSelection()
        {
            _suppress = true;
            txtQuery.Text = _selectedId == null && ClearedTitle == null
                ? ""
                : ProjectFilterText.DisplayName(_selectedId, _options, ClearedTitle ?? "");
            _suppress = false;
            btnClear.Visibility = _selectedId != null ? Visibility.Visible : Visibility.Collapsed;
            UpdatePlaceholder();
        }

        private void UpdatePlaceholder()
        {
            lblPlaceholder.Visibility = string.IsNullOrEmpty(txtQuery.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private List<string> Titles()
        {
            return ClearedTitle == null ? _options.Select(o => o.Name).ToList() : ProjectFilterText.Titles(ClearedTitle, _options);
        }

        private void ShowSuggestions(string typed)
        {
            string needle = (typed ?? "").Trim();
            var titles = Titles();
            lbSuggestions.ItemsSource = needle.Length == 0 || needle == ClearedTitle
                ? titles
                : titles.Where(t => t != ClearedTitle && t.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Suggestions.IsOpen = lbSuggestions.Items.Count > 0;
        }

        private void txtQuery_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            txtQuery.SelectAll();
            ShowSuggestions(null);
        }

        private void txtQuery_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholder();
            if (_suppress || !txtQuery.IsKeyboardFocusWithin) return;
            ShowSuggestions(txtQuery.Text);
        }

        private void txtQuery_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Commit(Suggestions.IsOpen && lbSuggestions.SelectedItem is string highlighted ? highlighted : txtQuery.Text);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Suggestions.IsOpen = false;
                    ShowSelection();
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (Suggestions.IsOpen && lbSuggestions.Items.Count > 0)
                    {
                        lbSuggestions.SelectedIndex = Math.Min(lbSuggestions.SelectedIndex + 1, lbSuggestions.Items.Count - 1);
                        e.Handled = true;
                    }
                    break;
                case Key.Up:
                    if (Suggestions.IsOpen && lbSuggestions.SelectedIndex > 0)
                    {
                        lbSuggestions.SelectedIndex--;
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void txtQuery_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // A click on a suggestion takes focus from the box first; that click commits on its own.
            if (lbSuggestions.IsMouseOver) return;
            if (Suggestions.IsOpen || txtQuery.Text != ProjectFilterText.DisplayName(_selectedId, _options, ClearedTitle ?? "")) Commit(txtQuery.Text);
        }

        private void lbSuggestions_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(lbSuggestions, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null) Commit(item.Content as string);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            Commit(null);
        }

        /// <summary>Settle on what <paramref name="text"/> names — or on no project.</summary>
        public void Commit(string text)
        {
            Suggestions.IsOpen = false;
            lbSuggestions.SelectedItem = null;
            string id = text == null || text == ClearedTitle ? null : ProjectFilterText.Resolve(text, _options);
            bool changed = id != _selectedId;
            _selectedId = id;
            ShowSelection();
            if (changed) SelectionCommitted?.Invoke(this, EventArgs.Empty);
        }
    }
}
