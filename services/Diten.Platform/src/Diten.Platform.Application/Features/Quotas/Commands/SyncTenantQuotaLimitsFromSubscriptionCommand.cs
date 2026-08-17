using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Commands;

public sealed record SyncTenantQuotaLimitsFromSubscriptionCommand(Guid TenantId, SyncTenantQuotaLimitsRequest Request) : IRequest<Response<IReadOnlyList<QuotaStatusDto>>>;
