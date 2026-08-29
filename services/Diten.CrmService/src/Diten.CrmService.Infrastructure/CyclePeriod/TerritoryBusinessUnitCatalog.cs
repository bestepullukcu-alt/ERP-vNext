using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.CyclePeriod;

/// <summary>
/// MOD-0165 FU07 — derives the business-unit candidates for the scope picker from MOD-0151 Territory, READ ONLY.
/// <para><b>It wraps the Territory repository so a CyclePeriod handler never holds one.</b>
/// <c>ITerritoryModelRepository</c> carries <c>InsertAsync</c> / <c>UpdateAsync</c>; this class exposes a single read
/// method, which makes "FU07 never writes to Territory" a structural fact rather than a convention someone has to
/// remember.</para>
/// <para><b>Only ACTIVE plans count</b> (D-TERRITORY-STATUS). A draft plan is not a commitment, and a superseded one
/// describes an organisation that no longer exists — offering either would show the author units nobody works in.</para>
/// <para><b>The window intersection is done IN MEMORY on purpose.</b> <c>EffectiveFrom</c> / <c>EffectiveTo</c> are
/// DateTimeOffset and therefore stored as BSON arrays; pushing a range comparison over both into Mongo is the
/// parallel-array trap that 500s the query. The active-plan set of one tenant is small, so this costs nothing.</para>
/// </summary>
public sealed class TerritoryBusinessUnitCatalog : ITerritoryBusinessUnitCatalog
{
    /// <summary>The Territory lifecycle value that means "in force". MOD-0151 keeps this vocabulary in MOD-0048; the
    /// literal matches the one its own repository filters on, so the two cannot disagree.</summary>
    private const string ActiveModelStatus = "active";

    /// <summary>FU02A supports business-unit scopes only; other classifications are later Territory FUs and must not
    /// leak into a business-unit picker.</summary>
    private const string BusinessUnitScopeType = "business-unit";

    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ILogger<TerritoryBusinessUnitCatalog> _logger;

    public TerritoryBusinessUnitCatalog(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ILogger<TerritoryBusinessUnitCatalog> logger)
    {
        _tenant = tenant;
        _models = models;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
        string? country,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<TerritoryBusinessUnitCandidate>();
        }

        IReadOnlyList<Domain.Entities.TerritoryModel> models;
        try
        {
            // Guid.Empty excludes nothing: the parameter exists for MOD-0151's own "every plan but this one" checks.
            models = await _models.ListActiveAsync(tenantId, Guid.Empty, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // An unreadable Territory is an EMPTY candidate list, never a substituted one: the caller degrades to the
            // governed business-unit vocabulary, which is still governed.
            _logger.LogWarning(ex, "Territory business-unit candidates could not be read; returning none.");
            return Array.Empty<TerritoryBusinessUnitCandidate>();
        }

        var wanted = string.IsNullOrWhiteSpace(country) ? null : country.Trim();

        var matching = models.Where(m =>
            string.Equals(m.Status, ActiveModelStatus, StringComparison.OrdinalIgnoreCase)
            && (wanted is null || string.Equals(m.CountryScope, wanted, StringComparison.OrdinalIgnoreCase))
            // Half-open on the right: a plan with no end date is open-ended, which is the normal case for a live plan.
            && m.EffectiveFrom <= endDate
            && (m.EffectiveTo is null || startDate <= m.EffectiveTo.Value));

        var byCode = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in matching)
        {
            foreach (var scope in model.BusinessScopes)
            {
                if (!string.Equals(scope.ScopeType, BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(scope.ScopeCode))
                {
                    continue;
                }

                var code = scope.ScopeCode.Trim();
                if (!byCode.TryGetValue(code, out var sources))
                {
                    sources = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    byCode[code] = sources;
                }

                if (!string.IsNullOrWhiteSpace(model.ModelCode))
                {
                    sources.Add(model.ModelCode.Trim());
                }
            }
        }

        return byCode
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new TerritoryBusinessUnitCandidate(pair.Key, pair.Value.ToList()))
            .ToList();
    }
}
