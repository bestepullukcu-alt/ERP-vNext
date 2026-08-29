using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Commands;

// MOD-0029-FU10 — release gate evaluation + evidence commands. Auditable via the central AuditBehavior. No hard delete.

internal static class ReleaseGateAudit
{
    public const string Module = "MOD-0029-FU10";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

public sealed record EvaluateReleaseGatesCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<ReleaseGateEvaluationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Execute, "DocumentReleaseGateEvaluation",
        EntityId: RegisterEntryId, SourceModule: ReleaseGateAudit.Module, CorrelationId: ReleaseGateAudit.Correlation(CorrelationId));
}

public sealed record RecordReleaseGateEvidenceCommand(Guid RegisterEntryId, RecordReleaseGateEvidenceInput Input, string CorrelationId)
    : IRequest<Response<ReleaseGateEvaluationModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Assign, "DocumentReleaseGateEvidence",
        EntityId: RegisterEntryId, SourceModule: ReleaseGateAudit.Module, CorrelationId: ReleaseGateAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["gateKey"] = Input.GateKey });
}
