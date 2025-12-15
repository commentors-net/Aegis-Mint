using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AegisMint.Core.Models;
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
    private readonly TokenControlService _tokenControlService;
    private string _currentNetwork = "sepolia";
    private string? _currentContractAddress;

    public MainWindow()
    {
        InitializeComponent();
        _tokenControlService = new TokenControlService(_vaultManager);
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
        
        // Initialize TokenControlService with current network
        var rpcUrl = GetRpcUrlForNetwork(_currentNetwork);
        _tokenControlService.SetNetwork(_currentNetwork, rpcUrl);
        
        // Get current contract address
        _currentContractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
        
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
            await UpdateBalanceStatsAsync();
            await UpdatePauseStatusAsync();
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
                    await HandleNetworkChangeAsync(message.payload);
                    break;
                case "send-tokens":
                    await HandleSendTokensAsync(message.payload);
                    break;
                case "freeze-address":
                    await HandleFreezeAddressAsync(message.payload);
                    break;
                case "retrieve-tokens":
                    await HandleRetrieveTokensAsync(message.payload);
                    break;
                case "set-paused":
                    await HandleSetPausedAsync(message.payload);
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
                await UpdateNetworkAsync(network);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to process network change", ex);
        }
    }

    private async Task UpdateNetworkAsync(string network)
    {
        _currentNetwork = network.Trim().ToLowerInvariant();
        _vaultManager.SaveLastNetwork(_currentNetwork);
        Title = $"Aegis Token Control - {_currentNetwork.ToUpperInvariant()}";
        
        // Update TokenControlService with network and RPC URL
        var rpcUrl = GetRpcUrlForNetwork(_currentNetwork);
        _tokenControlService.SetNetwork(_currentNetwork, rpcUrl);
        
        // Get current contract address
        _currentContractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
        
        await SendVaultStatusAsync();
        await UpdateBalanceStatsAsync();
        await UpdatePauseStatusAsync();
    }

    private string GetRpcUrlForNetwork(string network)
    {
        return network.ToLowerInvariant() switch
        {
            "localhost" => "http://127.0.0.1:8545",
            "mainnet" => "https://mainnet.infura.io/v3/YOUR_INFURA_API_KEY",
            "sepolia" => "https://sepolia.infura.io/v3/fc6598ddab264c89a508cdb97d5398ea",
            _ => "http://127.0.0.1:8545"
        };
    }

    private async Task HandleSendTokensAsync(JsonElement? payload)
    {
        try
        {
            if (!payload.HasValue)
            {
                Logger.Warning("Send tokens payload is missing");
                return;
            }

            var to = payload.Value.TryGetProperty("to", out var toProp) ? toProp.GetString() : null;
            var amountStr = payload.Value.TryGetProperty("amount", out var amountProp) ? amountProp.GetString() : null;
            var memo = payload.Value.TryGetProperty("memo", out var memoProp) ? memoProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(amountStr))
            {
                await SendOperationResultAsync("Send", false, null, "Invalid recipient or amount");
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Send", false, null, "No contract deployed on this network");
                return;
            }

            if (!decimal.TryParse(amountStr, out var amount))
            {
                await SendOperationResultAsync("Send", false, null, "Invalid amount format");
                return;
            }

            Logger.Info($"Initiating token transfer: {amount} tokens to {to}");

            await SendProgressAsync("Validating balances...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.TransferTokensAsync(
                _currentContractAddress,
                to,
                amount,
                memo);

            await SendOperationResultAsync("Send", result.Success, result.TransactionHash, result.ErrorMessage);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling send tokens", ex);
            await SendOperationResultAsync("Send", false, null, ex.Message);
        }
    }

    private async Task HandleFreezeAddressAsync(JsonElement? payload)
    {
        try
        {
            if (!payload.HasValue)
            {
                Logger.Warning("Freeze address payload is missing");
                return;
            }

            var address = payload.Value.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : null;
            var freeze = payload.Value.TryGetProperty("freeze", out var freezeProp) && freezeProp.GetBoolean();
            var reason = payload.Value.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(address))
            {
                await SendOperationResultAsync("Freeze", false, null, "Invalid address");
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Freeze", false, null, "No contract deployed on this network");
                return;
            }

            Logger.Info($"{(freeze ? "Freezing" : "Unfreezing")} address: {address}");

            await SendProgressAsync($"{(freeze ? "Freezing" : "Unfreezing")} address on blockchain...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.FreezeAddressAsync(
                _currentContractAddress,
                address,
                freeze,
                reason);

            await SendOperationResultAsync("Freeze", result.Success, result.TransactionHash, result.ErrorMessage);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling freeze address", ex);
            await SendOperationResultAsync("Freeze", false, null, ex.Message);
        }
    }

    private async Task HandleRetrieveTokensAsync(JsonElement? payload)
    {
        try
        {
            if (!payload.HasValue)
            {
                Logger.Warning("Retrieve tokens payload is missing");
                return;
            }

            var from = payload.Value.TryGetProperty("from", out var fromProp) ? fromProp.GetString() : null;
            var amountStr = payload.Value.TryGetProperty("amount", out var amountProp) ? amountProp.GetString() : null;
            var reason = payload.Value.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(from))
            {
                await SendOperationResultAsync("Retrieve", false, null, "Invalid source address");
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Retrieve", false, null, "No contract deployed on this network");
                return;
            }

            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(treasuryAddress))
            {
                await SendOperationResultAsync("Retrieve", false, null, "Treasury address not found");
                return;
            }

            decimal? amount = null;
            if (!string.IsNullOrWhiteSpace(amountStr) && decimal.TryParse(amountStr, out var parsedAmount))
            {
                amount = parsedAmount;
            }

            Logger.Info($"Retrieving {(amount.HasValue ? $"{amount} tokens" : "full balance")} from {from}");

            await SendProgressAsync("Wiping frozen address...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.RetrieveTokensAsync(
                _currentContractAddress,
                from,
                treasuryAddress,
                amount,
                reason);

            await SendOperationResultAsync("Retrieve", result.Success, result.TransactionHash, result.ErrorMessage);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling retrieve tokens", ex);
            await SendOperationResultAsync("Retrieve", false, null, ex.Message);
        }
    }

    private async Task HandleSetPausedAsync(JsonElement? payload)
    {
        try
        {
            if (!payload.HasValue)
            {
                Logger.Warning("Set paused payload is missing");
                return;
            }

            var paused = payload.Value.TryGetProperty("paused", out var pausedProp) && pausedProp.GetBoolean();

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Pause", false, null, "No contract deployed on this network");
                return;
            }

            Logger.Info($"{(paused ? "Pausing" : "Unpausing")} token contract");

            await SendProgressAsync($"{(paused ? "Pausing" : "Unpausing")} contract on blockchain...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.SetPausedAsync(_currentContractAddress, paused);

            await SendOperationResultAsync("Pause", result.Success, result.TransactionHash, result.ErrorMessage);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
                await UpdatePauseStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling set paused", ex);
            await SendOperationResultAsync("Pause", false, null, ex.Message);
        }
    }

    private async Task SendOperationResultAsync(string operation, bool success, string? transactionHash, string? errorMessage)
    {
        await SendToWebAsync("operation-result", new
        {
            operation,
            success,
            transactionHash,
            errorMessage
        });
    }

    private async Task SendProgressAsync(string message)
    {
        await SendToWebAsync("operation-progress", new
        {
            message
        });
    }

    private async Task HandleNetworkChangeAsync(JsonElement? payload)
    {
        try
        {
            if (payload.HasValue && payload.Value.TryGetProperty("network", out var netProp))
            {
                var network = netProp.GetString() ?? "sepolia";
                await UpdateNetworkAsync(network);
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

    private async Task UpdateBalanceStatsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendToWebAsync("balance-stats", new
                {
                    tokenBalance = "N/A",
                    ethBalance = "N/A",
                    contractAddress = "Not deployed",
                    totalSupply = "N/A"
                });
                return;
            }

            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(treasuryAddress))
            {
                await SendToWebAsync("balance-stats", new
                {
                    tokenBalance = "N/A",
                    ethBalance = "N/A",
                    contractAddress = _currentContractAddress,
                    totalSupply = "N/A"
                });
                return;
            }

            // Fetch balances and supply
            var tokenBalance = await _tokenControlService.GetTokenBalanceAsync(_currentContractAddress, treasuryAddress);
            var ethBalance = await _tokenControlService.GetEthBalanceAsync(treasuryAddress);
            var totalSupply = await _tokenControlService.GetTotalSupplyAsync(_currentContractAddress);

            await SendToWebAsync("balance-stats", new
            {
                tokenBalance = tokenBalance?.ToString("N6") ?? "0.00",
                ethBalance = ethBalance?.ToString("N6") ?? "0.00",
                contractAddress = _currentContractAddress,
                totalSupply = totalSupply?.ToString("N0") ?? "0"
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update balance stats", ex);
            await SendToWebAsync("balance-stats", new
            {
                tokenBalance = "Error",
                ethBalance = "Error",
                contractAddress = _currentContractAddress ?? "N/A",
                totalSupply = "Error"
            });
        }
    }

    private async Task UpdatePauseStatusAsync()
    {
        if (MainWebView.CoreWebView2 is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentContractAddress))
        {
            await SendToWebAsync("pause-status", new
            {
                contractDeployed = false,
                paused = false,
                contractAddress = "N/A"
            });
            return;
        }

        try
        {
            var paused = await _tokenControlService.GetPausedStatusAsync(_currentContractAddress);
            await SendToWebAsync("pause-status", new
            {
                contractDeployed = true,
                contractAddress = _currentContractAddress,
                paused = paused ?? false,
                pauseStatusUnknown = paused == null
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update pause status", ex);
            await SendToWebAsync("pause-status", new
            {
                contractDeployed = true,
                contractAddress = _currentContractAddress,
                paused = false,
                pauseStatusUnknown = true,
                error = ex.Message
            });
        }
    }
}
