using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace AegisMint.Core.Services;

public static class Logger
{
    private static readonly string LogFilePath;
    private static readonly object LockObject = new();

    static Logger()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AegisMint",
            "Logs");

        Directory.CreateDirectory(logDir);
        // Use one log file per day for easier tracking and sharing
        LogFilePath = Path.Combine(logDir, $"aegismint_{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Info(string message, [CallerMemberName] string? caller = null)
    {
        WriteLog("INFO", message, caller);
    }

    public static void Warning(string message, [CallerMemberName] string? caller = null)
    {
        WriteLog("WARN", message, caller);
    }

    public static void Error(string message, Exception? ex = null, [CallerMemberName] string? caller = null)
    {
        var fullMessage = ex != null 
            ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
            : message;
        WriteLog("ERROR", fullMessage, caller);
    }

    public static void Debug(string message, [CallerMemberName] string? caller = null)
    {
        WriteLog("DEBUG", message, caller);
    }

    private static void WriteLog(string level, string message, string? caller)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] [{caller ?? "Unknown"}] {message}";

        // Write to Debug output (Visual Studio Output window)
        System.Diagnostics.Debug.WriteLine(logEntry);

        // Write to file
        lock (LockObject)
        {
            try
            {
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently fail if we can't write to log file
            }
        }
    }

    public static string GetLogFilePath() => LogFilePath;
}
