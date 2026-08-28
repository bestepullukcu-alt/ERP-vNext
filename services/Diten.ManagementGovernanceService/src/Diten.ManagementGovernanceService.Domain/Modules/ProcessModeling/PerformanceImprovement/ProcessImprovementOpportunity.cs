namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.PerformanceImprovement;

public sealed class ProcessImprovementOpportunity : EntityBase
{
    public enum State { Open, Qualified, Closed }

    public ProcessImprovementOpportunity(
        Guid id,
        Guid tenantId,
        DateTime createdAtUtc,
        Guid processModelVersionId,
        string title,
        string problemStatement,
        string? expectedOutcome,
        int? priorityRank)
        : base(id, tenantId, createdAtUtc)
    {
        if (processModelVersionId == Guid.Empty) throw new ArgumentException(nameof(processModelVersionId));
        ProcessModelVersionId = processModelVersionId;
        Title = ProcessModelingText.Required(title, 200);
        ProblemStatement = ProcessModelingText.Required(problemStatement, 4000);
        ExpectedOutcome = ProcessModelingText.Optional(expectedOutcome, 2000);
        PriorityRank = ValidateOptionalPriority(priorityRank);
    }

    public Guid ProcessModelVersionId { get; }
    public string Title { get; private set; }
    public string ProblemStatement { get; private set; }
    public string? ExpectedOutcome { get; private set; }
    public int? PriorityRank { get; private set; }
    public State LifecycleState { get; private set; } = State.Open;

    public void Update(
        string title,
        string problemStatement,
        string? expectedOutcome,
        int? priorityRank,
        int expectedVersion,
        DateTime nowUtc)
    {
        Ensure(State.Open, expectedVersion);
        Title = ProcessModelingText.Required(title, 200);
        ProblemStatement = ProcessModelingText.Required(problemStatement, 4000);
        ExpectedOutcome = ProcessModelingText.Optional(expectedOutcome, 2000);
        PriorityRank = ValidateOptionalPriority(priorityRank);
        Touch(nowUtc);
    }

    public void Qualify(int expectedVersion, DateTime nowUtc)
    {
        Ensure(State.Open, expectedVersion);
        if (string.IsNullOrWhiteSpace(ExpectedOutcome)) throw new InvalidOperationException("expected_outcome_required");
        if (PriorityRank is null or <= 0) throw new InvalidOperationException("priority_rank_required");
        LifecycleState = State.Qualified;
        Touch(nowUtc);
    }

    public void RegisterOutcome(int expectedVersion, DateTime nowUtc)
    {
        Ensure(State.Qualified, expectedVersion);
        Touch(nowUtc);
    }

    public void Close(int outcomeCount, int expectedVersion, DateTime nowUtc)
    {
        Ensure(State.Qualified, expectedVersion);
        if (outcomeCount < 1) throw new InvalidOperationException("outcome_required_before_close");
        LifecycleState = State.Closed;
        Touch(nowUtc);
    }

    private void Ensure(State required, int expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("stale_concurrency");
        if (LifecycleState != required) throw new InvalidOperationException("lifecycle_conflict");
    }

    private static int? ValidateOptionalPriority(int? priorityRank)
    {
        if (priorityRank is <= 0) throw new ArgumentOutOfRangeException(nameof(priorityRank));
        return priorityRank;
    }
}
