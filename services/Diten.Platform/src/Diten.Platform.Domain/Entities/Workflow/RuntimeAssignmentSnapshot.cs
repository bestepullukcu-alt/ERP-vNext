using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Workflow;

public sealed class RuntimeAssignmentSnapshot : TenantScopedEntity
{
    public required Guid WorkflowInstanceId { get; set; }
    public Guid? ApprovalTaskId { get; set; }
    public required string ResolverSource { get; set; }
    public required string ResolvedPrincipalId { get; set; }
    public List<string> CandidatePrincipalIds { get; set; } = [];
    public DateTime ResolvedAt { get; set; } = DateTime.UtcNow;
    public required string TieBreakExplanation { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
