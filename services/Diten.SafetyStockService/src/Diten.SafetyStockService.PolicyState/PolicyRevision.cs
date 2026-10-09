namespace Diten.SafetyStockService.PolicyState;

public sealed class PolicyRevision
{
    private readonly string[] _lineageRevisionIds;
    private readonly string[] _materialContributors;

    internal PolicyRevision(
        string id,
        string policyId,
        PolicyScope scope,
        string? previousRevisionId,
        string contentKey,
        PolicyRevisionState state,
        long version,
        string preparedBy,
        IEnumerable<string> materialContributors,
        IEnumerable<string> lineageRevisionIds,
        RevisionDecision? decision)
    {
        Id = id;
        PolicyId = policyId;
        Scope = scope;
        PreviousRevisionId = previousRevisionId;
        ContentKey = contentKey;
        State = state;
        Version = version;
        PreparedBy = preparedBy;
        _materialContributors = materialContributors.ToArray();
        _lineageRevisionIds = lineageRevisionIds.ToArray();
        MaterialContributors = Array.AsReadOnly(_materialContributors);
        Decision = decision;
    }

    public string Id { get; }
    public string PolicyId { get; }
    public PolicyScope Scope { get; }
    public string? PreviousRevisionId { get; }
    public string ContentKey { get; }
    public PolicyRevisionState State { get; }
    public long Version { get; }
    public string PreparedBy { get; }
    public IReadOnlyList<string> MaterialContributors { get; }
    public RevisionDecision? Decision { get; }

    internal bool HasRevisionId(string id) => _lineageRevisionIds.Contains(id, StringComparer.Ordinal);

    internal IEnumerable<string> LineageRevisionIds => _lineageRevisionIds;
}
