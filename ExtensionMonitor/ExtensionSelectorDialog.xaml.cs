using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ExtensionMonitor
{
    public partial class ExtensionSelectorDialog : Window
    {
        public class SelectableExtension : INotifyPropertyChanged
        {
            private bool _monitor;

            public string ExtensionNumber { get; set; }
            public string LineName { get; set; }
            public string Status { get; set; }

            public bool Monitor
            {
                get { return _monitor; }
                set
                {
                    _monitor = value;
                    OnPropertyChanged("Monitor");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private ObservableCollection<SelectableExtension> _selectableExtensions;
        private CollectionViewSource _viewSource;

        public List<string> SelectedExtensions { get; private set; }

        public ExtensionSelectorDialog(List<ExtensionInfo> extensions)
        {
            InitializeComponent();

            _selectableExtensions = new ObservableCollection<SelectableExtension>();

            foreach (var ext in extensions)
            {
                _selectableExtensions.Add(new SelectableExtension
                {
                    ExtensionNumber = ext.ExtensionNumber,
                    LineName = ext.LineName,
                    Status = ext.Status,
                    Monitor = ext.IsMonitored
                });
            }

            _viewSource = new CollectionViewSource { Source = _selectableExtensions };
            _viewSource.View.Filter = FilterExtensions;
            ExtensionsGrid.ItemsSource = _viewSource.View;
        }

        private bool FilterExtensions(object item)
        {
            if (string.IsNullOrEmpty(FilterTextBox.Text))
                return true;

            var extension = item as SelectableExtension;
            if (extension == null)
                return false;

            var filter = FilterTextBox.Text.ToLower();
            return extension.ExtensionNumber.ToLower().Contains(filter) ||
                   extension.LineName.ToLower().Contains(filter);
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewSource?.View?.Refresh();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ext in _selectableExtensions)
            {
                if (_viewSource.View.Contains(ext))
                    ext.Monitor = true;
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var ext in _selectableExtensions)
            {
                if (_viewSource.View.Contains(ext))
                    ext.Monitor = false;
            }
        }

        private void SelectRange_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RangeSelectionDialog();
            if (dialog.ShowDialog() == true)
            {
                int startNum, endNum;
                if (int.TryParse(dialog.StartExtension, out startNum) &&
                    int.TryParse(dialog.EndExtension, out endNum))
                {
                    foreach (var ext in _selectableExtensions)
                    {
                        // Extract numeric part from extension number
                        var numMatch = System.Text.RegularExpressions.Regex.Match(ext.ExtensionNumber, @"\d+");
                        if (numMatch.Success)
                        {
                            int extNum;
                            if (int.TryParse(numMatch.Value, out extNum))
                            {
                                if (extNum >= startNum && extNum <= endNum)
                                {
                                    ext.Monitor = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedExtensions = _selectableExtensions
                .Where(x => x.Monitor)
                .Select(x => x.ExtensionNumber)
                .ToList();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}