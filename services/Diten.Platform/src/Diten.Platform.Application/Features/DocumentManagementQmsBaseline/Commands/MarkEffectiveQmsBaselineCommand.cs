using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

/// <summary>
/// MOD-0028-FU08 — transition an APPROVED baseline to EFFECTIVE (the single live canonical baseline for its
/// tenant + source key). Supersedes the previously Effective baseline of the same source key. Blocked when the
/// source register/package is still Draft/not-for-execution.
/// </summary>
public sealed record MarkEffectiveQmsBaselineCommand(
    Guid BaselineReleaseId,
    int ExpectedVersion,
    string CorrelationId) : IRequest<Response<QmsBaselineSummaryModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Activate, "QmsBaseline",
        EntityId: BaselineReleaseId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
