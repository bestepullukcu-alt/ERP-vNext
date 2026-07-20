using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record SeedDefaultSubscriptionPlansCommand()
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    // Operator/system-triggered baseline seed — recorded under System, not billing lifecycle.
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.System, Operation: AuditOperation.Create, EntityType: "SubscriptionPlan",
        IsPlatformGlobal: true, SourceModule: "subscription-billing");
}
