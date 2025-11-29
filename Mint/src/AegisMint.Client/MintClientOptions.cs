using System;

namespace AegisMint.Client;

public class MintClientOptions
{
    public string PipeName { get; set; } = "AegisMint_Service";
    public int ConnectTimeout { get; set; } = 5000; // milliseconds
}
