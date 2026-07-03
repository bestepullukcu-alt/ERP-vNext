using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record UpdateOrganizationUnitCommand(Guid Id, OrganizationUnitRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Update, EntityType: "OrganizationUnit",
        EntityId: Id, SourceModule: "organization");
}
