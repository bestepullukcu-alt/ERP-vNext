using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;

public sealed record DeleteTemplateMasterCommand(Guid TemplateMasterId, string CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Delete,
        "TemplateMaster",
        EntityId: TemplateMasterId,
        SourceModule: "MOD-0029-FU02",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
