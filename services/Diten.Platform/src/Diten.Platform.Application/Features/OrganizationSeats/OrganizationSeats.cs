using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.OrganizationSeats;

/// <summary>
/// WP-VW-W3L PLT-ORG-SEAT (K-L3, K-L8) — a user's current PRIMARY seat: position → organization unit → legal entity.
/// CRM's layered visit evidence settings resolve a rep's legal entity from it.
///
/// <para><b>The seat rule is consumed, not copied.</b> "Which seat does this person hold right now, and which is home" is
/// <see cref="ITaskSeatDirectory"/>'s question (the active window is MOD-0288's own rule, PRIMARY decides home); this
/// read asks it through <see cref="ITaskSeatDirectory.PrimarySeatsAsync"/> and only maps the answer to its unit and legal
/// entity.</para>
///
/// <para><b>Who may ask.</b> Any authenticated tenant user for THEMSELF; for anybody else the tenant-assignable key
/// <see cref="OrganizationSeatPermissions.Read"/>. The caller's identity is <see cref="ICurrentUserContext"/>; whether
/// the caller holds the key is decided by the API from the claims and handed over as data (the TasksController rule:
/// the claim evaluator lives in that layer). Refused before anything is read.</para>
///
/// <para><b>Tenant.</b> Every repository read is tenant-scoped; the reader also drops any row of another tenant it is
/// handed, so another tenant's user is simply not found (primary = null).</para>
///
/// <para><b>One read round.</b> The bulk form asks the directory once for the whole set, then the positions by id and the
/// units once — never a read per user.</para>
/// </summary>
public static class OrganizationSeatPermissions
{
    /// <summary>Read ANOTHER user's seat. Tenant-assignable (Organization module manifest, API permission).</summary>
    public const string Read = "platform.organization-seats.read";
}

/// <summary>K-L8 — <c>{ positionId, positionCode, organizationUnitId, organizationUnitCode, legalEntityId, effectiveFrom }</c>.</summary>
public sealed record OrganizationPrimarySeatDto(
    Guid PositionId,
    string PositionCode,
    Guid OrganizationUnitId,
    string OrganizationUnitCode,
    Guid LegalEntityId,
    DateTimeOffset EffectiveFrom);

/// <summary>K-L8 — <c>{ userId, primary | null }</c>.</summary>
public sealed record OrganizationSeatDto(Guid UserId, OrganizationPrimarySeatDto? Primary);

/// <summary>K-L8 — <c>{ items: [ … ] }</c>, in the order the user ids were asked (duplicates once).</summary>
public sealed record OrganizationSeatsDto(IReadOnlyList<OrganizationSeatDto> Items);

/// <summary>One user's seat. <paramref name="MayReadOthers"/> = the caller holds <see cref="OrganizationSeatPermissions.Read"/>.</summary>
public sealed record GetOrganizationSeatQuery(Guid UserId, bool MayReadOthers) : IRequest<Response<OrganizationSeatDto>>;

/// <summary>Up to <see cref="OrganizationSeatReader.MaxLookup"/> users' seats in one read round.</summary>
public sealed record LookupOrganizationSeatsQuery(IReadOnlyList<Guid>? UserIds, bool MayReadOthers) : IRequest<Response<OrganizationSeatsDto>>;

