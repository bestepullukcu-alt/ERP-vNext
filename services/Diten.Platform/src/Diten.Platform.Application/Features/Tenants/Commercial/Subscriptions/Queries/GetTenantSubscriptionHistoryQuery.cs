using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;

public sealed record GetTenantSubscriptionHistoryQuery(Guid TenantId)
    : IRequest<Response<IReadOnlyList<TenantSubscriptionHistoryDto>>>;
