using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
/// Response model from share operation logging endpoint
/// </summary>
public class ShareOperationLogResponse
{
    public string Id { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
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
                "/api/share-operations/log",
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
}
