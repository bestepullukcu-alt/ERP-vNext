using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record DeleteTenantCommand(Guid TenantId) : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Delete, EntityType: "Tenant",
        EntityId: TenantId, TargetTenantId: TenantId, SourceModule: "tenant-registry");
}
