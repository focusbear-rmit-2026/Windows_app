using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Focus_Bear.Utils
{
    /// <summary>
    /// Interaction logic for HourControl.xaml
    /// </summary>
    public partial class HourControl : UserControl
    {
        public event EventHandler<bool> ValueChanged;

        int CurrentTextBox = 0;

        int minSvalue = 0,
        maxSvalue = 59,
        startvalue = 0;

        int minHvalue = 0,
        maxHvalue = 12,
        startHvalue = 12;

        public bool isAM = false;

        public int HH
        {
            get { return Convert.ToInt32(NUDHTextBox.Text); }
            set
            {
                NUDHTextBox.Text = value.ToString("00");
            }
        }

        public void SetHH(int value)
        {
            NUDHTextBox.Text = value.ToString("00");
        }

        public void SetMM(int value)
        {
            NUDMTextBox.Text = value.ToString("00");
        }

        public void SetSS(int value)
        {
            NUDSTextBox.Text = value.ToString("00");
        }

        public void SetAMorPM(string value)
        {
            NUDAMPTextBox.Text = value;
        }

        /// <summary>
        /// Parses the current hour/minute/AM-PM entry into a 24h "HH:mm:ss" string.
        /// Returns false (and null) when the entry is incomplete or invalid, so the
        /// caller can show a validation message instead of failing silently.
        /// </summary>
        public bool TryGetTime24(out string time24)
        {
            time24 = null;
            string combined = $"{NUDHTextBox.Text}:{NUDMTextBox.Text}:00 {NUDAMPTextBox.Text}";
            if (!DateTime.TryParseExact(combined, "h:m:s tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                return false;
            time24 = parsed.ToString("HH:mm:ss");
            return true;
        }

        public int MM
        {
            get { return Convert.ToInt32(NUDMTextBox.Text); }
            set
            {
                //if(value == 0)
                //{
                //    NUDMTextBox.Text = "00";
                //}
                //else
                {
                    NUDMTextBox.Text = value.ToString("00");
                }
            }
        }

        public int SS
        {
            get { return Convert.ToInt32(NUDSTextBox.Text); }
            set
            {
                //if (value == 0)
                //{
                //    NUDSTextBox.Text = "00";
                //}
                //else
                {
                    NUDSTextBox.Text = value.ToString("00");
                }
            }
        }


        public HourControl()
        {
            InitializeComponent();

            var tmpStartValue = startvalue;
            //if(tmpStartValue == 0)
            //{
            //    NUDMTextBox.Text = "00";
            //    NUDSTextBox.Text = "00";
            //}
            //else
            {
                NUDMTextBox.Text = startvalue.ToString("00");
                NUDSTextBox.Text = startvalue.ToString("00");
            }
            NUDHTextBox.Text = startHvalue.ToString("00");

            if (isAM)
            {
                NUDAMPTextBox.Text = "AM";
            }
            else
            {
                NUDAMPTextBox.Text = "PM";
            }

        }



        public void UpdateMederianTime(bool mederian)
        {
            if (mederian)
            {
                NUDAMPTextBox.Text = "AM";
                isAM = true;
            }
            else
            {
                NUDAMPTextBox.Text = "PM";
                isAM = false;
            }
        }

        private void NUDButtonUP_Click(object sender, RoutedEventArgs e)
        {
            int number;
            if (CurrentTextBox == 2)
            {
                if (NUDSTextBox.Text != "") number = Convert.ToInt32(NUDSTextBox.Text);
                else number = 0;
                if (number < maxSvalue)
                    NUDSTextBox.Text = (number + 1).ToString("00");
                else if (number == maxSvalue)
                    NUDSTextBox.Text = minSvalue.ToString("00");
            }
            else if (CurrentTextBox == 1)
            {
                if (NUDMTextBox.Text != "") number = Convert.ToInt32(NUDMTextBox.Text);
                else number = 0;
                if (number < maxSvalue)
                    NUDMTextBox.Text = (number + 1).ToString("00");
                else if (number == maxSvalue)
                    NUDMTextBox.Text = minSvalue.ToString("00");
            }
            else if (CurrentTextBox == 4)
            {
                if (NUDAMPTextBox.Text == "AM")
                {
                    NUDAMPTextBox.Text = "PM";
                    isAM = false;
                }
                else
                {
                    NUDAMPTextBox.Text = "AM";
                    isAM = true;
                }
            }
            else
            {
                if (NUDHTextBox.Text != "")
                {
                    int tmpvalue = Convert.ToInt32(NUDHTextBox.Text);
                    if (tmpvalue == 12)
                        tmpvalue = 0;
                    number = tmpvalue;
                }
                else number = 0;
                if (number < maxHvalue)
                    NUDHTextBox.Text = (number + 1).ToString("00");
                else if (number == maxSvalue)
                    NUDHTextBox.Text = minSvalue.ToString("00");
            }

            ValueChanged?.Invoke(this, true);

        }

        private void NUDButtonDown_Click(object sender, RoutedEventArgs e)
        {
            int number;
            if (CurrentTextBox == 2)
            {
                if (NUDSTextBox.Text != "") number = Convert.ToInt32(NUDSTextBox.Text);
                else number = 0;
                if (number > minSvalue)
                {
                    int tmpNumber = number - 1;
                    //if (tmpNumber == 0)
                    {
                        NUDSTextBox.Text = tmpNumber.ToString("00");
                    }
                }
                else if (number == minSvalue)
                {
                    NUDSTextBox.Text = maxSvalue.ToString("00");
                }
            }
            else if (CurrentTextBox == 1)
            {
                if (NUDMTextBox.Text != "") number = Convert.ToInt32(NUDMTextBox.Text);
                else number = 0;
                if (number > minSvalue)
                {
                    int tmpNumber = number - 1;
                    {
                        NUDMTextBox.Text = tmpNumber.ToString("00");
                    }
                }
                else if (number == minSvalue)
                {
                    NUDMTextBox.Text = maxSvalue.ToString("00");
                }
            }
            else if (CurrentTextBox == 4)
            {
                if (NUDAMPTextBox.Text == "AM")
                {
                    NUDAMPTextBox.Text = "PM";
                    isAM = false;
                }
                else
                {
                    NUDAMPTextBox.Text = "AM";
                    isAM = true;
                }
            }
            else
            {
                if (NUDHTextBox.Text != "") number = Convert.ToInt32(NUDHTextBox.Text);
                else number = 1;
                if (number > minHvalue)
                {
                    int tmpNumber = number - 1;
                    if (tmpNumber == 0)
                    {
                        tmpNumber = 12;
                    }
                    NUDHTextBox.Text = tmpNumber.ToString("00");
                }
            }
            ValueChanged?.Invoke(this, true);
        }

        private void NUDTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Up)
            {
                NUDButtonUP.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { true });
            }


            if (e.Key == Key.Down)
            {
                NUDButtonDown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { true });
            }
        }


        private void NUDTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > -1)
            {
                NUDButtonUP_Click(this, null);
            }
            else
            {
                NUDButtonDown_Click(this, null);
            }
        }

        private void NUDHTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { false });

            if (e.Key == Key.Down)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { false });
        }



        private void NUDMTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                NUDButtonUP.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { true });
            }


            if (e.Key == Key.Down)
            {
                NUDButtonDown.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { true });
            }
        }

        private void NUDMTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { false });

            if (e.Key == Key.Down)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { false });
        }

        private void NUDSTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CurrentTextBox = 2;
            NUDSTextBox.SelectAll();
        }

        private void NUDMTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CurrentTextBox = 1;
            NUDSTextBox.SelectAll();
        }

        private void NUDHTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CurrentTextBox = 0;
            NUDSTextBox.SelectAll();
        }

        private void NUDAMPTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            CurrentTextBox = 4;
            NUDAMPTextBox.SelectAll();
        }

        private void NUDAMPTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Same self-correcting pattern as the hour/minute boxes above: while
            // typing, the field may only hold a prefix of "AM"/"PM" ("", "A", "P").
            // Any other character snaps straight back to the last valid value.
            string text = NUDAMPTextBox.Text.ToUpperInvariant();

            if (text != "" && text != "A" && text != "P" && text != "AM" && text != "PM")
            {
                NUDAMPTextBox.Text = isAM ? "AM" : "PM";
                NUDAMPTextBox.SelectAll();
                return; // re-enters with the corrected text
            }

            if (text != NUDAMPTextBox.Text)
            {
                int caret = NUDAMPTextBox.SelectionStart;
                NUDAMPTextBox.Text = text;
                NUDAMPTextBox.SelectionStart = caret;
                return; // re-enters with the normalised text
            }

            if (text == "AM") isAM = true;
            else if (text == "PM") isAM = false;

            ValueChanged?.Invoke(this, true);
        }

        private void NUDAMPTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // On blur, snap a partial/empty/invalid entry back to a valid value so
            // the save path always has a parseable AM/PM token.
            string text = NUDAMPTextBox.Text.ToUpperInvariant();

            if (text.StartsWith("A")) NUDAMPTextBox.Text = "AM";
            else if (text.StartsWith("P")) NUDAMPTextBox.Text = "PM";
            else NUDAMPTextBox.Text = isAM ? "AM" : "PM";
        }

        private void NUDHTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int number = 0;
            if (NUDHTextBox.Text != "")
                if (!int.TryParse(NUDHTextBox.Text, out number)) NUDHTextBox.Text = startHvalue.ToString("00");
            if (number > maxHvalue) NUDHTextBox.Text = maxHvalue.ToString("00");
            if (number < minHvalue) NUDHTextBox.Text = minHvalue.ToString("00");
            NUDHTextBox.SelectionStart = NUDHTextBox.Text.Length;

            ValueChanged?.Invoke(this, true);
        }

        private void NUDMTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int number = 0;
            if (NUDMTextBox.Text != "")
                if (!int.TryParse(NUDMTextBox.Text, out number)) NUDMTextBox.Text = startvalue.ToString("00");
            if (number > maxSvalue) NUDMTextBox.Text = maxSvalue.ToString("00");
            if (number < minSvalue) NUDMTextBox.Text = minSvalue.ToString("00");
            NUDMTextBox.SelectionStart = NUDMTextBox.Text.Length;

            ValueChanged?.Invoke(this, true);
        }

        private void NUDTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonUP, new object[] { false });

            if (e.Key == Key.Down)
                typeof(Button).GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(NUDButtonDown, new object[] { false });
        }

        private void NUDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int number = 0;
            if (NUDSTextBox.Text != "")
                if (!int.TryParse(NUDSTextBox.Text, out number)) NUDSTextBox.Text = startvalue.ToString("00");
            if (number > maxSvalue) NUDSTextBox.Text = maxSvalue.ToString("00");
            if (number < minSvalue) NUDSTextBox.Text = minSvalue.ToString("00");
            NUDSTextBox.SelectionStart = NUDSTextBox.Text.Length;

            ValueChanged?.Invoke(this, true);
        }
    }
}
