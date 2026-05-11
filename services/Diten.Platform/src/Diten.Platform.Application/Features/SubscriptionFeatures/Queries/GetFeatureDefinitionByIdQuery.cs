using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Queries;

public sealed record GetFeatureDefinitionByIdQuery(Guid Id) : IRequest<Response<FeatureDefinitionDto>>;
