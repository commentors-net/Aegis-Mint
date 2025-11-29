using System.Net;

namespace AegisMint.Client;

public record MintClientResult<T>(
    bool Success,
    T? Value,
    HttpStatusCode StatusCode,
    string? ErrorMessage);
