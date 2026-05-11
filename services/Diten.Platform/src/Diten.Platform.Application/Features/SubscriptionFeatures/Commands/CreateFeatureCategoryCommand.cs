using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record CreateFeatureCategoryCommand(CreateFeatureCategoryRequest Request) : IRequest<Response<Guid>>;
