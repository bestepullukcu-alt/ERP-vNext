using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record UpdateFeatureCategoryCommand(Guid Id, UpdateFeatureCategoryRequest Request)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Update, EntityType: "FeatureCategory",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "subscription-billing");
}
