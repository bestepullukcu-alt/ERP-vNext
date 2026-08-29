using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;

/// <summary>
/// Edits a period, with what may change decided by the lifecycle:
/// <list type="bullet">
/// <item><description><c>draft</c> — everything except the code (the stable business key is never renamed) and except
/// the scope LEVEL. A draft may still correct its scope REFERENCE — a mistyped country — which is a different act from
/// moving the period to another level.</description></item>
/// <item><description><c>active</c> — name and description ONLY. Moving a live period's days, year, sequence or scope
/// would silently re-date every plan pointing at it and could break the overlap ban after the fact, so those fields
/// answer 409 and the author is pointed at close-and-open-a-new-period.</description></item>
/// <item><description><c>closed</c> — nothing. Closed is terminal, and a past plan must stay explainable.</description></item>
/// </list>
/// <para><b>ScopeType is immutable at every status (FU07)</b>, draft included: the scope is half of the period's
/// identity, and an identity is not edited. A row written by FU06 carries no ScopeType, so the comparison is made
/// against its DERIVED scope — a legacy row therefore behaves as though it always had one.</para>
/// <para>Sending an unchanged value for an immutable field is NOT a conflict: the guard compares what the period would
/// become with what it already is, so a UI that round-trips the whole form is not punished for it.</para>
/// </summary>
public sealed class UpdateCyclePeriodHandler : IRequestHandler<UpdateCyclePeriodCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICyclePeriodRepository _periods;
    private readonly CyclePeriodScopeWriteValidator _scopes;

    public UpdateCyclePeriodHandler(
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

    public async Task<Response<bool>> Handle(UpdateCyclePeriodCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var period = await _periods.GetByIdAsync(tenantId, request.CyclePeriodId, cancellationToken);
        if (period is null)
        {
            return Response<bool>.Fail("Cycle period not found.", 404);
        }

        if (period.IsClosed())
        {
            return Response<bool>.Fail(
                new[] { "A closed cycle period cannot be modified.", CyclePeriodErrorCodes.Closed }, 409);
        }

        var shapeFailure = CyclePeriodValidation.ValidateShape(
            request.CycleName, request.Year, request.SequenceInYear,
            request.StartDate, request.EndDate, request.Description);
        if (shapeFailure is not null)
        {
            return Response<bool>.Fail(CyclePeriodValidation.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        var startDate = CyclePeriodValidation.ToDay(request.StartDate);
        var endDate = CyclePeriodValidation.ToDay(request.EndDate);

        // An omitted ScopeType is DERIVED from the references the payload carries, exactly as it is on create: a
        // caller written against FU06 sends the whole form (business unit included, null meaning tenant-wide), so the
        // derivation reproduces the scope that caller means. It deliberately does NOT fall back to the row's own
        // scope: that would silently ignore an author who cleared the business unit, and a dropped input is how
        // someone ends up believing they moved a period they did not move. When they did mean to move it, the
        // immutability guard below answers 409 with an actionable reason instead.
        var requestedScopeType = CyclePeriodValidation.Trim(request.ScopeType)
                                 ?? CyclePeriodScopeRules.DeriveScopeType(
                                     request.CountryScope, request.LegalEntityId, request.BusinessUnitId);
        if (requestedScopeType is null)
        {
            var scopeTypeFailure = CyclePeriodScopeRules.ScopeTypeRequired();
            return Response<bool>.Fail(
                CyclePeriodValidation.ToErrors(scopeTypeFailure), scopeTypeFailure.StatusCode);
        }

        if (!string.Equals(
                Domain.Entities.CyclePeriodScopeTypes.Normalize(requestedScopeType),
                period.EffectiveScopeType(),
                StringComparison.Ordinal))
        {
            return Response<bool>.Fail(
                new[]
                {
                    "A cycle period's scope type is part of its identity and cannot be changed. "
                    + "Close this period and open a new one at the intended scope.",
                    CyclePeriodErrorCodes.ScopeImmutable
                },
                409);
        }

        var scopeResult = await _scopes.ValidateAsync(
            requestedScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId,
            startDate, endDate, cancellationToken);
        if (scopeResult.Failure is not null || scopeResult.Scope is null)
        {
            var failure = scopeResult.Failure
                          ?? new CyclePeriodValidation.Failure(
                              "Scope is required.", CyclePeriodErrorCodes.ScopeTypeUnknown);
            return Response<bool>.Fail(CyclePeriodValidation.ToErrors(failure), failure.StatusCode);
        }

        var scope = scopeResult.Scope;

        if (period.IsActive()
            && HasStructuralChange(period, request.Year, request.SequenceInYear, startDate, endDate, scope))
        {
            return Response<bool>.Fail(
                new[]
                {
                    "An active cycle period's dates, year, sequence and scope are immutable. "
                    + "Close it and open a new period instead.",
                    CyclePeriodErrorCodes.DatesImmutable
                },
                409);
        }

        var expectedVersion = request.ExpectedVersion ?? period.Version;
        if (expectedVersion != period.Version)
        {
            return Response<bool>.Fail(
                new[]
                {
                    "The cycle period changed since it was loaded. Reload and try again.",
                    CyclePeriodErrorCodes.ConcurrencyConflict
                },
                409);
        }

        // Draft rows may still move, so the sequence rule is re-checked against the tenant's other rows.
        if (period.IsDraft())
        {
            var sameYear = await _periods.ListByYearAsync(tenantId, request.Year, cancellationToken);
            if (CyclePeriodOverlapRules.IsSequenceTaken(
                    sameYear, scope.ScopeType, scope.ScopeRef, request.SequenceInYear, period.Id))
            {
                return Response<bool>.Fail(
                    new[]
                    {
                        $"Sequence {request.SequenceInYear} of {request.Year} is already used at scope "
                        + $"{CyclePeriodScopeRules.Describe(scope.ScopeType, scope.ScopeRef)}.",
                        CyclePeriodErrorCodes.SequenceTaken
                    },
                    409);
            }

            period.Year = request.Year;
            period.SequenceInYear = request.SequenceInYear;
            period.StartDate = startDate;
            period.EndDate = endDate;
            CyclePeriodScopeRules.Apply(
                period, scope, scopeResult.BusinessUnitSource, request.BusinessUnitCountryContext);
        }
        else
        {
            // Active: the scope did not change (guarded above), but stamping the derived type persists it the first
            // time a legacy row is touched — the read-time derivation quietly becoming permanent, one row at a time.
            period.EnsureScopeType();
        }

        period.CycleName = request.CycleName.Trim();
        period.Description = CyclePeriodValidation.Trim(request.Description);
        period.UpdatedAt = DateTimeOffset.UtcNow;
        period.UpdatedBy = _actor.ActorName;

        var replaced = await _periods.ReplaceAsync(period, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(
                new[]
                {
                    "The cycle period changed since it was loaded. Reload and try again.",
                    CyclePeriodErrorCodes.ConcurrencyConflict
                },
                409);
    }

    /// <summary>Would this edit move an ACTIVE period? Comparing values (not "was the field present?") means an
    /// unchanged round-trip is accepted while a real move is refused.</summary>
    private static bool HasStructuralChange(
        PeriodEntity period, int year, int sequenceInYear,
        DateTimeOffset startDate, DateTimeOffset endDate, CyclePeriodScopeRules.NormalizedScope scope)
        => period.Year != year
           || period.SequenceInYear != sequenceInYear
           || period.StartDate != startDate
           || period.EndDate != endDate
           || !CyclePeriodScopeRules.SameScope(period, scope);
}
