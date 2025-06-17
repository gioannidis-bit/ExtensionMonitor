using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using JulMar.Atapi;

namespace ExtensionMonitor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private TapiManager _tapiManager;
        private Dictionary<int, TapiLine> _monitoredLines;
        private Dictionary<int, ExtensionInfo> _extensionMap;
        private DispatcherTimer _statusTimer;
        private CollectionViewSource _extensionsViewSource;

        // Properties for data binding
        private ObservableCollection<ExtensionInfo> _extensions;
        private string _connectionStatus = "Disconnected";
        private string _lastEventTime = "";
        private string _filterText = "";
        private int _totalCallsToday = 0;
        private int _activeCallsCount = 0;
        private int _monitoredExtensionsCount = 0;

        public ObservableCollection<ExtensionInfo> Extensions
        {
            get { return _extensions; }
            set { _extensions = value; OnPropertyChanged("Extensions"); }
        }

        public ICollectionView ExtensionsView { get; set; }

        public string ConnectionStatus
        {
            get { return _connectionStatus; }
            set { _connectionStatus = value; OnPropertyChanged("ConnectionStatus"); }
        }

        public string LastEventTime
        {
            get { return _lastEventTime; }
            set { _lastEventTime = value; OnPropertyChanged("LastEventTime"); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set
            {
                _filterText = value;
                OnPropertyChanged("FilterText");
                ExtensionsView?.Refresh();
            }
        }

        public int TotalCallsToday
        {
            get { return _totalCallsToday; }
            set { _totalCallsToday = value; OnPropertyChanged("TotalCallsToday"); }
        }

        public int ActiveCallsCount
        {
            get { return _activeCallsCount; }
            set { _activeCallsCount = value; OnPropertyChanged("ActiveCallsCount"); }
        }

        public int MonitoredExtensionsCount
        {
            get { return _monitoredExtensionsCount; }
            set { _monitoredExtensionsCount = value; OnPropertyChanged("MonitoredExtensionsCount"); }
        }

        public MainWindow()
        {
            InitializeComponent();
            InitializeCollections();
            DataContext = this;
            InitializeTimer();
            SetupExtensionsView();
        }

        private void InitializeCollections()
        {
            Extensions = new ObservableCollection<ExtensionInfo>();
            _monitoredLines = new Dictionary<int, TapiLine>();
            _extensionMap = new Dictionary<int, ExtensionInfo>();
        }

        private void SetupExtensionsView()
        {
            _extensionsViewSource = new CollectionViewSource { Source = Extensions };
            ExtensionsView = _extensionsViewSource.View;
            ExtensionsView.Filter = FilterExtensions;
            ExtensionsGrid.ItemsSource = ExtensionsView;
        }

        private bool FilterExtensions(object item)
        {
            if (string.IsNullOrEmpty(FilterText))
                return true;

            var extension = item as ExtensionInfo;
            if (extension == null)
                return false;

            return extension.ExtensionNumber.Contains(FilterText) ||
                   extension.LineName.Contains(FilterText) ||
                   extension.Status.Contains(FilterText) ||
                   extension.CallerId.Contains(FilterText) ||
                   extension.CalledNumber.Contains(FilterText);
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
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            TotalCallsToday = Extensions.Sum(e => e.CallCount);
            ActiveCallsCount = Extensions.Count(e => e.Status != "Idle" && e.Status != "Unknown");
            MonitoredExtensionsCount = Extensions.Count(e => e.IsMonitored);
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
                LogMessage($"Found {lines.Length} TAPI lines");

                // Clear existing extensions
                Extensions.Clear();
                _monitoredLines.Clear();
                _extensionMap.Clear();

                // Process all available lines
                foreach (var line in lines)
                {
                    try
                    {
                        // Create extension info
                        var extensionInfo = new ExtensionInfo(
                            GetExtensionNumber(line.Name),
                            line.Name
                        );

                        Extensions.Add(extensionInfo);
                        _extensionMap[line.Id] = extensionInfo;

                        LogMessage($"Found line: {line.Name} (ID: {line.Id})");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error processing line {line.Name}: {ex.Message}");
                    }
                }

                ConnectionStatus = "Connected";
                LogMessage($"Successfully connected. Found {Extensions.Count} extensions.");

                // Auto-monitor extensions (you can customize this logic)
                MonitorSelectedExtensions();
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Failed";
                LogMessage($"Error: {ex.Message}");
                throw;
            }
        }

        private string GetExtensionNumber(string lineName)
        {
            // Extract extension number from line name
            // Adjust this logic based on your NS1000 naming convention
            if (lineName.Contains("EXT"))
            {
                int startIndex = lineName.IndexOf("EXT");
                int endIndex = lineName.IndexOf(' ', startIndex);
                if (endIndex == -1) endIndex = lineName.Length;
                return lineName.Substring(startIndex, endIndex - startIndex);
            }

            // Try to extract numeric extension
            var numbers = System.Text.RegularExpressions.Regex.Match(lineName, @"\d+");
            if (numbers.Success)
                return numbers.Value;

            return lineName;
        }

        private void MonitorSelectedExtensions()
        {
            // Show extension selector dialog
            var selectorDialog = new ExtensionSelectorDialog(Extensions.ToList());
            if (selectorDialog.ShowDialog() == true)
            {
                var selectedExtensions = selectorDialog.SelectedExtensions;

                foreach (var line in _tapiManager.Lines)
                {
                    if (_extensionMap.TryGetValue(line.Id, out var extensionInfo))
                    {
                        if (selectedExtensions.Contains(extensionInfo.ExtensionNumber))
                        {
                            try
                            {
                                MonitorExtension(line);
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Failed to monitor line {line.Name}: {ex.Message}");
                            }
                        }
                    }
                }

                LogMessage($"Started monitoring {selectedExtensions.Count} extensions");
            }
            else
            {
                // If dialog was cancelled, monitor first 10 extensions by default
                var linesToMonitor = _tapiManager.Lines.Take(10);
                foreach (var line in linesToMonitor)
                {
                    try
                    {
                        MonitorExtension(line);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to monitor line {line.Name}: {ex.Message}");
                    }
                }
            }
        }

        private void MonitorExtension(TapiLine line)
        {
            try
            {
                // Open the line for monitoring
                line.Open(MediaModes.InteractiveVoice);

                // Subscribe to events
                line.NewCall += OnNewCall;
                line.CallStateChanged += OnCallStateChanged;
                line.CallInfoChanged += OnCallInfoChanged;

                // Start monitoring
                line.Monitor();

                // Store reference
                _monitoredLines[line.Id] = line;

                // Update extension info
                if (_extensionMap.TryGetValue(line.Id, out var extensionInfo))
                {
                    extensionInfo.IsMonitored = true;
                    extensionInfo.Status = "Idle";
                }

                LogMessage($"Monitoring started for: {line.Name}");
            }
            catch (Exception ex)
            {
                LogMessage($"Error monitoring line {line.Name}: {ex.Message}");
                throw;
            }
        }

        private void OnNewCall(object sender, NewCallEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var line = sender as TapiLine;
                if (line != null && _extensionMap.TryGetValue(line.Id, out var extensionInfo))
                {
                    extensionInfo.Status = "Ringing";
                    extensionInfo.CurrentCall = $"New Call - ID: {e.Call.Id}";
                    extensionInfo.CallerId = e.Call.CallerId ?? "Unknown";
                    extensionInfo.CalledNumber = e.Call.CalledId ?? "Unknown";
                    extensionInfo.IncrementCallCount();

                    LogMessage($"NEW CALL on {extensionInfo.ExtensionNumber}: From {extensionInfo.CallerId} to {extensionInfo.CalledNumber}");
                }
            });
        }

        private void OnCallStateChanged(object sender, CallStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var line = sender as TapiLine;
                if (line != null && _extensionMap.TryGetValue(line.Id, out var extensionInfo))
                {
                    var status = GetCallStateDescription(e.CallState);
                    extensionInfo.Status = status;
                    extensionInfo.CurrentCall = $"Call {e.Call.Id} - {status}";
                    extensionInfo.UpdateActivity();

                    LogMessage($"CALL STATE CHANGED on {extensionInfo.ExtensionNumber}: {e.CallState} - Call ID: {e.Call.Id}");

                    if (e.CallState == CallState.Idle || e.CallState == CallState.Disconnected)
                    {
                        extensionInfo.ClearCall();
                    }
                }
            });
        }

        private void OnCallInfoChanged(object sender, CallInfoChangeEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var line = sender as TapiLine;
                if (line != null && _extensionMap.TryGetValue(line.Id, out var extensionInfo))
                {
                    if (!string.IsNullOrEmpty(e.Call.CallerId))
                        extensionInfo.CallerId = e.Call.CallerId;
                    if (!string.IsNullOrEmpty(e.Call.CalledId))
                        extensionInfo.CalledNumber = e.Call.CalledId;

                    extensionInfo.UpdateActivity();
                    LogMessage($"CALL INFO CHANGED on {extensionInfo.ExtensionNumber}: From {extensionInfo.CallerId} to {extensionInfo.CalledNumber}");
                }
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
                // Unsubscribe and close all monitored lines
                foreach (var kvp in _monitoredLines)
                {
                    var line = kvp.Value;
                    line.NewCall -= OnNewCall;
                    line.CallStateChanged -= OnCallStateChanged;
                    line.CallInfoChanged -= OnCallInfoChanged;
                    line.Close();
                }

                _monitoredLines.Clear();

                if (_tapiManager != null)
                {
                    _tapiManager.Shutdown();
                    _tapiManager = null;
                }

                // Reset all extensions status
                foreach (var extension in Extensions)
                {
                    extension.Status = "Unknown";
                    extension.IsMonitored = false;
                    extension.ClearCall();
                }

                ConnectionStatus = "Disconnected";
                LogMessage("Disconnected from TAPI");
            }
            catch (Exception ex)
            {
                LogMessage($"Error during disconnect: {ex.Message}");
            }
        }

        private void MonitorExtension_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var extension = button?.DataContext as ExtensionInfo;

            if (extension != null && _tapiManager != null)
            {
                var line = _tapiManager.Lines.FirstOrDefault(l =>
                    _extensionMap.ContainsKey(l.Id) && _extensionMap[l.Id] == extension);

                if (line != null)
                {
                    if (extension.IsMonitored)
                    {
                        // Stop monitoring
                        if (_monitoredLines.ContainsKey(line.Id))
                        {
                            var monitoredLine = _monitoredLines[line.Id];
                            monitoredLine.NewCall -= OnNewCall;
                            monitoredLine.CallStateChanged -= OnCallStateChanged;
                            monitoredLine.CallInfoChanged -= OnCallInfoChanged;
                            monitoredLine.Close();
                            _monitoredLines.Remove(line.Id);
                            extension.IsMonitored = false;
                            extension.Status = "Not Monitored";
                            LogMessage($"Stopped monitoring: {extension.ExtensionNumber}");
                        }
                    }
                    else
                    {
                        // Start monitoring
                        try
                        {
                            MonitorExtension(line);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to monitor extension: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void ExportToCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"Extensions_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (var writer = new StreamWriter(saveDialog.FileName))
                    {
                        // Write headers
                        writer.WriteLine("Extension,Line Name,Status,Current Call,Caller ID,Called Number,Call Count,Last Activity,Monitored");

                        // Write data
                        foreach (var ext in Extensions)
                        {
                            writer.WriteLine($"{ext.ExtensionNumber},{ext.LineName},{ext.Status}," +
                                $"{ext.CurrentCall},{ext.CallerId},{ext.CalledNumber}," +
                                $"{ext.CallCount},{ext.LastActivity:yyyy-MM-dd HH:mm:ss},{ext.IsMonitored}");
                        }
                    }

                    MessageBox.Show("Export completed successfully!", "Export",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectExtensions_Click(object sender, RoutedEventArgs e)
        {
            if (_tapiManager == null || ConnectionStatus != "Connected")
            {
                MessageBox.Show("Please connect to TAPI first.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectorDialog = new ExtensionSelectorDialog(Extensions.ToList());
            if (selectorDialog.ShowDialog() == true)
            {
                var selectedExtensions = selectorDialog.SelectedExtensions;

                // Stop monitoring all currently monitored extensions
                foreach (var kvp in _monitoredLines.ToList())
                {
                    var line = kvp.Value;
                    line.NewCall -= OnNewCall;
                    line.CallStateChanged -= OnCallStateChanged;
                    line.CallInfoChanged -= OnCallInfoChanged;
                    line.Close();
                    _monitoredLines.Remove(kvp.Key);

                    if (_extensionMap.TryGetValue(kvp.Key, out var ext))
                    {
                        ext.IsMonitored = false;
                        ext.Status = "Not Monitored";
                    }
                }

                // Start monitoring selected extensions
                foreach (var line in _tapiManager.Lines)
                {
                    if (_extensionMap.TryGetValue(line.Id, out var extensionInfo))
                    {
                        if (selectedExtensions.Contains(extensionInfo.ExtensionNumber))
                        {
                            try
                            {
                                MonitorExtension(line);
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Failed to monitor line {line.Name}: {ex.Message}");
                            }
                        }
                    }
                }

                LogMessage($"Now monitoring {_monitoredLines.Count} extensions");
                UpdateStatistics();
            }
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
                MessageBox.Show($"Refresh failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();

                // Keep log size manageable
                if (LogTextBox.LineCount > 1000)
                {
                    var lines = LogTextBox.Text.Split('\n');
                    LogTextBox.Text = string.Join("\n", lines.Skip(lines.Length - 500));
                }
            });
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}