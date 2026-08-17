using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record DeactivateFeatureDefinitionCommand(Guid Id, byte[]? RowVersion) : IRequest<Response<NoContent>>;
