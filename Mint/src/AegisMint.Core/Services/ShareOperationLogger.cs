using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AegisMint.Core.Services;

/// <summary>
/// Enum for share operation types
/// </summary>
public enum ShareOperationType
{
    Creation,
    Retrieval
}

/// <summary>
/// Request model for logging share operations to the backend
/// </summary>
public class ShareOperationLogRequest
{
    [JsonPropertyName("operation_type")]
    public string OperationType { get; set; } = string.Empty;
    
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("operation_stage")]
    public string? OperationStage { get; set; }
    
    [JsonPropertyName("total_shares")]
    public int? TotalShares { get; set; }
    
    [JsonPropertyName("threshold")]
    public int? Threshold { get; set; }
    
    [JsonPropertyName("shares_used")]
    public int? SharesUsed { get; set; }
    
    [JsonPropertyName("token_name")]
    public string? TokenName { get; set; }
    
    [JsonPropertyName("token_address")]
    public string? TokenAddress { get; set; }
    
    [JsonPropertyName("network")]
    public string? Network { get; set; }
    
    [JsonPropertyName("shares_path")]
    public string? SharesPath { get; set; }
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
    
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Response model from share operation logging endpoint
/// </summary>
public class ShareOperationLogResponse
{
    public string Id { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Individual share operation log entry
/// </summary>
public class ShareOperationLogEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime? AtUtc { get; set; }
    public string DesktopAppId { get; set; } = string.Empty;
    public string AppType { get; set; } = string.Empty;
    public string? MachineName { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? OperationStage { get; set; }
    public int? TotalShares { get; set; }
    public int? Threshold { get; set; }
    public int? SharesUsed { get; set; }
    public string? TokenName { get; set; }
    public string? TokenAddress { get; set; }
    public string? Network { get; set; }
    public string? SharesPath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Response model for retrieving share operation logs
/// </summary>
public class ShareOperationLogsResponse
{
    public int Total { get; set; }
    public List<ShareOperationLogEntry> Logs { get; set; } = new();
}

/// <summary>
/// Service for logging share creation and retrieval operations to the backend
/// </summary>
public class ShareOperationLogger
{
    private readonly DesktopAuthenticationService _authService;
    private readonly JsonSerializerOptions _jsonOptions;

    public ShareOperationLogger(DesktopAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Logs a share operation to the backend server
    /// </summary>
    public async Task LogOperationAsync(
        ShareOperationType operationType,
        bool success,
        string? operationStage = null,
        int? totalShares = null,
        int? threshold = null,
        int? sharesUsed = null,
        string? tokenName = null,
        string? tokenAddress = null,
        string? network = null,
        string? sharesPath = null,
        string? errorMessage = null,
        string? notes = null)
    {
        try
        {
            var request = new ShareOperationLogRequest
            {
                OperationType = operationType.ToString(),
                Success = success,
                OperationStage = operationStage,
                TotalShares = totalShares,
                Threshold = threshold,
                SharesUsed = sharesUsed,
                TokenName = tokenName,
                TokenAddress = tokenAddress,
                Network = network,
                SharesPath = sharesPath,
                ErrorMessage = errorMessage,
                Notes = notes
            };

            var response = await _authService.PostAuthenticatedAsync<ShareOperationLogResponse>(
                "/share-operations/log",
                request);

            if (response?.Success == true)
            {
                Logger.Info($"Share operation logged to backend: {operationType} - Stage: {operationStage}");
            }
            else
            {
                Logger.Warning($"Failed to log share operation to backend: {response?.Message}");
            }
        }
        catch (Exception ex)
        {
            // Don't fail the main operation if logging fails
            Logger.Warning($"Error logging share operation to backend: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs share creation start
    /// </summary>
    public async Task LogShareCreationStartAsync(int totalShares, int threshold, string tokenName, string? network = null)
    {
        await LogOperationAsync(
            ShareOperationType.Creation,
            success: false, // Not yet complete
            operationStage: "Started",
            totalShares: totalShares,
            threshold: threshold,
            tokenName: tokenName,
            network: network
        );
    }

    /// <summary>
    /// Logs share creation success
    /// </summary>
    public async Task LogShareCreationSuccessAsync(
        int totalShares,
        int threshold,
        string tokenName,
        string sharesPath,
        string? tokenAddress = null,
        string? network = null)
    {
        await LogOperationAsync(
            ShareOperationType.Creation,
            success: true,
            operationStage: "Completed",
            totalShares: totalShares,
            threshold: threshold,
            tokenName: tokenName,
            tokenAddress: tokenAddress,
            network: network,
            sharesPath: sharesPath
        );
    }

    /// <summary>
    /// Logs share creation failure
    /// </summary>
    public async Task LogShareCreationFailureAsync(
        int totalShares,
        int threshold,
        string tokenName,
        string errorMessage,
        string? network = null)
    {
        await LogOperationAsync(
            ShareOperationType.Creation,
            success: false,
            operationStage: "Failed",
            totalShares: totalShares,
            threshold: threshold,
            tokenName: tokenName,
            network: network,
            errorMessage: errorMessage
        );
    }

    /// <summary>
    /// Logs share retrieval start
    /// </summary>
    public async Task LogShareRetrievalStartAsync(int sharesProvided, int threshold, string? tokenAddress = null)
    {
        await LogOperationAsync(
            ShareOperationType.Retrieval,
            success: false, // Not yet complete
            operationStage: "Started",
            threshold: threshold,
            sharesUsed: sharesProvided,
            tokenAddress: tokenAddress
        );
    }

    /// <summary>
    /// Logs share retrieval success
    /// </summary>
    public async Task LogShareRetrievalSuccessAsync(
        int sharesUsed,
        int threshold,
        string? tokenAddress = null,
        string? network = null)
    {
        await LogOperationAsync(
            ShareOperationType.Retrieval,
            success: true,
            operationStage: "Completed",
            threshold: threshold,
            sharesUsed: sharesUsed,
            tokenAddress: tokenAddress,
            network: network
        );
    }

    /// <summary>
    /// Logs share retrieval failure
    /// </summary>
    public async Task LogShareRetrievalFailureAsync(
        int sharesProvided,
        int threshold,
        string errorMessage,
        string? tokenAddress = null)
    {
        await LogOperationAsync(
            ShareOperationType.Retrieval,
            success: false,
            operationStage: "Failed",
            threshold: threshold,
            sharesUsed: sharesProvided,
            tokenAddress: tokenAddress,
            errorMessage: errorMessage
        );
    }

    /// <summary>
    /// Retrieves share operation logs from the backend
    /// </summary>
    /// <param name="desktopAppId">Optional: Filter by specific desktop ID</param>
    /// <param name="operationType">Optional: Filter by operation type (Creation or Retrieval)</param>
    /// <param name="limit">Maximum number of records to return (default: 100, max: 500)</param>
    /// <returns>Share operation logs response or null if failed</returns>
    public async Task<ShareOperationLogsResponse?> GetOperationLogsAsync(
        string? desktopAppId = null,
        string? operationType = null,
        int limit = 100)
    {
        try
        {
            var endpoint = $"/share-operations/logs?limit={limit}";
            
            if (!string.IsNullOrEmpty(desktopAppId))
            {
                endpoint += $"&desktop_app_id={desktopAppId}";
            }
            
            if (!string.IsNullOrEmpty(operationType))
            {
                endpoint += $"&operation_type={operationType}";
            }

            Logger.Info($"Retrieving share operation logs: limit={limit}, desktopId={desktopAppId ?? "all"}, type={operationType ?? "all"}");
            
            var response = await _authService.GetAuthenticatedAsync<ShareOperationLogsResponse>(endpoint);
            
            Logger.Info($"Retrieved {response?.Total ?? 0} share operation logs from backend");
            
            return response;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to retrieve share operation logs: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves the most recent share operations for the current desktop
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 10)</param>
    /// <returns>Recent share operation logs or null if failed</returns>
    public async Task<ShareOperationLogsResponse?> GetRecentOperationsAsync(int limit = 10)
    {
        try
        {
            var desktopAppId = _authService.DesktopAppId;
            return await GetOperationLogsAsync(desktopAppId, null, limit);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to retrieve recent operations: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves share creation operations for the current desktop
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 10)</param>
    /// <returns>Share creation logs or null if failed</returns>
    public async Task<ShareOperationLogsResponse?> GetCreationHistoryAsync(int limit = 10)
    {
        try
        {
            var desktopAppId = _authService.DesktopAppId;
            return await GetOperationLogsAsync(desktopAppId, "Creation", limit);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to retrieve creation history: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves share retrieval operations for the current desktop
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 10)</param>
    /// <returns>Share retrieval logs or null if failed</returns>
    public async Task<ShareOperationLogsResponse?> GetRetrievalHistoryAsync(int limit = 10)
    {
        try
        {
            var desktopAppId = _authService.DesktopAppId;
            return await GetOperationLogsAsync(desktopAppId, "Retrieval", limit);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to retrieve retrieval history: {ex.Message}");
            return null;
        }
    }
}
