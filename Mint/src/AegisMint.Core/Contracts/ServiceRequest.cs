namespace AegisMint.Core.Contracts;

public record ServiceRequest(string Command, Dictionary<string, object>? Parameters = null);
