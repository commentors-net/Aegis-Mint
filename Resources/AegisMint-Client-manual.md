# AegisMint.Client (NuGet) Manual

## Purpose
Typed .NET client for the local AegisMint service (Windows service hosting the internal API).

## Installing the package
1. Create or point to a feed (example local feed): `mkdir D:\nuget-local`.
2. Pack from repo root:  
   `cd Mint`  
   `dotnet pack src/AegisMint.Client/AegisMint.Client.csproj -c Release -o ..\..\nuget-local /p:PackageVersion=0.1.0`
3. Add the feed to `NuGet.Config`:
```xml
<configuration>
  <packageSources>
    <add key="nuget-local" value="D:\nuget-local" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```
4. Consume: `dotnet add package AegisMint.Client --version 0.1.0`

## Quickstart
```csharp
using AegisMint.Client;

var client = MintClient.CreateDefault(new MintClientOptions
{
    BaseAddress = new Uri("https://localhost:5050"),
    Timeout = TimeSpan.FromSeconds(10)
});

var ping = await client.PingAsync();           // health
var info = await client.GetDeviceInfoAsync();  // metadata
var unlock = await client.UnlockForDevelopmentAsync(15); // only if allowed in service config
var mnemonic = await client.GetMnemonicAsync(); // succeeds only when unlocked
var logs = await client.GetRecentLogsAsync(200);
```

## Endpoints covered
- `GET /ping` → health
- `GET /getDeviceInfo` → device metadata
- `GET /getMnemonic` → mnemonic (requires unlocked state; never log it)
- `POST /governance/unlock/dev` → dev-only unlock (guarded by server config)
- `POST /governance/lock` → lock now
- `GET /logs/recent?limit=N` → recent log tail

## Security Guidance
- Do not log the mnemonic value; treat it as a secret.
- Use HTTPS binding on the service; default is localhost-only.
- For production, disable dev unlock and drive unlocks via governance signals from Aegis Web.
