using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.CommandHandlers;

public sealed class AssignPlanToTenantCommandHandler : IRequestHandler<AssignPlanToTenantCommand, Response<Guid>>
{
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IQuotaService _quotaService;

    public AssignPlanToTenantCommandHandler(
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserContext currentUser,
        IQuotaService quotaService)
    {
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _currentUser = currentUser;
        _quotaService = quotaService;
    }

    public async Task<Response<Guid>> Handle(AssignPlanToTenantCommand request, CancellationToken ct)
    {
        var response = await TenantSubscriptionCommandSupport.CreateAsync(
            request.TenantId,
            request.Request.PlanId,
            request.Request.IsTrial,
            request.Request.TrialEndDateUtc,
            request.Request.CurrentPeriodStartUtc,
            request.Request.CurrentPeriodEndUtc,
            request.Request.Source,
            _tenantRepository,
            _planRepository,
            _subscriptionRepository,
            _currentUser,
            ct);

        if (!response.IsSuccessful)
        {
            return response;
        }

        // FIX-QUOTA-PLAN-SYNC — assigning/changing a plan must RE-SYNC the stored quota LIMITS to the new plan, not
        // just initialize-once. InitializeTenantQuotasAsync only creates missing usage rows and leaves an existing
        // LimitValue frozen (Free=3 stayed 3 after a Free→Enterprise change). SyncTenantQuotaLimitsAsync upserts each
        // limit (LimitValue + SubscriptionId + PlanId) while PRESERVING CurrentValue, and still creates rows for a
        // fresh tenant — so it is a superset that both onboards and re-syncs correctly.
        var quotaResponse = await _quotaService.SyncTenantQuotaLimitsAsync(
            request.TenantId,
            "SubscriptionActivation",
            "Tenant subscription assigned; quota limits synced to the plan.",
            _currentUser.ActorName,
            Guid.NewGuid().ToString(),
            ct);

        return quotaResponse.IsSuccessful
            ? response
            : Response<Guid>.Fail(quotaResponse.Errors, quotaResponse.StatusCode);
    }
}
