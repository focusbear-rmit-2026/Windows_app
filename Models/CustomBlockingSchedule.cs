using Focus_Bear.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Focus_Bear.Models
{
    public enum FOCUSMODESTRICTNESS
    {
        CHILL_MODE = 0,
        STRICT = 50,
        IRON_FOCUS = 100
    }
   
    public class CustomBlockingSchedule : INotifyPropertyChanged
    {
        public string id;
        public string _name;
        public string name
        {
            get { return _name; }
            set
            {
                if (value != _name)
                {
                    _name = value;
                    OnChanged("name");
                }
            }
        }

        public bool _isOn;
        public bool isOn
        {
            get { return _isOn; }
            set
            {
                if (value != _isOn)
                {
                    _isOn = value;
                    OnChanged("isOn");
                }
            }
        }

        public bool _isPaused;
        public bool isPaused
        {
            get { return _isPaused; }
            set
            {
                if (value != _isPaused)
                {
                    _isPaused = value;
                    OnChanged("isPaused");
                }
            }
        }

        public string StartTimeValue { get; set; }
        public string EndTimeValue { get; set; }

        private string _selectedFocusModeId;
        public string SelectedFocusModeId
        {
            get => _selectedFocusModeId;
            set
            {
                if (_selectedFocusModeId != value)
                {
                    _selectedFocusModeId = value;
                    OnChanged(nameof(SelectedFocusModeId));
                }
            }
        }

        public string FocusModeName
        {
            
            get
            {
                AppSettings appSettings = AppSettings.GetInstance;
                var mode = appSettings.arrFocusModes
                    .FirstOrDefault(f => f.id == SelectedFocusModeId);
                return mode?.name ?? "Update block list ⚠️"; //todo win texts
            }
        }
        
        // 0–6: Mon–Sun
        private bool[] _daysOfWeek = Enumerable.Repeat(false, 7).ToArray();
        public bool[] DaysOfWeek
        {
            get => _daysOfWeek;
            set
            {
                _daysOfWeek = value;
                OnChanged(nameof(DaysOfWeek));
            }
        }
        // ---- Individual day bindings ----
        
        public bool Monday
        {
            get => DaysOfWeek[0];
            set { DaysOfWeek[0] = value; NotifyPropertyChanged(nameof(Monday)); }
        }

        public bool Tuesday
        {
            get => DaysOfWeek[1];
            set { DaysOfWeek[1] = value; NotifyPropertyChanged(nameof(Tuesday)); }
        }

         public bool Wednesday
        {
            get => DaysOfWeek[2];
            set { DaysOfWeek[2] = value; NotifyPropertyChanged(nameof(Wednesday)); }
        }
        public bool Thursday
        {
            get => DaysOfWeek[3];
            set { DaysOfWeek[3] = value; NotifyPropertyChanged(nameof(Thursday)); }
        }
        public bool Friday
        {
            get => DaysOfWeek[4];
            set { DaysOfWeek[4] = value; NotifyPropertyChanged(nameof(Friday)); }
        }

        public bool Saturday
        {
            get => DaysOfWeek[5];
            set { DaysOfWeek[5] = value; NotifyPropertyChanged(nameof(Saturday)); }
        }
        public bool Sunday
        {
            get => DaysOfWeek[6];
            set { DaysOfWeek[6] = value; NotifyPropertyChanged(nameof(Sunday)); }
        }


        private FOCUSMODESTRICTNESS _blockLevel = FOCUSMODESTRICTNESS.CHILL_MODE;
        public FOCUSMODESTRICTNESS BlockLevel
        {
            get => _blockLevel;
            set
            {
                if (_blockLevel != value)
                {
                    _blockLevel = value;
                    OnChanged(nameof(BlockLevel)); // your existing method
                }
            }
        }

        //Only for display purposes - binding
        public string StrictnessLevelDisplay
        {
            get
            {
                switch (BlockLevel)
                {
                    case FOCUSMODESTRICTNESS.CHILL_MODE:
                        return "Normal";
                    case FOCUSMODESTRICTNESS.STRICT:
                        return "Strict";
                    case FOCUSMODESTRICTNESS.IRON_FOCUS:
                        return "Super Strict"; // todo win text
                }
                return string.Empty;
            }
            
        }
        public string BlockingScheduleDaysTime
        {
            get
            {
                DateTime timeStart = DateTime.ParseExact(StartTimeValue, "HH:mm:ss", CultureInfo.InvariantCulture);
                string timeS = timeStart.ToString("hh:mm:ss tt");

                DateTime timeEnd = DateTime.ParseExact(EndTimeValue, "HH:mm:ss", CultureInfo.InvariantCulture);
                string timeE = timeEnd.ToString("hh:mm:ss tt");

                string sDays = GetAllowedDaysString(_daysOfWeek) + ", " + timeS + " to " + timeE;
                return sDays;
            }
        }

        private string GetAllowedDaysString(bool[] daysOfWeek)
        {
            if (daysOfWeek == null || daysOfWeek.Length != 7)
                return "";

            string[] dayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

            if (daysOfWeek.All(d => d))
                return "Everyday"; //todo win texts

            List<string> parts = new List<string>();
            int start = -1;

            for (int i = 0; i < 7; i++)
            {
                if (daysOfWeek[i])
                {
                    if (start == -1)
                        start = i;
                }
                else
                {
                    if (start != -1)
                    {
                        AddDayRange(parts, dayNames, start, i - 1);
                        start = -1;
                    }
                }
            }

            if (start != -1)
                AddDayRange(parts, dayNames, start, 6);

            return string.Join(", ", parts);
        }

        private void AddDayRange(List<string> parts, string[] dayNames, int start, int end)
        {
            if (start == end)
                parts.Add(dayNames[start]);
            else if (end == start + 1)
                parts.Add($"{dayNames[start]}, {dayNames[end]}");
            else
                parts.Add($"{dayNames[start]} to {dayNames[end]}");
        }



        private bool _bAIBlockingEnabled;
        public bool bAIBlockingEnabled
        {
            get => _bAIBlockingEnabled;
            set
            {
                if (_bAIBlockingEnabled != value)
                {
                    _bAIBlockingEnabled = value;
                    OnChanged(nameof(bAIBlockingEnabled));
                }
            }
        }

        private bool _bMicroBreaksEnabled;
        public bool bMicroBreaksEnabled
        {
            get => _bMicroBreaksEnabled;
            set
            {
                if (_bMicroBreaksEnabled != value)
                {
                    _bMicroBreaksEnabled = value;
                    OnChanged(nameof(bMicroBreaksEnabled));
                }
            }
        }

        public CustomBlockingSchedule()
        {

        }
        public CustomBlockingSchedule(
            string id,
            string name,
            bool isOn,
            string startTimeValue,
            string endTimeValue,
            bool[] daysOfWeek,
            string selectedFocusModeId,
            FOCUSMODESTRICTNESS blockLevel,
            bool bAIBlockingEnabled,
            bool bMicroBreaksEnabled)
        {
            this.id = id;
            this.name = name;
            this.isOn = isOn;
            this.StartTimeValue = startTimeValue;
            this.EndTimeValue = endTimeValue;
            this.DaysOfWeek = daysOfWeek;
            this.SelectedFocusModeId = selectedFocusModeId;
            this.BlockLevel = blockLevel;
            this.bAIBlockingEnabled = bAIBlockingEnabled;
            this.bMicroBreaksEnabled = bMicroBreaksEnabled;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName) =>
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public void OnChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public CustomBlockingSchedule Clone()
        {
            return new CustomBlockingSchedule
            {
               id = this.id,
               name = this.name,
               isOn = this.isOn,
               StartTimeValue = this.StartTimeValue,
               EndTimeValue = this.EndTimeValue,
               DaysOfWeek = this.DaysOfWeek,
               SelectedFocusModeId = this.SelectedFocusModeId,
               BlockLevel = this.BlockLevel,
               bAIBlockingEnabled = this.bAIBlockingEnabled,
               bMicroBreaksEnabled = this.bMicroBreaksEnabled,
            };
        }

        public void CopyFrom(CustomBlockingSchedule other)
        {
            if (other == null) return;
            id = other.id;
            name = other.name;
            isOn = other.isOn;
            StartTimeValue = other.StartTimeValue;
            EndTimeValue = other.EndTimeValue;
            DaysOfWeek = other.DaysOfWeek;
            SelectedFocusModeId = other.SelectedFocusModeId;
            BlockLevel = other.BlockLevel;
            bAIBlockingEnabled = other.bAIBlockingEnabled;
            bMicroBreaksEnabled = other.bMicroBreaksEnabled;

            OnChanged(nameof(id));
            OnChanged(nameof(name));
            OnChanged(nameof(isOn));
            OnChanged(nameof(StartTimeValue));
            OnChanged(nameof(EndTimeValue));
            OnChanged(nameof(DaysOfWeek));
            OnChanged(nameof(SelectedFocusModeId));
            OnChanged(nameof(BlockLevel));
            OnChanged(nameof(bAIBlockingEnabled));
            OnChanged(nameof(bMicroBreaksEnabled));
        }
    }
}
