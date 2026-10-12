using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>Who may approve a week, decided at submit and stored on it (D6).</summary>
public sealed record TimesheetApprovers(
    IReadOnlyList<Guid> CandidateUserIds,
    TimesheetApproverResolution Resolution,
    Guid? LegalEntityId);

public interface IApproverResolver
{
    /// <summary>The candidates for <paramref name="userId"/>'s week, or null when nobody can approve it — the caller
    /// refuses the submit (<c>TIMESHEET_NO_APPROVER</c>); there is no automatic approval.</summary>
    Task<TimesheetApprovers?> ResolveAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D6 — the line manager through the primary seat.
///
/// <list type="number">
/// <item>The holders of the primary seat's <c>ReportsToPositionId</c>.</item>
/// <item>Nobody there (a vacant seat, or only the person themselves in a self-reporting seat) → the next seat up,
/// and so on to the top.</item>
/// <item>No chain at all (no primary seat, or vacant to the top) → the tenant's time-admin pool.</item>
/// <item>Pool empty or not configured → null. Never the person, never automatic.</item>
/// </list>
///
/// <para>The person is removed from EVERY candidate list, including the pool's: a time admin submitting their own
/// week is approved by another time admin, never by themselves.</para>
/// </summary>
public sealed class ApproverResolver : IApproverResolver
{
    /// <summary>The same depth guard MOD-0288's own manager-chain query uses.</summary>
    private const int MaxChainDepth = 32;

    private readonly ITimeEntryOrgGateway _org;
    private readonly ITimeEntrySettingsRepository _settings;

    public ApproverResolver(ITimeEntryOrgGateway org, ITimeEntrySettingsRepository settings)
    {
        _org = org;
        _settings = settings;
    }

    public async Task<TimesheetApprovers?> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        var seat = await _org.PrimarySeatAsync(userId, ct);

        if (seat is not null)
        {
            var origin = await _org.PositionAsync(seat.PositionId, ct);
            var visited = new HashSet<Guid> { seat.PositionId };
            var cursor = origin?.ReportsToPositionId;

            for (var depth = 1; cursor is { } positionId && depth <= MaxChainDepth; depth++)
            {
                if (!visited.Add(positionId))
                {
                    break; // a cycle in the org chart: stop walking and fall back to the pool
                }

                var holders = (await _org.HoldersOfAsync(positionId, ct)).Where(id => id != userId).Distinct().ToList();
                if (holders.Count > 0)
                {
                    return new TimesheetApprovers(
                        holders,
                        depth == 1 ? TimesheetApproverResolution.LineManager : TimesheetApproverResolution.ManagerChain,
                        seat.LegalEntityId);
                }

                var position = await _org.PositionAsync(positionId, ct);
                cursor = position?.ReportsToPositionId;
            }
        }

        var poolPositionId = (await _settings.GetAsync(ct))?.TimeAdminPoolPositionId;
        if (poolPositionId is { } pool)
        {
            var poolHolders = (await _org.HoldersOfAsync(pool, ct)).Where(id => id != userId).Distinct().ToList();
            if (poolHolders.Count > 0)
            {
                return new TimesheetApprovers(poolHolders, TimesheetApproverResolution.TimeAdminPool, seat?.LegalEntityId);
            }
        }

        return null;
    }
}
