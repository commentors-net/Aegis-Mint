using System;

namespace AegisMint.Core.Contracts;

public record PingResponse(string Status, DateTimeOffset Utc);
