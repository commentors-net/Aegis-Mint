using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private async void OnDeleteMnemonic(object sender, RoutedEventArgs e)
    {
        try
        {
            var confirmResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete the genesis key?\n\nThis action cannot be undone!",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
            {
                Log("Delete operation cancelled");
                return;
            }

            var result = await _client.DeleteMnemonicAsync();
            if (result.Success)
            {
                Log("✓ Genesis key deleted successfully");
                Log("  You can now set a new genesis key");
            }
            else
            {
                Log($"✗ Failed to delete genesis key ({result.StatusCode}): {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Log($"Delete mnemonic error: {ex.Message}");
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

    private async void OnExportShares(object sender, RoutedEventArgs e)
    {
        try
        {
            // First unlock to get the mnemonic
            var unlockResult = await _client.UnlockForDevelopmentAsync();
            if (!unlockResult.Success)
            {
                Log($"⚠ Failed to unlock device: {unlockResult.ErrorMessage}");
                Log("  Export requires device to be unlocked first.");
                return;
            }

            var mnemonicResult = await _client.GetMnemonicAsync();
            if (!mnemonicResult.Success || mnemonicResult.Value is null)
            {
                Log($"✗ Cannot export shares: {mnemonicResult.ErrorMessage}");
                return;
            }

            var mnemonic = mnemonicResult.Value.Mnemonic ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                Log("✗ No mnemonic found to export");
                return;
            }

            // Generate 8 shares with 3 threshold
            var secretBytes = System.Text.Encoding.UTF8.GetBytes(mnemonic);
            var shamir = new AegisMint.Core.Security.ShamirSecretSharingService();
            var shares = shamir.Split(secretBytes, threshold: 3, shareCount: 8);

            // Select folder to save 8 separate share files
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to save 8 share files",
                ShowNewFolderButton = true
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var savedFiles = new List<string>();

                foreach (var share in shares)
                {
                    var shareData = new
                    {
                        Version = 1,
                        Timestamp = DateTimeOffset.UtcNow,
                        Threshold = 3,
                        TotalShares = 8,
                        ShareId = share.Id,
                        ShareValue = share.Value
                    };

                    var fileName = $"aegis-share-{share.Id}-{timestamp}.json";
                    var filePath = Path.Combine(folderDialog.SelectedPath, fileName);
                    
                    var json = System.Text.Json.JsonSerializer.Serialize(shareData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(filePath, json);
                    savedFiles.Add(fileName);
                }
                
                Log($"✓ Exported 8 separate share files to: {folderDialog.SelectedPath}");
                Log($"  Files: {string.Join(", ", savedFiles.Take(3))}...");
                Log($"  Threshold: 3 shares required for recovery");
                Log($"  IMPORTANT: Distribute these files to different secure locations!");
            }
            else
            {
                Log("Export cancelled");
            }
        }
        catch (Exception ex)
        {
            Log($"Export error: {ex.Message}");
        }
    }

    private readonly List<AegisMint.Core.Models.ShamirShare> _loadedShares = new();

    private void OnImportRestore(object sender, RoutedEventArgs e)
    {
        // Show the import section
        ImportSection.Visibility = Visibility.Visible;
        _loadedShares.Clear();
        UpdateImportUI();
        Log("Import mode activated. Load at least 3 share files to restore.");
    }

    private async void OnLoadShareFile(object sender, RoutedEventArgs e)
    {
        try
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Share File",
                Multiselect = true
            };

            if (openDialog.ShowDialog() != true)
            {
                return;
            }

            foreach (var fileName in openDialog.FileNames)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(fileName);
                    var shareData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

                    if (!shareData.TryGetProperty("ShareId", out var idElement) || 
                        !shareData.TryGetProperty("ShareValue", out var valueElement))
                    {
                        Log($"✗ Invalid share file format: {Path.GetFileName(fileName)}");
                        continue;
                    }

                    var id = idElement.GetByte();
                    var value = valueElement.GetString() ?? string.Empty;

                    // Check for duplicates
                    if (_loadedShares.Any(s => s.Id == id))
                    {
                        Log($"⚠ Share #{id} already loaded, skipping: {Path.GetFileName(fileName)}");
                        continue;
                    }

                    _loadedShares.Add(new AegisMint.Core.Models.ShamirShare(id, value));
                    Log($"✓ Loaded share #{id} from: {Path.GetFileName(fileName)}");
                }
                catch (Exception ex)
                {
                    Log($"✗ Error loading {Path.GetFileName(fileName)}: {ex.Message}");
                }
            }

            UpdateImportUI();
        }
        catch (Exception ex)
        {
            Log($"Load error: {ex.Message}");
        }
    }

    private void OnClearShares(object sender, RoutedEventArgs e)
    {
        _loadedShares.Clear();
        UpdateImportUI();
        Log("All loaded shares cleared");
    }

    private async void OnRestoreFromShares(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_loadedShares.Count < 3)
            {
                Log($"✗ Cannot restore: only {_loadedShares.Count} shares loaded (minimum 3 required)");
                return;
            }

            // Use first 3 shares for reconstruction
            var selectedShares = _loadedShares.Take(3).ToList();
            var shamir = new AegisMint.Core.Security.ShamirSecretSharingService();
            var reconstructedBytes = shamir.Combine(selectedShares, threshold: 3);
            var reconstructedMnemonic = System.Text.Encoding.UTF8.GetString(reconstructedBytes);

            Log($"✓ Successfully reconstructed mnemonic from shares: {string.Join(", ", selectedShares.Select(s => $"#{s.Id}"))}");
            Log($"  Mnemonic: {reconstructedMnemonic}");
            
            // Auto-fill the mnemonic input
            MnemonicInput.Text = reconstructedMnemonic;
            
            // Hide import section and clear shares
            ImportSection.Visibility = Visibility.Collapsed;
            _loadedShares.Clear();
            
            Log($"  Ready to restore! Click 'Set Genesis Key' to complete the restoration.");
        }
        catch (Exception ex)
        {
            Log($"Restore error: {ex.Message}");
        }
    }

    private void UpdateImportUI()
    {
        var count = _loadedShares.Count;
        ImportStatusText.Text = $"Loaded: {count}/8 shares (Min 3 required)";
        
        var shareList = _loadedShares.Select(s => $"✓ Share #{s.Id} - {s.Value.Substring(0, Math.Min(32, s.Value.Length))}...").ToList();
        LoadedSharesList.ItemsSource = shareList;
        
        RestoreButton.IsEnabled = count >= 3;
        
        if (count >= 3)
        {
            ImportStatusText.Text += " ✓ Ready to restore!";
            ImportStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
        }
        else if (count > 0)
        {
            ImportStatusText.Text += $" (Need {3 - count} more)";
            ImportStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
        }
        else
        {
            ImportStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }
    }

    private async void OnGetMnemonic(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.GetMnemonicAsync();
            if (result.Success && result.Value is not null)
            {
                var mnemonic = result.Value.Mnemonic ?? "(empty)";
                var wordCount = mnemonic.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                Log($"✓ Mnemonic retrieved ({wordCount} words):");
                Log($"  {mnemonic}");
                Log("  IMPORTANT: Keep this phrase secure!");
            }
            else if (result.StatusCode == 423)
            {
                Log("⚠ Mint is locked. Obtain governance approvals or use dev unlock.");
            }
            else
            {
                Log($"✗ Mnemonic request failed ({result.StatusCode}): {result.ErrorMessage}");
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
