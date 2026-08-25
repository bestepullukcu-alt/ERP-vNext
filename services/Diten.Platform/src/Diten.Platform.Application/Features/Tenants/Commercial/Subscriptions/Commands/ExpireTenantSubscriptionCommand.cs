using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record ExpireTenantSubscriptionCommand(Guid TenantId, Guid SubscriptionId, byte[]? RowVersion)
    : IRequest<Response<NoContent>>, ITransactionOwnedAuditCommand;
