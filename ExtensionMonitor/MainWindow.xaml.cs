using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using JulMar.Atapi;

namespace ExtensionMonitor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private TapiManager _tapiManager;
        private TapiLine _monitoredLine;
        private readonly string TARGET_EXTENSION = "EXT00101";
        private DispatcherTimer _statusTimer;

        // Properties for data binding
        private string _connectionStatus = "Disconnected";
        private string _extensionStatus = "Unknown";
        private string _currentCall = "None";
        private string _lastEventTime = "";
        private string _callerId = "";
        private string _calledNumber = "";
        private Brush _statusColor = Brushes.Red;

        public string ConnectionStatus
        {
            get { return _connectionStatus; }
            set { _connectionStatus = value; OnPropertyChanged("ConnectionStatus"); }
        }

        public string ExtensionStatus
        {
            get { return _extensionStatus; }
            set
            {
                _extensionStatus = value;
                OnPropertyChanged("ExtensionStatus");
                UpdateStatusColor();
            }
        }

        public string CurrentCall
        {
            get { return _currentCall; }
            set { _currentCall = value; OnPropertyChanged("CurrentCall"); }
        }

        public string LastEventTime
        {
            get { return _lastEventTime; }
            set { _lastEventTime = value; OnPropertyChanged("LastEventTime"); }
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

        public Brush StatusColor
        {
            get { return _statusColor; }
            set { _statusColor = value; OnPropertyChanged("StatusColor"); }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            LastEventTime = DateTime.Now.ToString("HH:mm:ss");
        }

        private void UpdateStatusColor()
        {
            StatusColor = ExtensionStatus switch
            {
                "Idle" => Brushes.Green,
                "Ringing" => Brushes.Orange,
                "Connected" => Brushes.Blue,
                "Busy" => Brushes.Red,
                "On Hold" => Brushes.Yellow,
                _ => Brushes.Gray
            };
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectToTapi();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            DisconnectFromTapi();
        }

        private void ConnectToTapi()
        {
            try
            {
                // Initialize TAPI Manager
                _tapiManager = new TapiManager("ExtensionMonitor");
                if (!_tapiManager.Initialize())
                {
                    throw new Exception("Failed to initialize TAPI Manager");
                }

                ConnectionStatus = "Initializing...";

                // Get available lines
                var lines = _tapiManager.Lines;
                LogMessage("Found " + lines.Length + " TAPI lines");

                // Find the line that matches our target extension
                _monitoredLine = FindTargetLine(lines);

                if (_monitoredLine == null)
                {
                    throw new Exception("Extension " + TARGET_EXTENSION + " not found in available lines");
                }

                // Open the line for monitoring
                _monitoredLine.Open(MediaModes.InteractiveVoice);

                // Subscribe to events
                _monitoredLine.NewCall += OnNewCall;
                _monitoredLine.CallStateChanged += OnCallStateChanged;
                _monitoredLine.CallInfoChanged += OnCallInfoChanged;

                // Start monitoring
                _monitoredLine.Monitor();

                ConnectionStatus = "Connected";
                ExtensionStatus = "Idle";
                LogMessage("Successfully connected to extension " + TARGET_EXTENSION);
                LogMessage("Line Name: " + _monitoredLine.Name);
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Failed";
                LogMessage("Error: " + ex.Message);
                throw;
            }
        }

        private TapiLine FindTargetLine(TapiLine[] lines)
        {
            // Try to find line by exact extension match
            TapiLine line = null;
            foreach (var l in lines)
            {
                if (l.Name != null && l.Name.Contains(TARGET_EXTENSION))
                {
                    line = l;
                    break;
                }
            }

            if (line == null)
            {
                // Log all available lines for debugging
                LogMessage("Available lines:");
                foreach (var l in lines)
                {
                    LogMessage("  - " + l.Name + " (ID: " + l.Id + ")");
                }

                // If exact match not found, you might need to adjust this logic
                // based on how your NS1000 names the lines
                foreach (var l in lines)
                {
                    if (l.Name != null && l.Name.Contains("101"))
                    {
                        line = l;
                        break;
                    }
                }
            }

            return line;
        }

        private void OnNewCall(object sender, NewCallEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ExtensionStatus = "Ringing";
                CurrentCall = "New Call - ID: " + e.Call.Id;
                CallerId = e.Call.CallerId ?? "Unknown";
                CalledNumber = e.Call.CalledId ?? "Unknown";
                LogMessage("NEW CALL: From " + CallerId + " to " + CalledNumber);
            });
        }

        private void OnCallStateChanged(object sender, CallStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var status = GetCallStateDescription(e.CallState);
                ExtensionStatus = status;
                CurrentCall = "Call " + e.Call.Id + " - " + status;
                LogMessage("CALL STATE CHANGED: " + e.CallState + " - Call ID: " + e.Call.Id);

                if (e.CallState == CallState.Idle || e.CallState == CallState.Disconnected)
                {
                    // Call ended
                    CurrentCall = "None";
                    CallerId = "";
                    CalledNumber = "";
                    ExtensionStatus = "Idle";
                }
            });
        }

        private void OnCallInfoChanged(object sender, CallInfoChangeEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(e.Call.CallerId))
                    CallerId = e.Call.CallerId;
                if (!string.IsNullOrEmpty(e.Call.CalledId))
                    CalledNumber = e.Call.CalledId;

                LogMessage("CALL INFO CHANGED: From " + CallerId + " to " + CalledNumber);
            });
        }

        private string GetCallStateDescription(CallState state)
        {
            return state switch
            {
                CallState.Idle => "Idle",
                CallState.Offering => "Ringing",
                CallState.Accepted => "Answered",
                CallState.Connected => "Connected",
                CallState.Disconnected => "Disconnected",
                CallState.Busy => "Busy",
                CallState.OnHold => "On Hold",
                CallState.OnHoldPendingConference => "On Hold (Conference)",
                CallState.OnHoldPendingTransfer => "On Hold (Transfer)",
                CallState.Dialing => "Dialing",
                CallState.Proceeding => "Proceeding",
                CallState.Ringback => "Ringback",
                _ => state.ToString()
            };
        }

        private void DisconnectFromTapi()
        {
            try
            {
                if (_monitoredLine != null)
                {
                    _monitoredLine.NewCall -= OnNewCall;
                    _monitoredLine.CallStateChanged -= OnCallStateChanged;
                    _monitoredLine.CallInfoChanged -= OnCallInfoChanged;
                    _monitoredLine.Close();
                    _monitoredLine = null;
                }

                if (_tapiManager != null)
                {
                    _tapiManager.Shutdown();
                    _tapiManager = null;
                }

                ConnectionStatus = "Disconnected";
                ExtensionStatus = "Unknown";
                CurrentCall = "None";
                CallerId = "";
                CalledNumber = "";
                LogMessage("Disconnected from TAPI");
            }
            catch (Exception ex)
            {
                LogMessage($"Error during disconnect: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        private void RefreshExtensions_Click(object sender, RoutedEventArgs e)
        {
            if (_tapiManager != null)
            {
                DisconnectFromTapi();
            }
            try
            {
                ConnectToTapi();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Refresh failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            DisconnectFromTapi();
            if (_statusTimer != null)
                _statusTimer.Stop();
            base.OnClosing(e);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}