using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record ActivateTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, ActivateTenantSubscriptionRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Activate, EntityType: "TenantSubscription",
        EntityId: SubscriptionId, TargetTenantId: TenantId, SourceModule: "subscription-billing");
}
