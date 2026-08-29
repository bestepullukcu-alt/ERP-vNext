using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Commands;

// MOD-0029-FU17 — controlled copy / withdrawal / reconciliation commands. Auditable via the central AuditBehavior.
// No hard delete.

internal static class ControlledCopyAudit
{
    public const string Module = "MOD-0029-FU17";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record RegisterControlledCopyCommand(Guid RegisterEntryId, RegisterControlledCopyInput Input, string CorrelationId)
    : IRequest<Response<ControlledCopyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Create, "DocumentControlledCopy", RegisterEntryId, CorrelationId);
}

public sealed record WithdrawControlledCopyCommand(Guid RegisterEntryId, Guid CopyId, WithdrawControlledCopyInput Input, string CorrelationId)
    : IRequest<Response<ControlledCopyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentControlledCopy", CopyId, CorrelationId);
}

public sealed record ReconcileControlledCopyCommand(Guid RegisterEntryId, Guid CopyId, ReconcileControlledCopyInput Input, string CorrelationId)
    : IRequest<Response<ControlledCopyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentControlledCopy", CopyId, CorrelationId);
}

public sealed record MarkControlledCopyMissingCommand(Guid RegisterEntryId, Guid CopyId, MarkControlledCopyMissingInput Input, string CorrelationId)
    : IRequest<Response<ControlledCopyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentControlledCopy", CopyId, CorrelationId);
}

public sealed record MarkControlledCopyObsoleteCommand(Guid RegisterEntryId, Guid CopyId, MarkControlledCopyObsoleteInput Input, string CorrelationId)
    : IRequest<Response<ControlledCopyModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentControlledCopy", CopyId, CorrelationId);
}

public sealed record GenerateWithdrawalPlanCommand(Guid RegisterEntryId, GenerateWithdrawalPlanInput Input, string CorrelationId)
    : IRequest<Response<WithdrawalPlanModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Create, "DocumentCopyWithdrawalPlan", RegisterEntryId, CorrelationId);
}

public sealed record CompleteWithdrawalPlanCommand(Guid RegisterEntryId, Guid PlanId, CompleteWithdrawalPlanInput Input, string CorrelationId)
    : IRequest<Response<WithdrawalPlanModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentCopyWithdrawalPlan", PlanId, CorrelationId);
}

public sealed record EvaluateObsoleteCopyReconciliationCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<ObsoleteCopyFindingModel>>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Execute, "DocumentObsoleteCopyFinding", RegisterEntryId, CorrelationId);
}

public sealed record ResolveObsoleteCopyFindingCommand(Guid RegisterEntryId, Guid FindingId, ResolveObsoleteFindingInput Input, string CorrelationId)
    : IRequest<Response<ObsoleteCopyFindingModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => ControlledCopyAudit.Meta(AuditOperation.Update, "DocumentObsoleteCopyFinding", FindingId, CorrelationId);
}
