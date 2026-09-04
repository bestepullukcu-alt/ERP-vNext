using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class ActivateSubscriptionPlanCommandHandler : IRequestHandler<ActivateSubscriptionPlanCommand, Response<NoContent>>
{
    private readonly ITransactionalSubscriptionPlanRepository _repository;
    private readonly ILogger<ActivateSubscriptionPlanCommandHandler> _logger;
    private readonly IGlobalApplicabilityTransactionCoordinator _transaction;
    private readonly IGlobalApplicabilityStateRepository _state;

    public ActivateSubscriptionPlanCommandHandler(ITransactionalSubscriptionPlanRepository repository, ILogger<ActivateSubscriptionPlanCommandHandler> logger,
        IGlobalApplicabilityTransactionCoordinator transaction, IGlobalApplicabilityStateRepository state)
    {
        _repository = repository;
        _logger = logger;
        _transaction = transaction;
        _state = state;
    }

    public async Task<Response<NoContent>> Handle(ActivateSubscriptionPlanCommand request, CancellationToken ct)
    {
        return await _transaction.ExecuteAsync<Response<NoContent>>(
            new(nameof(ActivateSubscriptionPlanCommand), AuditOperation.Activate, "SubscriptionPlan", request.Id),
            async (session, transactionCt) =>
            {
                var plan = await _repository.GetByIdAsync(session, request.Id, transactionCt);
                if (plan is null) return new(Response<NoContent>.Fail("Subscription plan not found.", 404), false);
                if (plan.IsActive) return new(Response<NoContent>.Success(204), false);
                plan.IsActive = true;
                await _repository.UpdateAsync(session, plan, transactionCt);
                _logger.LogInformation("SubscriptionPlan activated PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
                return new(Response<NoContent>.Success(204), true,
                    (s, version, token) => _state.UpsertSubscriptionPlanAsync(s, plan, version, token));
            }, ct);
    }
}
