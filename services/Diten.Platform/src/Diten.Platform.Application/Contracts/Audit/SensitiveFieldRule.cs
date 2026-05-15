namespace Diten.Platform.Application.Contracts.Audit;

public sealed record SensitiveFieldRule(string Pattern, string Replacement = "[REDACTED]");
