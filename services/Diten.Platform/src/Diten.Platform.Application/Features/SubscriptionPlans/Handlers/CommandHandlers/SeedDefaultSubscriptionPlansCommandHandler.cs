using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class SeedDefaultSubscriptionPlansCommandHandler : IRequestHandler<SeedDefaultSubscriptionPlansCommand, Response<NoContent>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly ILogger<SeedDefaultSubscriptionPlansCommandHandler> _logger;

    public SeedDefaultSubscriptionPlansCommandHandler(ISubscriptionPlanRepository repository, ILogger<SeedDefaultSubscriptionPlansCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
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
                TrialDurationDays = 14
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
                TrialDurationDays = null
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
                TrialDurationDays = null
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
                TrialDurationDays = null
            }
        };

        foreach (var seed in seeds)
        {
            var normalized = SubscriptionPlanCodeNormalizer.Normalize(seed.Code);
            var exists = await _repository.ExistsByCodeAsync(normalized, ct: ct);
            if (exists)
            {
                continue;
            }

            seed.Code = normalized;

            if (seed.IsDefault && seed.IsActive)
            {
                var existingDefault = await _repository.GetActiveDefaultAsync(excludeId: null, ct);
                if (existingDefault is not null)
                {
                    // Respect "block conflicts" convention.
                    seed.IsDefault = false;
                }
            }

            await _repository.CreateAsync(seed, ct);
            _logger.LogInformation("AUDIT SubscriptionPlanSeeded PlanId={PlanId} Code={Code}", seed.Id, seed.Code);
        }

        return Response<NoContent>.Success(204);
    }
}