public sealed class OrganizationSeatReader :
    IRequestHandler<GetOrganizationSeatQuery, Response<OrganizationSeatDto>>,
    IRequestHandler<LookupOrganizationSeatsQuery, Response<OrganizationSeatsDto>>
{
    public const int MaxLookup = 100;

    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _caller;
    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _units;
    private readonly ILogger<OrganizationSeatReader> _logger;

    public OrganizationSeatReader(ITenantContext tenant, ICurrentUserContext caller, ITaskSeatDirectory seats,
        IPositionRepository positions, IOrganizationUnitRepository units, ILogger<OrganizationSeatReader> logger)
    {
        _tenant = tenant;
        _caller = caller;
        _seats = seats;
        _positions = positions;
        _units = units;
        _logger = logger;
    }

    public async Task<Response<OrganizationSeatDto>> Handle(GetOrganizationSeatQuery request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return Response<OrganizationSeatDto>.Fail("userId is required.", 400);
        if (Refusal([request.UserId], request.MayReadOthers) is { } refusal)
            return Response<OrganizationSeatDto>.Fail(refusal.Message, refusal.Status);

        var seats = await ReadAsync([request.UserId], ct);
        return Response<OrganizationSeatDto>.Success(seats[0]);
    }

    public async Task<Response<OrganizationSeatsDto>> Handle(LookupOrganizationSeatsQuery request, CancellationToken ct)
    {
        var asked = (request.UserIds ?? []).Distinct().ToList();
        if (asked.Count == 0)
            return Response<OrganizationSeatsDto>.Fail("userIds must name at least one user.", 400);
        if (asked.Count > MaxLookup)
            return Response<OrganizationSeatsDto>.Fail($"userIds may name at most {MaxLookup} users.", 400);
        if (asked.Contains(Guid.Empty))
            return Response<OrganizationSeatsDto>.Fail("userIds may not contain an empty id.", 400);
        if (Refusal(asked, request.MayReadOthers) is { } refusal)
            return Response<OrganizationSeatsDto>.Fail(refusal.Message, refusal.Status);

        return Response<OrganizationSeatsDto>.Success(new OrganizationSeatsDto(await ReadAsync(asked, ct)));
    }

    /// <summary>Tenant + identity + key, before anything is read. Null = allowed.</summary>
    private (string Message, int Status)? Refusal(IReadOnlyCollection<Guid> userIds, bool mayReadOthers)
    {
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
            return ("A tenant is required.", 403);
        if (!_caller.IsAuthenticated || _caller.UserId == Guid.Empty)
            return ("An authenticated user is required.", 403);
        if (!mayReadOthers && userIds.Any(userId => userId != _caller.UserId))
            return ($"Reading another user's seat needs {OrganizationSeatPermissions.Read}.", 403);
        return null;
    }

    private async Task<IReadOnlyList<OrganizationSeatDto>> ReadAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var primaries = (await _seats.PrimarySeatsAsync(userIds, ct))
            .Where(pair => pair.Value.Assignment.TenantId == tenantId)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        var positionIds = primaries.Values.Select(seat => seat.Assignment.PositionId).Distinct().ToList();
        var positions = positionIds.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Organization.Position>()
            : (await _positions.GetByIdsAsync(positionIds, ct))
                .Where(position => position.TenantId == tenantId)
                .ToDictionary(position => position.Id);
        var units = positions.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit>()
            : (await _units.GetAllAsync(ct))
                .Where(unit => unit.TenantId == tenantId)
                .ToDictionary(unit => unit.Id);

        return userIds.Select(userId =>
        {
            if (!primaries.TryGetValue(userId, out var seat)) return new OrganizationSeatDto(userId, null);
            if (seat.PrimaryCount > 1)
                _logger.LogWarning(
                    "PLT-ORG-SEAT: user {UserId} holds {Count} active Primary assignments in tenant {TenantId}; the earliest ({AssignmentId}) is used.",
                    userId, seat.PrimaryCount, tenantId, seat.Assignment.Id);
            if (!positions.TryGetValue(seat.Assignment.PositionId, out var position)
                || !units.TryGetValue(position.OrganizationUnitId, out var unit))
            {
                _logger.LogWarning(
                    "PLT-ORG-SEAT: user {UserId}'s Primary assignment {AssignmentId} points at a position or unit that is not readable; no seat.",
                    userId, seat.Assignment.Id);
                return new OrganizationSeatDto(userId, null);
            }

            return new OrganizationSeatDto(userId, new OrganizationPrimarySeatDto(
                position.Id, position.Code, unit.Id, unit.Code, unit.LegalEntityId, seat.Assignment.EffectiveFrom));
        }).ToList();
    }
}
