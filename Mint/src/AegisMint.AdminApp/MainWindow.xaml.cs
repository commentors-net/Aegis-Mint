using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using AegisMint.Client;

namespace AegisMint.AdminApp;

public partial class MainWindow : Window, IDisposable
{
    private readonly MintClient _client;
    private readonly MintClientOptions _options;

    public MainWindow()
    {
        InitializeComponent();
        _options = LoadOptions();
        _client = MintClient.CreateDefault(_options);
        BaseAddressText.Text = $"Service Pipe: {_options.PipeName}";
        Log("Admin UI ready. Use Unlock (dev) before requesting the mnemonic.");
    }

    private MintClientOptions LoadOptions()
    {
        var options = new MintClientOptions();
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            return options;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("Service", out var serviceNode) &&
                serviceNode.TryGetProperty("PipeName", out var pipeNameNode))
            {
                var pipeName = pipeNameNode.GetString();
                if (!string.IsNullOrWhiteSpace(pipeName))
                {
                    options.PipeName = pipeName;
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to read appsettings.json: {ex.Message}");
        }

        return options;
    }

    private async void OnCheckStatus(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.HasMnemonicAsync();
            if (result.Success && result.Value is not null)
            {
                if (result.Value.HasMnemonic)
                {
                    Log($"✓ Genesis key is configured for device: {result.Value.DeviceId}");
                }
                else
                {
                    Log($"⚠ No genesis key found. Device ID: {result.Value.DeviceId}");
                    Log("  Use 'Set Genesis Key' to configure the mnemonic.");
                }
            }
            else
            {
                Log($"Status check failed ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Status check error: {ex.Message}");
        }
    }

    private async void OnSetMnemonic(object sender, RoutedEventArgs e)
    {
        try
        {
            var mnemonic = MnemonicInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                Log("⚠ Please enter a 12-word mnemonic phrase");
                return;
            }

            var result = await _client.SetMnemonicAsync(mnemonic);
            if (result.Success && result.Value is not null)
            {
                Log($"✓ {result.Value.Message}");
                if (result.Value.Shares is not null && result.Value.Shares.Count > 0)
                {
                    Log($"  Shamir shares generated:");
                    foreach (var share in result.Value.Shares)
                    {
                        var valuePreview = share.Value.Length > 32 ? share.Value.Substring(0, 32) + "..." : share.Value;
                        Log($"    Share #{share.Id}: {valuePreview}");
                    }
                    Log($"  IMPORTANT: Save these shares securely for recovery!");
                }
                MnemonicInput.Clear();
            }
            else
            {
                Log($"✗ Failed to set genesis key ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Set mnemonic error: {ex.Message}");
        }
    }

    private async void OnPing(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.PingAsync();
            if (result.Success && result.Value is not null)
            {
                Log($"Ping ok at {result.Value.Utc:O}");
            }
            else
            {
                Log($"Ping failed ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Ping error: {ex.Message}");
        }
    }

    private async void OnDeviceInfo(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.GetDeviceInfoAsync();
            if (result.Success && result.Value is not null)
            {
                var device = result.Value.Device;
                Log($"DeviceId: {device.DeviceId}, Shares: {device.RecoveryThreshold}/{device.ShareCount}, Governance quorum: {device.GovernanceQuorum}, Unlock window: {device.UnlockWindowMinutes} mins");
            }
            else
            {
                Log($"Device info failed ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Device info error: {ex.Message}");
        }
    }

    private async void OnDevUnlock(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.UnlockForDevelopmentAsync();
            if (result.Success)
            {
                Log("Unlocked for development (temporary).");
            }
            else
            {
                Log($"Unlock refused ({result.StatusCode}). Enable Service.AllowDevBypassUnlock in server config if this is a dev box.");
            }
        }
        catch (Exception ex)
        {
            Log($"Unlock error: {ex.Message}");
        }
    }

    private async void OnLock(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.LockAsync();
            if (result.Success)
            {
                Log("Device locked.");
            }
            else
            {
                Log($"Lock failed ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Lock error: {ex.Message}");
        }
    }

    private async void OnGetMnemonic(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.GetMnemonicAsync();
            if (result.Success && result.Value is not null)
            {
                // We intentionally do not print the mnemonic to avoid accidental exposure.
                var wordCount = result.Value.Mnemonic?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
                Log($"Mnemonic retrieved securely ({wordCount} words, value hidden).");
            }
            else if (result.StatusCode == 423)
            {
                Log("Mint is locked. Obtain governance approvals or use dev unlock.");
            }
            else
            {
                Log($"Mnemonic request failed ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Mnemonic error: {ex.Message}");
        }
    }

    private async void OnRefreshLogs(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.GetRecentLogsAsync();
            if (result.Success && result.Value is not null)
            {
                LogsBox.Text = string.Join(Environment.NewLine, result.Value.Lines);
                LogsBox.ScrollToEnd();
                Log("Logs refreshed.");
            }
            else
            {
                Log($"Logs unavailable ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Logs error: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        OutputBox.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        OutputBox.ScrollToEnd();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Dispose();
    }
}
