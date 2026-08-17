using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record CreateFeatureDefinitionCommand(CreateFeatureDefinitionRequest Request) : IRequest<Response<Guid>>;
