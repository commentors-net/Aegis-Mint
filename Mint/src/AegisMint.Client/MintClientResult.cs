namespace AegisMint.Client;

public record MintClientResult<T>(
    bool Success,
    T? Value,
    int StatusCode,
    string? ErrorMessage);
