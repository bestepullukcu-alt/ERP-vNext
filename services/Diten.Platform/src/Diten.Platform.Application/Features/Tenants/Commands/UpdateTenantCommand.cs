using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantCommand(Guid TenantId, TenantUpdateRequest Request) : IRequest<Response<TenantDetailDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Update, EntityType: "Tenant",
        EntityId: TenantId, TargetTenantId: TenantId, SourceModule: "tenant-registry");
}
