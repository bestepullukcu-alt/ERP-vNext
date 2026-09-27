using System.Security.Claims;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
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

public sealed record MyResourceItemDto(string ResourceId, string ResourceType, string Status);

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

    public GetMyResourcesQueryHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<MyResourcesDto>> Handle(GetMyResourcesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            // Authenticated-but-identityless is meaningless for "me": refuse rather than return an empty list.
            return Task.FromResult(Response<MyResourcesDto>.Fail("Caller identity could not be resolved.", 401));
        }

        // Tenant is still mandatory: the resource id is only meaningful inside the caller's tenant, and a
        // header/token tenant mismatch has already been refused 400 by TenantResolutionMiddleware.
        if (_tenant.TenantId is null)
        {
            return Task.FromResult(Response<MyResourcesDto>.Fail("Tenant context is required.", 400));
        }

        var item = new MyResourceItemDto(request.UserId.Trim(), PlannedVisitResourceTypes.User, MyResourceStatuses.Active);
        return Task.FromResult(Response<MyResourcesDto>.Success(new MyResourcesDto([item])));
    }
}
