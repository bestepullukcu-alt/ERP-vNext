using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record CreateSubscriptionPlanCommand(CreateSubscriptionPlanRequest Request)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Create, EntityType: "SubscriptionPlan",
        IsPlatformGlobal: true, SourceModule: "subscription-billing");
}
