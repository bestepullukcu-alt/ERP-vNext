using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record UpdateFeatureCategoryCommand(Guid Id, UpdateFeatureCategoryRequest Request) : IRequest<Response<NoContent>>;
