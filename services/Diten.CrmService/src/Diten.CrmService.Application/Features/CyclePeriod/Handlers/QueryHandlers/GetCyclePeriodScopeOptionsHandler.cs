using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// FU07 — one round trip for the cascading scope selector: the levels, the governed country values, the tenant's
/// referenceable legal entities, and the business units its territory plans actually cover.
/// <para><b>Three sources, three separate readiness flags.</b> An empty list because a reference set is unpublished, an
/// empty list because MDM is unreachable, and an empty list because no plan matches are three different situations. A
/// UI that cannot tell them apart shows the author a silent empty dropdown and no way to act, so each is reported on
/// its own. <b>A hardcoded fallback list is forbidden in all three cases</b> (PSS-LOOKUPS-001): an option the platform
/// does not know would be authored and then refused at save.</para>
/// <para><b>The business-unit fallback is a different thing from a hardcoded list.</b> When no territory plan matches,
/// the list falls back to the published <c>business-unit</c> vocabulary — still governed, still the same set the write
/// path validates against, only no longer narrowed by a plan. This keeps business-unit periods authorable when the
/// field plan does not exist yet, and it is also what makes the country-vocabulary transition safe: should MOD-0151's
/// country codes and this FU's ever disagree, the picker degrades to the full alphabet instead of going blank.
/// <c>BusinessUnitFromTerritory</c> tells the UI which of the two it is looking at.</para>
/// <para>This handler decides nothing. It is a READ; what may be WRITTEN is decided by the write path's vocabulary
/// check, so a code missing from this list but present in the set is still accepted (and stamped <c>manual</c>).</para>
/// </summary>
public sealed class GetCyclePeriodScopeOptionsHandler
    : IRequestHandler<GetCyclePeriodScopeOptionsQuery, Response<CyclePeriodScopeOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _references;
    private readonly ICyclePeriodLegalEntityCatalog _legalEntities;
    private readonly ITerritoryBusinessUnitCatalog _territory;

    public GetCyclePeriodScopeOptionsHandler(
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

    public async Task<Response<CyclePeriodScopeOptionsDto>> Handle(
        GetCyclePeriodScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<CyclePeriodScopeOptionsDto>.Fail("Tenant context is required.", 400);
        }

        var countrySet = await _references.GetPublishedValuesAsync(
            CyclePeriodReferenceSets.CountrySet, cancellationToken);
        var countries = countrySet.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new CyclePeriodScopeOptionDto(
                v.ValueCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim().ToUpperInvariant() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var legalEntityLookup = await _legalEntities.GetReferenceableAsync(cancellationToken);
        var legalEntities = legalEntityLookup.Options
            .Select(o => new CyclePeriodScopeOptionDto(
                o.LegalEntityId.ToString("D"),
                string.IsNullOrWhiteSpace(o.DisplayName) ? o.Code : o.DisplayName,
                string.IsNullOrWhiteSpace(o.Code) ? null : o.Code))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (businessUnits, fromTerritory) = await LoadBusinessUnitsAsync(request, cancellationToken);

        var dto = new CyclePeriodScopeOptionsDto(
            CyclePeriodScopeTypes.ByPrecedence,
            countries,
            countrySet.IsPublished && countries.Count > 0,
            legalEntities,
            legalEntityLookup.IsAvailable,
            businessUnits,
            businessUnits.Count > 0,
            fromTerritory,
            CyclePeriodReferenceSets.CountrySet,
            CyclePeriodReferenceSets.BusinessUnitSet);

        return Response<CyclePeriodScopeOptionsDto>.Success(dto);
    }

    private async Task<(IReadOnlyList<CyclePeriodScopeOptionDto> Options, bool FromTerritory)> LoadBusinessUnitsAsync(
        GetCyclePeriodScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        // Without a window there is nothing to intersect a plan against, so the picker shows the vocabulary until the
        // author has typed the dates — rather than an empty list that looks like "no business units exist".
        if (request.StartDate is { } start && request.EndDate is { } end)
        {
            var candidates = await _territory.GetCandidatesAsync(
                CyclePeriodScopeRules.NormalizeCountry(request.Country),
                CyclePeriodValidation.ToDay(start),
                CyclePeriodValidation.ToDay(end),
                cancellationToken);

            if (candidates.Count > 0)
            {
                return (candidates
                    .Select(c => new CyclePeriodScopeOptionDto(
                        c.BusinessUnitCode,
                        c.BusinessUnitCode,
                        c.SourceModelCodes.Count == 0 ? null : string.Join(", ", c.SourceModelCodes)))
                    .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList(), true);
            }
        }

        var set = await _references.GetPublishedValuesAsync(
            CyclePeriodReferenceSets.BusinessUnitSet, cancellationToken);

        return (set.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new CyclePeriodScopeOptionDto(
                v.ValueCode.Trim(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList(), false);
    }
}
