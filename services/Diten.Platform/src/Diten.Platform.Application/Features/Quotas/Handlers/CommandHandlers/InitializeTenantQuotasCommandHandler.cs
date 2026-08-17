using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class InitializeTenantQuotasCommandHandler : IRequestHandler<InitializeTenantQuotasCommand, Response<IReadOnlyList<QuotaStatusDto>>>
{
    private readonly IQuotaService _quotaService;

    public InitializeTenantQuotasCommandHandler(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    public Task<Response<IReadOnlyList<QuotaStatusDto>>> Handle(InitializeTenantQuotasCommand request, CancellationToken ct) =>
        _quotaService.InitializeTenantQuotasAsync(
            request.TenantId,
            request.Request.Source ?? "SubscriptionActivation",
            request.Request.Reason ?? "Tenant quota initialization.",
            request.Request.ActorId ?? "System",
            request.Request.CorrelationId ?? Guid.NewGuid().ToString(),
            ct);
}
