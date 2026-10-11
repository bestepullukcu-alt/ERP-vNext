using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Quotas.Handlers.CommandHandlers;

public sealed class InitializeTenantQuotasCommandHandler : IRequestHandler<InitializeTenantQuotasCommand, Response<IReadOnlyList<QuotaStatusDto>>>
{
    private readonly IQuotaService _quotaService;
    private readonly ICurrentUserContext _currentUser;

    public InitializeTenantQuotasCommandHandler(IQuotaService quotaService, ICurrentUserContext currentUser)
    {
        _quotaService = quotaService;
        _currentUser = currentUser;
    }

    public Task<Response<IReadOnlyList<QuotaStatusDto>>> Handle(InitializeTenantQuotasCommand request, CancellationToken ct) =>
        _quotaService.InitializeTenantQuotasAsync(
            request.TenantId,
            request.Request.Source ?? "SubscriptionActivation",
            request.Request.Reason ?? "Tenant quota initialization.",
            // BL-521 FIX1 F5 — the actor is the caller of THIS request (its token), never a value from the body: the body's
            // ActorId is ignored, so the stored "created by" cannot be written in someone else's name.
            _currentUser.ActorName,
            request.Request.CorrelationId ?? Guid.NewGuid().ToString(),
            ct);
}
