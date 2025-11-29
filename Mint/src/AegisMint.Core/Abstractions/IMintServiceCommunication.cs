using AegisMint.Core.Contracts;

namespace AegisMint.Core.Abstractions;

/// <summary>
/// Abstraction for communication between AegisMint service and clients.
/// </summary>
public interface IMintServiceCommunication
{
    Task<ServiceResponse<PingResponse>> PingAsync(CancellationToken cancellationToken = default);
    Task<ServiceResponse<DeviceInfoResponse>> GetDeviceInfoAsync(CancellationToken cancellationToken = default);
    Task<ServiceResponse<MnemonicResponse>> GetMnemonicAsync(CancellationToken cancellationToken = default);
    Task<ServiceResponse<UnlockStatusResponse>> UnlockForDevelopmentAsync(int minutes, CancellationToken cancellationToken = default);
    Task<ServiceResponse<LockStatusResponse>> LockAsync(CancellationToken cancellationToken = default);
    Task<ServiceResponse<LogsResponse>> GetRecentLogsAsync(int limit, CancellationToken cancellationToken = default);
}

public class ServiceResponse<T>
{
    public bool Success { get; set; }
    public T? Value { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    public ServiceResponse(bool success, T? value, int statusCode, string? errorMessage = null)
    {
        Success = success;
        Value = value;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
    }
}
