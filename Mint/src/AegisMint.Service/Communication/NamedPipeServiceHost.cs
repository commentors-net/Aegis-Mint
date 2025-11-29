using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AegisMint.Core.Abstractions;
using AegisMint.Core.Configuration;
using AegisMint.Core.Contracts;
using AegisMint.Service.Governance;
using AegisMint.Service.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AegisMint.Service.Communication;

public class NamedPipeServiceHost : BackgroundService
{
    private readonly IGenesisVault _vault;
    private readonly GovernanceState _governance;
    private readonly IOptions<MintServiceOptions> _serviceOptions;
    private readonly ILogger<NamedPipeServiceHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public NamedPipeServiceHost(
        IGenesisVault vault,
        GovernanceState governance,
        IOptions<MintServiceOptions> serviceOptions,
        ILogger<NamedPipeServiceHost> logger,
        ILoggerFactory loggerFactory)
    {
        _vault = vault;
        _governance = governance;
        _serviceOptions = serviceOptions;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AegisMint Named Pipe Service starting on pipe: {PipeName}", _serviceOptions.Value.PipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipeServer = new NamedPipeServerStream(
                    _serviceOptions.Value.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(stoppingToken);
                _logger.LogInformation("Client connected to pipe");

                // Handle client in background, don't await
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleClientAsync(pipeServer, stoppingToken);
                    }
                    finally
                    {
                        pipeServer.Dispose();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipe server loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("AegisMint Named Pipe Service stopped");
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            // Read request
            using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cancellationToken);
            
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                _logger.LogWarning("Received empty request");
                return;
            }

            var request = JsonSerializer.Deserialize<ServiceRequest>(requestLine, _jsonOptions);
            if (request == null)
            {
                _logger.LogWarning("Failed to deserialize request");
                return;
            }

            // Process request
            var response = await ProcessRequestAsync(request, cancellationToken);
            
            // Write response
            using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = false };
            await SendResponseAsync(writer, response, cancellationToken);
            await writer.FlushAsync(cancellationToken);
            
            _logger.LogInformation("Request processed: {Command}", request.Command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection");
        }
    }

    private async Task<ServiceResponse> ProcessRequestAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Command.ToLowerInvariant() switch
            {
                "ping" => await HandlePingAsync(cancellationToken),
                "getdeviceinfo" => await HandleGetDeviceInfoAsync(cancellationToken),
                "hasmnemonic" => await HandleHasMnemonicAsync(cancellationToken),
                "setmnemonic" => await HandleSetMnemonicAsync(request, cancellationToken),
                "getmnemonic" => await HandleGetMnemonicAsync(cancellationToken),
                "unlockdev" => await HandleUnlockDevAsync(request, cancellationToken),
                "lock" => HandleLock(),
                "getrecentlogs" => await HandleGetRecentLogsAsync(request, cancellationToken),
                _ => new ServiceResponse(false, 404, null, $"Unknown command: {request.Command}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}", request.Command);
            return new ServiceResponse(false, 500, null, ex.Message);
        }
    }

    private async Task<ServiceResponse> HandlePingAsync(CancellationToken cancellationToken)
    {
        var response = new PingResponse("ok", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task<ServiceResponse> HandleGetDeviceInfoAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Device info requested");
        var device = await _vault.GetDeviceInfoAsync(cancellationToken);
        var response = new DeviceInfoResponse(device);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task<ServiceResponse> HandleHasMnemonicAsync(CancellationToken cancellationToken)
    {
        var hasMnemonic = await _vault.HasMnemonicAsync(cancellationToken);
        var device = await _vault.GetDeviceInfoAsync(cancellationToken);
        var response = new HasMnemonicResponse(hasMnemonic, device.DeviceId);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task<ServiceResponse> HandleSetMnemonicAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            string? mnemonic = null;
            if (request.Parameters?.TryGetValue("mnemonic", out var mnemonicObj) == true)
            {
                if (mnemonicObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                {
                    mnemonic = jsonElement.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(mnemonic))
            {
                return new ServiceResponse(false, 400, null, "Mnemonic is required");
            }

            await _vault.SetMnemonicAsync(mnemonic, cancellationToken);
            var shares = await _vault.GetOrCreateSharesAsync(cancellationToken);
            
            _logger.LogInformation("Genesis key set successfully with {ShareCount} shares", shares.Count);
            
            var response = new SetMnemonicResponse(true, $"Genesis key stored successfully. {shares.Count} shares generated.", shares);
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            return new ServiceResponse(true, 200, json);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid mnemonic provided");
            return new ServiceResponse(false, 400, null, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot set mnemonic");
            return new ServiceResponse(false, 409, null, ex.Message);
        }
    }

    private async Task<ServiceResponse> HandleGetMnemonicAsync(CancellationToken cancellationToken)
    {
        if (!_governance.IsUnlocked)
        {
            _logger.LogWarning("Mnemonic requested while locked");
            return new ServiceResponse(false, 423, null, "Device is locked");
        }

        var mnemonic = await _vault.GetOrCreateMnemonicAsync(cancellationToken);
        _loggerFactory.CreateLogger("Mnemonic").LogInformation("Mnemonic requested while unlocked");
        var response = new MnemonicResponse(mnemonic);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task<ServiceResponse> HandleUnlockDevAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (!_serviceOptions.Value.AllowDevBypassUnlock)
        {
            return new ServiceResponse(false, 403, null, "Dev unlock is disabled");
        }

        var minutes = _serviceOptions.Value.DefaultUnlockMinutes;
        if (request.Parameters?.TryGetValue("minutes", out var minutesObj) == true)
        {
            if (minutesObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            {
                minutes = jsonElement.GetInt32();
            }
        }

        minutes = Math.Max(1, minutes);
        var expiresAt = _governance.Unlock(TimeSpan.FromMinutes(minutes));
        _loggerFactory.CreateLogger("Governance").LogWarning(
            "Dev unlock invoked for {Minutes} minutes. Expires at {ExpiresAt}", minutes, expiresAt);

        var response = new UnlockStatusResponse("unlocked", expiresAt);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private ServiceResponse HandleLock()
    {
        _governance.Lock();
        _logger.LogInformation("Device locked");
        var response = new LockStatusResponse("locked");
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task<ServiceResponse> HandleGetRecentLogsAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        var limit = 200;
        if (request.Parameters?.TryGetValue("limit", out var limitObj) == true)
        {
            if (limitObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            {
                limit = jsonElement.GetInt32();
            }
        }

        var path = Path.IsPathRooted(_serviceOptions.Value.LogFilePath)
            ? _serviceOptions.Value.LogFilePath
            : Path.Combine(AppContext.BaseDirectory, _serviceOptions.Value.LogFilePath);

        if (!File.Exists(path))
        {
            return new ServiceResponse(false, 404, null, "Log file not found");
        }

        var max = Math.Min(Math.Max(limit, 1), 1000);
        var lines = await ReadLastLinesAsync(path, max);
        var response = new LogsResponse(lines);
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        return new ServiceResponse(true, 200, json);
    }

    private async Task SendResponseAsync(StreamWriter writer, ServiceResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await writer.WriteLineAsync(json);
    }

    private static async Task<IReadOnlyCollection<string>> ReadLastLinesAsync(string path, int limit)
    {
        var lines = new List<string>(limit);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null) break;
            lines.Add(line);
            if (lines.Count > limit)
            {
                lines.RemoveAt(0);
            }
        }
        return lines;
    }
}
