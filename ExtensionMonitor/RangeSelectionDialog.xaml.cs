using System.ComponentModel;
using System.Windows;

namespace ExtensionMonitor
{
    public partial class RangeSelectionDialog : Window, INotifyPropertyChanged
    {
        private string _startExtension = "";
        private string _endExtension = "";

        public string StartExtension
        {
            get { return _startExtension; }
            set
            {
                _startExtension = value;
                OnPropertyChanged(nameof(StartExtension));
            }
        }

        public string EndExtension
        {
            get { return _endExtension; }
            set
            {
                _endExtension = value;
                OnPropertyChanged(nameof(EndExtension));
            }
        }

        public RangeSelectionDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(StartExtension) || string.IsNullOrWhiteSpace(EndExtension))
            {
                MessageBox.Show("Please enter both start and end extension numbers.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(StartExtension, out int start) || !int.TryParse(EndExtension, out int end))
            {
                MessageBox.Show("Please enter valid numeric extension numbers.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (start > end)
            {
                MessageBox.Show("Start extension must be less than or equal to end extension.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}