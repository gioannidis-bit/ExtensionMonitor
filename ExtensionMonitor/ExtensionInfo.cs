using System;
using System.ComponentModel;
using System.Windows.Media;

namespace ExtensionMonitor
{
    public class ExtensionInfo : INotifyPropertyChanged
    {
        private string _extensionNumber;
        private string _status;
        private string _currentCall;
        private string _callerId;
        private string _calledNumber;
        private DateTime _lastActivity;
        private int _callCount;
        private Brush _statusColor;
        private bool _isMonitored;

        public ExtensionInfo(string extensionNumber, string lineName = null)
        {
            ExtensionNumber = extensionNumber;
            LineName = lineName ?? extensionNumber;
            Status = "Unknown";
            CurrentCall = "None";
            CallerId = "";
            CalledNumber = "";
            LastActivity = DateTime.Now;
            CallCount = 0;
            StatusColor = Brushes.Gray;
            IsMonitored = false;
        }

        public string ExtensionNumber
        {
            get { return _extensionNumber; }
            set { _extensionNumber = value; OnPropertyChanged("ExtensionNumber"); }
        }

        public string LineName { get; set; }

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
                UpdateStatusColor();
            }
        }

        public string CurrentCall
        {
            get { return _currentCall; }
            set { _currentCall = value; OnPropertyChanged("CurrentCall"); }
        }

        public string CallerId
        {
            get { return _callerId; }
            set { _callerId = value; OnPropertyChanged("CallerId"); }
        }

        public string CalledNumber
        {
            get { return _calledNumber; }
            set { _calledNumber = value; OnPropertyChanged("CalledNumber"); }
        }

        public DateTime LastActivity
        {
            get { return _lastActivity; }
            set { _lastActivity = value; OnPropertyChanged("LastActivity"); OnPropertyChanged("LastActivityString"); }
        }

        public string LastActivityString
        {
            get { return LastActivity.ToString("HH:mm:ss"); }
        }

        public int CallCount
        {
            get { return _callCount; }
            set { _callCount = value; OnPropertyChanged("CallCount"); }
        }

        public Brush StatusColor
        {
            get { return _statusColor; }
            set { _statusColor = value; OnPropertyChanged("StatusColor"); }
        }

        public bool IsMonitored
        {
            get { return _isMonitored; }
            set { _isMonitored = value; OnPropertyChanged("IsMonitored"); }
        }

        private void UpdateStatusColor()
        {
            switch (Status)
            {
                case "Idle":
                    StatusColor = Brushes.Green;
                    break;
                case "Ringing":
                case "Offering":
                    StatusColor = Brushes.Orange;
                    break;
                case "Connected":
                case "Accepted":
                    StatusColor = Brushes.Blue;
                    break;
                case "Busy":
                    StatusColor = Brushes.Red;
                    break;
                case "On Hold":
                    StatusColor = Brushes.Yellow;
                    break;
                case "Dialing":
                case "Proceeding":
                    StatusColor = Brushes.Purple;
                    break;
                case "Disconnected":
                    StatusColor = Brushes.Gray;
                    break;
                default:
                    StatusColor = Brushes.LightGray;
                    break;
            }
        }

        public void UpdateActivity()
        {
            LastActivity = DateTime.Now;
        }

        public void IncrementCallCount()
        {
            CallCount++;
            UpdateActivity();
        }

        public void ClearCall()
        {
            CurrentCall = "None";
            CallerId = "";
            CalledNumber = "";
            Status = "Idle";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}