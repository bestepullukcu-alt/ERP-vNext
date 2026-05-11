using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record ExpireTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, byte[]? RowVersion)
    : IRequest<Response<NoContent>>;
