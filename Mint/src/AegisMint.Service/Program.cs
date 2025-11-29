using AegisMint.Core.Abstractions;
using AegisMint.Core.Configuration;
using AegisMint.Core.Contracts;
using AegisMint.Core.Storage;
using AegisMint.Core.Vault;
using AegisMint.Service.Governance;
using AegisMint.Service.Options;
using AegisMint.Service.Logging;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using System.IO;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.Configure<MintOptions>(builder.Configuration.GetSection("Mint"));
builder.Services.Configure<MintServiceOptions>(builder.Configuration.GetSection("Service"));

builder.Services.AddSingleton<IProtectedKeyStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MintOptions>>().Value;
    var path = Path.IsPathRooted(options.DataDirectory)
        ? options.DataDirectory
        : Path.Combine(AppContext.BaseDirectory, options.DataDirectory);
    return new FileProtectedKeyStore(path);
});
builder.Services.AddSingleton<IGenesisVault, GenesisVault>();
builder.Services.AddSingleton<GovernanceState>();

var serviceOptions = builder.Configuration.GetSection("Service").Get<MintServiceOptions>() ?? new MintServiceOptions();
var logFilePath = Path.IsPathRooted(serviceOptions.LogFilePath)
    ? serviceOptions.LogFilePath
    : Path.Combine(AppContext.BaseDirectory, serviceOptions.LogFilePath);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(logFilePath));

builder.WebHost.ConfigureKestrel(options =>
{
    if (serviceOptions.UseHttps)
    {
        options.ListenLocalhost(serviceOptions.Port, listenOptions => listenOptions.UseHttps());
    }
    else
    {
        options.ListenLocalhost(serviceOptions.Port);
    }
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

if (serviceOptions.UseHttps)
{
    app.UseHttpsRedirection();
}

app.MapGet("/ping", () =>
{
    return TypedResults.Ok(new PingResponse("ok", DateTimeOffset.UtcNow));
}).WithName("Ping");

app.MapGet("/getDeviceInfo", async Task<Results<Ok<DeviceInfoResponse>, StatusCodeHttpResult>> (
    IGenesisVault vault,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var requestLogger = loggerFactory.CreateLogger("DeviceInfo");
    requestLogger.LogInformation("Device info requested.");
    var device = await vault.GetDeviceInfoAsync(cancellationToken);
    return TypedResults.Ok(new DeviceInfoResponse(device));
}).WithName("GetDeviceInfo");

app.MapGet("/getMnemonic", async Task<IResult> (
    IGenesisVault vault,
    GovernanceState governance,
    IOptions<MintServiceOptions> options,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (!governance.IsUnlocked)
    {
        return Results.StatusCode(StatusCodes.Status423Locked);
    }

    var mnemonic = await vault.GetOrCreateMnemonicAsync(cancellationToken);
    loggerFactory.CreateLogger("Mnemonic").LogInformation("Mnemonic requested while unlocked.");
    // Never log or expose the mnemonic outside of this controlled response.
    return TypedResults.Ok(new MnemonicResponse(mnemonic));
}).WithName("GetMnemonic");

app.MapPost("/governance/unlock/dev", Results<Ok<object>, ForbidHttpResult> (
    GovernanceState governance,
    IOptions<MintServiceOptions> options,
    ILoggerFactory loggerFactory,
    UnlockRequest? request) =>
{
    if (!options.Value.AllowDevBypassUnlock)
    {
        return TypedResults.Forbid();
    }

    var minutes = request?.Minutes ?? options.Value.DefaultUnlockMinutes;
    minutes = Math.Max(1, minutes);
    var expiresAt = governance.Unlock(TimeSpan.FromMinutes(minutes));
    loggerFactory.CreateLogger("Governance").LogWarning("Dev unlock invoked for {Minutes} minutes. Expires at {ExpiresAt}", minutes, expiresAt);

    return TypedResults.Ok<object>(new
    {
        status = "unlocked",
        expiresAt
    });
}).WithName("DevUnlock");

app.MapPost("/governance/lock", (GovernanceState governance) =>
{
    governance.Lock();
    return TypedResults.Ok(new { status = "locked" });
}).WithName("Lock");

app.MapGet("/logs/recent", async Task<Results<Ok<LogsResponse>, NotFound>> (
    IOptions<MintServiceOptions> options,
    int? limit) =>
{
    var path = Path.IsPathRooted(options.Value.LogFilePath)
        ? options.Value.LogFilePath
        : Path.Combine(AppContext.BaseDirectory, options.Value.LogFilePath);

    if (!File.Exists(path))
    {
        return TypedResults.NotFound();
    }

    var max = Math.Min(Math.Max(limit ?? 200, 1), 1000);
    var lines = await ReadLastLinesAsync(path, max);
    return TypedResults.Ok(new LogsResponse(lines));
}).WithName("RecentLogs");

app.Run();

static async Task<IReadOnlyCollection<string>> ReadLastLinesAsync(string path, int limit)
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
