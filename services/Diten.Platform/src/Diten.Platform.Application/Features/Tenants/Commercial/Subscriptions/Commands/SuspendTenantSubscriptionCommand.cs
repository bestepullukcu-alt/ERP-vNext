using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record SuspendTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, SuspendTenantSubscriptionRequest Request)
    : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;
