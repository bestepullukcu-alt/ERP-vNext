using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, Response<Guid>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly ILogger<CreateSubscriptionPlanCommandHandler> _logger;

    public CreateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ILogger<CreateSubscriptionPlanCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(CreateSubscriptionPlanCommand request, CancellationToken ct)
    {
        var normalizedCode = SubscriptionPlanCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsByCodeAsync(normalizedCode, ct: ct))
        {
            return Response<Guid>.Fail("Code already exists.", 409);
        }

        if (request.Request.IsDefault && request.Request.IsActive)
        {
            var existingDefault = await _repository.GetActiveDefaultAsync(excludeId: null, ct);
            if (existingDefault is not null)
            {
                return Response<Guid>.Fail("Only one active default plan is allowed.", 409);
            }
        }

        var plan = new SubscriptionPlan
        {
            Code = normalizedCode,
            Name = request.Request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            IsActive = request.Request.IsActive,
            IsDefault = request.Request.IsDefault,
            SortOrder = request.Request.SortOrder ?? 0,
            PriceMonthly = request.Request.PriceMonthly,
            PriceYearly = request.Request.PriceYearly,
            Currency = string.IsNullOrWhiteSpace(request.Request.Currency) ? null : request.Request.Currency.Trim().ToUpperInvariant(),
            IsTrialPlan = request.Request.IsTrialPlan,
            TrialDurationDays = request.Request.IsTrialPlan ? request.Request.TrialDurationDays : null,
            DefaultQuotas = request.Request.DefaultQuotas is null ? null : new Dictionary<string, decimal>(request.Request.DefaultQuotas, StringComparer.OrdinalIgnoreCase),
            IncludedFeatures = request.Request.IncludedFeatures?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            IncludedModuleKeys = request.Request.IncludedModuleKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []
        };

        await _repository.CreateAsync(plan, ct);

        _logger.LogInformation("AUDIT SubscriptionPlanCreated PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
        return Response<Guid>.Success(plan.Id, 201);
    }
}
