using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Commands;

// MOD-0029-FU20 — downtime / temporary controlled issue commands. Auditable via the central AuditBehavior.
// No command deletes anything; closure, cancellation and reconciliation are status changes with evidence.

internal static class DowntimeAudit
{
    public const string Module = "MOD-0029-FU20";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── downtime events ──────────────────────────────────────────────────────────

public sealed record OpenRepositoryDowntimeEventCommand(OpenDowntimeEventInput Input, string CorrelationId)
    : IRequest<Response<DowntimeEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Create, "DocumentRepositoryDowntimeEvent", Guid.Empty, CorrelationId);
}

public sealed record MarkRepositoryRestoredCommand(Guid Id, MarkRepositoryRestoredInput Input, string CorrelationId)
    : IRequest<Response<DowntimeEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentRepositoryDowntimeEvent", Id, CorrelationId);
}

/// <summary>Explicit evaluation — there is deliberately no scheduler in this FU.</summary>
public sealed record EvaluateDowntimeEscalationCommand(Guid Id, string CorrelationId)
    : IRequest<Response<DowntimeEscalationEvaluationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Execute, "DocumentRepositoryDowntimeEvent", Id, CorrelationId);
}

public sealed record CloseRepositoryDowntimeEventCommand(Guid Id, CloseDowntimeEventInput Input, string CorrelationId)
    : IRequest<Response<DowntimeEventModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentRepositoryDowntimeEvent", Id, CorrelationId);
}

// ── temporary controlled issues ──────────────────────────────────────────────

public sealed record RequestTemporaryControlledIssueCommand(Guid DowntimeEventId, RequestTemporaryIssueInput Input, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Create, "DocumentTemporaryControlledIssue", DowntimeEventId, CorrelationId);
}

public sealed record ApproveTemporaryControlledIssueCommand(Guid DowntimeEventId, Guid IssueId, ApproveTemporaryIssueInput Input, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentTemporaryControlledIssue", IssueId, CorrelationId);
}

public sealed record IssueTemporaryControlledCopyCommand(Guid DowntimeEventId, Guid IssueId, IssueTemporaryControlledCopyInput Input, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentTemporaryControlledIssue", IssueId, CorrelationId);
}

public sealed record ReconcileTemporaryControlledIssueCommand(Guid DowntimeEventId, Guid IssueId, ReconcileTemporaryIssueInput Input, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentTemporaryControlledIssue", IssueId, CorrelationId);
}

public sealed record EvaluateTemporaryIssueOverdueCommand(Guid DowntimeEventId, Guid IssueId, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Execute, "DocumentTemporaryControlledIssue", IssueId, CorrelationId);
}

public sealed record CancelTemporaryControlledIssueCommand(Guid DowntimeEventId, Guid IssueId, CancelTemporaryIssueInput Input, string CorrelationId)
    : IRequest<Response<TemporaryControlledIssueModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => DowntimeAudit.Meta(AuditOperation.Update, "DocumentTemporaryControlledIssue", IssueId, CorrelationId);
}
