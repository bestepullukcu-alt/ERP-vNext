using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record CreateTenantSubscriptionCommand(Guid TenantId, CreateTenantSubscriptionRequest Request)
    : IRequest<Response<Guid>>;
