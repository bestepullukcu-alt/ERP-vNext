using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record CancelTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, CancelTenantSubscriptionRequest Request)
    : IRequest<Response<NoContent>>;
