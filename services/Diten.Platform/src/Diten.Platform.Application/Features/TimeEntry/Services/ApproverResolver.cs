using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
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
/// <para>Steps 1–2 are the platform's shared <see cref="ILineManagerResolver"/> (WP-VW-W5 PLT-NOTIFY moved the walk
/// there, unchanged, so a visit notification finds the same manager a timesheet does). Step 3, the pool, is the
/// timesheet's own and stays here: a notification has no such fallback.</para>
///
/// <para>The person is removed from EVERY candidate list, including the pool's: a time admin submitting their own
/// week is approved by another time admin, never by themselves.</para>
/// </summary>
public sealed class ApproverResolver : IApproverResolver
{
    private readonly ITimeEntryOrgGateway _org;
    private readonly ILineManagerResolver _lineManagers;
    private readonly ITenantContext _tenant;
    private readonly ITimeEntrySettingsRepository _settings;

    public ApproverResolver(
        ITimeEntryOrgGateway org,
        ILineManagerResolver lineManagers,
        ITenantContext tenant,
        ITimeEntrySettingsRepository settings)
    {
        _org = org;
        _lineManagers = lineManagers;
        _tenant = tenant;
        _settings = settings;
    }

    public async Task<TimesheetApprovers?> ResolveAsync(Guid userId, CancellationToken ct = default)
    {
        // The seat is still read here for its legal entity, which the week stores whichever step found the approver.
        var seat = await _org.PrimarySeatAsync(userId, ct);

        if (seat is not null)
        {
            var managers = await _lineManagers.ResolveAsync(_tenant.IsResolved ? _tenant.TenantId : Guid.Empty, userId, ct);
            if (managers.UserIds.Count > 0)
            {
                return new TimesheetApprovers(
                    managers.UserIds,
                    managers.Resolution == LineManagerResolution.LineManager
                        ? TimesheetApproverResolution.LineManager
                        : TimesheetApproverResolution.ManagerChain,
                    seat.LegalEntityId);
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
