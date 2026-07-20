using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record ArchiveFeatureCategoryCommand(Guid Id, byte[]? RowVersion)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.SubscriptionBilling, Operation: AuditOperation.Deactivate, EntityType: "FeatureCategory",
        EntityId: Id, IsPlatformGlobal: true, SourceModule: "subscription-billing");
}
