using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class UpdateSubscriptionPlanCommandHandler : IRequestHandler<UpdateSubscriptionPlanCommand, Response<NoContent>>
{
    private readonly ITransactionalSubscriptionPlanRepository _repository;
    private readonly ILogger<UpdateSubscriptionPlanCommandHandler> _logger;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public UpdateSubscriptionPlanCommandHandler(ITransactionalSubscriptionPlanRepository repository, ILogger<UpdateSubscriptionPlanCommandHandler> logger,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _logger = logger;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(UpdateSubscriptionPlanCommand request, CancellationToken ct)
    {
        var normalizedCode = SubscriptionPlanCodeNormalizer.Normalize(request.Request.Code);
        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(UpdateSubscriptionPlanCommand), AuditOperation.Update, "SubscriptionPlan", request.Id),
            async (session, transactionCt) =>
            {
                var plan = await _repository.GetByIdAsync(session, request.Id, transactionCt);
                if (plan is null) return new(Response<NoContent>.Fail("Subscription plan not found.", 404), false);
                if (await _repository.ExistsByCodeAsync(session, normalizedCode, plan.Id, transactionCt))
                    return new(Response<NoContent>.Fail("Code already exists.", 409), false);
                if (request.Request.IsDefault && request.Request.IsActive
                    && await _repository.GetActiveDefaultAsync(session, plan.Id, transactionCt) is not null)
                    return new(Response<NoContent>.Fail("Only one active default plan is allowed.", 409), false);

                var includedFeatures = request.Request.IncludedFeatures?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
                var includedModules = request.Request.IncludedModuleKeys?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
                var name = request.Request.Name.Trim();
                var description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();
                var currency = string.IsNullOrWhiteSpace(request.Request.Currency) ? null : request.Request.Currency.Trim().ToUpperInvariant();
                var noOp = plan.Code == normalizedCode && plan.Name == name && plan.Description == description
                    && plan.IsActive == request.Request.IsActive && plan.IsDefault == request.Request.IsDefault
                    && plan.SortOrder == (request.Request.SortOrder ?? 0) && plan.PriceMonthly == request.Request.PriceMonthly
                    && plan.PriceYearly == request.Request.PriceYearly && plan.Currency == currency
                    && plan.IsTrialPlan == request.Request.IsTrialPlan
                    && plan.TrialDurationDays == (request.Request.IsTrialPlan ? request.Request.TrialDurationDays : null)
                    && DictionaryEqual(plan.DefaultQuotas, request.Request.DefaultQuotas)
                    && plan.IncludedFeatures.SequenceEqual(includedFeatures, StringComparer.OrdinalIgnoreCase)
                    && plan.IncludedModuleKeys.SequenceEqual(includedModules, StringComparer.OrdinalIgnoreCase);
                if (noOp) return new(Response<NoContent>.Success(204), false);

                plan.Code = normalizedCode; plan.Name = name; plan.Description = description;
                plan.IsActive = request.Request.IsActive; plan.IsDefault = request.Request.IsDefault;
                plan.SortOrder = request.Request.SortOrder ?? 0; plan.PriceMonthly = request.Request.PriceMonthly;
                plan.PriceYearly = request.Request.PriceYearly; plan.Currency = currency;
                plan.IsTrialPlan = request.Request.IsTrialPlan;
                plan.TrialDurationDays = request.Request.IsTrialPlan ? request.Request.TrialDurationDays : null;
                plan.DefaultQuotas = request.Request.DefaultQuotas is null ? null : new Dictionary<string, decimal>(request.Request.DefaultQuotas, StringComparer.OrdinalIgnoreCase);
                plan.IncludedFeatures = includedFeatures; plan.IncludedModuleKeys = includedModules;
                await _repository.UpdateAsync(session, plan, transactionCt);
                _logger.LogInformation("SubscriptionPlan updated PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
                return new(Response<NoContent>.Success(204), true,
                    (s, version, token) => _state.UpsertSubscriptionPlanAsync(s, plan, version, token));
            }, ct);
    }

    private static bool DictionaryEqual(IReadOnlyDictionary<string, decimal>? left, IReadOnlyDictionary<string, decimal>? right)
    {
        left ??= new Dictionary<string, decimal>(); right ??= new Dictionary<string, decimal>();
        return left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
