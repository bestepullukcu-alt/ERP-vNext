using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class PortfolioService(
    IPortfolioRepository repository, IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork,
    ITenantContext tenant, ICurrentActorContext actor, ICorrelationContext correlation, IPpmAccessAuthorizer access,
    IInvestmentCaseRepository? investmentCases = null)
{
    public async Task<Response<PortfolioDto>> Create(CreatePortfolioCommand request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<PortfolioDto>();
        if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), null, ct)) return Response<PortfolioDto>.Fail("Portfolio code already exists.", 409);
        var entity = new Portfolio(tenant.TenantId, actor.ActorId, request.Code, request.Name, request.Description, request.VisibilityPolicyKey);
        return await unitOfWork.ExecuteInTransactionAsync(async token => { await repository.AddAsync(entity, token); await audit.AddAsync(Intent(entity, "created"), token); return Response<PortfolioDto>.Success(entity.ToDto(), 201); }, ct);
    }
    public async Task<Response<PortfolioDto>> Update(UpdatePortfolioCommand request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<PortfolioDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Response<PortfolioDto>.Fail("Portfolio was not found.", 404);
        if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), request.Id, ct)) return Response<PortfolioDto>.Fail("Portfolio code already exists.", 409);
        entity.Update(actor.ActorId, request.Code, request.Name, request.Description, request.VisibilityPolicyKey);
        return await Persist(entity, request.ExpectedVersion, "updated", ct);
    }
    public async Task<Response<PortfolioDto>> Transition(TransitionPortfolioLifecycleCommand request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<PortfolioDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Response<PortfolioDto>.Fail("Portfolio was not found.", 404);
        if (!entity.CanTransitionTo(request.TargetState)) return Response<PortfolioDto>.Fail("Invalid lifecycle transition.", 409);
        entity.Transition(actor.ActorId, request.TargetState);
        return await Persist(entity, request.ExpectedVersion, "lifecycle-changed", ct);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeletePortfolioCommand request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        if (investmentCases is null) return Response<NoContent>.Fail("Portfolio dependency validation is unavailable.", 503);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, token);
            if (entity is null) return Response<NoContent>.Fail("Portfolio was not found.", 404);
            await repository.AdvanceInvestmentCaseCollectionFenceAsync(entity, token);
            if (await investmentCases.ExistsForPortfolioAsync(tenant.TenantId, entity.Id, token))
                return Response<NoContent>.Fail("Portfolio has active investment cases.", 409);
            entity.SoftDelete(actor.ActorId);
            await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
            await audit.AddAsync(Intent(entity, "soft-deleted"), token);
            return Response<NoContent>.SuccessWithoutData();
        }, ct);
    }
    public async Task<Response<PortfolioDto>> GetById(GetPortfolioByIdQuery request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<PortfolioDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        return entity is null ? Response<PortfolioDto>.Fail("Portfolio was not found.", 404) : Response<PortfolioDto>.Success(entity.ToDto());
    }
    public async Task<Response<IReadOnlyList<PortfolioDto>>> List(ListPortfoliosQuery request, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.PortfoliosRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<PortfolioDto>>();
        var items = await repository.ListAsync(tenant.TenantId, ct);
        return Response<IReadOnlyList<PortfolioDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
    private AuditIntent Intent(Portfolio x, string mutation) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(Portfolio), x.Id, mutation, DateTime.UtcNow);
    private Task<Response<PortfolioDto>> Persist(Portfolio entity, int expectedVersion, string mutation, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(entity, expectedVersion, token); await audit.AddAsync(Intent(entity, mutation), token); return Response<PortfolioDto>.Success(entity.ToDto()); }, ct);
}
