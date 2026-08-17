using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;

public sealed class DeactivateSubscriptionPlanCommandHandler : IRequestHandler<DeactivateSubscriptionPlanCommand, Response<NoContent>>
{
    private readonly ISubscriptionPlanRepository _repository;
    private readonly ILogger<DeactivateSubscriptionPlanCommandHandler> _logger;

    public DeactivateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ILogger<DeactivateSubscriptionPlanCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeactivateSubscriptionPlanCommand request, CancellationToken ct)
    {
        var plan = await _repository.GetByIdAsync(request.Id, ct);
        if (plan is null)
        {
            return Response<NoContent>.Fail("Subscription plan not found.", 404);
        }

        // Deactivated plans cannot remain default.
        plan.IsActive = false;
        plan.IsDefault = false;
        await _repository.UpdateAsync(plan, ct);
        _logger.LogInformation("AUDIT SubscriptionPlanDeactivated PlanId={PlanId} Code={Code}", plan.Id, plan.Code);
        return Response<NoContent>.Success(204);
    }
}

