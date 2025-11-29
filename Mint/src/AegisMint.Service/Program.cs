using AegisMint.Core.Abstractions;
using AegisMint.Core.Configuration;
using AegisMint.Core.Storage;
using AegisMint.Core.Vault;
using AegisMint.Service.Communication;
using AegisMint.Service.Governance;
using AegisMint.Service.Logging;
using AegisMint.Service.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

var builder = Host.CreateApplicationBuilder(args);

// Configure as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AegisMint Service";
});

// Configure options
builder.Services.Configure<MintOptions>(builder.Configuration.GetSection("Mint"));
builder.Services.Configure<MintServiceOptions>(builder.Configuration.GetSection("Service"));

// Configure core services
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

// Configure Named Pipe communication host
builder.Services.AddHostedService<NamedPipeServiceHost>();

// Configure logging
var serviceOptions = builder.Configuration.GetSection("Service").Get<MintServiceOptions>() ?? new MintServiceOptions();
var logFilePath = Path.IsPathRooted(serviceOptions.LogFilePath)
    ? serviceOptions.LogFilePath
    : Path.Combine(AppContext.BaseDirectory, serviceOptions.LogFilePath);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(logFilePath));

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("AegisMint Service starting with Named Pipe: {PipeName}", serviceOptions.PipeName);

await host.RunAsync();
