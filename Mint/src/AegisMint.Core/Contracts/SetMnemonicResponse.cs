using AegisMint.Core.Models;

namespace AegisMint.Core.Contracts;

public record SetMnemonicResponse(bool Success, string Message, IReadOnlyCollection<ShamirShare>? Shares = null);
