using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AegisMint.Core.Security;

namespace AegisMint.Core.Services;

/// <summary>
/// Handles desktop application authentication and communication with the governance backend.
/// </summary>
public class DesktopAuthenticationService
{
    private readonly VaultManager _vaultManager;
    private readonly HttpClient _httpClient;
    private string _desktopAppId;
    private string _secretKey;
    private string _apiBaseUrl;

    public DesktopAuthenticationService(VaultManager vaultManager)
    {
        _vaultManager = vaultManager ?? throw new ArgumentNullException(nameof(vaultManager));
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        
        _desktopAppId = _vaultManager.GetDesktopAppId();
        _secretKey = _vaultManager.GetDesktopSecretKey();
        _apiBaseUrl = _vaultManager.GetApiBaseUrl();
    }

    public string DesktopAppId => _desktopAppId;

    /// <summary>
    /// Registers the desktop application with the backend server.
    /// </summary>
    public async Task<DesktopRegisterResponse> RegisterAsync(string machineName, string tokenControlVersion, string osUser, string nameLabel)
    {
        var requestBody = new
        {
            desktopAppId = _desktopAppId,
            machineName,
            tokenControlVersion,
            osUser,
            nameLabel
        };

        // Registration doesn't require authentication (first-time setup)
        var response = await PostUnauthenticatedAsync<DesktopRegisterResponse>("/desktop/register", requestBody);
        
        // If backend returned a secret key (first registration), save it
        if (!string.IsNullOrEmpty(response.SecretKey))
        {
            Logger.Info("Received secret key from backend - saving to vault");
            _secretKey = response.SecretKey;
            _vaultManager.SaveDesktopSecretKey(response.SecretKey);
        }
        
        return response;
    }

    /// <summary>
    /// Checks the unlock status of the desktop application.
    /// </summary>
    public async Task<UnlockStatusResponse> CheckUnlockStatusAsync()
    {
        var response = await GetAuthenticatedAsync<UnlockStatusResponse>($"/desktop/{_desktopAppId}/unlock-status");
        
        // Check if backend rotated the key
        if (!string.IsNullOrWhiteSpace(response.NewSecretKey))
        {
            Logger.Info("Backend rotated secret key - updating local storage");
            _secretKey = response.NewSecretKey;
            _vaultManager.SaveDesktopSecretKey(response.NewSecretKey);
        }
        
        return response;
    }

    /// <summary>
    /// Sends a heartbeat to the backend to keep the desktop registration alive.
    /// </summary>
    public async Task<DesktopRegisterResponse> HeartbeatAsync(string machineName, string tokenControlVersion, string osUser)
    {
        var requestBody = new
        {
            machineName,
            tokenControlVersion,
            osUser
        };

        var response = await PostAuthenticatedAsync<DesktopRegisterResponse>($"/desktop/{_desktopAppId}/heartbeat", requestBody);
        return response;
    }

    /// <summary>
    /// Generates and submits Certificate Signing Request (CSR) to backend
    /// </summary>
    public async Task<bool> SubmitCertificateRequestAsync(string machineName, string osUser)
    {
        try
        {
            // Generate CSR and private key
            var (csrPem, privateKeyPem) = CertificateManager.GenerateCertificateRequest(
                _desktopAppId,
                machineName,
                osUser
            );

            // Store private key immediately (we'll need it when cert is signed)
            _vaultManager.SavePrivateKey(privateKeyPem);

            // Submit CSR to backend
            var requestBody = new { csr_pem = csrPem };
            var response = await PostAuthenticatedAsync<SubmitCSRResponse>(
                $"/desktop/{_desktopAppId}/submit-csr",
                requestBody
            );

            return response.Success;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to submit certificate request: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if signed certificate is available from backend
    /// </summary>
    public async Task<CertificateResponse?> GetCertificateAsync()
    {
        try
        {
            var response = await GetAuthenticatedAsync<CertificateResponse>(
                $"/desktop/{_desktopAppId}/certificate"
            );

            // Store certificate if received
            if (!string.IsNullOrEmpty(response.CertificatePem))
            {
                var privateKeyPem = _vaultManager.GetPrivateKey();

                if (!string.IsNullOrEmpty(privateKeyPem))
                {
                    CertificateManager.StoreCertificate(_vaultManager, response.CertificatePem, privateKeyPem);
                }
            }

            return response;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            // Certificate not yet issued
            return null;
        }
    }

    /// <summary>
    /// Performs an authenticated GET request.
    /// </summary>
    private async Task<T> GetAuthenticatedAsync<T>(string endpoint)
    {
        var url = _apiBaseUrl.TrimEnd('/') + endpoint;
        var timestamp = HmacSignature.GetUnixTimestamp();
        var message = HmacSignature.CreateSignatureMessage(_desktopAppId, timestamp, "");
        var signature = HmacSignature.ComputeSignature(message, _secretKey);

        Logger.Debug($"Full URL: {url}");
        Logger.Debug($"Base URL: {_apiBaseUrl}, Endpoint: {endpoint}");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Desktop-Id", _desktopAppId);
        request.Headers.Add("X-Desktop-Timestamp", timestamp.ToString());
        request.Headers.Add("X-Desktop-Signature", signature);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Logger.Debug($"Response: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Logger.Error($"API call failed to {url}: {response.StatusCode} - {content}");
            throw new HttpRequestException($"API request failed: {response.StatusCode} - {content}");
        }

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    /// <summary>
    /// Performs an authenticated POST request.
    /// </summary>
    private async Task<T> PostAuthenticatedAsync<T>(string endpoint, object body)
    {
        var url = _apiBaseUrl.TrimEnd('/') + endpoint;
        var timestamp = HmacSignature.GetUnixTimestamp();
        var bodyJson = JsonSerializer.Serialize(body);
        var message = HmacSignature.CreateSignatureMessage(_desktopAppId, timestamp, bodyJson);
        var signature = HmacSignature.ComputeSignature(message, _secretKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Desktop-Id", _desktopAppId);
        request.Headers.Add("X-Desktop-Timestamp", timestamp.ToString());
        request.Headers.Add("X-Desktop-Signature", signature);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API request failed: {response.StatusCode} - {content}");
        }

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    /// <summary>
    /// Performs an unauthenticated POST request (for registration).
    /// </summary>
    private async Task<T> PostUnauthenticatedAsync<T>(string endpoint, object body)
    {
        var url = _apiBaseUrl.TrimEnd('/') + endpoint;
        var bodyJson = JsonSerializer.Serialize(body);

        Logger.Debug($"POST Full URL: {url}");
        Logger.Debug($"POST Base URL: {_apiBaseUrl}, Endpoint: {endpoint}");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Logger.Debug($"POST Response: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Logger.Error($"POST API call failed to {url}: {response.StatusCode} - {content}");
            throw new HttpRequestException($"API request failed: {response.StatusCode} - {content}");
        }

        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Response DTOs
public class DesktopRegisterResponse
{
    public string DesktopStatus { get; set; } = "";
    public int RequiredApprovalsN { get; set; }
    public int UnlockMinutes { get; set; }
    public string? SecretKey { get; set; }  // Populated on first registration
}

public class UnlockStatusResponse
{
    public string DesktopStatus { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedUntilUtc { get; set; }
    public int RemainingSeconds { get; set; }
    public int RequiredApprovalsN { get; set; }
    public int ApprovalsSoFar { get; set; }
    public string SessionStatus { get; set; } = "";
    public string? NewSecretKey { get; set; }  // Populated if backend rotated the key
}

public class SubmitCSRResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string DesktopAppId { get; set; } = "";
}

public class CertificateResponse
{
    public string CertificatePem { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
