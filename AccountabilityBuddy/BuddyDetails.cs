using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Focus_Bear.AccountabilityBuddy
{
    public class BuddyDetails : INotifyPropertyChanged
    {
        private string _id;
        private string _buddyUserId;
        private string _buddyEmail;
        private string _invitationStatus;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string BuddyUserId
        {
            get => _buddyUserId;
            set { _buddyUserId = value; OnPropertyChanged(nameof(BuddyUserId)); }
        }

        public string BuddyEmail
        {
            get => _buddyEmail;
            set { _buddyEmail = value; OnPropertyChanged(nameof(BuddyEmail)); }
        }

        public string InvitationStatus
        {
            get => _invitationStatus;
            set { _invitationStatus = value; OnPropertyChanged(nameof(InvitationStatus)); }
        }

        // Constructor with all parameters
        public BuddyDetails(string id, string buddyUserId, string buddyEmail, string invitationStatus)
        {
            Id = id;
            BuddyUserId = buddyUserId;
            BuddyEmail = buddyEmail;
            InvitationStatus = invitationStatus;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

}
