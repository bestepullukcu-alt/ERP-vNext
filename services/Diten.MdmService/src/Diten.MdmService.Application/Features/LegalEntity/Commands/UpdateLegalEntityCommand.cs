using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record UpdateLegalEntityCommand(Guid LegalEntityId, LegalEntityWriteRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "LegalEntity", SourceModule: "legal-entity");
}
