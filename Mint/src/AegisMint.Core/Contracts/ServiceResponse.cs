namespace AegisMint.Core.Contracts;

public record ServiceResponse(bool Success, int StatusCode, string? Data = null, string? ErrorMessage = null);
