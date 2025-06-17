using System.Windows;

namespace ExtensionMonitor
{
    public partial class RangeSelectionDialog : Window
    {
        public string StartExtension { get; set; }
        public string EndExtension { get; set; }

        public RangeSelectionDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}