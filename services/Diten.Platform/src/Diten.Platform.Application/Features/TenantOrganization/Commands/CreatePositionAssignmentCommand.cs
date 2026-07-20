using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record CreatePositionAssignmentCommand(PositionAssignmentRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Create, EntityType: "PositionAssignment",
        SourceModule: "organization");
}
