using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Commands;

public sealed record CreateLegalEntityCommand(LegalEntityWriteRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Create,
        EntityType: "LegalEntity", SourceModule: "legal-entity");
}
