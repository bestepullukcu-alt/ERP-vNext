using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record ActivateLegalEntityCommand(Guid LegalEntityId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Activate,
        EntityType: "LegalEntity", SourceModule: "legal-entity");
}
