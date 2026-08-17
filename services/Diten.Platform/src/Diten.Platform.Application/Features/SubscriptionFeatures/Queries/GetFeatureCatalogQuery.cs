using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Queries;

public sealed record GetFeatureCatalogQuery(FeatureCatalogFilterRequest Filter) : IRequest<Response<PagedResult<FeatureDefinitionListItemDto>>>;
