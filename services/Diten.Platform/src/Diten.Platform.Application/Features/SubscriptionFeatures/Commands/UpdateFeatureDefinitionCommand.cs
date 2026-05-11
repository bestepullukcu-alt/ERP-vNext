using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record UpdateFeatureDefinitionCommand(Guid Id, UpdateFeatureDefinitionRequest Request) : IRequest<Response<NoContent>>;
