using System.Security.Claims;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Resources;

/// <summary>
/// WP-MOB-B02 — "which resource(s) am I?" for the signed-in caller. <see cref="UserId"/> is resolved by the controller
/// from the caller principal (<see cref="MyResourceIdentity.ResolveUserId"/>) — never from a route, query or body, so
/// a caller can only ever ask about themselves.
/// </summary>
public sealed record GetMyResourcesQuery(string? UserId) : IRequest<Response<MyResourcesDto>>;

/// <summary>
/// Response contract. <c>items[]</c> is fixed even though the interim mapping always yields exactly one entry, so a
/// later real User→Resource bridge (multiple resources, inactive ones) does not break mobile clients.
/// Field names align with <see cref="PlannedVisit"/>'s <c>ResourceId</c> / <c>ResourceType</c>.
/// </summary>
public sealed record MyResourcesDto(IReadOnlyList<MyResourceItemDto> Items);

public sealed record MyResourceItemDto(
    string ResourceId,
    string ResourceType,
    string Status,
    // WP-VP-2 (B-1, additive) — the caller's display name from the user directory (null when it cannot be resolved).
    // WP-VP-4G (F4-10) — a person's name (first + last); the user name / e-mail only as the last resort.
    string? DisplayName = null,
    // WP-VP-4G (F4-8, additive) — the rep's country: the country of a current territory assignment (Territory's
    // lower-case `country` vocabulary), else the legal entity's (no source in CRM yet), else null.
    string? CountryCode = null);

public static class MyResourceStatuses
{
    public const string Active = "active";
}

public static class MyResourceIdentity
{
    /// <summary>
    /// The caller's stable user id: <c>sub</c>, else <see cref="ClaimTypes.NameIdentifier"/> (the JWT handler's
    /// default inbound mapping of <c>sub</c>). Deliberately NO email/name fallback (unlike <c>HttpActorContext</c>):
    /// a resource id must be the stable identifier, not a display value. Null when unauthenticated or absent.
    /// </summary>
    public static string? ResolveUserId(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// ⚠ INTERIM user-as-resource (owner decision 2026-09-25): the caller's user id IS their ResourceId, type
/// <c>user</c>, always <c>active</c>, always exactly one item. No resource master is consulted because none exists
/// yet. When the real <c>UserResourceAssignment</c> lands, only this handler's body changes — the contract stays.
/// </summary>
public sealed class GetMyResourcesQueryHandler : IRequestHandler<GetMyResourcesQuery, Response<MyResourcesDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IUserDisplayNameResolver _names;
    private readonly ITerritoryResourceAssignmentRepository? _assignments;
    private readonly ITerritoryModelRepository? _models;
    private readonly ITerritoryNodeRepository? _nodes;

    public GetMyResourcesQueryHandler(
        ITenantContext tenant,
        IUserDisplayNameResolver names,
        // WP-VP-4G (F4-8) — the rep's current territory assignment (its node's / model's country).
        ITerritoryResourceAssignmentRepository? assignments = null,
        ITerritoryModelRepository? models = null,
        ITerritoryNodeRepository? nodes = null)
    {
        _tenant = tenant;
        _names = names;
        _assignments = assignments;
        _models = models;
        _nodes = nodes;
    }

    public async Task<Response<MyResourcesDto>> Handle(GetMyResourcesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            // Authenticated-but-identityless is meaningless for "me": refuse rather than return an empty list.
            return Response<MyResourcesDto>.Fail("Caller identity could not be resolved.", 401);
        }

        // Tenant is still mandatory: the resource id is only meaningful inside the caller's tenant, and a
        // header/token tenant mismatch has already been refused 400 by TenantResolutionMiddleware.
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<MyResourcesDto>.Fail("Tenant context is required.", 400);
        }

        var userId = request.UserId.Trim();
        string? displayName = null;
        if (Guid.TryParse(userId, out var id))
        {
            var names = await _names.ResolveAsync(new[] { id }, cancellationToken);
            displayName = names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : null;
        }

        var item = new MyResourceItemDto(
            userId, PlannedVisitResourceTypes.User, MyResourceStatuses.Active, displayName,
            await CountryOfAsync(tenantId, userId, cancellationToken));
        return Response<MyResourcesDto>.Success(new MyResourcesDto([item]));
    }

    /// <summary>
    /// WP-VP-4G (F4-8) — the rep's country, lower-case (Territory's <c>country</c> vocabulary): the node country of the
    /// rep's CURRENT territory assignments (the "my accounts" rule, <see cref="RepTerritoryCoverage"/>; several → the most
    /// common, then alphabetical), else the owning model's country scope. The legal-entity fallback has no source in CRM
    /// yet (the token carries no legal entity), so it is null then.
    /// </summary>
    private async Task<string?> CountryOfAsync(Guid tenantId, string resourceId, CancellationToken cancellationToken)
    {
        if (_assignments is null || _models is null)
        {
            return null;
        }

        var current = await VisitPlanning.MyAccounts.RepTerritoryCoverage.CurrentAssignmentsAsync(
            _assignments, _models, tenantId, resourceId, DateTimeOffset.UtcNow, cancellationToken);
        if (current.Count == 0)
        {
            return null;
        }

        var countries = new List<string>();
        if (_nodes is not null)
        {
            var nodes = await _nodes.ListByIdsAsync(
                tenantId, current.Select(a => a.TerritoryId!.Value).Distinct().ToList(), cancellationToken);
            countries.AddRange(nodes.Select(n => n.CountryCode).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim().ToLowerInvariant()));
        }

        if (countries.Count == 0)
        {
            var models = await _models.ListByIdsAsync(tenantId, current.Select(a => a.ModelId).Distinct().ToList(), cancellationToken);
            countries.AddRange(models.Select(m => m.CountryScope).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim().ToLowerInvariant()));
        }

        return countries
            .GroupBy(c => c, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();
    }
}
