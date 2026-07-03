using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record ReactivateTenantCommand(Guid TenantId, string? Reason = null) : IRequest<Response<TenantLifecycleResultDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.TenantAdministration, Operation: AuditOperation.Reactivate, EntityType: "Tenant",
        EntityId: TenantId, TargetTenantId: TenantId, SourceModule: "tenant-registry",
        Metadata: new Dictionary<string, object?> { ["reason"] = Reason });
}
