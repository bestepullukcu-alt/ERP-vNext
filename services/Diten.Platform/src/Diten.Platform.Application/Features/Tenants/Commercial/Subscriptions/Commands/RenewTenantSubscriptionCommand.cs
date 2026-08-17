using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record RenewTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, RenewTenantSubscriptionRequest Request)
    : IRequest<Response<NoContent>>;
