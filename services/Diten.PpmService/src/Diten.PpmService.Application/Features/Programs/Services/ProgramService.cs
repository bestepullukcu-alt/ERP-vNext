using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Programs;

public sealed class ProgramService(
    IProgramRepository repository, IPortfolioRepository portfolios, IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork,
    ITenantContext tenant, ICurrentActorContext actor, ICorrelationContext correlation, IPpmAccessAuthorizer access)
{
    public async Task<Response<ProgramDto>> Create(CreateProgramCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProgramDto>();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(r.PortfolioId, token)) return Response<ProgramDto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), null, token)) return Response<ProgramDto>.Fail("Program code already exists.", 409);
            var e = new Program(tenant.TenantId, actor.ActorId, r.Code, r.Name, r.Description, r.PortfolioId, r.VisibilityPolicyKey);
            await repository.AddAsync(e, token);
            await audit.AddAsync(Intent(e, "created"), token);
            return Response<ProgramDto>.Success(e.ToDto(), 201);
        }, ct);
    }
    public async Task<Response<ProgramDto>> Update(UpdateProgramCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProgramDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<ProgramDto>.Fail("Program was not found.", 404);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(r.PortfolioId, token)) return Response<ProgramDto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), r.Id, token)) return Response<ProgramDto>.Fail("Program code already exists.", 409);
            e.Update(actor.ActorId, r.Code, r.Name, r.Description, r.PortfolioId, r.VisibilityPolicyKey);
            await repository.ReplaceAsync(e, r.ExpectedVersion, token);
            await audit.AddAsync(Intent(e, "updated"), token);
            return Response<ProgramDto>.Success(e.ToDto());
        }, ct);
    }
    public async Task<Response<ProgramDto>> Transition(TransitionProgramLifecycleCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProgramDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<ProgramDto>.Fail("Program was not found.", 404);
        if (!e.CanTransitionTo(r.TargetState)) return Response<ProgramDto>.Fail("Invalid lifecycle transition.", 409);
        e.Transition(actor.ActorId, r.TargetState);
        return await Persist(e, r.ExpectedVersion, "lifecycle-changed", ct);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeleteProgramCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<NoContent>.Fail("Program was not found.", 404);
        e.SoftDelete(actor.ActorId);
        return await unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, r.ExpectedVersion, token); await audit.AddAsync(Intent(e, "soft-deleted"), token); return Response<NoContent>.SuccessWithoutData(); }, ct);
    }
    public async Task<Response<ProgramDto>> GetById(GetProgramByIdQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProgramDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        return e is null ? Response<ProgramDto>.Fail("Program was not found.", 404) : Response<ProgramDto>.Success(e.ToDto());
    }
    public async Task<Response<IReadOnlyList<ProgramDto>>> List(ListProgramsQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProgramsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<ProgramDto>>();
        var items = await repository.ListAsync(tenant.TenantId, ct);
        return Response<IReadOnlyList<ProgramDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
    private async Task<bool> PortfolioExists(Guid? id, CancellationToken ct) => id is null || await portfolios.GetByIdAsync(tenant.TenantId, id.Value, ct) is not null;
    private AuditIntent Intent(Program e, string m) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(Program), e.Id, m, DateTime.UtcNow);
    private Task<Response<ProgramDto>> Persist(Program e, int version, string mutation, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, version, token); await audit.AddAsync(Intent(e, mutation), token); return Response<ProgramDto>.Success(e.ToDto()); }, ct);
}
