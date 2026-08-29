using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Campaign.Handlers.QueryHandlers;

/// <summary>
/// MOD-0165 FU09 — the campaign scope selector's option source, in one round trip.
///
/// <para><b>Three sources, three readiness flags.</b> An empty list because a reference set is unpublished, an empty
/// list because MDM is unreachable, and an empty list because no territory plan matches are three different
/// situations, and an author needs to know which one they are looking at. A hardcoded fallback list is forbidden in
/// all three cases: an option the platform does not know would be authored and then refused at save.</para>
///
/// <para>This handler decides NOTHING. What may be written is decided by the write path's vocabulary check, so a code
/// missing from this list but present in the published set is still accepted.</para>
///
/// <para>It reads through the SAME read-only catalog seams the cycle-period selector uses. Those seams are narrow
/// windows onto MOD-0151 and MDM rather than cycle-period logic; cloning them would mean two outbound clients and two
/// different behaviours the day a dependency is slow. The scope RULES are mirrored — an outbound dependency window is
/// not a rule.</para>
/// </summary>
public sealed class GetCampaignScopeOptionsHandler
    : IRequestHandler<GetCampaignScopeOptionsQuery, Response<CampaignScopeOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _references;
    private readonly ICyclePeriodLegalEntityCatalog _legalEntities;
    private readonly ITerritoryBusinessUnitCatalog _territory;

    public GetCampaignScopeOptionsHandler(
        ITenantContext tenant,
        IReferenceDataCatalogReader references,
        ICyclePeriodLegalEntityCatalog legalEntities,
        ITerritoryBusinessUnitCatalog territory)
    {
        _tenant = tenant;
        _references = references;
        _legalEntities = legalEntities;
        _territory = territory;
    }

    public async Task<Response<CampaignScopeOptionsDto>> Handle(
        GetCampaignScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<CampaignScopeOptionsDto>.Fail("Tenant context is required.", 400);
        }

        var countrySet = await _references.GetPublishedValuesAsync(
            CampaignScopeReferenceSets.CountrySet, cancellationToken);
        var countries = countrySet.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new CampaignScopeOptionDto(
                v.ValueCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim().ToUpperInvariant() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var legalEntityLookup = await _legalEntities.GetReferenceableAsync(cancellationToken);
        var legalEntities = legalEntityLookup.Options
            .Select(o => new CampaignScopeOptionDto(
                o.LegalEntityId.ToString("D"),
                string.IsNullOrWhiteSpace(o.DisplayName) ? o.Code : o.DisplayName))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (businessUnits, fromTerritory) = await LoadBusinessUnitsAsync(request, cancellationToken);

        return Response<CampaignScopeOptionsDto>.Success(new CampaignScopeOptionsDto(
            CampaignScopeTypes.ByPrecedence,
            countries,
            countrySet.IsPublished && countries.Count > 0,
            legalEntities,
            legalEntityLookup.IsAvailable,
            businessUnits,
            businessUnits.Count > 0,
            fromTerritory));
    }

    /// <summary>
    /// The territory-derived narrowing, or the published vocabulary it falls back to.
    /// <para>Without a window there is nothing to intersect a plan against, so the picker shows the vocabulary until
    /// the author has typed the dates — rather than an empty list that reads as "no business units exist". The
    /// fallback is also what keeps business-unit campaigns authorable before their field plan exists, and what keeps
    /// the picker useful if the country vocabularies ever disagree.</para>
    /// </summary>
    private async Task<(IReadOnlyList<CampaignScopeOptionDto> Options, bool FromTerritory)> LoadBusinessUnitsAsync(
        GetCampaignScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        if (request.StartDate is { } start && request.EndDate is { } end)
        {
            var candidates = await _territory.GetCandidatesAsync(
                CampaignScopeRules.NormalizeCountry(request.Country), start, end, cancellationToken);

            if (candidates.Count > 0)
            {
                return (candidates
                    .Select(c => new CampaignScopeOptionDto(c.BusinessUnitCode, c.BusinessUnitCode))
                    .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList(), true);
            }
        }

        var set = await _references.GetPublishedValuesAsync(
            CampaignScopeReferenceSets.BusinessUnitSet, cancellationToken);

        return (set.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new CampaignScopeOptionDto(
                v.ValueCode.Trim(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList(), false);
    }
}

/// <summary>
/// MOD-0165 FU09 — the cycle periods applicable to a campaign at the scope the author is currently editing.
///
/// <para><b>The rule lives here, on the server.</b> Deciding applicability in the browser would be a second copy of
/// it, and a direct API call would walk straight past it — the write path refuses the same mismatch with
/// <c>campaign_cycle_period_scope_mismatch</c>, so the picker and the guard answer from one rule.</para>
///
/// <para><b>ACTIVE periods only</b>, which mirrors the FU08 bind rule: a draft period's dates can still move and a
/// closed one cannot be newly bound, so offering either would show the author something the save would reject. A
/// campaign already bound to a period that has since closed keeps that binding — the FORM re-injects that value, the
/// picker does not resurrect it.</para>
///
/// <para>It reads through the existing read seam and adds no method to it: <c>ListByYearAsync</c> already takes a
/// scope filter, and at most two addresses are ever consulted (the campaign's own, plus the tenant fallback).</para>
/// </summary>
public sealed class GetApplicableCyclePeriodsHandler
    : IRequestHandler<GetApplicableCyclePeriodsQuery, Response<CampaignApplicableCyclePeriodsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodReader _periods;

    public GetApplicableCyclePeriodsHandler(ITenantContext tenant, ICyclePeriodReader periods)
    {
        _tenant = tenant;
        _periods = periods;
    }

    public async Task<Response<CampaignApplicableCyclePeriodsDto>> Handle(
        GetApplicableCyclePeriodsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<CampaignApplicableCyclePeriodsDto>.Fail("Tenant context is required.", 400);
        }

        // The scope is normalised with the same rules the write path uses, so the picker can never show a set the save
        // would disagree with. A malformed scope is a bad request here too, not an empty list.
        var (scope, failure) = CampaignScopeRules.Normalize(
            request.ScopeType, request.CountryScope, request.LegalEntityId, request.BusinessUnitId);
        if (failure is not null || scope is null)
        {
            return Response<CampaignApplicableCyclePeriodsDto>.Fail(failure!.Error, failure.StatusCode);
        }

        var applicable = CampaignCycleApplicability.ApplicableScopes(scope.ScopeType, scope.ScopeRef);
        var seen = new HashSet<Guid>();
        var items = new List<CampaignCyclePeriodDto>();

        foreach (var (scopeType, scopeRef) in applicable)
        {
            // One listing per applicable address, most specific first. ListByYearAsync needs a year, and a picker has
            // to span the years a campaign may sit in, so the current and the neighbouring years are consulted.
            foreach (var year in NeighbouringYears())
            {
                var rows = await _periods.ListByYearAsync(year, scopeType, scopeRef, cancellationToken);
                foreach (var row in rows)
                {
                    if (!string.Equals(row.CycleStatus, CyclePeriodStatuses.Active, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (seen.Add(row.CyclePeriodId))
                    {
                        items.Add(CampaignMapper.ToCyclePeriodDto(row));
                    }
                }
            }
        }

        return Response<CampaignApplicableCyclePeriodsDto>.Success(new CampaignApplicableCyclePeriodsDto(
            scope.ScopeType,
            scope.ScopeRef,
            applicable.Select(s => CampaignScopeRules.Describe(s.ScopeType, s.ScopeRef)).ToList(),
            items));
    }

    /// <summary>
    /// The planning years a picker should span. A campaign is authored around now, and a period may belong to the
    /// previous or next planning year (a cycle crossing a year boundary is real), so three years are consulted rather
    /// than guessing one from the campaign's dates — which the author may not have typed yet.
    /// </summary>
    private static IEnumerable<int> NeighbouringYears()
    {
        var current = DateTimeOffset.UtcNow.Year;
        yield return current;
        yield return current + 1;
        yield return current - 1;
    }
}
