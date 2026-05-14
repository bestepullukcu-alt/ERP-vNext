using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class SyncTenantQuotaLimitsFromSubscriptionCommandHandler : IRequestHandler<SyncTenantQuotaLimitsFromSubscriptionCommand, Response<IReadOnlyList<QuotaStatusDto>>>
{
    private readonly IQuotaService _quotaService;

    public SyncTenantQuotaLimitsFromSubscriptionCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<IReadOnlyList<QuotaStatusDto>>> Handle(SyncTenantQuotaLimitsFromSubscriptionCommand request, CancellationToken ct) =>
        _quotaService.SyncTenantQuotaLimitsAsync(
            request.TenantId,
            request.Request.Source ?? "SubscriptionPlanSync",
            request.Request.Reason ?? "Tenant quota limits synchronized from subscription.",
            request.Request.ActorId ?? "System",
            request.Request.CorrelationId ?? Guid.NewGuid().ToString(),
            ct);
}
