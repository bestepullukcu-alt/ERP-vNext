using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.OrganizationSeats;

/// <summary>One person a user reports to: who (and their name, when AuthService answers), in which seat. No e-mail.</summary>
public sealed record OrganizationLineManagerDto(Guid UserId, string? DisplayName, Guid PositionId, string? PositionCode);

/// <summary>
/// WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-2) — <c>{ userId, resolution, positionId?, positionCode?, positionName?, managers }</c>.
/// <paramref name="Resolution"/> is <c>LineManager</c> | <c>ManagerChain</c> | <c>None</c> (<see cref="LineManagerResolution"/>);
/// the position is the seat the managers hold (the seat the user reports to, or the one further up the chain) — null
/// with <c>None</c>.
/// </summary>
public sealed record OrganizationLineManagersDto(
    Guid UserId,
    string Resolution,
    Guid? PositionId,
    string? PositionCode,
    string? PositionName,
    IReadOnlyList<OrganizationLineManagerDto> Managers);

/// <summary>The line managers of one user; <paramref name="MayReadOthers"/> = the caller holds
/// <see cref="OrganizationSeatPermissions.Read"/> (the seat read's rule, K-L8).</summary>
public sealed record GetOrganizationLineManagersQuery(Guid UserId, bool MayReadOthers) : IRequest<Response<OrganizationLineManagersDto>>;

/// <summary>
/// WP-VW-W3L K-L8 — WHO may read a user's organization data, in one place for every seat endpoint: a tenant must be
/// resolved, the caller authenticated, and anybody but the caller themself needs <see cref="OrganizationSeatPermissions.Read"/>.
/// Asked BEFORE anything is read. Null = allowed.
/// </summary>
public static class OrganizationSeatAuthority
{
    public static (string Message, int Status)? Refusal(
        ITenantContext tenant, ICurrentUserContext caller, IReadOnlyCollection<Guid> userIds, bool mayReadOthers)
    {
        if (!tenant.IsResolved || tenant.TenantId == Guid.Empty)
            return ("A tenant is required.", 403);
        if (!caller.IsAuthenticated || caller.UserId == Guid.Empty)
            return ("An authenticated user is required.", 403);
        if (!mayReadOthers && userIds.Any(userId => userId != caller.UserId))
            return ($"Reading another user's seat needs {OrganizationSeatPermissions.Read}.", 403);
        return null;
    }
}

/// <summary>
/// WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-2) — "who does this user report to": the W6 approver's name and the Work Center's
/// candidates, read by CRM. The rule is <see cref="ILineManagerResolver"/>'s (PLT-NOTIFY: primary seat →
/// <c>ReportsToPositionId</c> → its holders, the person excluded, a vacant seat moves up) — consumed, not copied. The
/// authority rule is the seat read's (<see cref="OrganizationSeatAuthority"/>), refused before any read. Names come in ONE
/// batch from <see cref="IUserDisplayNameResolver"/>, best effort (AuthService down → names absent, never a failure); no
/// e-mail address is ever read or returned.
/// <para><b>Tenant.</b> The resolver answers nobody for any tenant but the ambient one; the seat itself is read with the
/// tenant filter and dropped when it is another tenant's.</para>
/// </summary>
public sealed class OrganizationLineManagerReader : IRequestHandler<GetOrganizationLineManagersQuery, Response<OrganizationLineManagersDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _caller;
    private readonly ILineManagerResolver _lineManagers;
    private readonly IPositionRepository _positions;
    private readonly IUserDisplayNameResolver? _names;
    private readonly ILogger<OrganizationLineManagerReader> _logger;

    public OrganizationLineManagerReader(ITenantContext tenant, ICurrentUserContext caller, ILineManagerResolver lineManagers,
        IPositionRepository positions, ILogger<OrganizationLineManagerReader> logger, IUserDisplayNameResolver? names = null)
    {
        _tenant = tenant;
        _caller = caller;
        _lineManagers = lineManagers;
        _positions = positions;
        _names = names;
        _logger = logger;
    }

    public async Task<Response<OrganizationLineManagersDto>> Handle(GetOrganizationLineManagersQuery request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return Response<OrganizationLineManagersDto>.Fail("userId is required.", 400);
        if (OrganizationSeatAuthority.Refusal(_tenant, _caller, [request.UserId], request.MayReadOthers) is { } refusal)
            return Response<OrganizationLineManagersDto>.Fail(refusal.Message, refusal.Status);

        var tenantId = _tenant.TenantId;
        var found = await _lineManagers.ResolveAsync(tenantId, request.UserId, ct);
        var position = found.PositionId is { } positionId
            ? (await _positions.GetByIdsAsync([positionId], ct)).FirstOrDefault(p => p.Id == positionId && p.TenantId == tenantId)
            : null;
        var managerIds = found.UserIds.Where(id => id != request.UserId).Distinct().ToList();
        var names = await NamesAsync(managerIds, ct);

        var resolution = managerIds.Count == 0 ? LineManagerResolution.None : found.Resolution;
        var managers = resolution == LineManagerResolution.None || found.PositionId is not { } seat
            ? []
            : managerIds.Select(id => new OrganizationLineManagerDto(id, names.GetValueOrDefault(id), seat, position?.Code)).ToList();
        return Response<OrganizationLineManagersDto>.Success(new OrganizationLineManagersDto(
            request.UserId,
            resolution.ToString(),
            managers.Count == 0 ? null : found.PositionId,
            managers.Count == 0 ? null : position?.Code,
            managers.Count == 0 ? null : position?.Name,
            managers));
    }

    /// <summary>One batch, never a call per manager; a failure is "no names", logged, never a failed read.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (_names is null || userIds.Count == 0)
            return new Dictionary<Guid, string>();
        try
        {
            return await _names.ResolveAsync(userIds, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "PLT-W6: line manager names could not be resolved; the ids are returned without names.");
            return new Dictionary<Guid, string>();
        }
    }
}
