using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Queries;

public sealed record GetPlanFeatureMappingsQuery(Guid SubscriptionPlanId) : IRequest<Response<IReadOnlyList<PlanFeatureMappingDto>>>;
