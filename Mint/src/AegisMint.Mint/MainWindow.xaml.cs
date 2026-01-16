using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Numerics;
using System.Net.Http;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WinForms = System.Windows.Forms;
using Microsoft.Win32;
using AegisMint.Core.Services;
using AegisMint.Core.Security;
using AegisMint.Core.Models;
using Nethereum.Web3;

namespace AegisMint.Mint;

public enum LockReason
{
    Checking,
    FirstTimeRegistration,
    PendingApproval,
    AwaitingGovernance,
    SessionExpired,
    Disabled,
    NetworkError,
    Error
}

public partial class MainWindow : Window
{
    private const int RegistrationMessageDelayMs = 5000; // 5 seconds to display registration message before shutdown
    
    private string? _htmlPath;
    private readonly VaultManager _vaultManager;
    private EthereumService? _ethereumService;
    private string _currentNetwork = "sepolia"; // default
    private string? _tokenAbi;
    private string? _tokenBytecode;
    private readonly ContractArtifactLoader _artifactLoader = new();
    private DesktopAuthenticationService? _authService;
    private DispatcherTimer? _approvalCheckTimer;
    private DispatcherTimer? _countdownTimer;
    private DateTime? _unlockedUntilUtc;
    private bool _hasAuthenticationSucceeded = false;
    private bool _hasAuthenticationChecked = false;
    private bool _hasNavigationCompleted = false;
    private string _cachedTitle = string.Empty;

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
            "mainnet" => "https://mainnet.infura.io/v3/fc5bd40a3f054a4f9842f53d0d711e0e",
            "sepolia" => "https://sepolia.infura.io/v3/fc6598ddab264c89a508cdb97d5398ea",
            _ => "https://sepolia.infura.io/v3/fc6598ddab264c89a508cdb97d5398ea"
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
        Logger.Debug("OnLoaded - START");
        LoadContractArtifacts();
        await InitializeAuthenticationAsync();
        Logger.Debug("OnLoaded - Auth completed, starting WebView init");
        await InitializeWebViewAsync();
        Logger.Debug("OnLoaded - END");
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            Logger.Info("Initializing WebView");
            OverlayStatus.Text = "Loading UI...";
            MainWebView.CreationProperties = BuildWebViewProperties();
            _htmlPath = Path.Combine(AppContext.BaseDirectory, "Assets", "aegis_mint.html");

