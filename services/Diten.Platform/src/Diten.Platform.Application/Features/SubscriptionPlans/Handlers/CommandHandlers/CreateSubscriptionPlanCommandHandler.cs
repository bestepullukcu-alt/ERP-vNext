using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, Response<Guid>>
{
    private readonly ITransactionalSubscriptionPlanRepository _repository;
    private readonly ILogger<CreateSubscriptionPlanCommandHandler> _logger;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public CreateSubscriptionPlanCommandHandler(ITransactionalSubscriptionPlanRepository repository, ILogger<CreateSubscriptionPlanCommandHandler> logger,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _logger = logger;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<Guid>> Handle(CreateSubscriptionPlanCommand request, CancellationToken ct)
    {
        var normalizedCode = SubscriptionPlanCodeNormalizer.Normalize(request.Request.Code);
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
            IncludedModuleKeys = request.Request.IncludedModuleKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? []
        };

        var result = await _transaction.ExecuteAsync(
            new(nameof(CreateSubscriptionPlanCommand), AuditOperation.Create, "SubscriptionPlan", plan.Id),
            async (session, transactionCt) =>
            {
                if (await _repository.ExistsByCodeAsync(session, normalizedCode, ct: transactionCt))
                    return new GlobalApplicabilityMutation<Response<Guid>>(Response<Guid>.Fail("Code already exists.", 409), false);
                if (plan.IsDefault && plan.IsActive
                    && await _repository.GetActiveDefaultAsync(session, excludeId: null, transactionCt) is not null)
                    return new GlobalApplicabilityMutation<Response<Guid>>(Response<Guid>.Fail("Only one active default plan is allowed.", 409), false);
                await _repository.CreateAsync(session, plan, transactionCt);
                return new GlobalApplicabilityMutation<Response<Guid>>(Response<Guid>.Success(plan.Id, 201), true,
                    (s, version, token) => _state.UpsertSubscriptionPlanAsync(s, plan, version, token));
            }, ct);

        _logger.LogInformation("SubscriptionPlan create completed PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
        return result;
    }
}
