using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record ArchiveLegalEntityCommand(Guid LegalEntityId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    // Platform's audit enum has no Archive; a soft-retire maps to Deactivate (mirrors ArchiveOrganizationUnit).
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Deactivate,
        EntityType: "LegalEntity", SourceModule: "legal-entity");
}
