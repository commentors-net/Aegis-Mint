using System.Collections.Generic;

namespace AegisMint.Core.Contracts;

public record LogsResponse(IReadOnlyCollection<string> Lines);
