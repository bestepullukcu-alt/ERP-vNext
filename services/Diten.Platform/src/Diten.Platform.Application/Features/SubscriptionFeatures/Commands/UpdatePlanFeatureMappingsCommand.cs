using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Commands;

public sealed record UpdatePlanFeatureMappingsCommand(Guid SubscriptionPlanId, UpdatePlanFeatureMappingsRequest Request)
    : IRequest<Response<NoContent>>;
