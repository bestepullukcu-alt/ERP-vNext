namespace Diten.SafetyStockService.PolicyState;

public sealed class PolicyRevisionResult
{
    private PolicyRevisionResult(PolicyRevision? revision, PolicyRevisionError? error, string? detail)
    {
        Revision = revision;
        Error = error;
        Detail = detail;
    }

    public bool IsSuccess => Revision is not null;
    public PolicyRevision? Revision { get; }
    public PolicyRevisionError? Error { get; }
    public string? Detail { get; }

    internal static PolicyRevisionResult Success(PolicyRevision revision) => new(revision, null, null);

    internal static PolicyRevisionResult Failure(PolicyRevisionError error, string detail) => new(null, error, detail);
}
