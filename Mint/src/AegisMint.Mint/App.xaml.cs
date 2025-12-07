using System.Configuration;
using System.Data;
using System.Windows;

namespace AegisMint.Mint;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Prevent automatic shutdown when dialog closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Show network selection dialog
        var networkDialog = new NetworkSelectionWindow();
        var result = networkDialog.ShowDialog();

        if (result == true)
        {
            try
            {
                // User selected a network, create and show main window
                var mainWindow = new MainWindow(networkDialog.SelectedNetwork, networkDialog.RpcUrl);
                
                // Change shutdown mode to close when main window closes
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow; // Set as the application's main window
                
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error creating main window: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Startup Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
        else
        {
            // User cancelled, exit application
            Shutdown();
        }
    }
}
