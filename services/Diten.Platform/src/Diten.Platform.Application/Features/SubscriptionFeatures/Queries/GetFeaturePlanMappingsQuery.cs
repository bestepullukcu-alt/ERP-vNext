using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Queries;

public sealed record GetFeaturePlanMappingsQuery(Guid FeatureDefinitionId) : IRequest<Response<IReadOnlyList<PlanFeatureMappingDto>>>;