            if (!File.Exists(_htmlPath))
            {
                Logger.Error($"UI asset not found: {_htmlPath}");
                OverlayStatus.Text = "Missing UI asset: aegis_mint_main_screen.html";
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
        settings.AreDevToolsEnabled = true; // Enable for debugging
        settings.IsStatusBarEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsScriptEnabled = true; // Explicitly enable JavaScript

        MainWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        MainWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Logger.Debug($"OnNavigationCompleted - IsSuccess={e.IsSuccess}");
        if (e.IsSuccess)
        {
            // Mark navigation as completed
            _hasNavigationCompleted = true;
            Logger.Debug("Navigation completed - setting flag to true");
            
            // Only hide overlays if authentication explicitly succeeded
            // Lock overlay visibility is controlled ONLY by ShowLockOverlay/HideLockOverlay
            Logger.Debug($"OnNavigationCompleted check - AuthChecked={_hasAuthenticationChecked}, AuthSucceeded={_hasAuthenticationSucceeded}, LockOverlay={LockOverlay.Visibility}, Overlay={Overlay.Visibility}");
            
            if (_hasAuthenticationSucceeded)
            {
                // Authentication succeeded - hide all overlays to show WebView
                Logger.Debug("Auth succeeded - hiding all overlays");
                Overlay.Visibility = Visibility.Collapsed;
                LockOverlay.Visibility = Visibility.Collapsed;
            }
            else if (_hasAuthenticationChecked && LockOverlay.Visibility == Visibility.Visible)
            {
                // Auth checked but not succeeded - lock overlay is showing, just hide loading overlay
                Logger.Debug("Auth checked but not succeeded - lock overlay visible, hiding loading overlay only");
                Overlay.Visibility = Visibility.Collapsed;
            }
            else if (_hasAuthenticationChecked)
            {
                // Auth checked but lock overlay not showing - this shouldn't happen
                Logger.Debug("WARNING: Auth checked but lock overlay not visible - keeping loading overlay");
            }
            else
            {
                Logger.Debug("Auth check not completed yet - keeping loading overlay visible");
            }
            
            await SendToWebAsync("host-info", new { host = "Aegis Mint WPF", version = "1.0", network = _currentNetwork });
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
                case "refresh-treasury-eth":
                    await HandleRefreshTreasuryEthAsync(message.payload);
                    break;
                case "open-faucet":
                    HandleOpenFaucet(message.payload);
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

    private CoreWebView2CreationProperties BuildWebViewProperties()
    {
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AegisMint",
            "WebView2",
            "Mint");

        Directory.CreateDirectory(userData);

        return new CoreWebView2CreationProperties
        {
            UserDataFolder = userData
        };
    }

    private void HandleLog(JsonElement? payload)
    {
        try
        {
            var level = payload.HasValue && payload.Value.TryGetProperty("level", out var lvl) ? lvl.GetString() ?? "info" : "info";
            var message = payload.HasValue && payload.Value.TryGetProperty("message", out var msg) ? msg.GetString() ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                // Log with appropriate level
                switch (level.ToLower())
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
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling log message from WebView", ex);
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
                Logger.Info($"Network switched to: {network}");
                _vaultManager.SaveLastNetwork(network);
                Title = $"Aegis Mint - {network.ToUpperInvariant()}";
                _ = CheckExistingVaults();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error changing network", ex);
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
            var deployedContractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
            var snapshot = _vaultManager.GetDeploymentSnapshot(_currentNetwork);
            decimal? balance = null;
            string? liveTreasuryEth = null;
            string? liveTreasuryTokens = null;
            if (_ethereumService != null && !string.IsNullOrWhiteSpace(treasuryAddress))
            {
                try
                {
                    balance = await _ethereumService.GetBalanceAsync(treasuryAddress);
                    if (balance.HasValue)
                    {
                        liveTreasuryEth = balance.Value.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                }
                catch
                {
                    balance = null;
                }
            }

            // Fetch live token balance if we have a contract and ABI
            if (_ethereumService != null && !string.IsNullOrWhiteSpace(deployedContractAddress) && !string.IsNullOrWhiteSpace(_tokenAbi) && snapshot != null && !string.IsNullOrWhiteSpace(treasuryAddress))
            {
                try
                {
                    var tokenBal = await _ethereumService.GetTokenBalanceAsync(_tokenAbi, deployedContractAddress, treasuryAddress, snapshot.TokenDecimals);
                    liveTreasuryTokens = tokenBal.ToString("0.####", CultureInfo.InvariantCulture);
                }
                catch
                {
                    liveTreasuryTokens = null;
                }
            }
            
            var prefill = snapshot is null ? null : MapSnapshotForUi(snapshot, liveTreasuryEth, liveTreasuryTokens);
            
            await SendToWebAsync("vault-status", new 
            { 
                hasTreasury = treasuryAddress != null,
                treasuryAddress = treasuryAddress,
                balance = balance,
                liveTreasuryEth,
                liveTreasuryTokens,
                contractDeployed = _vaultManager.HasDeployedContract(_currentNetwork),
                contractAddress = deployedContractAddress,
                prefill,
                currentNetwork = _currentNetwork
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
            if (_vaultManager.HasDeployedContract(_currentNetwork))
            {
                await SendToWebAsync("host-error", new
                {
                    message = "Contract deployed. Deployment disabled."
                });
                return;
            }

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

    private async Task HandleRefreshTreasuryEthAsync(JsonElement? payload)
    {
        try
        {
            if (_ethereumService == null)
            {
                await SendToWebAsync("host-error", new { message = "Ethereum service is not initialized." });
                return;
            }

            string? address = null;
            if (payload.HasValue && payload.Value.TryGetProperty("address", out var addrProp))
            {
                address = addrProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                address = _vaultManager.GetTreasuryAddress();
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                await SendToWebAsync("host-error", new { message = "Treasury address not found." });
                return;
            }

            var balance = await _ethereumService.GetBalanceAsync(address);
            await SendToWebAsync("treasury-eth-updated", new { eth = balance });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to refresh treasury ETH", ex);
            await SendToWebAsync("host-error", new { message = $"Failed to refresh treasury ETH: {ex.Message}" });
        }
    }

    private void HandleOpenFaucet(JsonElement? payload)
    {
        try
        {
            var network = payload.HasValue && payload.Value.TryGetProperty("network", out var netProp)
                ? netProp.GetString()
                : _currentNetwork;

            if (!string.Equals(network, "sepolia", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            //var url = "https://faucet.quicknode.com/ethereum/sepolia";
            var url = "https://cloud.google.com/application/web3/faucet/ethereum/sepolia";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to open faucet link: {ex.Message}");
        }
    }

    private async Task HandleMintSubmit(JsonElement? payload
)
    {
        try
        {
            Logger.Info("Mint submission started");

            if (_vaultManager.HasDeployedContract(_currentNetwork))
            {
                var recorded = _vaultManager.GetDeployedContractAddress(_currentNetwork);
                await SendToWebAsync("host-error", new
                {
                    message = "Contract deployed. Deployment disabled."
                });
                await SendToWebAsync("contract-deployed", new
                {
                    address = recorded
                });
                return;
            }

            // Step 1: Validate mint payload structure
            var (ok, error) = ValidateMintPayload(payload);
            if (!ok)
            {
                Logger.Warning($"Validation failed: {error}");
                await SendToWebAsync("validation-result", new { ok = false, message = error ?? "Validation failed" });
                return;
            }

            Logger.Info("Payload validation passed");

            // Step 2: Validate treasury balance on blockchain
            if (_ethereumService != null && payload.HasValue && payload.Value.TryGetProperty("treasuryAddress", out var addrProp))
            {
                var treasuryAddress = addrProp.GetString();
                if (!string.IsNullOrWhiteSpace(treasuryAddress))
                {
                    try
                    {
                        Logger.Debug($"Checking balance for treasury: {treasuryAddress}");
                        var balance = await _ethereumService.GetBalanceAsync(treasuryAddress);
                        
                        if (balance <= 0)
                        {
                            Logger.Warning($"Treasury has insufficient balance: {balance} ETH");
                            await SendToWebAsync("validation-result", new 
                            { 
                                ok = false, 
                                message = $"Treasury address has 0 ETH balance on {_currentNetwork}. Fund the address before minting." 
                            });
                            return;
                        }
                        
                        Logger.Info($"Treasury balance confirmed: {balance} ETH");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to check balance on {_currentNetwork}", ex);
                        await SendToWebAsync("validation-result", new 
                        { 
                            ok = false, 
                            message = $"Failed to check balance on {_currentNetwork}: {ex.Message}" 
                        });
                        return;
                    }
                }
            }

            // Step 3: Validate required components
            if (_ethereumService == null)
            {
                Logger.Error("Ethereum service is not initialized");
                await SendToWebAsync("host-error", new { message = "Ethereum service is not initialized." });
                return;
            }

            if (string.IsNullOrWhiteSpace(_tokenAbi) || string.IsNullOrWhiteSpace(_tokenBytecode))
            {
                Logger.Error("Token artifacts not loaded");
                await SendToWebAsync("host-error", new { message = "Token ABI/bytecode not loaded. Ensure TokenImplementationV2 artifacts are present." });
                return;
            }

            var privateKey = _vaultManager.GetTreasuryPrivateKey();
            if (string.IsNullOrWhiteSpace(privateKey))
            {
                Logger.Error("Treasury key not found");
                await SendToWebAsync("host-error", new { message = "Treasury key not found. Generate treasury first." });
                return;
            }

            // Step 4: Generate recovery shares before deployment
            var govShares = GetInt(payload, "govShares");
            var govThreshold = GetInt(payload, "govThreshold");
            var totalShares = govShares + govThreshold;
            if (govThreshold < 2)
            {
                await SendToWebAsync("host-error", new { message = "Threshold must be at least 2 to create recovery shares." });
                return;
            }
            if (totalShares > 255)
            {
                await SendToWebAsync("host-error", new { message = "Total share count cannot exceed 255." });
                return;
            }
            if (!await CreateAndSaveRecoverySharesAsync(totalShares, govThreshold, govShares))
            {
                return;
            }

            // Step 4: Extract token parameters
            var tokenName = GetString(payload, "tokenName");
            var tokenDecimals = GetInt(payload, "tokenDecimals");
            var tokenSymbol = DeriveSymbol(tokenName);
            var tokenSupplyRaw = GetString(payload, "tokenSupply");
            if (!decimal.TryParse(tokenSupplyRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var tokenSupplyDecimal) || tokenSupplyDecimal <= 0)
            {
                await SendToWebAsync("host-error", new { message = "Invalid token supply." });
                return;
            }
            var tokenSupplyBaseUnits = Web3.Convert.ToWei(tokenSupplyDecimal, tokenDecimals);

            Logger.Info($"Deploying token: {tokenName} ({tokenSymbol}), decimals: {tokenDecimals}");
            await SendToWebAsync("validation-result", new { ok = true, message = "Configuration validated. Deploying token..." });

            // Derive proxy admin as the second address from the treasury mnemonic (index 1)
            var proxyAdminAddress = _vaultManager.GetSecondaryAddress();
            if (string.IsNullOrWhiteSpace(proxyAdminAddress))
            {
                Logger.Warning("Failed to derive secondary address; defaulting proxy admin to treasury address");
                proxyAdminAddress = _vaultManager.GetTreasuryAddress();
            }
            Logger.Info($"Using proxy admin address: {proxyAdminAddress}");

            // Step 5: Deploy token using JSON-RPC
            Logger.Info("Starting token deployment via JSON-RPC");
            var deployResult = await _ethereumService.DeployTokenAsync(
                privateKey,
                _tokenAbi,
                _tokenBytecode,
                tokenName,
                tokenSymbol,
                (byte)tokenDecimals,
                tokenSupplyBaseUnits,
                proxyAdminAddress);

            if (!deployResult.Success)
            {
                Logger.Error($"Token deployment failed: {deployResult.ErrorMessage}");
                await SendToWebAsync("host-error", new 
                { 
                    message = $"Deployment failed: {deployResult.ErrorMessage}" 
                });
                return;
            }

            Logger.Info($"✓ Token deployed successfully at: {deployResult.ContractAddress}");
            Logger.Info($"  Deployment TX: {deployResult.DeploymentTxHash}");
            if (!string.IsNullOrEmpty(deployResult.InitializeTxHash))
            {
                Logger.Info($"  Initialize TX: {deployResult.InitializeTxHash}");
            }
            if (!string.IsNullOrEmpty(deployResult.IncreaseSupplyTxHash))
            {
                Logger.Info($"  Mint supply TX: {deployResult.IncreaseSupplyTxHash}");
            }
            Logger.Info($"  Gas used: {deployResult.GasUsed}");
            Logger.Info($"  Block: {deployResult.BlockNumber}");

            // Step 6: Send success response to UI
            await SendToWebAsync("validation-result", new 
            { 
                ok = true, 
                message = $"✓ Token deployed at {deployResult.ContractAddress}" 
            });

            await SendToWebAsync("mint-received", new 
            { 
                ok = true, 
                received = payload,
                contractAddress = deployResult.ContractAddress,
                deployTx = deployResult.DeploymentTxHash,
                initTx = deployResult.InitializeTxHash,
                increaseSupplyTx = deployResult.IncreaseSupplyTxHash,
                gasUsed = deployResult.GasUsed,
                blockNumber = deployResult.BlockNumber
            });

            // Persist contract address and notify UI so future deployments are blocked
            var recordedContractAddress = !string.IsNullOrWhiteSpace(deployResult.ProxyAddress)
                ? deployResult.ProxyAddress
                : deployResult.ContractAddress;
            if (!string.IsNullOrWhiteSpace(recordedContractAddress))
            {
                try
                {
                    _vaultManager.RecordContractDeployment(recordedContractAddress, _currentNetwork);

                    string? liveTreasuryEth = null;
                    string? liveTreasuryTokens = null;
                    try
                    {
                        var liveEth = await _ethereumService.GetBalanceAsync(GetString(payload, "treasuryAddress"));
                        liveTreasuryEth = liveEth.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                    catch { /* ignore */ }

                    try
                    {
                        var liveTokenBal = await _ethereumService.GetTokenBalanceAsync(_tokenAbi, recordedContractAddress, GetString(payload, "treasuryAddress"), tokenDecimals);
                        liveTreasuryTokens = liveTokenBal.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                    catch { /* ignore */ }

                    var snapshot = new DeploymentSnapshot(
                        _currentNetwork,
                        recordedContractAddress!,
                        tokenName ?? string.Empty,
                        GetString(payload, "tokenSupply"),
                        tokenDecimals,
                        GetInt(payload, "govShares"),
                        GetInt(payload, "govThreshold"),
                        GetString(payload, "treasuryAddress"),
                        GetString(payload, "treasuryEth"),
                        FormatTokenDisplay(GetString(payload, "tokenSupply"), tokenDecimals),
                        DateTimeOffset.UtcNow);

                    _vaultManager.RecordDeploymentSnapshot(_currentNetwork, snapshot);

                    await SendToWebAsync("contract-deployed", new
                    {
                        address = recordedContractAddress,
                        prefill = MapSnapshotForUi(snapshot, liveTreasuryEth, liveTreasuryTokens)
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to record deployed contract address: {ex.Message}");
                }
            }

            Logger.Info("Token deployment completed successfully");
        }
        catch (Exception ex)
        {
            Logger.Error("Mint submission failed with exception", ex);
            await SendToWebAsync("host-error", new { message = $"Mint failed: {ex.Message}" });
        }
    }

    private (bool ok, string? error) ValidateMintPayload(JsonElement? payload)
    {
        if (payload is null) return (false, "No payload received.");

        bool TryGetInt32(string name, out int value)
        {
            value = 0;
            if (!payload.Value.TryGetProperty(name, out var prop)) return false;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value)) return true;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value)) return true;
            return false;
        }

        bool TryGetDecimal(string name, out decimal value)
        {
            value = 0m;
            if (!payload.Value.TryGetProperty(name, out var prop)) return false;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out value)) return true;
            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out value)) return true;
            return false;
        }

        var tokenName = GetString(payload, "tokenName");
        var treasuryAddress = GetString(payload, "treasuryAddress");

        if (string.IsNullOrWhiteSpace(tokenName)) return (false, "Token Name is required.");
        if (string.IsNullOrWhiteSpace(treasuryAddress)) return (false, "Treasury address is required.");

        if (!TryGetInt32("tokenDecimals", out var decimals) || decimals < 0 || decimals > 36)
        {
            return (false, "Token decimals must be between 0 and 36.");
        }

        if (!TryGetInt32("govShares", out var shares) || shares <= 0)
        {
            return (false, "Number of shares must be greater than zero.");
        }

        if (!TryGetInt32("govThreshold", out var threshold) || threshold <= 0)
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

    private string DeriveSymbol(string tokenName)
    {
        if (string.IsNullOrWhiteSpace(tokenName)) return "TOKEN";
        var trimmed = new string(tokenName.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(trimmed)) return "TOKEN";
        return trimmed.Length > 8 ? trimmed[..8].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }

    private string GetString(JsonElement? payload, string name)
    {
        if (!payload.HasValue || !payload.Value.TryGetProperty(name, out var prop))
        {
            return string.Empty;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            JsonValueKind.Number => prop.GetRawText(), // preserve numeric text without throwing
            JsonValueKind.True or JsonValueKind.False => prop.GetRawText(),
            _ => string.Empty
        };
    }

    private string FormatTokenDisplay(string supplyRaw, int decimals)
    {
        if (string.IsNullOrWhiteSpace(supplyRaw))
        {
            return "0";
        }

        if (decimal.TryParse(supplyRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var supply))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:N0}", supply);
        }

        return supplyRaw;
    }

    private int GetInt(JsonElement? payload, string name)
    {
        if (payload.HasValue && payload.Value.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
            {
                return value;
            }
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
            {
                return value;
            }
        }
        return 0;
    }

    private object MapSnapshotForUi(DeploymentSnapshot snapshot, string? liveTreasuryEth = null, string? liveTreasuryTokens = null)
    {
        return new
        {
            network = snapshot.Network,
            contractAddress = snapshot.ContractAddress,
            tokenName = snapshot.TokenName,
            tokenSupply = snapshot.TokenSupply,
            tokenDecimals = snapshot.TokenDecimals,
            govShares = snapshot.GovShares,
            govThreshold = snapshot.GovThreshold,
            treasuryAddress = snapshot.TreasuryAddress,
            treasuryEth = liveTreasuryEth ?? snapshot.TreasuryEth,
            treasuryTokens = liveTreasuryTokens ?? snapshot.TreasuryTokens,
            liveTreasuryEth,
            liveTreasuryTokens,
            createdAtUtc = snapshot.CreatedAtUtc
        };
    }

    private async Task<bool> CreateAndSaveRecoverySharesAsync(int totalShares, int threshold, int clientShares)
    {
        try
        {
            var mnemonic = _vaultManager.GetTreasuryMnemonic();
            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                await SendToWebAsync("host-error", new { message = "Treasury mnemonic missing; cannot create recovery shares." });
                return false;
            }

            // Generate a random encryption key
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var encryptionKey = new byte[32]; // 256-bit key
            rng.GetBytes(encryptionKey);
            var encryptionKeyHex = BitConverter.ToString(encryptionKey).Replace("-", "");

            // Encrypt mnemonic with the random key
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = encryptionKey;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
            var encryptedBytes = encryptor.TransformFinalBlock(mnemonicBytes, 0, mnemonicBytes.Length);
            var encryptedMnemonicHex = BitConverter.ToString(encryptedBytes).Replace("-", "");
            var ivHex = BitConverter.ToString(aes.IV).Replace("-", "");
            
            // Split the encryption key using Shamir
            var shamir = new ShamirSecretSharingService();
            var keyBytes = Encoding.UTF8.GetBytes(encryptionKeyHex);
            var shares = shamir.Split(keyBytes, threshold, totalShares);

            using var folderDialog = new WinForms.FolderBrowserDialog
            {
                Description = "Choose a folder to save individual recovery share files",
                ShowNewFolderButton = true
            };

            var dialogResult = folderDialog.ShowDialog();
            if (dialogResult != WinForms.DialogResult.OK || string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                await SendToWebAsync("host-error", new { message = "Share creation cancelled by user." });
                return false;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var createdAt = DateTimeOffset.UtcNow;
            var tokenAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
            
            foreach (var share in shares)
            {
                var sharePayload = new
                {
                    createdAtUtc = createdAt,
                    network = _currentNetwork,
                    totalShares,
                    threshold,
                    clientShareCount = clientShares,
                    safekeepingShareCount = threshold,
                    share = $"{share.Id}-{share.Value}",
                    encryptedMnemonic = encryptedMnemonicHex,
                    iv = ivHex,
                    encryptionVersion = 1,
                    tokenAddress = tokenAddress
                };

                var fileName = $"aegis-share-{share.Id:D3}.json";
                var path = Path.Combine(folderDialog.SelectedPath, fileName);
                var json = JsonSerializer.Serialize(sharePayload, options);
                await File.WriteAllTextAsync(path, json);
                Logger.Info($"Recovery share saved: {path}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to create recovery shares", ex);
            await SendToWebAsync("host-error", new { message = $"Failed to create recovery shares: {ex.Message}" });
            return false;
        }
    }

    // Application-specific encryption key derivation
    private string DeriveApplicationEncryptionKey()
    {
        // Application-specific constant embedded in code
        const string APP_SALT = "AegisMint-Recovery-v1-2026";
        
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(APP_SALT)));
    }

    // AES-256 encryption
    private string EncryptMnemonic(string mnemonic, string key)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = Convert.FromBase64String(key);
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
        var encrypted = encryptor.TransformFinalBlock(mnemonicBytes, 0, mnemonicBytes.Length);
        
        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        
        return Convert.ToBase64String(result);
    }

    // AES-256 decryption
    private string DecryptMnemonic(string encryptedMnemonic, string key)
    {
        var data = Convert.FromBase64String(encryptedMnemonic);
        
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = Convert.FromBase64String(key);
        
        // Extract IV from beginning
        var iv = new byte[aes.IV.Length];
        var encrypted = new byte[data.Length - iv.Length];
        Buffer.BlockCopy(data, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(data, iv.Length, encrypted, 0, encrypted.Length);
        
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        
        return Encoding.UTF8.GetString(decrypted);
    }

    // Authentication and Access Control Methods
    private async Task InitializeAuthenticationAsync()
    {
        try
        {
            Logger.Debug("InitializeAuthenticationAsync - START");
            _authService = new DesktopAuthenticationService(_vaultManager);
            Logger.Info($"Desktop App ID: {_authService.DesktopAppId}");

            Logger.Debug("Showing lock overlay: Checking authorization...");
            ShowLockOverlay(LockReason.Checking, false);
            await CheckAuthenticationStatusAsync();
            Logger.Debug("InitializeAuthenticationAsync - END");
        }
        catch (Exception ex)
        {
            Logger.Error("Authentication initialization failed", ex);
            ShowLockOverlay(LockReason.Error, true);
        }
    }

    private async Task CheckAuthenticationStatusAsync()
    {
        if (_authService == null) return;

        try
        {
            Logger.Debug("CheckAuthenticationStatusAsync - Calling API...");
            var status = await _authService.CheckUnlockStatusAsync();
            Logger.Debug($"CheckAuthenticationStatusAsync - Status: {status.DesktopStatus}, Unlocked: {status.IsUnlocked}");

            // Mark that authentication check has completed
            _hasAuthenticationChecked = true;

            if (status.DesktopStatus == "Pending")
            {
                ShowLockOverlay(LockReason.PendingApproval, false, status);
                await Task.Delay(RegistrationMessageDelayMs);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            if (status.DesktopStatus == "Disabled")
            {
                ShowLockOverlay(LockReason.Disabled, false, status);
                return;
            }

            if (status.DesktopStatus != "Active")
            {
                ShowLockOverlay(LockReason.Error, true, status);
                return;
            }

            // Desktop is Active
            if (!status.IsUnlocked)
            {
                ShowLockOverlay(LockReason.AwaitingGovernance, false, status);
                
                // Check every 30 seconds while waiting for approval
                StartApprovalCheckTimer();
                return;
            }

            // Unlocked - allow access
            Logger.Debug("Authentication SUCCEEDED - Setting flag to true");
            _hasAuthenticationSucceeded = true;
            _unlockedUntilUtc = status.UnlockedUntilUtc?.ToLocalTime();
            HideLockOverlay();

            if (_unlockedUntilUtc.HasValue)
            {
                var remaining = _unlockedUntilUtc.Value - DateTime.Now;
                Logger.Info($"Access granted until {_unlockedUntilUtc.Value:yyyy-MM-dd HH:mm:ss} (Remaining: {remaining.TotalMinutes:F1} minutes)");
                StartCountdownTimer(_unlockedUntilUtc.Value);
            }
            else
            {
                // No expiration time, just update title
                UpdateTitle();
            }
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Desktop not found"))
        {
            // First time registration
            Logger.Debug("Desktop not found - registering...");
            await RegisterDesktopAsync();
        }
        catch (HttpRequestException ex)
        {
            Logger.Debug($"Network error caught: {ex.Message}");
            Logger.Error("Network error during authentication check", ex);
            ShowLockOverlay(LockReason.NetworkError, true);
            Logger.Debug($"Auth failed - _hasAuthenticationSucceeded={_hasAuthenticationSucceeded}");
        }
        catch (Exception ex)
        {
            Logger.Debug($"General exception caught: {ex.Message}");
            Logger.Error("Authentication check failed", ex);
            ShowLockOverlay(LockReason.Error, true);
            Logger.Debug($"Auth failed - _hasAuthenticationSucceeded={_hasAuthenticationSucceeded}");
        }
    }

    private async Task RegisterDesktopAsync()
    {
        if (_authService == null) return;

        try
        {
            var machineName = Environment.MachineName;
            var osUser = Environment.UserName;
            var version = GetAppVersion();
            var nameLabel = $"{machineName} - {osUser} (Registered: {DateTime.Now:yyyy-MM-dd HH:mm})";

            var response = await _authService.RegisterAsync(machineName, version, osUser, nameLabel, "Mint");

            Logger.Info($"Desktop registered with status: {response.DesktopStatus}");

            ShowLockOverlay(LockReason.FirstTimeRegistration, false);

            await Task.Delay(RegistrationMessageDelayMs);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Desktop registration failed", ex);
            ShowLockOverlay(LockReason.Error, true);
        }
    }

    private void StartCountdownTimer(DateTime unlockUntil)
    {
        // Stop any existing timers
        _countdownTimer?.Stop();
        _approvalCheckTimer?.Stop();
        
        // Create countdown timer that updates every second
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _countdownTimer.Tick += async (s, e) =>
        {
            var remaining = unlockUntil - DateTime.Now;
            
            if (remaining.TotalSeconds <= 0)
            {
                // Time expired - lock the application
                _countdownTimer?.Stop();
                _hasAuthenticationSucceeded = false;
                _unlockedUntilUtc = null;
                
                // Hide countdown timer in web UI
                await SendCountdownUpdateAsync(null, false);
                
                ShowLockOverlay(LockReason.SessionExpired, false);
                DisableApplication();
                Logger.Info("Access time expired - application locked");
                
                // Start checking for new approvals (will check immediately and then every 30s)
                Logger.Info("Starting approval check timer to monitor for re-approval");
                StartApprovalCheckTimer();
                
                // Immediately check status to see if there are pending approvals
                // This will update the UI from "SessionExpired" to "AwaitingGovernance" if desktop is still Active
                await Task.Delay(1000); // Brief delay to let user see the expired message
                await CheckAuthenticationStatusAsync();
            }
            else
            {
                // Send countdown update to web UI
                await SendCountdownUpdateAsync(remaining, true);
            }
        };

        _countdownTimer.Start();
        // Send initial countdown update (don't wait - would deadlock UI thread)
        var initialRemaining = unlockUntil - DateTime.Now;
        _ = SendCountdownUpdateAsync(initialRemaining, true);
        Logger.Info($"Countdown timer started - expires at {unlockUntil:HH:mm:ss}");
    }

    private void StartApprovalCheckTimer()
    {
        _approvalCheckTimer?.Stop();
        _approvalCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };

        _approvalCheckTimer.Tick += async (s, e) =>
        {
            await CheckAuthenticationStatusAsync();
        };

        _approvalCheckTimer.Start();
        Logger.Debug("Started approval check timer (30s interval)");
    }

    private void UpdateTitle(TimeSpan? remaining = null)
    {
        // Show version and network name
        var baseTitle = $"Aegis Mint {GetAppVersion()} - {_currentNetwork.ToUpperInvariant()}";
        
        // Only update if changed
        if (baseTitle != _cachedTitle)
        {
            _cachedTitle = baseTitle;
            Title = baseTitle;
        }
    }

    private string GetAppVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
    }

    private string GetShortDesktopId()
    {
        if (_authService == null || string.IsNullOrEmpty(_authService.DesktopAppId))
            return "Loading...";
        
        var id = _authService.DesktopAppId;
        if (id.Length > 8)
        {
            return $"{id[..4]}...{id[^3..]}";
        }
        return id;
    }

    private void ShowLockOverlay(LockReason reason, bool showRetry, UnlockStatusResponse? statusInfo = null)
    {
        Logger.Debug($"ShowLockOverlay called: reason={reason}, showRetry={showRetry}");
        Dispatcher.Invoke(() =>
        {
            // Update app info
            LockAppVersion.Text = $"Aegis Mint {GetAppVersion()}";
            LockDesktopId.Text = $"Desktop ID: {GetShortDesktopId()}";
            LockNetwork.Text = $"Network: {_currentNetwork.ToUpperInvariant()}";

            // Set title and message based on reason
            string title, message, explanation;
            switch (reason)
            {
                case LockReason.FirstTimeRegistration:
                    title = "Registration Complete";
                    message = "Your application has been registered and is pending approval by an administrator. " +
                             "The application will close now. Please restart after approval.";
                    explanation = "New desktop applications require administrator approval before they can be used. " +
                                 "This is a security measure to prevent unauthorized access.";
                    break;

                case LockReason.PendingApproval:
                    title = "Pending Approval";
                    message = statusInfo != null 
                        ? $"This computer is already registered. Access requires approval from {statusInfo.RequiredApprovalsN} administrator(s). " +
                          $"Current approvals: {statusInfo.ApprovalsSoFar}. Please wait for authorization."
                        : "Your application registration is pending approval by an administrator. " +
                          "The application will close now. Please restart after approval.";
                    explanation = "Desktop applications require administrator approval before access can be granted. " +
                                 "Approvals are currently missing or pending.";
                    break;

                case LockReason.AwaitingGovernance:
                    title = "Awaiting Governance Approvals";
                    message = statusInfo != null
                        ? $"Access requires approval from {statusInfo.RequiredApprovalsN} administrator(s). " +
                          $"Current approvals: {statusInfo.ApprovalsSoFar}. Please wait for authorization."
                        : "Awaiting required governance approvals. Please wait for administrator authorization.";
                    explanation = "Multi-signature governance requires multiple administrators to approve access. " +
                                 "This ensures no single person has unilateral control.";
                    break;

                case LockReason.SessionExpired:
                    title = "Session Expired";
                    message = "Your access session has expired. Please request new approvals to regain access.";
                    explanation = "Access sessions have a time limit for security purposes. " +
                                 "When a session expires, new approvals are required.";
                    break;

                case LockReason.Disabled:
                    title = "Application Disabled";
                    message = "This desktop application has been disabled by an administrator.";
                    explanation = "An administrator has explicitly disabled this desktop application. " +
                                 "Contact your administrator for more information.";
                    break;

                case LockReason.NetworkError:
                    title = "Connection Error";
                    message = "Unable to connect to the governance server. Check your network connection and try again.";
                    explanation = "The application cannot connect to the governance backend to verify authorization. " +
                                 "This could be due to network issues or server unavailability.";
                    break;

                case LockReason.Checking:
                    title = "Verifying Access";
                    message = "Checking authorization...";
                    explanation = "Connecting to the governance server to verify your access permissions.";
                    break;

                default:
                    title = "Access Restricted";
                    message = "An error occurred while checking authorization.";
                    explanation = "This application is locked due to an unexpected error. Please try again or contact support.";
                    break;
            }

            LockTitle.Text = title;
            LockMessage.Text = message;
            LockExplanation.Text = explanation;

            // Update session info if available
            if (statusInfo != null && statusInfo.SessionStatus != "None")
            {
                LastSessionInfo.Visibility = Visibility.Visible;
                
                // Determine if this is current session (Pending) or last session (Expired/Unlocked)
                bool isCurrentSession = statusInfo.SessionStatus == "Pending";
                
                // Update title
                SessionInfoTitle.Text = isCurrentSession ? "Current Session Information" : "Last Session Information";
                
                // Show approval status
                LastApprovalCount.Text = isCurrentSession 
                    ? $"Current approvals: {statusInfo.ApprovalsSoFar} of {statusInfo.RequiredApprovalsN}"
                    : $"Last approval count: {statusInfo.ApprovalsSoFar} of {statusInfo.RequiredApprovalsN}";

                // For current session (Pending), show different information
                if (isCurrentSession)
                {
                    // Don't show "Last unlock: Never" for active approval session
                    if (statusInfo.ApprovalsSoFar > 0)
                    {
                        LastUnlockTime.Text = $"Approval session active";
                    }
                    else
                    {
                        LastUnlockTime.Text = $"Waiting for first approval";
                    }
                    
                    // Show time remaining or status
                    if (statusInfo.UnlockedUntilUtc.HasValue && statusInfo.UnlockedUntilUtc.Value > DateTime.Now)
                    {
                        var remaining = statusInfo.UnlockedUntilUtc.Value.ToLocalTime() - DateTime.Now;
                        LastSessionExpiry.Text = $"Time to complete: {remaining.TotalMinutes:F0} minutes";
                    }
                    else
                    {
                        LastSessionExpiry.Text = "Session expires when threshold reached";
                    }
                }
                else
                {
                    // Last session (Expired/Unlocked) - show historical info
                    if (statusInfo.UnlockedUntilUtc.HasValue && statusInfo.UnlockedUntilUtc.Value > DateTime.MinValue)
                    {
                        var unlockTime = statusInfo.UnlockedUntilUtc.Value.ToLocalTime();
                        var expiredAgo = DateTime.Now - unlockTime;
                        
                        if (expiredAgo.TotalMinutes < 60)
                            LastUnlockTime.Text = $"Last unlock: {expiredAgo.TotalMinutes:F0} minutes ago";
                        else if (expiredAgo.TotalHours < 24)
                            LastUnlockTime.Text = $"Last unlock: {expiredAgo.TotalHours:F0} hours ago";
                        else
                            LastUnlockTime.Text = $"Last unlock: {unlockTime:MMM dd, HH:mm}";
                    }
                    else
                    {
                        LastUnlockTime.Text = "Last unlock: Never";
                    }

                    // Session expiry
                    if (statusInfo.UnlockedUntilUtc.HasValue && statusInfo.UnlockedUntilUtc.Value > DateTime.MinValue)
                    {
                        var expiryLocal = statusInfo.UnlockedUntilUtc.Value.ToLocalTime();
                        LastSessionExpiry.Text = $"Last session expiry: {expiryLocal:MMM dd, HH:mm}";
                    }
                    else
                    {
                        LastSessionExpiry.Text = "Last session expiry: N/A";
                    }
                }
            }
            else
            {
                LastSessionInfo.Visibility = Visibility.Collapsed;
            }

            RetryButton.Visibility = showRetry ? Visibility.Visible : Visibility.Collapsed;
            LockOverlay.Visibility = Visibility.Visible;
            // Hide the loading overlay so LockOverlay is visible
            Overlay.Visibility = Visibility.Collapsed;
            // CRITICAL: Hide WebView to prevent it rendering on top of overlay (WebView2 Z-order issue)
            MainWebView.Visibility = Visibility.Collapsed;
            Logger.Debug($"ShowLockOverlay completed: LockOverlay={LockOverlay.Visibility}, Overlay={Overlay.Visibility}, WebView={MainWebView.Visibility}");
        });
    }

    private void HideLockOverlay()
    {
        Logger.Debug("HideLockOverlay called");
        Dispatcher.Invoke(() =>
        {
            LockOverlay.Visibility = Visibility.Collapsed;
            // Also hide loading overlay when unlocking
            Overlay.Visibility = Visibility.Collapsed;
            // Show WebView now that we're unlocked (safe to do here - auth succeeded)
            MainWebView.Visibility = Visibility.Visible;
            Logger.Debug($"HideLockOverlay completed: LockOverlay={LockOverlay.Visibility}, Overlay={Overlay.Visibility}, WebView={MainWebView.Visibility}");
        });
    }

    private void DisableApplication()
    {
        // Disable WebView interaction
        if (MainWebView?.CoreWebView2 != null)
        {
            MainWebView.IsEnabled = false;
        }
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        ShowLockOverlay(LockReason.Checking, false);
        await Task.Delay(500);
        await CheckAuthenticationStatusAsync();
    }

    private Task SendCountdownUpdateAsync(TimeSpan? remaining, bool visible)
    {
        if (remaining.HasValue && remaining.Value.TotalSeconds > 0)
        {
            var minutes = (int)remaining.Value.TotalMinutes;
            var seconds = remaining.Value.Seconds;
            var timeRemaining = $"{minutes}:{seconds:D2}";
            
            return SendToWebAsync("countdown-update", new
            {
                visible = visible,
                timeRemaining = timeRemaining,
                totalSeconds = (int)remaining.Value.TotalSeconds
            });
        }
        else
        {
            return SendToWebAsync("countdown-update", new { visible = false });
        }
    }

    private record BridgeMessage(string? type, JsonElement? payload);

    private void LoadContractArtifacts()
    {
        try
        {
            var artifacts = _artifactLoader.LoadTokenImplementation();

            Logger.Debug($"Loading contract artifacts from: {Path.GetDirectoryName(artifacts.AbiPath)}");

            _tokenAbi = artifacts.Abi;
            _tokenBytecode = artifacts.Bytecode;

            if (!artifacts.HasAbi)
            {
                Logger.Warning($"Token ABI not found at: {artifacts.AbiPath}");
            }
            else
            {
                Logger.Info("Token ABI loaded successfully");
            }

            if (!artifacts.HasBytecode)
            {
                Logger.Warning($"Token bytecode not found at: {artifacts.BinPath}");
            }
            else if (string.IsNullOrWhiteSpace(_tokenBytecode))
            {
                Logger.Warning("Token bytecode file was empty or contained no hex characters");
            }
            else
            {
                Logger.Info($"Token bytecode loaded successfully (length: {_tokenBytecode.Length} chars)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load contract artifacts", ex);
            OverlayStatus.Text = $"Failed to load contract artifacts: {ex.Message}";
        }
    }
}
