namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

public static class PerformanceImprovementContract
{
    public const string ModuleCode = "MOD-0355";
    public const string EventName = "management-governance.process-performance-improvement-audit-intent.submitted.v1";
    public const string EventType = "ProcessPerformanceImprovementAuditIntentSubmittedV1";
    public const int EventVersion = 1;
    public const string SchemaIdentity = "urn:diten:management-governance:process-performance-improvement-audit-intent-submitted:v1";

    public enum Command
    {
        CreateOpportunity,
        UpdateOpportunity,
        QualifyOpportunity,
        CloseOpportunity,
        RecordOutcome
    }

    public enum Participant { Business = 1, Receipt = 2, AuditIntent = 3, Outbox = 4 }

    public sealed record MutationRequest(
        Guid TenantId,
        Guid ActorId,
        Guid AggregateId,
        Guid ProcessModelVersionId,
        Guid? OutcomeId,
        Command CommandFamily,
        string Permission,
        string IdempotencyKey,
        string CanonicalPayloadHash,
        int ExpectedVersion,
        string? Title,
        string? ProblemStatement,
        string? ExpectedOutcome,
        int? PriorityRank,
        string? Summary,
        DateTime OccurredAtUtc);

    public sealed record MutationResult(bool Success, int StatusCode, string Code, Guid AggregateId)
    {
        public static MutationResult Unavailable(Guid id, string code = "performance_improvement_unavailable") => new(false, 503, code, id);
        public static MutationResult Conflict(Guid id, string code = "performance_improvement_conflict") => new(false, 409, code, id);
        public static MutationResult NotFound(Guid id) => new(false, 404, "performance_improvement_not_found", id);
        public static MutationResult Invalid(Guid id, string code = "performance_improvement_invalid") => new(false, 400, code, id);
    }

    public interface IMutationStore
    {
        Task<MutationResult> ExecuteAsync(MutationRequest request, CancellationToken cancellationToken = default);
    }

    public sealed record OpportunityDto(
        Guid Id,
        Guid ProcessModelVersionId,
        string Title,
        string ProblemStatement,
        string? ExpectedOutcome,
        int? PriorityRank,
        string LifecycleState,
        int Version,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc,
        IReadOnlyList<OutcomeDto> Outcomes);

    public sealed record OutcomeDto(Guid Id, string Summary, DateTime ObservedAtUtc);

    public interface IQueryStore
    {
        Task<IReadOnlyList<OpportunityDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<OpportunityDto?> GetAsync(Guid tenantId, Guid opportunityId, CancellationToken cancellationToken = default);
    }
}
