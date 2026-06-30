using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record CreateManualQmsBaselineCommand(
    ManualQmsBaselineRequestModel Request,
    string CorrelationId) : IRequest<Response<QmsBaselineSummaryModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "QmsBaseline",
        SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
