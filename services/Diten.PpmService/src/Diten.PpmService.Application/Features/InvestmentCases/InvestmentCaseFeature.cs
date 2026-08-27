using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;
using FluentValidation;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record CreateInvestmentCaseCommand(string Code, string Title, string? Description, Guid PortfolioId,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate) : IRequest<Response<InvestmentCaseDto>>;
public sealed record UpdateInvestmentCaseCommand(Guid Id, string Code, string Title, string? Description,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, int ExpectedVersion) : IRequest<Response<InvestmentCaseDto>>;
public sealed record TransitionInvestmentCaseLifecycleCommand(Guid Id, InvestmentCaseLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<InvestmentCaseDto>>;
public sealed record SoftDeleteInvestmentCaseCommand(Guid Id, int ExpectedVersion) : IRequest<Response<NoContent>>;
public sealed record GetInvestmentCaseByIdQuery(Guid Id) : IRequest<Response<InvestmentCaseDto>>;
public sealed record ListInvestmentCasesQuery : IRequest<Response<IReadOnlyList<InvestmentCaseDto>>>;

public sealed class CreateInvestmentCaseValidator : AbstractValidator<CreateInvestmentCaseCommand>
{
    public CreateInvestmentCaseValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PortfolioId).NotEmpty();
        RuleFor(x => x).Must(x => !x.PlannedStartDate.HasValue || !x.PlannedEndDate.HasValue || x.PlannedEndDate >= x.PlannedStartDate)
            .WithMessage("PlannedEndDate cannot be before PlannedStartDate.");
    }
}
public sealed class UpdateInvestmentCaseValidator : AbstractValidator<UpdateInvestmentCaseCommand>
{
    public UpdateInvestmentCaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
        RuleFor(x => x).Must(x => !x.PlannedStartDate.HasValue || !x.PlannedEndDate.HasValue || x.PlannedEndDate >= x.PlannedStartDate)
            .WithMessage("PlannedEndDate cannot be before PlannedStartDate.");
    }
}
public sealed class TransitionInvestmentCaseValidator : AbstractValidator<TransitionInvestmentCaseLifecycleCommand>
{ public TransitionInvestmentCaseValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.TargetState).IsInEnum(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); } }
public sealed class SoftDeleteInvestmentCaseValidator : AbstractValidator<SoftDeleteInvestmentCaseCommand>
{ public SoftDeleteInvestmentCaseValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.ExpectedVersion).GreaterThan(0); } }

