using Focus_Bear.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Focus_Bear.Habits.DayPlan
{
    /// <summary>
    /// Interaction logic for DayPlanLogQuantityWindow.xaml — asks a habit's log-quantity answers when
    /// it's marked Done from the Day Plan. Mirrors the app's existing "Log Quantity of activity"
    /// dialog (heading/buttons from the logQuantityInfo config block). Answers two-way-bind into each
    /// <see cref="QuantityQuestion"/>'s <c>quantity</c>; the caller passes the list to PostActivityCompleted.
    /// </summary>
    public partial class DayPlanLogQuantityWindow : Window
    {
        // habitName is accepted for call-site compatibility; the dialog uses the generic config title,
        // exactly like the existing GridLogQuantityNag.
        public DayPlanLogQuantityWindow(string habitName, List<QuantityQuestion> questions, Window owner)
        {
            InitializeComponent();
            Owner = owner;

            JObject cfg = AppSettings.GetInstance.ConfigFileStrings;
            HeadingLogQuantity.Text = LogCfg(cfg, "title", "Log Quantity of activity");
            btnCancelText.Text = LogCfg(cfg, "buttonCancelText", "Cancel");
            btnOKText.Text = LogCfg(cfg, "buttonOkText", "OK");

            ListLogQuestions.ItemsSource = questions ?? new List<QuantityQuestion>();
            // The answers are bound straight into the habit's own questions, so a cancelled dialog must put the old
            // values back: otherwise the numbers typed and cancelled became the habit's defaults for the next log.
            var asked = questions ?? new List<QuantityQuestion>();
            var before = asked.Select(q => q.quantity).ToList();
            Closed += (s, e) =>
            {
                if (DialogResult == true) return;
                for (int i = 0; i < asked.Count && i < before.Count; i++) asked[i].quantity = before[i];
            };

            // Keyboard-first: land in the first answer with its default selected so typing replaces it,
            // select-all on every box reached with Tab, and Enter saves from anywhere in the dialog.
            AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((s, e) => (e.NewFocus as TextBox)?.SelectAll()));
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
                if (Keyboard.FocusedElement is Button) return;   // Enter on Cancel still cancels
                e.Handled = true;
                CommitTypedAnswers();
                DialogResult = true;
            };
            ContentRendered += (s, e) => FocusFirstAnswer();
            // A paste goes through the same rule as typing (it wasn't checked at all).
            DataObject.AddPastingHandler(ListLogQuestions, QuantityPasting);
        }

        /// <summary>The answer boxes commit on LostFocus (so "6." isn't re-formatted to "6" mid-typing); push them now.</summary>
        private void CommitTypedAnswers()
        {
            // Visible boxes only: each question also has a hidden box bound to the same answer, still holding the old value.
            foreach (TextBox box in Descendants(ListLogQuestions).OfType<TextBox>().Where(b => b.IsVisible))
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private void FocusFirstAnswer()
        {
            ListLogQuestions.UpdateLayout();
            TextBox first = Descendants(ListLogQuestions).OfType<TextBox>().FirstOrDefault(t => t.IsVisible && t.IsEnabled);
            if (first == null) return;
            first.Focus();
            Keyboard.Focus(first);
            first.SelectAll();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (DependencyObject d in Descendants(child)) yield return d;
            }
        }

        /// <summary>Null-safe read from the logQuantityInfo config block, with a fallback.</summary>
        private static string LogCfg(JObject cfg, string key, string fallback)
        {
            try
            {
                JToken v = cfg?["logQuantityInfo"]?[key];
                string s = v?.ToString();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }

        // Numbers only, the rule HabitWindow's answer boxes use (Core QuantityTyping): a comma is the decimal point,
        // and typing over a selection replaces it. The answer binds to a double, so a comma left in the text was
        // read as a thousands separator: "6,85" logged 685.
        private void Quantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextBox box)) return;
            e.Handled = true;
            Type(box, e.Text);
        }

        private void QuantityPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(e.OriginalSource is TextBox box) || !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)) return;
            e.CancelCommand();
            Type(box, (e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? "").Trim());
        }

        /// <summary>Puts <paramref name="typed"/> into the box at its selection when the result is still a number; false otherwise.</summary>
        internal static bool Type(TextBox box, string typed)
        {
            string next = FocusBear.Core.Schedule.QuantityTyping.Apply(box.Text, box.SelectionStart, box.SelectionLength, typed);
            if (next == null) return false;
            int caret = box.SelectionStart + (typed ?? "").Length;
            box.Text = next;
            box.SelectionStart = System.Math.Min(caret, next.Length);
            box.SelectionLength = 0;
            return true;
        }

        private void Quantity_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            CommitTypedAnswers();
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
