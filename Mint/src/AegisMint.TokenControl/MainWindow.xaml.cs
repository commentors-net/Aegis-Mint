using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AegisMint.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AegisMint.TokenControl;

public partial class MainWindow : Window
{
    private string? _htmlPath;
    private readonly ContractArtifactLoader _artifactLoader = new();
    private ContractArtifacts? _tokenArtifacts;
    private readonly VaultManager _vaultManager = new();
    private string _currentNetwork = "sepolia";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        Title = "Aegis Token Control";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadInitialState();
        LoadContractArtifacts();
        await InitializeWebViewAsync();
    }

    private void LoadInitialState()
    {
        var lastNetwork = _vaultManager.GetLastNetwork();
        if (!string.IsNullOrWhiteSpace(lastNetwork))
        {
            _currentNetwork = lastNetwork.Trim().ToLowerInvariant();
        }
        Title = $"Aegis Token Control - {_currentNetwork.ToUpperInvariant()}";
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F12 && MainWebView?.CoreWebView2 != null)
        {
            MainWebView.CoreWebView2.OpenDevToolsWindow();
            e.Handled = true;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            Logger.Info("Initializing WebView (TokenControl)");
            OverlayStatus.Text = "Loading UI...";
            MainWebView.CreationProperties = BuildWebViewProperties();
            _htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "aegis_token_control.html");

            if (!File.Exists(_htmlPath))
            {
                Logger.Error($"UI asset not found: {_htmlPath}");
                OverlayStatus.Text = "Missing UI asset: aegis_token_control.html";
                return;
            }

            await MainWebView.EnsureCoreWebView2Async();
            ConfigureWebView();
            MainWebView.Source = new Uri(_htmlPath);
            Logger.Info("WebView initialized successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize WebView", ex);
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
        settings.AreDevToolsEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsScriptEnabled = true;

        MainWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        MainWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Overlay.Visibility = Visibility.Collapsed;
            await SendToWebAsync("host-info", new { host = "Aegis Token Control WPF", version = "1.0", network = _currentNetwork });
            await SendVaultStatusAsync();
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
                case "log":
                    HandleLog(message.payload);
                    break;
                case "network-changed":
                    HandleNetworkChange(message.payload);
                    break;
                default:
                    Logger.Debug($"Unhandled web message type: {message.type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to handle web message", ex);
        }
    }

    private async void HandleNetworkChange(JsonElement? payload)
    {
        try
        {
            if (payload.HasValue && payload.Value.TryGetProperty("network", out var netProp))
            {
                var network = netProp.GetString() ?? "sepolia";
                _currentNetwork = network.Trim().ToLowerInvariant();
                _vaultManager.SaveLastNetwork(_currentNetwork);
                Title = $"Aegis Token Control - {_currentNetwork.ToUpperInvariant()}";
                await SendVaultStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to process network change", ex);
        }
    }

    private void HandleLog(JsonElement? payload)
    {
        try
        {
            var level = payload.HasValue && payload.Value.TryGetProperty("level", out var lvl) ? lvl.GetString() ?? "info" : "info";
            var message = payload.HasValue && payload.Value.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            switch (level.ToLowerInvariant())
            {
                case "error":
                    Logger.Error($"[WEBVIEW] {message}");
                    break;
                case "warn":
                case "warning":
                    Logger.Warning($"[WEBVIEW] {message}");
                    break;
                case "debug":
                    Logger.Debug($"[WEBVIEW] {message}");
                    break;
                default:
                    Logger.Info($"[WEBVIEW] {message}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling log message from WebView", ex);
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

    private CoreWebView2CreationProperties BuildWebViewProperties()
    {
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AegisMint",
            "WebView2",
            "TokenControl");

        Directory.CreateDirectory(userData);

        return new CoreWebView2CreationProperties
        {
            UserDataFolder = userData
        };
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

    private void LoadContractArtifacts()
    {
        try
        {
            _tokenArtifacts = _artifactLoader.LoadTokenImplementation();

            Logger.Debug($"Loading contract artifacts from: {Path.GetDirectoryName(_tokenArtifacts.AbiPath)}");

            if (!_tokenArtifacts.HasAbi)
            {
                Logger.Warning($"Token ABI not found at: {_tokenArtifacts.AbiPath}");
            }
            else
            {
                Logger.Info("Token ABI loaded successfully");
            }

            if (!_tokenArtifacts.HasBytecode)
            {
                Logger.Warning($"Token bytecode not found at: {_tokenArtifacts.BinPath}");
            }
            else if (string.IsNullOrWhiteSpace(_tokenArtifacts.Bytecode))
            {
                Logger.Warning("Token bytecode file was empty or contained no hex characters");
            }
            else
            {
                Logger.Info($"Token bytecode loaded successfully (length: {_tokenArtifacts.Bytecode.Length} chars)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load contract artifacts", ex);
            OverlayStatus.Text = $"Failed to load contract artifacts: {ex.Message}";
        }
    }

    private record BridgeMessage(string? type, JsonElement? payload);

    private async Task SendVaultStatusAsync()
    {
        if (MainWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            var contractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
            var snapshot = _vaultManager.GetDeploymentSnapshot(_currentNetwork);

            object? prefill = null;
            if (snapshot != null)
            {
                prefill = new
                {
                    network = snapshot.Network,
                    contractAddress = snapshot.ContractAddress,
                    tokenName = snapshot.TokenName,
                    tokenSupply = snapshot.TokenSupply,
                    tokenDecimals = snapshot.TokenDecimals,
                    govShares = snapshot.GovShares,
                    govThreshold = snapshot.GovThreshold,
                    treasuryAddress = snapshot.TreasuryAddress,
                    treasuryEth = snapshot.TreasuryEth,
                    treasuryTokens = snapshot.TreasuryTokens,
                    createdAtUtc = snapshot.CreatedAtUtc
                };
            }

            await SendToWebAsync("vault-status", new
            {
                currentNetwork = _currentNetwork,
                hasTreasury = !string.IsNullOrWhiteSpace(treasuryAddress),
                treasuryAddress,
                contractDeployed = !string.IsNullOrWhiteSpace(contractAddress),
                contractAddress,
                prefill
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to send vault status", ex);
        }
    }
}