public sealed class InvestmentCaseService(IInvestmentCaseRepository repository, IPortfolioRepository portfolios,
    IBenefitCommitmentRepository benefitCommitments,
    IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork, ITenantContext tenant, ICurrentActorContext actor,
    ICorrelationContext correlation, IPpmAccessAuthorizer access,
    IGateIDecisionTraceLifecyclePolicy? gateIDecisionTraceLifecycle = null)
{
    public async Task<Response<InvestmentCaseDto>> Create(CreateInvestmentCaseCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InvestmentCaseDto>();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var portfolio = await portfolios.GetByIdAsync(tenant.TenantId, r.PortfolioId, token);
            if (portfolio is null || portfolio.LifecycleState != PortfolioLifecycleState.Active)
                return Response<InvestmentCaseDto>.Fail("Portfolio was not found.", 404);
            await portfolios.AdvanceInvestmentCaseCollectionFenceAsync(portfolio, token);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), null, token))
                return Response<InvestmentCaseDto>.Fail("Investment case code already exists.", 409);
            var entity = new InvestmentCase(tenant.TenantId, actor.ActorId, r.Code, r.Title, r.Description,
                r.PortfolioId, r.PlannedStartDate, r.PlannedEndDate);
            await repository.AddAsync(entity, token); await audit.AddAsync(Intent(entity, "created"), token);
            return Response<InvestmentCaseDto>.Success(entity.ToDto(), 201);
        }, ct);
    }

    public async Task<Response<InvestmentCaseDto>> Update(UpdateInvestmentCaseCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InvestmentCaseDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<InvestmentCaseDto>.Fail("Investment case was not found.", 404);
        if (entity.LifecycleState is InvestmentCaseLifecycleState.Closed or InvestmentCaseLifecycleState.Withdrawn)
            return Response<InvestmentCaseDto>.Fail("Terminal investment cases cannot be updated.", 409);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), r.Id, token))
                return Response<InvestmentCaseDto>.Fail("Investment case code already exists.", 409);
            entity.Update(actor.ActorId, r.Code, r.Title, r.Description, r.PlannedStartDate, r.PlannedEndDate);
            await repository.ReplaceAsync(entity, r.ExpectedVersion, token); await audit.AddAsync(Intent(entity, "updated"), token);
            return Response<InvestmentCaseDto>.Success(entity.ToDto());
        }, ct);
    }

    public async Task<Response<InvestmentCaseDto>> Transition(TransitionInvestmentCaseLifecycleCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InvestmentCaseDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<InvestmentCaseDto>.Fail("Investment case was not found.", 404);
        if (!entity.CanTransitionTo(r.TargetState)) return Response<InvestmentCaseDto>.Fail("Invalid lifecycle transition.", 409);
        try
        {
            GateIDecisionTraceLifecycleGuard.Validate(
                entity,
                r.TargetState,
                gateIDecisionTraceLifecycle ?? DisabledGateIDecisionTraceLifecyclePolicy.Instance);
        }
        catch (InvalidOperationException exception)
        {
            return Response<InvestmentCaseDto>.Fail(exception.Message, 409);
        }
        entity.Transition(actor.ActorId, r.TargetState);
        return await Persist(entity, r.ExpectedVersion, "lifecycle-changed", ct);
    }

    public async Task<Response<NoContent>> SoftDelete(SoftDeleteInvestmentCaseCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (entity is null) return Response<NoContent>.Fail("Investment case was not found.", 404);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await repository.AdvanceBenefitCommitmentCollectionFenceAsync(entity, token);
            if (await benefitCommitments.ExistsForInvestmentCaseAsync(tenant.TenantId, entity.Id, token))
                return Response<NoContent>.Fail("Investment case has active benefit commitments.", 409);
            entity.SoftDelete(actor.ActorId);
            await repository.ReplaceAsync(entity, r.ExpectedVersion, token);
            await audit.AddAsync(Intent(entity, "soft-deleted"), token);
            return Response<NoContent>.SuccessWithoutData();
        }, ct);
    }

    public async Task<Response<InvestmentCaseDto>> Get(GetInvestmentCaseByIdQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<InvestmentCaseDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        return entity is null ? Response<InvestmentCaseDto>.Fail("Investment case was not found.", 404) : Response<InvestmentCaseDto>.Success(entity.ToDto());
    }
    public async Task<Response<IReadOnlyList<InvestmentCaseDto>>> List(CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.InvestmentCasesRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<InvestmentCaseDto>>();
        return Response<IReadOnlyList<InvestmentCaseDto>>.Success((await repository.ListAsync(tenant.TenantId, ct)).Select(x => x.ToDto()).ToArray());
    }
    private AuditIntent Intent(InvestmentCase e, string mutation) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(InvestmentCase), e.Id, mutation, DateTime.UtcNow);
    private Task<Response<InvestmentCaseDto>> Persist(InvestmentCase e, int version, string mutation, CancellationToken ct) =>
        unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, version, token); await audit.AddAsync(Intent(e, mutation), token); return Response<InvestmentCaseDto>.Success(e.ToDto()); }, ct);
}

public sealed class CreateInvestmentCaseHandler(InvestmentCaseService s) : IRequestHandler<CreateInvestmentCaseCommand, Response<InvestmentCaseDto>> { public Task<Response<InvestmentCaseDto>> Handle(CreateInvestmentCaseCommand r, CancellationToken c) => s.Create(r, c); }
public sealed class UpdateInvestmentCaseHandler(InvestmentCaseService s) : IRequestHandler<UpdateInvestmentCaseCommand, Response<InvestmentCaseDto>> { public Task<Response<InvestmentCaseDto>> Handle(UpdateInvestmentCaseCommand r, CancellationToken c) => s.Update(r, c); }
public sealed class TransitionInvestmentCaseHandler(InvestmentCaseService s) : IRequestHandler<TransitionInvestmentCaseLifecycleCommand, Response<InvestmentCaseDto>> { public Task<Response<InvestmentCaseDto>> Handle(TransitionInvestmentCaseLifecycleCommand r, CancellationToken c) => s.Transition(r, c); }
public sealed class SoftDeleteInvestmentCaseHandler(InvestmentCaseService s) : IRequestHandler<SoftDeleteInvestmentCaseCommand, Response<NoContent>> { public Task<Response<NoContent>> Handle(SoftDeleteInvestmentCaseCommand r, CancellationToken c) => s.SoftDelete(r, c); }
public sealed class GetInvestmentCaseHandler(InvestmentCaseService s) : IRequestHandler<GetInvestmentCaseByIdQuery, Response<InvestmentCaseDto>> { public Task<Response<InvestmentCaseDto>> Handle(GetInvestmentCaseByIdQuery r, CancellationToken c) => s.Get(r, c); }
public sealed class ListInvestmentCasesHandler(InvestmentCaseService s) : IRequestHandler<ListInvestmentCasesQuery, Response<IReadOnlyList<InvestmentCaseDto>>> { public Task<Response<IReadOnlyList<InvestmentCaseDto>>> Handle(ListInvestmentCasesQuery r, CancellationToken c) => s.List(c); }
