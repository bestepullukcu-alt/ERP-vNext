using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Application.Features.StrategyTemplate.Rules;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — the play scope selector's option source, in one round trip. A deliberate mirror of
/// <c>GetCampaignScopeOptionsHandler</c>.
///
/// <para><b>Three sources, three readiness flags.</b> An empty list because a reference set is unpublished, an empty list
/// because MDM is unreachable, and an empty list because no territory plan matches are three different situations, and an
/// author needs to know which one they are looking at. A hardcoded fallback list is forbidden in all three cases: an
/// option the platform does not know would be authored and then refused at save.</para>
///
/// <para>This handler decides NOTHING. What may be written is decided by the write path's vocabulary check, so a code
/// missing from this list but present in the published set is still accepted.</para>
///
/// <para>It reads through the SAME read-only catalog seams the campaign and cycle-period selectors use. Those seams are
/// narrow windows onto MOD-0151 and MDM rather than another module's logic; cloning them would mean two outbound clients
/// and two behaviours the day a dependency is slow. The scope RULES are mirrored — an outbound dependency window is not a
/// rule.</para>
/// </summary>
public sealed class GetStrategyTemplateScopeOptionsHandler
    : IRequestHandler<GetStrategyTemplateScopeOptionsQuery, Response<StrategyTemplateScopeOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _references;
    private readonly ICyclePeriodLegalEntityCatalog _legalEntities;
    private readonly ITerritoryBusinessUnitCatalog _territory;

    public GetStrategyTemplateScopeOptionsHandler(
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

    public async Task<Response<StrategyTemplateScopeOptionsDto>> Handle(
        GetStrategyTemplateScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<StrategyTemplateScopeOptionsDto>.Fail("Tenant context is required.", 400);
        }

        var countrySet = await _references.GetPublishedValuesAsync(
            StrategyTemplateScopeReferenceSets.CountrySet, cancellationToken);
        var countries = countrySet.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new StrategyTemplateScopeOptionDto(
                v.ValueCode.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim().ToUpperInvariant() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var legalEntityLookup = await _legalEntities.GetReferenceableAsync(cancellationToken);
        var legalEntities = legalEntityLookup.Options
            .Select(o => new StrategyTemplateScopeOptionDto(
                o.LegalEntityId.ToString("D"),
                string.IsNullOrWhiteSpace(o.DisplayName) ? o.Code : o.DisplayName))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (businessUnits, fromTerritory) = await LoadBusinessUnitsAsync(request, cancellationToken);

        return Response<StrategyTemplateScopeOptionsDto>.Success(new StrategyTemplateScopeOptionsDto(
            StrategyTemplateScopeTypes.ByPrecedence,
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
    /// <para>Without a window there is nothing to intersect a plan against, so the picker shows the vocabulary until the
    /// author has typed the dates — rather than an empty list that reads as "no business units exist". The fallback is
    /// also what keeps business-unit plays authorable before their field plan exists.</para>
    /// </summary>
    private async Task<(IReadOnlyList<StrategyTemplateScopeOptionDto> Options, bool FromTerritory)> LoadBusinessUnitsAsync(
        GetStrategyTemplateScopeOptionsQuery request, CancellationToken cancellationToken)
    {
        if (request.StartDate is { } start && request.EndDate is { } end)
        {
            var candidates = await _territory.GetCandidatesAsync(
                StrategyTemplateScopeRules.NormalizeCountry(request.Country), start, end, cancellationToken);

            if (candidates.Count > 0)
            {
                return (candidates
                    .Select(c => new StrategyTemplateScopeOptionDto(c.BusinessUnitCode, c.BusinessUnitCode))
                    .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList(), true);
            }
        }

        var set = await _references.GetPublishedValuesAsync(
            StrategyTemplateScopeReferenceSets.BusinessUnitSet, cancellationToken);

        return (set.Values
            .Where(v => v.IsActive && !v.IsDeprecated && !string.IsNullOrWhiteSpace(v.ValueCode))
            .Select(v => new StrategyTemplateScopeOptionDto(
                v.ValueCode.Trim(),
                string.IsNullOrWhiteSpace(v.DisplayName) ? v.ValueCode.Trim() : v.DisplayName.Trim()))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList(), false);
    }
}
