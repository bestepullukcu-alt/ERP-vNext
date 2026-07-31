using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class InitiativeService(
    IInitiativeRepository repository, IPortfolioRepository portfolios, IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork,
    ITenantContext tenant, ICurrentActorContext actor, ICorrelationContext correlation, IPpmAccessAuthorizer access)
{
    public async Task<Response<InitiativeDto>> Create(CreateInitiativeCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InitiativeDto>();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(r.PortfolioId, token)) return Response<InitiativeDto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), null, token)) return Response<InitiativeDto>.Fail("Initiative code already exists.", 409);
            var e = new Initiative(tenant.TenantId, actor.ActorId, r.Code, r.Name, r.Description, r.PortfolioId, r.VisibilityPolicyKey);
            await repository.AddAsync(e, token);
            await audit.AddAsync(Intent(e, "created"), token);
            return Response<InitiativeDto>.Success(e.ToDto(), 201);
        }, ct);
    }
    public async Task<Response<InitiativeDto>> Update(UpdateInitiativeCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InitiativeDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<InitiativeDto>.Fail("Initiative was not found.", 404);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(r.PortfolioId, token)) return Response<InitiativeDto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), r.Id, token)) return Response<InitiativeDto>.Fail("Initiative code already exists.", 409);
            e.Update(actor.ActorId, r.Code, r.Name, r.Description, r.PortfolioId, r.VisibilityPolicyKey);
            await repository.ReplaceAsync(e, r.ExpectedVersion, token);
            await audit.AddAsync(Intent(e, "updated"), token);
            return Response<InitiativeDto>.Success(e.ToDto());
        }, ct);
    }
    public async Task<Response<InitiativeDto>> Transition(TransitionInitiativeLifecycleCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InitiativeDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<InitiativeDto>.Fail("Initiative was not found.", 404);
        if (!e.CanTransitionTo(r.TargetState)) return Response<InitiativeDto>.Fail("Invalid lifecycle transition.", 409);
        e.Transition(actor.ActorId, r.TargetState);
        return await Persist(e, r.ExpectedVersion, "lifecycle-changed", ct);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeleteInitiativeCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<NoContent>.Fail("Initiative was not found.", 404);
        e.SoftDelete(actor.ActorId);
        return await unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, r.ExpectedVersion, token); await audit.AddAsync(Intent(e, "soft-deleted"), token); return Response<NoContent>.SuccessWithoutData(); }, ct);
    }
    public async Task<Response<InitiativeDto>> GetById(GetInitiativeByIdQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InitiativeDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        return e is null ? Response<InitiativeDto>.Fail("Initiative was not found.", 404) : Response<InitiativeDto>.Success(e.ToDto());
    }
    public async Task<Response<IReadOnlyList<InitiativeDto>>> List(ListInitiativesQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InitiativesRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<InitiativeDto>>();
        var items = await repository.ListAsync(tenant.TenantId, ct);
        return Response<IReadOnlyList<InitiativeDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
    private async Task<bool> PortfolioExists(Guid? id, CancellationToken ct) => id is null || await portfolios.GetByIdAsync(tenant.TenantId, id.Value, ct) is not null;
    private AuditIntent Intent(Initiative e, string m) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(Initiative), e.Id, m, DateTime.UtcNow);
    private Task<Response<InitiativeDto>> Persist(Initiative e, int version, string mutation, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, version, token); await audit.AddAsync(Intent(e, mutation), token); return Response<InitiativeDto>.Success(e.ToDto()); }, ct);
}
