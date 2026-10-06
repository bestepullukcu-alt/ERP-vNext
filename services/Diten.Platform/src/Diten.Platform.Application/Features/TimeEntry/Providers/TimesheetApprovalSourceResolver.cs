using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Providers;

/// <summary>
/// MOD-0280-FU01 (pack §3.3, BL-437) — tells the Task Center's approval card what a timesheet approval is about:
/// "Timesheet · 2026-W40 · {person}", with a marker when the week has a day above the 660-minute threshold (A3), who
/// sent it and where the read-only week lives. Read only; it never touches the approval or a permission.
/// </summary>
public sealed class TimesheetApprovalSourceResolver : IApprovalSourceResolver
{
    /// <summary>Appended to the title when the week carries flagged days — a plausibility marker, not a score.</summary>
    public const string FlaggedMarker = " · ⚑";

    private readonly ITimesheetWeekRepository _weeks;
    private readonly IUserDisplayNameResolver _displayNames;

    public TimesheetApprovalSourceResolver(ITimesheetWeekRepository weeks, IUserDisplayNameResolver displayNames)
    {
        _weeks = weeks;
        _displayNames = displayNames;
    }

    public bool Handles(string objectType)
        => string.Equals(objectType, TimeEntryModule.ApprovalObjectType, StringComparison.Ordinal);

    public async Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveAsync(
        IReadOnlyCollection<WorkflowInstance> instances,
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, ApprovalSourceContext>();
        var owned = instances
            .Where(i => Handles(i.ObjectType))
            .Select(i => (Instance: i, WeekId: Guid.TryParse(i.ObjectId, out var id) ? id : Guid.Empty))
            .Where(x => x.WeekId != Guid.Empty)
            .ToList();
        if (owned.Count == 0)
        {
            return result;
        }

        var weeks = (await _weeks.ListByIdsAsync(owned.Select(x => x.WeekId).Distinct().ToList(), ct)).ToDictionary(w => w.Id);
        var names = await _displayNames.ResolveAsync(weeks.Values.Select(w => w.UserId).Distinct().ToList(), ct);

        foreach (var (instance, weekId) in owned)
        {
            // Only the approval the week is actually waiting on (F1): a stray, withdrawn or superseded instance of the same
            // week gets no card context, so nothing presents it as "this week, awaiting you".
            if (!weeks.TryGetValue(weekId, out var week)
                || week.Status != TimesheetWeekStatus.Submitted
                || week.WorkflowInstanceId != instance.Id)
            {
                continue;
            }

            var name = names.TryGetValue(week.UserId, out var resolved) && !string.IsNullOrWhiteSpace(resolved) ? resolved : null;
            var title = $"Timesheet · {week.WeekKey}" + (name is null ? string.Empty : $" · {name}")
                        + (week.FlaggedDates.Count > 0 ? FlaggedMarker : string.Empty);

            result[instance.Id] = new ApprovalSourceContext(
                Title: title,
                Requester: new WorkItemPersonDto(week.UserId.ToString(), name, week.UserId == actor.UserId),
                DeepLink: $"/TimeEntry/Approvals/{week.Id}");
        }

        return result;
    }
}
