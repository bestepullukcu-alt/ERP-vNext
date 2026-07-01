using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;

public sealed record CreateTemplateMasterCommand(CreateTemplateMasterInput Input, string CorrelationId)
    : IRequest<Response<TemplateMasterDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "TemplateMaster",
        SourceModule: "MOD-0029-FU02",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
