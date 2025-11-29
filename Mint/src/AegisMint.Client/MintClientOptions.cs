using System;

namespace AegisMint.Client;

public class MintClientOptions
{
    public Uri BaseAddress { get; set; } = new("https://localhost:5050");
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
