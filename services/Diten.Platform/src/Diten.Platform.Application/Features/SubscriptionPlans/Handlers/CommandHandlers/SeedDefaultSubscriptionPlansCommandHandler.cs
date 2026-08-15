using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Features.Quotas;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class SeedDefaultSubscriptionPlansCommandHandler : IRequestHandler<SeedDefaultSubscriptionPlansCommand, Response<NoContent>>
{
    private readonly ITransactionalSubscriptionPlanRepository _repository;
    private readonly ILogger<SeedDefaultSubscriptionPlansCommandHandler> _logger;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public SeedDefaultSubscriptionPlansCommandHandler(ITransactionalSubscriptionPlanRepository repository, ILogger<SeedDefaultSubscriptionPlansCommandHandler> logger,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _logger = logger;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(SeedDefaultSubscriptionPlansCommand request, CancellationToken ct)
    {
        // Non-destructive seed: create only when missing by Code. Never overwrite admin edits.
        var seeds = new[]
        {
            new SubscriptionPlan
            {
                Code = "FREE",
                Name = "Free",
                Description = "Time-limited free access plan",
                IsActive = true,
                IsDefault = true,
                SortOrder = 0,
                PriceMonthly = 0,
                PriceYearly = 0,
                Currency = "USD",
                IsTrialPlan = true,
                TrialDurationDays = 14,
                DefaultQuotas = BuildQuotas(5, 100, 10_000, 3)
            },
            new SubscriptionPlan
            {
                Code = "STARTER",
                Name = "Starter",
                Description = "Starter plan",
                IsActive = true,
                IsDefault = false,
                SortOrder = 10,
                PriceMonthly = 49,
                PriceYearly = 499,
                Currency = "USD",
                IsTrialPlan = false,
                TrialDurationDays = null,
                DefaultQuotas = BuildQuotas(25, 250, 100_000, 10)
            },
            new SubscriptionPlan
            {
                Code = "PROFESSIONAL",
                Name = "Professional",
                Description = "Professional plan",
                IsActive = true,
                IsDefault = false,
                SortOrder = 20,
                PriceMonthly = 99,
                PriceYearly = 999,
                Currency = "USD",
                IsTrialPlan = false,
                TrialDurationDays = null,
                DefaultQuotas = BuildQuotas(100, 1_000, 1_000_000, 25)
            },
            new SubscriptionPlan
            {
                Code = "ENTERPRISE",
                Name = "Enterprise",
                Description = "Enterprise plan (custom pricing)",
                IsActive = true,
                IsDefault = false,
                SortOrder = 30,
                PriceMonthly = null,
                PriceYearly = null,
                Currency = null,
                IsTrialPlan = false,
                TrialDurationDays = null,
                DefaultQuotas = BuildQuotas(1_000, 10_000, 10_000_000, 100)
            }
        };

        foreach (var seed in seeds)
        {
            var normalized = SubscriptionPlanCodeNormalizer.Normalize(seed.Code);
            seed.Code = normalized;
            await _transaction.ExecuteAsync(
                new(nameof(SeedDefaultSubscriptionPlansCommand), AuditOperation.Create, "SubscriptionPlan", seed.Id),
                async (session, transactionCt) =>
                {
                    var existing = await _repository.GetByCodeAsync(session, normalized, transactionCt);
                    if (existing is not null)
                    {
                        // Non-destructive compatibility backfill: quota values are filled only for legacy
                        // seeded plans that have no quota map. Operator-owned values are never overwritten.
                        if (existing.DefaultQuotas is { Count: > 0 })
                            return new GlobalApplicabilityMutation<bool>(false, false);

                        existing.DefaultQuotas = seed.DefaultQuotas;
                        await _repository.UpdateAsync(session, existing, transactionCt);
                        _logger.LogInformation("SubscriptionPlan quota map backfilled PlanId={PlanId} Code={Code}", existing.Id, existing.Code);
                        return new GlobalApplicabilityMutation<bool>(true, true,
                            (s, version, token) => _state.UpsertSubscriptionPlanAsync(s, existing, version, token));
                    }
                    if (seed.IsDefault && seed.IsActive
                        && await _repository.GetActiveDefaultAsync(session, excludeId: null, transactionCt) is not null)
                        seed.IsDefault = false;
                    await _repository.CreateAsync(session, seed, transactionCt);
                    _logger.LogInformation("SubscriptionPlan seeded PlanId={PlanId} Code={Code}", seed.Id, seed.Code);
                    return new GlobalApplicabilityMutation<bool>(true, true,
                        (s, version, token) => _state.UpsertSubscriptionPlanAsync(s, seed, version, token));
                }, ct);
        }

        return Response<NoContent>.Success(204);
    }

    private static Dictionary<string, decimal> BuildQuotas(
        decimal users,
        decimal storageGb,
        decimal apiCallsPerMonth,
        decimal modules) => new()
    {
        [QuotaKeys.UsersMax] = users,
        [QuotaKeys.StorageGbMax] = storageGb,
        [QuotaKeys.ApiCallsPerMonth] = apiCallsPerMonth,
        [QuotaKeys.ModulesMax] = modules
    };
}
