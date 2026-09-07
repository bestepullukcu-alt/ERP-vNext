namespace Diten.Platform.Application.Authorization;

/// <summary>
/// Reads the bounded Platform-owned Organization/Position facts needed to derive Legal Entity candidates.
/// Implementations must enforce tenant scope and all limits before returning.
/// </summary>
public interface IOrgDataScopeCandidateFactReader
{
    Task<IReadOnlyList<Guid>> ResolveLegalEntityIdsAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset effectiveAtUtc,
        int maxCandidates,
        CancellationToken cancellationToken);
}
