using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;

public sealed record UpdateTenantModuleEntitlementExpiryCommand(Guid TenantId, Guid EntitlementId, UpdateTenantModuleEntitlementExpiryRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Update, EntityType: "TenantModuleEntitlement",
        EntityId: EntitlementId, TargetTenantId: TenantId, SourceModule: "subscription-billing");
}
