using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Billing.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums.Billing;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Handlers.CommandHandlers;

public sealed class CreateBillingPlanCommandHandler : IRequestHandler<CreateBillingPlanCommand, Response<BillingPlanDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateBillingPlanCommandHandler(IBillingRepository repository, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<BillingPlanDto>> Handle(CreateBillingPlanCommand request, CancellationToken ct)
    {
        var planCode = NormalizePlanCode(request.Request.PlanCode);
        var existing = await _repository.GetLatestPlanVersionAsync(planCode, ct);
        if (existing is not null)
        {
            return Response<BillingPlanDto>.Fail("Billing plan code already exists.", 409);
        }

        var plan = new BillingPlan
        {
            TenantId = _tenantContext.TenantId,
            CreatedBy = _currentUser.UserId.ToString(),
            PlanCode = planCode,
            PlanVersion = 1,
            Name = request.Request.Name.Trim(),
            Amount = BillingMoney.Round(request.Request.Amount),
            Currency = BillingMoney.NormalizeCurrency(request.Request.Currency),
            BillingInterval = Enum.Parse<BillingInterval>(request.Request.BillingInterval, true)
        };

        await _repository.CreatePlanAsync(plan, ct);
        return Response<BillingPlanDto>.Success(BillingMapper.ToDto(plan), 201);
    }

    private static string NormalizePlanCode(string value) => value.Trim().ToUpperInvariant();
}

public sealed class ActivateBillingPlanCommandHandler : IRequestHandler<ActivateBillingPlanCommand, Response<BillingPlanDto>>
{
    private readonly IBillingRepository _repository;

    public ActivateBillingPlanCommandHandler(IBillingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BillingPlanDto>> Handle(ActivateBillingPlanCommand request, CancellationToken ct)
    {
        var succeeded = await _repository.ActivatePlanAsync(request.PlanId, ct);
        if (!succeeded)
        {
            return Response<BillingPlanDto>.Fail("Billing plan was not found or cannot be activated.", 404);
        }

        var plan = await _repository.GetPlanAsync(request.PlanId, ct);
        return plan is null
            ? Response<BillingPlanDto>.Fail("Billing plan was not found.", 404)
            : Response<BillingPlanDto>.Success(BillingMapper.ToDto(plan));
    }
}

public sealed class ReviseBillingPlanCommandHandler : IRequestHandler<ReviseBillingPlanCommand, Response<BillingPlanDto>>
{
    private readonly IBillingRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public ReviseBillingPlanCommandHandler(IBillingRepository repository, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<BillingPlanDto>> Handle(ReviseBillingPlanCommand request, CancellationToken ct)
    {
        var current = await _repository.GetPlanAsync(request.PlanId, ct);
        if (current is null)
        {
            return Response<BillingPlanDto>.Fail("Billing plan was not found.", 404);
        }

        if (current.Status != BillingPlanStatus.Active && current.Status != BillingPlanStatus.Retired)
        {
            return Response<BillingPlanDto>.Fail("Only active or retired billing plans can create a new version.", 400);
        }

        var latest = await _repository.GetLatestPlanVersionAsync(current.PlanCode, ct) ?? current;
        var revised = new BillingPlan
        {
            TenantId = _tenantContext.TenantId,
            CreatedBy = _currentUser.UserId.ToString(),
            PlanCode = current.PlanCode,
            PlanVersion = latest.PlanVersion + 1,
            Name = request.Request.Name.Trim(),
            Amount = BillingMoney.Round(request.Request.Amount),
            Currency = BillingMoney.NormalizeCurrency(request.Request.Currency),
            BillingInterval = Enum.Parse<BillingInterval>(request.Request.BillingInterval, true)
        };

        await _repository.CreatePlanAsync(revised, ct);
        return Response<BillingPlanDto>.Success(BillingMapper.ToDto(revised), 201);
    }
}
