using System;
using System.Windows;
using System.Windows.Threading;
using AegisMint.Core.Services;

namespace AegisMint.RecoverShares;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Logger.Info("Starting AegisMint.RecoverShares");

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled UI exception", e.Exception);

        var result = MessageBox.Show(
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
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}\n\nLog file: {Logger.GetLogFilePath()}",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
