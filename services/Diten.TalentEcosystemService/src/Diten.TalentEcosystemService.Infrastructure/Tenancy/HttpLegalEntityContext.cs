using Diten.TalentEcosystemService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.TalentEcosystemService.Infrastructure.Tenancy;

/// <summary>
/// Resolves legal-entity scoping for the current request. Selection comes from the
/// <c>X-Legal-Entity-Id</c> header; the actable set is the JWT <c>legal_entities</c> claim expanded
/// with descendants from <see cref="ILegalEntityHierarchyCache"/>. Scoped: the actable set is computed
/// once per request and reused.
/// </summary>
public sealed class HttpLegalEntityContext : ILegalEntityContext
{
    public const string HeaderName = "X-Legal-Entity-Id";
    public const string ClaimType = "legal_entities";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILegalEntityHierarchyCache _hierarchyCache;

    private HashSet<Guid>? _actableSet;

    public HttpLegalEntityContext(
        IHttpContextAccessor httpContextAccessor,
        ILegalEntityHierarchyCache hierarchyCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _hierarchyCache = hierarchyCache;
    }

    public Guid? SelectedLegalEntityId
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
            return Guid.TryParse(header, out var id) && id != Guid.Empty ? id : null;
        }
    }

    public async Task<bool> IsSelectionAllowedAsync(CancellationToken ct)
    {
        if (SelectedLegalEntityId is not { } selected)
        {
            return false;
        }

        var actable = await GetActableSetAsync(ct);
        return actable.Contains(selected);
    }

    public async Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct)
    {
        var actable = await GetActableSetAsync(ct);

        // No selection: read across the caller's full actable scope (roll-up over everything assigned).
        if (SelectedLegalEntityId is not { } selected)
        {
            return actable;
        }

        // Selection present: roll up over the selected entity and its descendants, but never outside
        // the actable set (a selection the caller may not act on yields an empty, safe scope).
        var selectedScope = await _hierarchyCache.GetSelfAndDescendantsAsync(selected, ct);
        var effective = new HashSet<Guid>(selectedScope);
        effective.IntersectWith(actable);
        return effective;
    }

    private async Task<HashSet<Guid>> GetActableSetAsync(CancellationToken ct)
    {
        if (_actableSet is not null)
        {
            return _actableSet;
        }

        var assigned = ReadAssignedLegalEntityIds();
        var actable = new HashSet<Guid>();
        foreach (var id in assigned)
        {
            foreach (var scoped in await _hierarchyCache.GetSelfAndDescendantsAsync(id, ct))
            {
                actable.Add(scoped);
            }
        }

        _actableSet = actable;
        return actable;
    }

    private IReadOnlyCollection<Guid> ReadAssignedLegalEntityIds()
    {
        var raw = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<Guid>();
        }

        var ids = new HashSet<Guid>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id) && id != Guid.Empty)
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
