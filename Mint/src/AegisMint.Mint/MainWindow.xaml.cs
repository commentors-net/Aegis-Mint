using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using AegisMint.Mint.Services;

namespace AegisMint.Mint;

public partial class MainWindow : Window
{
    private string? _htmlPath;
    private readonly VaultManager _vaultManager;
    private EthereumService? _ethereumService;
    private string _currentNetwork = "sepolia"; // default

    public MainWindow(string network, string rpcUrl)
    {
        InitializeComponent();
        _vaultManager = new VaultManager();
        Loaded += OnLoaded;
        
        // Add F12 keyboard shortcut to open DevTools
        PreviewKeyDown += OnPreviewKeyDown;
        
        // Initialize EthereumService with selected network
        _currentNetwork = network;
        _ethereumService = new EthereumService(rpcUrl);
        
        // Update window title to show selected network
        Title = $"Aegis Mint - {network.ToUpper()}";
    }

    private void InitializeEthereumService(string network)
    {
        var rpcUrl = network switch
        {
            "localhost" => "http://127.0.0.1:8545",
            "mainnet" => "https://eth.llamarpc.com",
            "sepolia" => "https://ethereum-sepolia-rpc.publicnode.com",
            _ => "https://ethereum-sepolia-rpc.publicnode.com"
        };
        
        _ethereumService = new EthereumService(rpcUrl);
        _currentNetwork = network;
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // F12 to open/close DevTools
        if (e.Key == System.Windows.Input.Key.F12 && MainWebView?.CoreWebView2 != null)
        {
            MainWebView.CoreWebView2.OpenDevToolsWindow();
            e.Handled = true;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            OverlayStatus.Text = "Loading UI...";
            _htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "aegis_mint.html");

            if (!File.Exists(_htmlPath))
            {
                OverlayStatus.Text = "Missing UI asset: aegis_mint_main_screen.html";
                return;
            }

            await MainWebView.EnsureCoreWebView2Async();
            ConfigureWebView();
            MainWebView.Source = new Uri(_htmlPath);
        }
        catch (Exception ex)
        {
            OverlayStatus.Text = $"Failed to load UI: {ex.Message}";
        }
    }

    private void ConfigureWebView()
    {
        if (MainWebView.CoreWebView2 is null)
        {
            return;
        }

        var settings = MainWebView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = true; // Enable for debugging
        settings.IsStatusBarEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsScriptEnabled = true; // Explicitly enable JavaScript

        MainWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        MainWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Overlay.Visibility = Visibility.Collapsed;
            await SendToWebAsync("host-info", new { host = "Aegis Mint WPF", version = "1.0" });
        }
        else
        {
            OverlayStatus.Text = $"Navigation failed: {e.WebErrorStatus}";
            Overlay.Visibility = Visibility.Visible;
        }
    }

    private async void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await SendToWebAsync("navigation-blocked", new { uri = e.Uri });
        }
    }

    private async void OnWebViewInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            OverlayStatus.Text = $"WebView init failed: {e.InitializationException?.Message}";
            return;
        }

        await SendToWebAsync("bridge-ready", new { ready = true });
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson);
            if (message?.type is null)
            {
                return;
            }

            switch (message.type)
            {
                case "bridge-ready":
                    await SendToWebAsync("host-info", new { host = "Aegis Mint WPF", version = "1.0" });
                    await CheckExistingVaults();
                    break;
                case "log":
                    HandleLog(message.payload);
                    break;
                case "network-changed":
                    HandleNetworkChange(message.payload);
                    break;
                case "generate-treasury":
                    await HandleGenerateTreasury();
                    break;
                case "mint-submit":
                    await HandleMintSubmit(message.payload);
                    break;
                case "reset":
                    _vaultManager.ClearVaults();
                    await SendToWebAsync("reset-ack", new { ok = true });
                    break;
            }
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new { message = ex.Message });
        }
    }

    private Task SendToWebAsync(string type, object payload)
    {
        if (MainWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        var json = JsonSerializer.Serialize(new { type, payload });
        var script = $"window.receiveHostMessage && window.receiveHostMessage({json});";
        return MainWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void HandleLog(JsonElement? payload)
    {
        try
        {
            var level = payload.HasValue && payload.Value.TryGetProperty("level", out var lvl) ? lvl.GetString() ?? "info" : "info";
            var message = payload.HasValue && payload.Value.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                System.Diagnostics.Debug.WriteLine($"[WEBVIEW:{level}] {message}");
            }
        }
        catch
        {
            // Swallow logging errors to avoid crashing the bridge
        }
    }

    private void HandleNetworkChange(JsonElement? payload)
    {
        try
        {
            if (payload.HasValue && payload.Value.TryGetProperty("network", out var networkProp))
            {
                var network = networkProp.GetString() ?? "sepolia";
                InitializeEthereumService(network);
                System.Diagnostics.Debug.WriteLine($"[NETWORK] Switched to: {network}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NETWORK] Error changing network: {ex.Message}");
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async Task CheckExistingVaults()
    {
        try
        {
            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            decimal? balance = null;
            if (_ethereumService != null && !string.IsNullOrWhiteSpace(treasuryAddress))
            {
                try
                {
                    balance = await _ethereumService.GetBalanceAsync(treasuryAddress);
                }
                catch
                {
                    balance = null;
                }
            }
            
            await SendToWebAsync("vault-status", new 
            { 
                hasTreasury = treasuryAddress != null,
                treasuryAddress = treasuryAddress,
                balance = balance
            });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new { message = $"Error loading vaults: {ex.Message}" });
        }
    }

    private async Task HandleGenerateTreasury()
    {
        try
        {
            // Check if treasury already exists
            if (_vaultManager.HasTreasury())
            {
                await SendToWebAsync("host-error", new 
                { 
                    message = "Treasury already exists. Use Reset to clear existing vaults." 
                });
                return;
            }

            // Generate new treasury
            var address = _vaultManager.GenerateTreasury();
            
            await SendToWebAsync("treasury-generated", new 
            { 
                address = address,
                status = "Treasury generated and secured"
            });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new 
            { 
                message = $"Failed to generate Treasury: {ex.Message}" 
            });
        }
    }

    private async Task HandleMintSubmit(JsonElement? payload)
    {
        try
        {
            // Step 1: Validate mint payload structure
            var (ok, error) = ValidateMintPayload(payload);
            if (!ok)
            {
                await SendToWebAsync("validation-result", new { ok = false, message = error ?? "Validation failed" });
                return;
            }

            // Step 2: Validate treasury balance on blockchain
            if (_ethereumService != null && payload.HasValue && payload.Value.TryGetProperty("treasuryAddress", out var addrProp))
            {
                var treasuryAddress = addrProp.GetString();
                if (!string.IsNullOrWhiteSpace(treasuryAddress))
                {
                    try
                    {
                        var balance = await _ethereumService.GetBalanceAsync(treasuryAddress);
                        if (balance <= 0)
                        {
                            await SendToWebAsync("validation-result", new 
                            { 
                                ok = false, 
                                message = $"Treasury address has 0 ETH balance on {_currentNetwork}. Fund the address before minting." 
                            });
                            return;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[VALIDATION] Treasury balance: {balance} ETH");
                    }
                    catch (Exception ex)
                    {
                        await SendToWebAsync("validation-result", new 
                        { 
                            ok = false, 
                            message = $"Failed to check balance on {_currentNetwork}: {ex.Message}" 
                        });
                        return;
                    }
                }
            }

            // Step 3: All validations passed, proceed with minting
            await SendToWebAsync("validation-result", new { ok = true, message = "Configuration validated. Deploying token..." });
            await SendToWebAsync("mint-received", new { ok = true, received = payload });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new { message = $"Mint failed: {ex.Message}" });
        }
    }

    private (bool ok, string? error) ValidateMintPayload(JsonElement? payload)
    {
        if (payload is null) return (false, "No payload received.");

        string GetString(string name)
        {
            return payload.Value.TryGetProperty(name, out var prop) ? prop.GetString() ?? string.Empty : string.Empty;
        }

        bool TryGetDecimal(string name, out decimal value)
        {
            value = 0m;
            if (!payload.Value.TryGetProperty(name, out var prop)) return false;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out value)) return true;
            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out value)) return true;
            return false;
        }

        var tokenName = GetString("tokenName");
        var treasuryAddress = GetString("treasuryAddress");

        if (string.IsNullOrWhiteSpace(tokenName)) return (false, "Token Name is required.");
        if (string.IsNullOrWhiteSpace(treasuryAddress)) return (false, "Treasury address is required.");

        if (!payload.Value.TryGetProperty("tokenDecimals", out var decProp) || !decProp.TryGetInt32(out var decimals) || decimals < 0 || decimals > 36)
        {
            return (false, "Token decimals must be between 0 and 36.");
        }

        if (!payload.Value.TryGetProperty("govShares", out var sharesProp) || !sharesProp.TryGetInt32(out var shares) || shares <= 0)
        {
            return (false, "Number of shares must be greater than zero.");
        }

        if (!payload.Value.TryGetProperty("govThreshold", out var thresholdProp) || !thresholdProp.TryGetInt32(out var threshold) || threshold <= 0)
        {
            return (false, "Threshold must be greater than zero.");
        }

        if (threshold > shares)
        {
            return (false, "Threshold must be less than or equal to number of shares.");
        }

        if (!TryGetDecimal("tokenSupply", out var supply) || supply <= 0)
        {
            return (false, "Token supply must be greater than zero.");
        }

        if (!TryGetDecimal("treasuryEth", out var treasuryEth) || treasuryEth <= 0)
        {
            return (false, "Treasury ETH must be greater than zero.");
        }

        return (true, null);
    }

    private record BridgeMessage(string? type, JsonElement? payload);
}
