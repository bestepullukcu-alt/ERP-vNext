using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.CyclePeriod.Read;

/// <summary>
/// The one implementation of <see cref="ICyclePeriodReader"/>. It composes the repository with the pure
/// <see cref="CyclePeriodResolveEngine"/> and adds nothing of its own.
/// <para>It takes an <see cref="ITenantContext"/> and an <see cref="ICyclePeriodRepository"/> and <b>nothing else</b> —
/// in particular no <c>HttpClient</c> (no self-call), no legal-entity validator (that is a write-path concern, and a
/// read must not fail because MDM is down), no Territory catalog and no write path. It also holds no MicroTarget,
/// Campaign, VisitFrequencyPolicy or StrategyTemplate dependency: reading which period is in force must never become a
/// doorway into another module's aggregate.</para>
/// </summary>
public sealed class CyclePeriodReader : ICyclePeriodReader
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;

    public CyclePeriodReader(ITenantContext tenant, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _periods = periods;
    }

    public async Task<CyclePeriodResolution> ResolveActiveAsync(
        DateTimeOffset at,
        string? country,
        Guid? legalEntityId,
        string? businessUnitId,
        CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            // No tenant means no calendar to read — an empty answer, never a period from somewhere else.
            return new CyclePeriodResolution(
                CyclePeriodResolutionOutcomes.None, null, Array.Empty<Guid>(), "Tenant context is required.", null);
        }

        var active = await _periods.ListActiveAsync(tenantId, cancellationToken);
        var request = new CyclePeriodResolveEngine.ScopeRequest(
            CyclePeriodScopeRules.NormalizeCountry(country),
            legalEntityId is { } id && id != Guid.Empty ? id : null,
            CyclePeriodScopeRules.Trim(businessUnitId));

        var resolution = CyclePeriodResolveEngine.Resolve(active, at, request);

        return new CyclePeriodResolution(
            resolution.Outcome,
            resolution.Period is null ? null : CyclePeriodSnapshot.From(resolution.Period),
            resolution.CandidateIds,
            resolution.Reason,
            resolution.ResolvedScopeType);
    }

    public async Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        var period = await _periods.GetByIdAsync(tenantId, cyclePeriodId, cancellationToken);
        return period is null ? null : CyclePeriodSnapshot.From(period);
    }

    /// <summary>
    /// FU08 batch lookup. One round trip on purpose: the campaign grid shows the bound period's code per row, and
    /// calling <see cref="GetByIdAsync"/> per row would be an N+1 over a page of campaigns.
    /// <para>It reads the tenant's periods through the SAME repository method the picker already uses and narrows in
    /// memory — deliberately, so that no new query shape and no new index is introduced on a protected FU06 surface.
    /// A tenant's planning calendar is tens of rows, not thousands.</para>
    /// <para>Unknown ids are simply absent from the result: this is a lookup, not a validation. The write path proves
    /// a binding through <see cref="GetByIdAsync"/>, which distinguishes "missing" from "found".</para>
    /// </summary>
    public async Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
        IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId || cyclePeriodIds is null || cyclePeriodIds.Count == 0)
        {
            return Array.Empty<CyclePeriodSnapshot>();
        }

        var wanted = cyclePeriodIds.Where(id => id != Guid.Empty).ToHashSet();
        if (wanted.Count == 0)
        {
            return Array.Empty<CyclePeriodSnapshot>();
        }

        var rows = await _periods.ListAsync(tenantId, cancellationToken);
        return rows
            .Where(p => wanted.Contains(p.Id))
            .Select(CyclePeriodSnapshot.From)
            .ToList();
    }

    public async Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
        int year, string? scopeType, string? scopeRef, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<CyclePeriodSnapshot>();
        }

        var rows = await _periods.ListByYearAsync(tenantId, year, cancellationToken);

        // A listing, not a resolution: with no scope named it shows every level side by side, and it never falls back.
        var filtered = CyclePeriodScopeTypes.IsKnown(scopeType)
            ? CyclePeriodOverlapRules.InScope(
                rows, CyclePeriodScopeTypes.Normalize(scopeType), CyclePeriodScopeRules.Trim(scopeRef))
            : rows;

        return filtered
            .OrderBy(p => p.SequenceInYear)
            .Select(CyclePeriodSnapshot.From)
            .ToList();
    }
}
