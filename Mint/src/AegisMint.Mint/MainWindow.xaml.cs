using System;
using System.IO;
using System.Collections.Generic;
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
    private readonly EthereumService _ethereumService;
    private readonly Dictionary<string, string> _rpcMap = new()
    {
        { "mainnet", "https://ethereum.publicnode.com" },
        { "sepolia", "https://ethereum-sepolia-rpc.publicnode.com" }
    };
    private string _currentNetwork = "mainnet";

    public MainWindow()
    {
        InitializeComponent();
        _vaultManager = new VaultManager();
        _ethereumService = new EthereumService(_rpcMap[_currentNetwork]);
        Loaded += OnLoaded;
        
        // Add F12 keyboard shortcut to open DevTools
        PreviewKeyDown += OnPreviewKeyDown;
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

        MainWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
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
                    await SendToWebAsync("host-info", new { host = "Aegis Mint WPF", version = "1.0", network = _currentNetwork });
                    await CheckExistingVaults();
                    break;
                case "log":
                    HandleLog(message.payload);
                    break;
                case "network-changed":
                    ApplyNetwork(message.payload);
                    break;
                case "generate-engine":
                    await HandleGenerateEngine();
                    break;
                case "generate-treasury":
                    await HandleGenerateTreasury();
                    break;
                case "mint-submit":
                    await SendToWebAsync("mint-received", new { ok = true, received = message.payload });
                    break;
                case "validate":
                    await HandleValidateConfiguration();
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
    private void ApplyNetwork(JsonElement? payload)
    {
        try
        {
            var network = payload.HasValue && payload.Value.TryGetProperty("network", out var netProp)
                ? netProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(network))
            {
                return;
            }

            if (_rpcMap.TryGetValue(network, out var rpc))
            {
                _ethereumService.SetRpcUrl(rpc);
                _currentNetwork = network;
                _ = SendToWebAsync("network-updated", new { network = _currentNetwork });
            }
            else
            {
                _ = SendToWebAsync("host-error", new { message = $"Unknown network: {network}" });
            }
        }
        catch (Exception ex)
        {
            _ = SendToWebAsync("host-error", new { message = $"Network change failed: {ex.Message}" });
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
            var engineAddress = _vaultManager.GetEngineAddress();
            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            
            // Send combined vault status
            await SendToWebAsync("vault-status", new 
            { 
                hasEngine = engineAddress != null,
                engineAddress = engineAddress,
                hasTreasury = treasuryAddress != null,
                treasuryAddress = treasuryAddress
            });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new { message = $"Error loading vaults: {ex.Message}" });
        }
    }

    private async Task HandleGenerateEngine()
    {
        try
        {
            // Check if engine already exists
            if (_vaultManager.HasEngine())
            {
                await SendToWebAsync("host-error", new 
                { 
                    message = "Engine already exists. Use Reset to clear existing vaults." 
                });
                return;
            }

            // Generate new engine
            var address = _vaultManager.GenerateEngine();
            
            await SendToWebAsync("engine-generated", new 
            { 
                address = address,
                status = "Engine generated and secured"
            });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("host-error", new 
            { 
                message = $"Failed to generate Engine: {ex.Message}" 
            });
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

    private async Task HandleValidateConfiguration()
    {
        try
        {
            // Check if Engine exists
            var engineAddress = _vaultManager.GetEngineAddress();
            if (engineAddress == null)
            {
                await SendToWebAsync("validation-result", new 
                { 
                    ok = false,
                    message = "Engine must be generated first.",
                    canMint = false
                });
                return;
            }

            // Check if Treasury exists
            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            if (treasuryAddress == null)
            {
                await SendToWebAsync("validation-result", new 
                { 
                    ok = false,
                    message = "Treasury must be generated first.",
                    canMint = false
                });
                return;
            }

            // Validate RPC connection
            var isConnected = await _ethereumService.ValidateConnectionAsync();
            if (!isConnected)
            {
                await SendToWebAsync("validation-result", new 
                { 
                    ok = false,
                    message = "Failed to connect to Ethereum network. Please check your internet connection.",
                    canMint = false
                });
                return;
            }

            // Get network name
            var networkName = await _ethereumService.GetNetworkNameAsync();

            // Check Engine balance
            var balance = await _ethereumService.GetBalanceAsync(engineAddress);
            var hasSufficientBalance = balance >= 0.01m;

            if (!hasSufficientBalance)
            {
                await SendToWebAsync("validation-result", new 
                { 
                    ok = false,
                    message = $"Engine address has insufficient balance.\n\nAddress: {engineAddress}\nCurrent Balance: {balance:F6} ETH\nRequired: 0.01 ETH minimum\n\nPlease fund this address with ETH to cover gas fees for deployment.",
                    canMint = false,
                    balance = balance,
                    engineAddress = engineAddress,
                    network = networkName
                });
                return;
            }

            // All validations passed
            await SendToWebAsync("validation-result", new 
            { 
                ok = true,
                message = $"Configuration validated successfully!\n\nNetwork: {networkName}\nEngine Balance: {balance:F6} ETH\nReady to mint token.",
                canMint = true,
                balance = balance,
                engineAddress = engineAddress,
                network = networkName
            });
        }
        catch (Exception ex)
        {
            await SendToWebAsync("validation-result", new 
            { 
                ok = false,
                message = $"Validation failed: {ex.Message}",
                canMint = false
            });
        }
    }

    private record BridgeMessage(string? type, JsonElement? payload);
}
