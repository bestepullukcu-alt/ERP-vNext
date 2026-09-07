namespace Diten.Platform.Application.Authorization;

public sealed record OrgDataScopeCandidateSet(IReadOnlyList<Guid> LegalEntityIds)
{
    public static OrgDataScopeCandidateSet Empty { get; } = new(Array.Empty<Guid>());
}
