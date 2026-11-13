using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.OrganizationSeats;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY — the <see cref="ILineManagerResolver"/> over MOD-0288's in-process seams. "Who sits where" is
/// asked through <see cref="ITaskSeatDirectory"/>, the platform's one place for the active-assignment rule, with the
/// same seat the timesheet approver always used: the first of the person's active seats, primary first.
///
/// <para>The walk is the one the timesheet approver had (MOD-0280-FU01 D6), moved here unchanged: at most
/// <see cref="MaxChainDepth"/> seats up, a seat already visited ends it (a cycle), and the person is never their own
/// manager — a self-reporting seat counts as vacant.</para>
/// </summary>
public sealed class LineManagerResolver : ILineManagerResolver
{
    /// <summary>The same depth guard MOD-0288's own manager-chain query uses.</summary>
    public const int MaxChainDepth = 32;

    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LineManagerResolver> _logger;

    public LineManagerResolver(
        ITaskSeatDirectory seats,
        IPositionRepository positions,
        ITenantContext tenant,
        ILogger<LineManagerResolver> logger)
    {
        _seats = seats;
        _positions = positions;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<LineManagers> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || !_tenant.IsResolved || _tenant.TenantId != tenantId)
        {
            // The reads below are scoped by the AMBIENT tenant: answering for another one would be answering wrongly.
            _logger.LogWarning(
                "line_manager.tenant_mismatch Asked={TenantId} Resolved={Resolved}; nobody is returned.",
                tenantId, _tenant.IsResolved);
            return LineManagers.Nobody;
        }

        var seats = await _seats.ActiveForUserAsync(userId, ct);
        if (seats.Count == 0)
        {
            return LineManagers.Nobody;
        }

        var origin = await _positions.GetByIdAsync(seats[0].PositionId, ct);
        if (origin is null)
        {
            return LineManagers.Nobody;
        }

        var visited = new HashSet<Guid> { origin.Id };
        var cursor = origin.ReportsToPositionId;

        for (var depth = 1; cursor is { } positionId && depth <= MaxChainDepth; depth++)
        {
            if (!visited.Add(positionId))
            {
                break; // a cycle in the org chart: stop walking
            }

            var holders = (await _seats.HoldersOfAsync(new HashSet<Guid> { positionId }, ct))
                .Where(id => id != userId)
                .Distinct()
                .ToList();
            if (holders.Count > 0)
            {
                return new LineManagers(
                    holders,
                    depth == 1 ? LineManagerResolution.LineManager : LineManagerResolution.ManagerChain,
                    positionId);
            }

            var position = await _positions.GetByIdAsync(positionId, ct);
            cursor = position?.ReportsToPositionId;
        }

        return LineManagers.Nobody;
    }
}
