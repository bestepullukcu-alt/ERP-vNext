using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record ActivateSubscriptionPlanCommand(Guid Id)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Activate, EntityType: "SubscriptionPlan",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "subscription-billing");
}
