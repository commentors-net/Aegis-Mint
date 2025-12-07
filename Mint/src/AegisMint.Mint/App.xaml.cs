using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AegisMint.Core.Services;

namespace AegisMint.Mint;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        // Handle unhandled exceptions
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Logger.Info("Application starting...");

        // Prevent automatic shutdown when dialog closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Show network selection dialog
        var networkDialog = new NetworkSelectionWindow();
        var result = networkDialog.ShowDialog();

        if (result == true)
        {
            try
            {
                Logger.Info($"User selected network: {networkDialog.SelectedNetwork}");

                // User selected a network, create and show main window
                var mainWindow = new MainWindow(networkDialog.SelectedNetwork, networkDialog.RpcUrl);
                
                // Change shutdown mode to close when main window closes
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow = mainWindow; // Set as the application's main window
                
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create main window", ex);
                System.Windows.MessageBox.Show($"Error creating main window: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Startup Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                Shutdown();
            }
        }
        else
        {
            Logger.Info("User cancelled network selection");
            // User cancelled, exit application
            Shutdown();
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled UI exception", e.Exception);
        
        var result = System.Windows.MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nLog file: {Logger.GetLogFilePath()}\n\nContinue running?",
            "Error",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        e.Handled = result == MessageBoxResult.Yes;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Error("Unhandled domain exception", ex);
            System.Windows.MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}\n\nLog file: {Logger.GetLogFilePath()}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
