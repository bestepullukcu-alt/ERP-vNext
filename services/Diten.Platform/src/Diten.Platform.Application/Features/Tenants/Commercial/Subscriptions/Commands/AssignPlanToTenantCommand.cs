using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record AssignPlanToTenantCommand(Guid TenantId, AssignPlanToTenantRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider, ITransactionOwnedAuditCommand
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Assign, EntityType: "TenantSubscription",
        TargetTenantId: TenantId, SourceModule: "subscription-billing");
}
