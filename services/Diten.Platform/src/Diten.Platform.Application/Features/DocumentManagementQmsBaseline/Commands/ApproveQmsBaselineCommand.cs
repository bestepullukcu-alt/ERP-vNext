using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

/// <summary>
/// MOD-0028-FU08 — transition a DRAFT baseline to APPROVED. Freezes the immutable snapshot/manifest at review time.
/// Does NOT make the baseline live/instantiable (that is MarkEffective).
/// </summary>
public sealed record ApproveQmsBaselineCommand(
    Guid BaselineReleaseId,
    int ExpectedVersion,
    string? ApprovalReference,
    string? ApprovalComment,
    string CorrelationId) : IRequest<Response<QmsBaselineSummaryModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "QmsBaseline",
        EntityId: BaselineReleaseId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
