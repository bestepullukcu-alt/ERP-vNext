using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Commands;

// MOD-0029-FU13 — suspension / retirement / temporary-instruction commands. Auditable via the central AuditBehavior.
// No hard delete anywhere.

internal static class SuspensionAudit
{
    public const string Module = "MOD-0029-FU13";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

// ── suspension ───────────────────────────────────────────────────────────────

public sealed record OpenSuspensionCaseCommand(Guid RegisterEntryId, OpenSuspensionCaseInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Create, "DocumentSuspensionCase", RegisterEntryId, CorrelationId);
}

public sealed record EscalateSuspensionCaseCommand(Guid RegisterEntryId, Guid CaseId, EscalateSuspensionCaseInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentSuspensionCase", CaseId, CorrelationId);
}

public sealed record ApproveSuspensionCommand(Guid RegisterEntryId, Guid CaseId, ApproveSuspensionInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentSuspensionCase", CaseId, CorrelationId);
}

public sealed record RejectSuspensionCommand(Guid RegisterEntryId, Guid CaseId, RejectSuspensionInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentSuspensionCase", CaseId, CorrelationId);
}

public sealed record ExecuteSuspensionCommand(Guid RegisterEntryId, Guid CaseId, ExecuteSuspensionInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Suspend, "DocumentSuspensionCase", CaseId, CorrelationId);
}

public sealed record CloseSuspensionCaseCommand(Guid RegisterEntryId, Guid CaseId, CloseSuspensionCaseInput Input, string CorrelationId)
    : IRequest<Response<SuspensionCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentSuspensionCase", CaseId, CorrelationId);
}

// ── retirement ───────────────────────────────────────────────────────────────

public sealed record RequestRetirementCommand(Guid RegisterEntryId, RequestRetirementInput Input, string CorrelationId)
    : IRequest<Response<RetirementCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Create, "DocumentRetirementCase", RegisterEntryId, CorrelationId);
}

public sealed record ApproveRetirementCommand(Guid RegisterEntryId, Guid CaseId, ApproveRetirementInput Input, string CorrelationId)
    : IRequest<Response<RetirementCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentRetirementCase", CaseId, CorrelationId);
}

public sealed record RejectRetirementCommand(Guid RegisterEntryId, Guid CaseId, RejectRetirementInput Input, string CorrelationId)
    : IRequest<Response<RetirementCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "DocumentRetirementCase", CaseId, CorrelationId);
}

public sealed record ExecuteRetirementCommand(Guid RegisterEntryId, Guid CaseId, ExecuteRetirementInput Input, string CorrelationId)
    : IRequest<Response<RetirementCaseModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Deactivate, "DocumentRetirementCase", CaseId, CorrelationId);
}

// ── temporary instruction ────────────────────────────────────────────────────

public sealed record StartTemporaryInstructionControlCommand(Guid RegisterEntryId, StartTemporaryInstructionInput Input, string CorrelationId)
    : IRequest<Response<TemporaryInstructionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Create, "TemporaryInstructionControl", RegisterEntryId, CorrelationId);
}

public sealed record EvaluateTemporaryInstructionExpiryCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<TemporaryInstructionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Execute, "TemporaryInstructionControl", RegisterEntryId, CorrelationId);
}

public sealed record CloseTemporaryInstructionCommand(Guid RegisterEntryId, CloseTemporaryInstructionInput Input, string CorrelationId)
    : IRequest<Response<TemporaryInstructionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => SuspensionAudit.Meta(AuditOperation.Update, "TemporaryInstructionControl", RegisterEntryId, CorrelationId);
}
