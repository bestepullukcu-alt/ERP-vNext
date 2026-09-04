using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record RemoveTenantManualModuleOverrideCommand(Guid TenantId, Guid EntitlementId, RemoveTenantManualModuleOverrideRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider, ITransactionOwnedAuditCommand
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Revoke, EntityType: "TenantModuleEntitlement",
        EntityId: EntitlementId, TargetTenantId: TenantId, SourceModule: "subscription-billing");
}
