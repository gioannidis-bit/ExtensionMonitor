using System.Windows;

namespace ExtensionMonitor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Handle any unhandled exceptions
            this.DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"An unexpected error occurred:\n{ex.Exception.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
        }
    }
}