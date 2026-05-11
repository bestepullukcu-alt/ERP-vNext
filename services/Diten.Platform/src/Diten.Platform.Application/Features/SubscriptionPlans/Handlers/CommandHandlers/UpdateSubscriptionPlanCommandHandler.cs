using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class UpdateSubscriptionPlanCommandHandler : IRequestHandler<UpdateSubscriptionPlanCommand, Response<NoContent>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly ILogger<UpdateSubscriptionPlanCommandHandler> _logger;

    public UpdateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ILogger<UpdateSubscriptionPlanCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(UpdateSubscriptionPlanCommand request, CancellationToken ct)
    {
        var plan = await _repository.GetByIdAsync(request.Id, ct);
        if (plan is null)
        {
            return Response<NoContent>.Fail("Subscription plan not found.", 404);
        }

        var normalizedCode = SubscriptionPlanCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsByCodeAsync(normalizedCode, excludeId: plan.Id, ct: ct))
        {
            return Response<NoContent>.Fail("Code already exists.", 409);
        }

        if (request.Request.IsDefault && request.Request.IsActive)
        {
            var existingDefault = await _repository.GetActiveDefaultAsync(excludeId: plan.Id, ct);
            if (existingDefault is not null)
            {
                return Response<NoContent>.Fail("Only one active default plan is allowed.", 409);
            }
        }

        plan.Code = normalizedCode;
        plan.Name = request.Request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
        plan.IsActive = request.Request.IsActive;
        plan.IsDefault = request.Request.IsDefault;
        plan.SortOrder = request.Request.SortOrder ?? 0;
        plan.PriceMonthly = request.Request.PriceMonthly;
        plan.PriceYearly = request.Request.PriceYearly;
        plan.Currency = string.IsNullOrWhiteSpace(request.Request.Currency) ? null : request.Request.Currency.Trim().ToUpperInvariant();
        plan.IsTrialPlan = request.Request.IsTrialPlan;
        plan.TrialDurationDays = request.Request.IsTrialPlan ? request.Request.TrialDurationDays : null;
        plan.DefaultQuotas = request.Request.DefaultQuotas is null ? null : new Dictionary<string, decimal>(request.Request.DefaultQuotas, StringComparer.OrdinalIgnoreCase);
        plan.IncludedFeatures = request.Request.IncludedFeatures?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        plan.IncludedModuleKeys = request.Request.IncludedModuleKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

        await _repository.UpdateAsync(plan, ct);
        _logger.LogInformation("AUDIT SubscriptionPlanUpdated PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
        return Response<NoContent>.Success(204);
    }
}
