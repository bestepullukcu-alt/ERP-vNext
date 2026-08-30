using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class BenefitCommitmentService(IBenefitCommitmentRepository repository, IInvestmentCaseRepository investmentCases,
    IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork, ITenantContext tenant, ICurrentActorContext actor,
    ICorrelationContext correlation, IPpmAccessAuthorizer access)
{
    public async Task<Response<BenefitCommitmentDto>> Create(CreateBenefitCommitmentCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<BenefitCommitmentDto>();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var parent = await investmentCases.GetByIdAsync(tenant.TenantId, r.InvestmentCaseId, token);
            if (parent is null || !parent.IsReferenceable) return Response<BenefitCommitmentDto>.Fail("Investment case was not found.", 404);
            await investmentCases.AdvanceBenefitCommitmentCollectionFenceAsync(parent, token);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), null, token))
                return Response<BenefitCommitmentDto>.Fail("Benefit commitment code already exists.", 409);
            var entity = new BenefitCommitment(tenant.TenantId, actor.ActorId, r.Code, r.Title, r.Description,
                r.InvestmentCaseId, r.TargetDescription, r.TargetDate);
            await repository.AddAsync(entity, token); await audit.AddAsync(Intent(entity, "created"), token);
            return Response<BenefitCommitmentDto>.Success(entity.ToDto(), 201);
        }, ct);
    }
    public async Task<Response<BenefitCommitmentDto>> Update(UpdateBenefitCommitmentCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<BenefitCommitmentDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<BenefitCommitmentDto>.Fail("Benefit commitment was not found.", 404);
        if (entity.LifecycleState is BenefitCommitmentLifecycleState.Closed or BenefitCommitmentLifecycleState.Cancelled)
            return Response<BenefitCommitmentDto>.Fail("Terminal benefit commitments cannot be updated.", 409);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), r.Id, token))
                return Response<BenefitCommitmentDto>.Fail("Benefit commitment code already exists.", 409);
            entity.Update(actor.ActorId, r.Code, r.Title, r.Description, r.TargetDescription, r.TargetDate);
            await repository.ReplaceAsync(entity, r.ExpectedVersion, token); await audit.AddAsync(Intent(entity, "updated"), token);
            return Response<BenefitCommitmentDto>.Success(entity.ToDto());
        }, ct);
    }
    public async Task<Response<BenefitCommitmentDto>> Transition(TransitionBenefitCommitmentLifecycleCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<BenefitCommitmentDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<BenefitCommitmentDto>.Fail("Benefit commitment was not found.", 404);
        if (!entity.CanTransitionTo(r.TargetState)) return Response<BenefitCommitmentDto>.Fail("Invalid lifecycle transition.", 409);
        entity.Transition(actor.ActorId, r.TargetState);
        return await Persist(entity, r.ExpectedVersion, "lifecycle-changed", ct);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeleteBenefitCommitmentCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<NoContent>.Fail("Benefit commitment was not found.", 404);
        entity.SoftDelete(actor.ActorId);
        return await unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(entity, r.ExpectedVersion, token); await audit.AddAsync(Intent(entity, "soft-deleted"), token); return Response<NoContent>.SuccessWithoutData(); }, ct);
    }
    public async Task<Response<BenefitCommitmentDto>> Get(GetBenefitCommitmentByIdQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<BenefitCommitmentDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        return entity is null ? Response<BenefitCommitmentDto>.Fail("Benefit commitment was not found.", 404) : Response<BenefitCommitmentDto>.Success(entity.ToDto());
    }
    public async Task<Response<IReadOnlyList<BenefitCommitmentDto>>> List(CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.BenefitCommitmentsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<BenefitCommitmentDto>>();
        return Response<IReadOnlyList<BenefitCommitmentDto>>.Success((await repository.ListAsync(tenant.TenantId, ct)).Select(x => x.ToDto()).ToArray());
    }
    private AuditIntent Intent(BenefitCommitment e, string mutation) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(BenefitCommitment), e.Id, mutation, DateTime.UtcNow);
    private Task<Response<BenefitCommitmentDto>> Persist(BenefitCommitment e, int version, string mutation, CancellationToken ct) =>
        unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, version, token); await audit.AddAsync(Intent(e, mutation), token); return Response<BenefitCommitmentDto>.Success(e.ToDto()); }, ct);
}
