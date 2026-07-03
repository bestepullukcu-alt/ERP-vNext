using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;

public sealed record PublishTemplateMasterVersionCommand(
    Guid TemplateMasterId,
    FileUploadInput File,
    string? ChangeSummary,
    bool AllowUnchanged,
    string CorrelationId)
    : IRequest<Response<TemplateMasterVersionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "TemplateMasterVersion",
        EntityId: TemplateMasterId,
        SourceModule: "MOD-0029-FU02",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
