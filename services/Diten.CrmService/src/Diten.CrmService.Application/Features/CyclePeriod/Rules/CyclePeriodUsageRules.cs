using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CyclePeriod.Rules;

/// <summary>
/// WP-CYC-UI-1 — what the usage rows MEAN, as pure functions over <see cref="CyclePeriodUsageSnapshot"/>.
/// <list type="bullet">
/// <item><description>A period's planned visits are the union (deduplicated) of its sessions' committed visit ids that
/// the reader found. <c>plannedVisits.byStatus</c> counts every one of them by status.</description></item>
/// <item><description><b>Demand</b> (<c>demandByMonth</c>, the list's <c>plannedVisitCount</c>) excludes
/// <c>cancelled</c> and <c>archived</c> visits: a cancelled visit asks nothing of the field force.</description></item>
/// <item><description>The period's capacity is its live (non-archived) one; with none live, the archived one is shown
/// so the screen can say "archived" instead of "no capacity".</description></item>
/// </list>
/// </summary>
public static class CyclePeriodUsageRules
{
    /// <summary>Visit statuses that place no demand on the field force.</summary>
    public static bool CountsAsDemand(string? status)
    {
        var normalized = PlannedVisitStatus.Normalize(status);
        return !string.Equals(normalized, PlannedVisitStatus.Cancelled, StringComparison.Ordinal)
               && !string.Equals(normalized, PlannedVisitStatus.Archived, StringComparison.Ordinal);
    }

    public static CyclePeriodUsageDto Build(
        Guid cyclePeriodId,
        CyclePeriodUsageSnapshot snapshot,
        IReadOnlyDictionary<Guid, string> ownerNames)
    {
        var capacity = CapacityOf(cyclePeriodId, snapshot);

        var campaigns = snapshot.Campaigns
            .Where(c => c.CyclePeriodId == cyclePeriodId)
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CyclePeriodUsageCampaignDto(c.CampaignId, c.Code, c.Name, c.Status))
            .ToList();

        var sessions = SessionsOf(cyclePeriodId, snapshot);
        var visits = VisitsOf(sessions, snapshot);

        var sessionDtos = sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new CyclePeriodUsageSessionDto(
                s.PlanningSessionId,
                SessionName(s),
                Guid.TryParse(s.ResourceId, out var ownerId) && ownerNames.TryGetValue(ownerId, out var name) ? name : null,
                s.Status,
                visits.Count(v => s.CommittedPlannedVisitIds.Contains(v.PlannedVisitId))))
            .ToList();

        var byStatus = visits
            .GroupBy(v => PlannedVisitStatus.Normalize(v.Status), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var demand = visits
            .Where(v => CountsAsDemand(v.Status))
            .GroupBy(v => (v.PlannedDate.Year, v.PlannedDate.Month))
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new CyclePeriodUsageDemandMonthDto(g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        return new CyclePeriodUsageDto(
            capacity is null ? null : new CyclePeriodUsageCapacityDto(capacity.CycleCapacityId, capacity.IsArchived),
            campaigns,
            sessionDtos,
            new CyclePeriodUsageVisitsDto(visits.Count, byStatus),
            demand);
    }

    /// <summary>The list's per-row summary — one pass over one snapshot for every row.</summary>
    public static CyclePeriodUsageSummary Summarize(Guid cyclePeriodId, CyclePeriodUsageSnapshot snapshot)
    {
        var sessions = SessionsOf(cyclePeriodId, snapshot);
        return new CyclePeriodUsageSummary(
            snapshot.Capacities.Any(c => c.CyclePeriodId == cyclePeriodId && !c.IsArchived),
            snapshot.Campaigns.Count(c => c.CyclePeriodId == cyclePeriodId),
            VisitsOf(sessions, snapshot).Count(v => CountsAsDemand(v.Status)));
    }

    /// <summary>A session has no name of its own; it is labelled by its plan week, else its creation day (ISO, so the
    /// screen formats it in the reader's locale). Never a person's name.</summary>
    public static string SessionName(CyclePeriodUsageSessionRow session)
        => string.IsNullOrWhiteSpace(session.TargetWeekStart)
            ? session.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : session.TargetWeekStart.Trim();

    private static CyclePeriodUsageCapacityRow? CapacityOf(Guid cyclePeriodId, CyclePeriodUsageSnapshot snapshot)
    {
        var mine = snapshot.Capacities.Where(c => c.CyclePeriodId == cyclePeriodId).ToList();
        return mine.FirstOrDefault(c => !c.IsArchived) ?? mine.FirstOrDefault();
    }

    private static IReadOnlyList<CyclePeriodUsageSessionRow> SessionsOf(Guid cyclePeriodId, CyclePeriodUsageSnapshot snapshot)
        => snapshot.Sessions.Where(s => s.CyclePeriodId == cyclePeriodId).ToList();

    private static IReadOnlyList<CyclePeriodUsageVisitRow> VisitsOf(
        IReadOnlyList<CyclePeriodUsageSessionRow> sessions, CyclePeriodUsageSnapshot snapshot)
    {
        var ids = sessions.SelectMany(s => s.CommittedPlannedVisitIds).ToHashSet();
        return snapshot.Visits
            .Where(v => ids.Contains(v.PlannedVisitId))
            .GroupBy(v => v.PlannedVisitId)
            .Select(g => g.First())
            .ToList();
    }
}

/// <summary>The list row's usage summary.</summary>
public sealed record CyclePeriodUsageSummary(bool HasCapacity, int CampaignCount, int PlannedVisitCount);
