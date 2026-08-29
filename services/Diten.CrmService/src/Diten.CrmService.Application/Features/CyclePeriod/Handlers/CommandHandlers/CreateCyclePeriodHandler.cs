using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;

/// <summary>
/// Creates a planning period, always <c>draft</c>: a period is never born live, because going live is where the
/// overlap ban is decided and that is a separate act with a separate permission.
/// <para>Order matters and is fixed (FU07): shape → scope (invariant, governed vocabulary, then the fail-closed MDM
/// check) → the two set rules that need other rows (code uniqueness across the tenant, sequence uniqueness within the
/// scope) → the write. <b>Every external check completes before the insert</b>, so a dependency outage can never leave
/// a half-authored period behind. The active-overlap check deliberately does NOT run here — drafts may overlap, which
/// is what lets a planner sketch alternatives.</para>
/// <para>This handler touches exactly one collection. It creates no MicroTarget row, no Campaign, no
/// VisitFrequencyPolicy and no working-day calculation, and it never writes to Territory or MDM: a period says which
/// period it is and where it lives, and nothing else.</para>
/// </summary>
public sealed class CreateCyclePeriodHandler : IRequestHandler<CreateCyclePeriodCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICyclePeriodRepository _periods;
    private readonly CyclePeriodScopeWriteValidator _scopes;

    public CreateCyclePeriodHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICyclePeriodRepository periods,
        CyclePeriodScopeWriteValidator scopes)
    {
        _tenant = tenant;
        _actor = actor;
        _periods = periods;
        _scopes = scopes;
    }

    public async Task<Response<Guid>> Handle(CreateCyclePeriodCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (CyclePeriodValidation.ValidateCycleCode(request.CycleCode) is { } codeFailure)
        {
            return Response<Guid>.Fail(CyclePeriodValidation.ToErrors(codeFailure), codeFailure.StatusCode);
        }

        var shapeFailure = CyclePeriodValidation.ValidateShape(
            request.CycleName, request.Year, request.SequenceInYear,
            request.StartDate, request.EndDate, request.Description);
        if (shapeFailure is not null)
        {
            return Response<Guid>.Fail(CyclePeriodValidation.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        var startDate = CyclePeriodValidation.ToDay(request.StartDate);
        var endDate = CyclePeriodValidation.ToDay(request.EndDate);

        var scopeResult = await _scopes.ValidateAsync(
            request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
            startDate, endDate, cancellationToken);
        if (scopeResult.Failure is { } scopeFailure || scopeResult.Scope is null)
        {
            var failure = scopeResult.Failure
                          ?? new CyclePeriodValidation.Failure("Scope is required.", CyclePeriodErrorCodes.ScopeTypeUnknown);
            return Response<Guid>.Fail(CyclePeriodValidation.ToErrors(failure), failure.StatusCode);
        }

        var scope = scopeResult.Scope;
        var code = request.CycleCode.Trim().ToLowerInvariant();

        // A closed period keeps its code forever: reusing it would make an old plan's provenance ambiguous. The check
        // is tenant-wide and NOT per scope - one code names one period, wherever it lives.
        var sameCode = await _periods.ListByCodeAsync(tenantId, code, cancellationToken);
        if (CyclePeriodOverlapRules.IsCodeTaken(sameCode))
        {
            return Response<Guid>.Fail(
                new[] { $"A cycle period already uses CycleCode '{code}'.", CyclePeriodErrorCodes.CodeTaken }, 409);
        }

        var sameYear = await _periods.ListByYearAsync(tenantId, request.Year, cancellationToken);
        if (CyclePeriodOverlapRules.IsSequenceTaken(sameYear, scope.ScopeType, scope.ScopeRef, request.SequenceInYear))
        {
            return Response<Guid>.Fail(
                new[]
                {
                    $"Sequence {request.SequenceInYear} of {request.Year} is already used at scope "
                    + $"{CyclePeriodScopeRules.Describe(scope.ScopeType, scope.ScopeRef)}.",
                    CyclePeriodErrorCodes.SequenceTaken
                },
                409);
        }

        var entity = new PeriodEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CycleCode = code,
            CycleName = request.CycleName.Trim(),
            Year = request.Year,
            SequenceInYear = request.SequenceInYear,
            StartDate = startDate,
            EndDate = endDate,
            Description = CyclePeriodValidation.Trim(request.Description),
            CycleStatus = CyclePeriodStatuses.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };
        CyclePeriodScopeRules.Apply(
            entity, scope, scopeResult.BusinessUnitSource, request.BusinessUnitCountryContext);

        await _periods.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
