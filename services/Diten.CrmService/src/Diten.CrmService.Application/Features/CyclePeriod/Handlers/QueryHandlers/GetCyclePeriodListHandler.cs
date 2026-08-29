using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// The period grid. Filtering and ordering happen in memory over the tenant's rows, so no DateTimeOffset field ever
/// becomes a Mongo sort key: <c>StartDate</c> / <c>EndDate</c> are stored as BSON arrays and sorting two of them
/// together is the parallel-array trap that 500s the query. Ordering is (year desc, sequence desc) — plain integers,
/// and the order a planner actually reads a calendar in.
/// <para>In-memory narrowing is also what makes FU07 free of a data migration: the scope filters run over
/// <c>EffectiveScopeType()</c>, which derives the scope of a row FU06 wrote, so no query can miss a row for lacking a
/// field.</para>
/// <para>An unknown status or scope-type filter is REFUSED (400) rather than ignored: silently returning everything
/// when the caller asked for something specific is how a UI ends up lying about what it is showing.</para>
/// </summary>
public sealed class GetCyclePeriodListHandler : IRequestHandler<GetCyclePeriodListQuery, Response<CyclePeriodListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;

    public GetCyclePeriodListHandler(ITenantContext tenant, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _periods = periods;
    }

    public async Task<Response<CyclePeriodListDto>> Handle(
        GetCyclePeriodListQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CyclePeriodListDto>.Fail("Tenant context is required.", 400);
        }

        if (CyclePeriodValidation.ValidateStatusFilter(request.CycleStatus) is { } statusFailure)
        {
            return Response<CyclePeriodListDto>.Fail(
                CyclePeriodValidation.ToErrors(statusFailure), statusFailure.StatusCode);
        }

        if (CyclePeriodValidation.ValidateScopeTypeFilter(request.ScopeType) is { } scopeTypeFailure)
        {
            return Response<CyclePeriodListDto>.Fail(
                CyclePeriodValidation.ToErrors(scopeTypeFailure), scopeTypeFailure.StatusCode);
        }

        var rows = await _periods.ListAsync(tenantId, cancellationToken);
        IEnumerable<Domain.Entities.CyclePeriod> filtered = rows;

        if (!string.IsNullOrWhiteSpace(request.CycleStatus))
        {
            var status = CyclePeriodStatuses.Normalize(request.CycleStatus);
            filtered = filtered.Where(p => string.Equals(p.CycleStatus, status, StringComparison.Ordinal));
        }

        if (request.Year is { } year)
        {
            filtered = filtered.Where(p => p.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(request.ScopeType))
        {
            var scopeType = CyclePeriodScopeTypes.Normalize(request.ScopeType);
            filtered = filtered.Where(p =>
                string.Equals(p.EffectiveScopeType(), scopeType, StringComparison.Ordinal));
        }

        // Each reference filter narrows to its own level. They stack rather than fall back: a listing shows what
        // exists, and mixing levels here would quietly reproduce the resolver's precedence in a place that must not
        // have one.
        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            var country = CyclePeriodScopeRules.NormalizeCountry(request.Country);
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.Country, country));
        }

        if (request.LegalEntityId is { } legalEntityId && legalEntityId != Guid.Empty)
        {
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.LegalEntity, legalEntityId.ToString("D")));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessUnitId))
        {
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.BusinessUnit, request.BusinessUnitId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(request.CycleCode))
        {
            filtered = filtered.Where(p => string.Equals(
                p.CycleCode, request.CycleCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // "Which periods contain this day?" — a listing filter, NOT the resolve decision: it ignores status and scope
        // on purpose, so a planner can see a draft that overlaps the day they are looking at.
        if (request.CoversDate is { } coversDate)
        {
            var day = CyclePeriodValidation.ToDay(coversDate);
            filtered = filtered.Where(p => p.CoversInstant(day));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(p =>
                p.CycleCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || p.CycleName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var items = filtered
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.SequenceInYear)
            .ThenBy(p => p.CycleCode, StringComparer.OrdinalIgnoreCase)
            .Select(CyclePeriodMapper.ToListItem)
            .ToList();

        return Response<CyclePeriodListDto>.Success(new CyclePeriodListDto(items, items.Count));
    }
}
