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

        var vault = new VaultManager();
        var lastNetwork = vault.GetLastNetwork();
        if (string.IsNullOrWhiteSpace(lastNetwork))
        {
            lastNetwork = "sepolia";
        }

        var rpcUrl = ResolveRpcUrl(lastNetwork);

        try
        {
            Logger.Info($"Launching with network: {lastNetwork}");

            var mainWindow = new MainWindow(lastNetwork, rpcUrl);

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = mainWindow;
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

    private static string ResolveRpcUrl(string network)
    {
        return network switch
        {
            "localhost" => "http://127.0.0.1:8545",
            "mainnet" => "https://eth.llamarpc.com",
            "sepolia" => "https://ethereum-sepolia-rpc.publicnode.com",
            _ => "https://ethereum-sepolia-rpc.publicnode.com"
        };
    }
}
