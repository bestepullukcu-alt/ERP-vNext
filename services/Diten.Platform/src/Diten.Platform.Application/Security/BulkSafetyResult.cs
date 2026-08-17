namespace Diten.Platform.Application.Security;

public sealed record BulkSafetyResult(
    IReadOnlyCollection<Guid> EffectiveTargets,
    IReadOnlyCollection<Guid> SkippedSelfIds);
