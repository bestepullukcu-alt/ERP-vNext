using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

/// <summary>
/// Retires a definition. ⚠ It does NOT delete what the tenant already recorded: stored values stay readable
/// (§12), and only NEW writes are refused. Deactivation that took the data with it would make "we stopped
/// collecting this" indistinguishable from "this was never collected".
/// </summary>
public sealed record DeactivateOrganizationFieldDefinitionCommand(Guid Id, int ExpectedVersion)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Update,
        EntityType: "OrganizationFieldDefinition", EntityId: Id, SourceModule: "organization");
}
