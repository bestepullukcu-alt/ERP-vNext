using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
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

    public AssignPlanToTenantCommandHandler(
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserContext currentUser)
    {
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _currentUser = currentUser;
    }

    public Task<Response<Guid>> Handle(AssignPlanToTenantCommand request, CancellationToken ct)
    {
        return TenantSubscriptionCommandSupport.CreateAsync(
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
    }
}
