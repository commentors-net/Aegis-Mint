using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using WinForms = System.Windows.Forms;
using Microsoft.Win32;
using AegisMint.Core.Services;
using AegisMint.Core.Security;
using Nethereum.Web3;

namespace AegisMint.Mint;

public partial class MainWindow : Window
{
    private string? _htmlPath;
    private readonly VaultManager _vaultManager;
    private EthereumService? _ethereumService;
    private string _currentNetwork = "sepolia"; // default
    private string? _tokenAbi;
    private string? _tokenBytecode;

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
        LoadContractArtifacts();
        await InitializeWebViewAsync();
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
        if (e.IsSuccess)
        {
            Overlay.Visibility = Visibility.Collapsed;
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
            var prefill = snapshot is null ? null : MapSnapshotForUi(snapshot);
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
                balance = balance,
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
                    message = "Contract already deployed. Deployment disabled."
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
                    message = "Contract already deployed. Deployment disabled."
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
                        prefill = MapSnapshotForUi(snapshot)
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

    private object MapSnapshotForUi(DeploymentSnapshot snapshot)
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
            treasuryEth = snapshot.TreasuryEth,
            treasuryTokens = snapshot.TreasuryTokens,
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

            var shamir = new ShamirSecretSharingService();
            var secretBytes = Encoding.UTF8.GetBytes(mnemonic);
            var shares = shamir.Split(secretBytes, threshold, totalShares);

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
                    shareId = share.Id,
                    shareValue = share.Value
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

    private record BridgeMessage(string? type, JsonElement? payload);

    private void LoadContractArtifacts()
    {
        try
        {
            var basePath = Path.Combine(AppContext.BaseDirectory, "Resources");
            var abiPath = Path.Combine(basePath, "TokenImplementationV2.abi");
            var binPath = Path.Combine(basePath, "TokenImplementationV2.bin");

            Logger.Debug($"Loading contract artifacts from: {basePath}");

            if (File.Exists(abiPath))
            {
                _tokenAbi = File.ReadAllText(abiPath);
                Logger.Info("Token ABI loaded successfully");
            }
            else
            {
                Logger.Warning($"Token ABI not found at: {abiPath}");
            }

            if (File.Exists(binPath))
            {
                var rawBytecode = File.ReadAllText(binPath);
                _tokenBytecode = NormalizeHex(rawBytecode);

                if (string.IsNullOrWhiteSpace(_tokenBytecode))
                {
                    Logger.Warning("Token bytecode file was empty or contained no hex characters");
                }
                else
                {
                    Logger.Info($"Token bytecode loaded successfully (length: {_tokenBytecode.Length} chars)");
                }
            }
            else
            {
                Logger.Warning($"Token bytecode not found at: {binPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load contract artifacts", ex);
            OverlayStatus.Text = $"Failed to load contract artifacts: {ex.Message}";
        }
    }

    /// <summary>
    /// Strips whitespace, newlines, and an optional 0x prefix so the bytecode is pure hex.
    /// </summary>
    private static string NormalizeHex(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        // Keep only hex digits to avoid signer errors when converting to bytes.
        return new string(trimmed.Where(Uri.IsHexDigit).ToArray());
    }
}
