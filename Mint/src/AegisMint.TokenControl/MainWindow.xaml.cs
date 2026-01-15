using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Threading;
using AegisMint.Core.Models;
using AegisMint.Core.Services;
using AegisMint.Core.Security;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Web3;

namespace AegisMint.TokenControl;

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
    private string? _htmlPath;
    private readonly ContractArtifactLoader _artifactLoader = new();
    private ContractArtifacts? _tokenArtifacts;
    private readonly VaultManager _vaultManager = new();
    private readonly TokenControlService _tokenControlService;
    private DesktopAuthenticationService? _authService;
    private DispatcherTimer? _approvalCheckTimer;
    private DispatcherTimer? _countdownTimer;
    private DateTime? _unlockedUntilUtc;
    private bool _hasAuthenticationSucceeded = false;
    private bool _hasAuthenticationChecked = false;
    private bool _hasNavigationCompleted = false;
    private string _currentNetwork = "sepolia";
    private string? _currentContractAddress;
    private string _cachedTitle = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        
        // Enable caching to reduce flickering
        CacheMode = new System.Windows.Media.BitmapCache { EnableClearType = true, RenderAtScale = 1.0 };
        
        _tokenControlService = new TokenControlService(_vaultManager);
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        Title = $"Aegis Token Control {GetAppVersion()}";
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Logger.Debug("OnLoaded - START");
        LoadInitialState();
        LoadContractArtifacts();
        await InitializeAuthenticationAsync();
        Logger.Debug("OnLoaded - Auth completed, starting WebView init");
        await InitializeWebViewAsync();
        Logger.Debug("OnLoaded - END");
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
        
        UpdateTitle();
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
            
            await SendToWebAsync("host-info", new { host = "Aegis Token Control WPF", version = "1.0", network = _currentNetwork });
            await SendVaultStatusAsync();
            await UpdateBalanceStatsAsync();
            await UpdatePauseStatusAsync();
            await SendFreezeHistoryAsync();
            Logger.Debug($"OnNavigationCompleted - After all async calls: LockOverlay={LockOverlay.Visibility}, Overlay={Overlay.Visibility}");
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
                case "refresh-balances":
                    await UpdateBalanceStatsAsync();
                    break;
                case "recover-from-shares":
                    await HandleRecoverFromSharesAsync();
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
        UpdateTitle();
        
        // Update TokenControlService with network and RPC URL
        var rpcUrl = GetRpcUrlForNetwork(_currentNetwork);
        _tokenControlService.SetNetwork(_currentNetwork, rpcUrl);
        
        // Get current contract address
        _currentContractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
        
        await SendVaultStatusAsync();
        await UpdateBalanceStatsAsync();
        await UpdatePauseStatusAsync();
        await SendFreezeHistoryAsync();
    }

    private string GetRpcUrlForNetwork(string network)
    {
        return network.ToLowerInvariant() switch
        {
            "localhost" => "http://127.0.0.1:8545",
            "mainnet" => "https://mainnet.infura.io/v3/fc5bd40a3f054a4f9842f53d0d711e0e",
            "sepolia" => "https://sepolia.infura.io/v3/fc6598ddab264c89a508cdb97d5398ea",
            _ => "http://127.0.0.1:8545"
        };
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
                await Task.Delay(5000);
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
            var version = "1.0.0"; // TODO: Get from assembly version
            var nameLabel = $"{machineName} - {osUser} (Registered: {DateTime.Now:yyyy-MM-dd HH:mm})";

            var response = await _authService.RegisterAsync(machineName, version, osUser, nameLabel);

            Logger.Info($"Desktop registered with status: {response.DesktopStatus}");

            ShowLockOverlay(LockReason.FirstTimeRegistration, false);

            await Task.Delay(5000);
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
        var baseTitle = $"Aegis Token Control {GetAppVersion()} - {_currentNetwork.ToUpperInvariant()}";
        
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
            return $"{id.Substring(0, 4)}...{id.Substring(id.Length - 3)}";
        }
        return id;
    }

    private void ShowLockOverlay(LockReason reason, bool showRetry, UnlockStatusResponse? statusInfo = null)
    {
        Logger.Debug($"ShowLockOverlay called: reason={reason}, showRetry={showRetry}");
        Dispatcher.Invoke(() =>
        {
            // Update app info
            LockAppVersion.Text = $"Token Control {GetAppVersion()}";
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
                await SendOperationResultAsync("Send", false, null, "Invalid recipient or amount", to);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Send", false, null, "No contract deployed on this network", to);
                return;
            }

            if (!decimal.TryParse(amountStr, out var amount))
            {
                await SendOperationResultAsync("Send", false, null, "Invalid amount format", to);
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

            await SendOperationResultAsync("Send", result.Success, result.TransactionHash, result.ErrorMessage, to);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling send tokens", ex);
            await SendOperationResultAsync("Send", false, null, ex.Message, null);
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
                await SendOperationResultAsync("Freeze", false, null, "Invalid address", address);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Freeze", false, null, "No contract deployed on this network", address);
                return;
            }

            var addressUtil = new AddressUtil();
            if (!addressUtil.IsValidEthereumAddressHexFormat(address))
            {
                await SendOperationResultAsync("Freeze", false, null, "Invalid address format", address);
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

            await SendOperationResultAsync("Freeze", result.Success, result.TransactionHash, result.ErrorMessage, address);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling freeze address", ex);
            await SendOperationResultAsync("Freeze", false, null, ex.Message, null);
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
            var reason = payload.Value.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(from))
            {
                await SendOperationResultAsync("Retrieve", false, null, "Invalid source address", from);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendOperationResultAsync("Retrieve", false, null, "No contract deployed on this network", from);
                return;
            }

            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(treasuryAddress))
            {
                await SendOperationResultAsync("Retrieve", false, null, "Treasury address not found", from);
                return;
            }

            Logger.Info($"Retrieving full balance from {from}");

            await SendProgressAsync("Wiping frozen address...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.RetrieveTokensAsync(
                _currentContractAddress,
                from,
                treasuryAddress,
                null,
                reason);

            await SendOperationResultAsync("Retrieve", result.Success, result.TransactionHash, result.ErrorMessage, from);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling retrieve tokens", ex);
            await SendOperationResultAsync("Retrieve", false, null, ex.Message, null);
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
                await SendOperationResultAsync("Pause", false, null, "No contract deployed on this network", _currentContractAddress);
                return;
            }

            Logger.Info($"{(paused ? "Pausing" : "Unpausing")} token contract");

            await SendProgressAsync($"{(paused ? "Pausing" : "Unpausing")} contract on blockchain...");
            await Task.Delay(100); // Give UI time to update

            var result = await _tokenControlService.SetPausedAsync(_currentContractAddress, paused);

            await SendOperationResultAsync("Pause", result.Success, result.TransactionHash, result.ErrorMessage, _currentContractAddress);
            if (result.Success)
            {
                await UpdateBalanceStatsAsync();
                await UpdatePauseStatusAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error handling set paused", ex);
            await SendOperationResultAsync("Pause", false, null, ex.Message, _currentContractAddress);
        }
    }

    private async Task HandleRecoverFromSharesAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select recovery share files",
                Filter = "Share files (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            var result = dialog.ShowDialog();
            if (result != true || dialog.FileNames.Length == 0)
            {
                await SendToWebAsync("host-error", new { message = "Recovery cancelled. No files selected." });
                return;
            }

            var shares = new List<ShareFilePayload>();
            ShareSetMetadata? metadata = null;
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            foreach (var file in dialog.FileNames)
            {
                var payload = ParseShareFile(file, jsonOptions, out var shareLength);

                if (metadata is null)
                {
                    metadata = new ShareSetMetadata(
                        payload.TotalShares,
                        payload.Threshold,
                        payload.ClientShareCount,
                        payload.SafekeepingShareCount,
                        payload.Network ?? string.Empty,
                        shareLength);
                }
                else if (!metadata.IsCompatibleWith(payload, shareLength))
                {
                    throw new InvalidOperationException($"Share file {Path.GetFileName(file)} metadata mismatch. Expected {metadata.Description}.");
                }

                shares.Add(payload);
            }

            if (metadata is null)
            {
                throw new InvalidOperationException("No valid shares were loaded.");
            }

            if (shares.Count < metadata.Threshold)
            {
                throw new InvalidOperationException($"Need at least {metadata.Threshold} shares; only {shares.Count} provided.");
            }

            var sharesToUse = shares
                .OrderBy(s => s.ShareId)
                .Take(metadata.Threshold)
                .Select(s => new ShamirShare(s.ShareId, s.ShareValue ?? string.Empty))
                .ToArray();

            var shamir = new ShamirSecretSharingService();
            var secretBytes = shamir.Combine(sharesToUse, metadata.Threshold);
            var encryptedMnemonic = Encoding.UTF8.GetString(secretBytes).Trim();
            
            // Decrypt the reconstructed mnemonic
            var encryptionKey = DeriveApplicationEncryptionKey();
            var mnemonicRaw = DecryptMnemonic(encryptedMnemonic, encryptionKey);
            var mnemonic = string.Join(" ", mnemonicRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            _vaultManager.ImportTreasuryMnemonic(mnemonic);

            if (!string.IsNullOrWhiteSpace(metadata.Network))
            {
                _vaultManager.SaveLastNetwork(metadata.Network);
                await UpdateNetworkAsync(metadata.Network);
            }

            await DiscoverAndPersistContractAsync();
            await SendVaultStatusAsync();
            await UpdateBalanceStatsAsync();
            await UpdatePauseStatusAsync();
            await SendToWebAsync("recovery-result", new { ok = true, message = "Treasury recovered from shares." });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to recover from shares", ex);
            await SendToWebAsync("host-error", new { message = $"Recovery failed: {ex.Message}" });
        }
    }

    private ShareFilePayload ParseShareFile(string path, JsonSerializerOptions options, out int shareLength)
    {
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<ShareFilePayload>(json, options)
            ?? throw new InvalidOperationException("File did not contain a share payload.");

        if (payload.ShareId <= 0)
        {
            throw new InvalidOperationException("ShareId must be greater than zero.");
        }

        if (payload.Threshold <= 0 || payload.TotalShares <= 0 || payload.Threshold > payload.TotalShares)
        {
            throw new InvalidOperationException("Invalid threshold/total share values.");
        }

        if (string.IsNullOrWhiteSpace(payload.ShareValue))
        {
            throw new InvalidOperationException("Missing share value.");
        }

        var shareBytes = Convert.FromBase64String(payload.ShareValue);
        if (shareBytes.Length == 0)
        {
            throw new InvalidOperationException("Share payload was empty.");
        }

        shareLength = shareBytes.Length;
        return payload;
    }

    private sealed record ShareSetMetadata(int TotalShares, int Threshold, int ClientShareCount, int SafekeepingShareCount, string Network, int ShareLength)
    {
        public string Description => $"{Threshold}-of-{TotalShares} ({Network})";

        public bool IsCompatibleWith(ShareFilePayload payload, int shareLength)
        {
            return payload.TotalShares == TotalShares
                   && payload.Threshold == Threshold
                   && payload.ClientShareCount == ClientShareCount
                   && payload.SafekeepingShareCount == SafekeepingShareCount
                   && shareLength == ShareLength
                   && string.Equals(payload.Network ?? string.Empty, Network, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class ShareFilePayload
    {
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string? Network { get; set; }
        public int TotalShares { get; set; }
        public int Threshold { get; set; }
        public int ClientShareCount { get; set; }
        public int SafekeepingShareCount { get; set; }
        public byte ShareId { get; set; }
        public string? ShareValue { get; set; }
        public int EncryptionVersion { get; set; }
    }

    // Application-specific encryption key derivation
    private string DeriveApplicationEncryptionKey()
    {
        // Application-specific constant embedded in code (MUST match Mint app)
        const string APP_SALT = "AegisMint-Recovery-v1-2026";
        
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(APP_SALT)));
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

    private async Task<bool> DiscoverAndPersistContractAsync()
    {
        try
        {
            var treasuryAddress = _vaultManager.GetTreasuryAddress();
            if (string.IsNullOrWhiteSpace(treasuryAddress))
            {
                return false;
            }

            var rpcUrl = GetRpcUrlForNetwork(_currentNetwork);
            var rpc = new JsonRpcClient(rpcUrl);

            var contractAddress = await DiscoverLatestContractAsync(rpc, treasuryAddress);
            if (string.IsNullOrWhiteSpace(contractAddress))
            {
                Logger.Warning("No deployed contracts discovered for treasury.");
                return false;
            }

            _vaultManager.RecordContractDeployment(contractAddress, _currentNetwork);
            _currentContractAddress = contractAddress;

            await PopulateSnapshotFromChainAsync(contractAddress, treasuryAddress, rpcUrl);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to discover and persist contract: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> DiscoverLatestContractAsync(JsonRpcClient rpc, string treasuryAddress)
    {
        try
        {
            var txCountHex = await rpc.GetTransactionCountAsync(treasuryAddress, "latest");
            var txCount = HexToBigInteger(txCountHex);

            var attempts = (int)Math.Min((long)txCount, 20); // scan last 20 nonces max
            for (var i = 0; i < attempts; i++)
            {
                var nonce = txCount - 1 - i;
                if (nonce < 0) break;

                var candidate = ComputeContractAddress(treasuryAddress, nonce);
                var codeElement = await rpc.SendRequestAsync("eth_getCode", candidate, "latest");
                var code = codeElement.GetString() ?? "0x";
                if (!string.Equals(code, "0x", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info($"Discovered deployed contract at {candidate} (nonce {nonce})");
                    return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Contract discovery failed: {ex.Message}");
        }

        return null;
    }

    private async Task PopulateSnapshotFromChainAsync(string contractAddress, string treasuryAddress, string rpcUrl)
    {
        try
        {
            if (_tokenArtifacts == null || string.IsNullOrWhiteSpace(_tokenArtifacts.Abi))
            {
                Logger.Warning("Token ABI unavailable; skipping snapshot enrichment.");
                return;
            }

            var web3 = new Web3(rpcUrl);
            var contract = web3.Eth.GetContract(_tokenArtifacts.Abi, contractAddress);

            var nameFn = contract.GetFunction("name");
            var decimalsFn = contract.GetFunction("decimals");
            var totalSupplyFn = contract.GetFunction("totalSupply");
            var balanceOfFn = contract.GetFunction("balanceOf");

            var name = await nameFn.CallAsync<string>();
            var decimals = await decimalsFn.CallAsync<byte>();
            var totalSupplyRaw = await totalSupplyFn.CallAsync<BigInteger>();
            var balanceRaw = await balanceOfFn.CallAsync<BigInteger>(treasuryAddress);

            var totalSupply = Web3.Convert.FromWei(totalSupplyRaw, decimals);
            var treasuryTokens = Web3.Convert.FromWei(balanceRaw, decimals);
            var treasuryEth = await _tokenControlService.GetEthBalanceAsync(treasuryAddress);

            var snapshot = new DeploymentSnapshot(
                _currentNetwork,
                contractAddress,
                name ?? "Recovered Token",
                totalSupply.ToString("0.####"),
                decimals,
                0,
                0,
                treasuryAddress,
                (treasuryEth ?? 0m).ToString("0.####"),
                treasuryTokens.ToString("0.####"),
                DateTimeOffset.UtcNow);

            _vaultManager.RecordDeploymentSnapshot(_currentNetwork, snapshot);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to populate snapshot from chain: {ex.Message}");
        }
    }

    private static BigInteger HexToBigInteger(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex == "0x" || hex == "0x0")
            return BigInteger.Zero;

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex.Substring(2);

        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }

    private static string ComputeContractAddress(string sender, BigInteger nonce)
    {
        var addressBytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(sender.Replace("0x", string.Empty));
        var nonceBytesLe = nonce.ToByteArray();
        var nonceBytes = nonceBytesLe.Reverse().SkipWhile(b => b == 0).ToArray();

        var encoded = RLP.EncodeList(RLP.EncodeElement(addressBytes), RLP.EncodeElement(nonceBytes));
        var hash = new Sha3Keccack().CalculateHash(encoded);
        var contractBytes = hash.Skip(hash.Length - 20).ToArray();
        return "0x" + BitConverter.ToString(contractBytes).Replace("-", "").ToLowerInvariant();
    }

    private async Task SendOperationResultAsync(string operation, bool success, string? transactionHash, string? errorMessage, string? address = null)
    {
        await SendToWebAsync("operation-result", new
        {
            operation,
            success,
            transactionHash,
            errorMessage,
            address,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    private async Task SendProgressAsync(string message)
    {
        await SendToWebAsync("operation-progress", new
        {
            message
        });
    }

    private async Task SendFreezeHistoryAsync()
    {
        try
        {
            var history = _vaultManager.GetFreezeOperations(_currentNetwork, 100);
            var frozen = history
                .Where(h => h.IsFrozen)
                .Select(h => new
                {
                    address = h.TargetAddress,
                    timestamp = h.CompletedAtUtc ?? h.CreatedAtUtc
                })
                .ToList();

            var unfrozen = history
                .Where(h => !h.IsFrozen)
                .Select(h => new
                {
                    address = h.TargetAddress,
                    timestamp = h.CompletedAtUtc ?? h.CreatedAtUtc
                })
                .ToList();

            await SendToWebAsync("freeze-history", new
            {
                frozen,
                unfrozen
            });
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to send freeze history: {ex.Message}");
        }
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
            var treasuryAddress = _vaultManager.GetKnownTreasuryAddress();
            var hasTreasuryKey = _vaultManager.HasTreasury();
            var contractAddress = _vaultManager.GetDeployedContractAddress(_currentNetwork);
            var snapshot = _vaultManager.GetDeploymentSnapshot(_currentNetwork);
            var bootstrapThreshold = _vaultManager.GetBootstrapThreshold();

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
                hasTreasuryKey,
                treasuryAddress,
                contractDeployed = !string.IsNullOrWhiteSpace(contractAddress),
                contractAddress,
                bootstrapThreshold,
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
            var treasuryAddress = _vaultManager.GetKnownTreasuryAddress();
            decimal? ethBalance = null;
            if (!string.IsNullOrWhiteSpace(treasuryAddress))
            {
                ethBalance = await _tokenControlService.GetEthBalanceAsync(treasuryAddress);
            }

            if (string.IsNullOrWhiteSpace(_currentContractAddress))
            {
                await SendToWebAsync("balance-stats", new
                {
                    tokenBalance = "N/A",
                    ethBalance = ethBalance?.ToString("N6") ?? "0.00",
                    contractAddress = "Not deployed",
                    totalSupply = "N/A"
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(treasuryAddress))
            {
                await SendToWebAsync("balance-stats", new
                {
                    tokenBalance = "N/A",
                    ethBalance = ethBalance?.ToString("N6") ?? "0.00",
                    contractAddress = _currentContractAddress,
                    totalSupply = "N/A"
                });
                return;
            }

            // Fetch balances and supply
            var tokenBalance = await _tokenControlService.GetTokenBalanceAsync(_currentContractAddress, treasuryAddress);
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
