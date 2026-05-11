using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record ActivateTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, ActivateTenantSubscriptionRequest Request)
    : IRequest<Response<NoContent>>;
