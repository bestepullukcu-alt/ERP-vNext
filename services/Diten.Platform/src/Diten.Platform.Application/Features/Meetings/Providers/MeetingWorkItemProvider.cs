using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Meetings.Providers;

/// <summary>
/// MOD-0357 S5c (pack §3 Providers "4. IWorkItemProvider", K5) — projects the actor's own PENDING meeting
/// invitations into the Task Center inbox.
///
/// <para><b>K5 — accepted/declined never becomes an İşlerim item (BL-026).</b> The source query
/// (<see cref="IMeetingAttendeeRepository.ListPendingByUserIdAsync"/>) reads <c>InvitationResponse == Pending</c>
/// only — once an attendee responds, their row simply stops matching this query and the row is gone from every
/// board this provider feeds, on the very next read. There is no second "hide it" step to forget.</para>
///
/// <para><b>READ-ONLY</b>, like every other <see cref="IWorkItemProvider"/>: Accept/Decline is
/// <see cref="MeetingWorkItemActionDispatcher"/>'s job, on the sibling write interface.</para>
/// </summary>
public sealed class MeetingWorkItemProvider : IWorkItemProvider
{
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IWorkItemSlaCalculator _sla;
    private readonly IRecordLinkService? _recordLinks;
    private readonly IRelatedRecordResolverRegistry? _relatedRecordResolvers;

    public MeetingWorkItemProvider(
        IMeetingAttendeeRepository attendees,
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IUserDisplayNameResolver displayNames,
        IWorkItemSlaCalculator sla,
        IRecordLinkService? recordLinks = null,
        IRelatedRecordResolverRegistry? relatedRecordResolvers = null)
    {
        _attendees = attendees;
        _meetings = meetings;
        _types = types;
        _displayNames = displayNames;
        _sla = sla;
        _recordLinks = recordLinks;
        _relatedRecordResolvers = relatedRecordResolvers;
    }

    public string ProviderCode => WorkItemContract.ProviderCodeMeetings;

    public string ProviderContractVersion => "1.0";

    public IReadOnlyCollection<string> RequiredActionPermissions { get; } = [MeetingPermissions.Read];

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor, CancellationToken ct = default)
    {
        var pending = await _attendees.ListPendingByUserIdAsync(actor.UserId, ct);
        if (pending.Count == 0)
        {
            return [];
        }

        var meetingIds = pending.Select(a => a.MeetingId).Distinct().ToList();
        var meetingsById = (await _meetings.ListByIdsAsync(meetingIds, ct)).ToDictionary(m => m.Id);
        if (meetingsById.Count == 0)
        {
            return [];
        }

        // "toplantı Scheduled, StartAt gelecekte ya da bugün" (pack) — a cancelled meeting or one already past
        // has nothing left for this actor to decide; K5's own respond handler independently refuses those with
        // a 409, so this filter is a courtesy that keeps a dead invite off the board, not the enforcement.
        var today = DateTimeOffset.UtcNow.Date;
        var candidates = pending
            .Where(a => meetingsById.TryGetValue(a.MeetingId, out var m)
                        && m.Lifecycle == MeetingLifecycle.Scheduled
                        && m.StartAt.UtcDateTime.Date >= today)
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        // The type catalogue is tenant-wide and small (pack precedent: MeetingEligibility's own "small scale"
        // posture) — one full read beats one GetByIdAsync per distinct type on every page.
        var typeNameById = (await _types.ListAsync(ct)).ToDictionary(t => t.Id, t => t.Name);

        var organizerIds = candidates.Select(a => meetingsById[a.MeetingId].OrganizerUserId).Distinct().ToList();
        var organizerNames = await _displayNames.ResolveAsync(organizerIds, ct);

        var relatedRecordsByMeeting = await ResolveRelatedRecordsAsync(
            candidates.Select(a => a.MeetingId).Distinct().ToList(), ct);

        var now = DateTimeOffset.UtcNow;
        var items = new List<WorkItemProjectionDto>(candidates.Count);
        foreach (var attendee in candidates)
        {
            var meeting = meetingsById[attendee.MeetingId];
            var typeName = typeNameById.GetValueOrDefault(meeting.MeetingTypeId, string.Empty);
            var organizerName = organizerNames.GetValueOrDefault(meeting.OrganizerUserId);

            items.Add(Project(attendee, meeting, typeName, organizerName, actor, now,
                relatedRecordsByMeeting.GetValueOrDefault(meeting.Id)));
        }

        return items;
    }

    private WorkItemProjectionDto Project(
        MeetingAttendee attendee,
        Meeting meeting,
        string meetingTypeName,
        string? organizerName,
        WorkItemActor actor,
        DateTimeOffset now,
        IReadOnlyList<WorkItemRelatedRecordDto>? relatedRecords)
    {
        var actions = new[]
        {
            Build("acceptInvite", "WorkAggregation_Action_AcceptInvite", actor),
            Build("declineInvite", "WorkAggregation_Action_DeclineInvite", actor)
        };

        return new WorkItemProjectionDto(
            FixtureKind: WorkItemContract.FixtureKindWorkItem,
            Id: meeting.Id.ToString(),
            WorkIntent: WorkItemContract.IntentMeetingInvite,
            AssignmentMode: "direct",
            OwnershipState: WorkItemContract.NotApplicable,
            AdmissionState: WorkItemContract.NotApplicable,
            NormalizedStatus: WorkItemContract.StatusPending,
            TaskLifecycle: WorkItemContract.NotApplicable,
            ExecutionState: WorkItemContract.NotApplicable,
            TimerState: WorkItemContract.NotApplicable,
            SystemState: WorkItemContract.SystemFresh,
            ActionDepth: WorkItemContract.DepthInline,
            Title: WorkItemLabelDto.Display(meeting.Title),
            NativeStatus: new WorkItemNativeStatusDto(
                Code: "Pending", Label: WorkItemLabelDto.Resource("WorkAggregation_NativeStatus_Pending")),
            Source: new WorkItemSourceDto(
                ProviderCode: ProviderCode,
                ProviderContractVersion: ProviderContractVersion,
                // "tür" (pack) — the MEETING's own type, not this provider's item type: the generic
                // meetingInvite chip/icon already says "this is an invitation", so the source's own object
                // type is free to say WHICH KIND of meeting it is instead of repeating that.
                ObjectType: meetingTypeName,
                ObjectId: meeting.Id.ToString(),
                DeepLink: $"/Meetings/{meeting.Id}"),
            LifecycleOwner: ProviderCode,
            WorkItemCapabilities: relatedRecords is null ? [] : ["relatedRecords"],
            Actions: actions,
            Concurrency: new WorkItemConcurrencyDto("version", attendee.Version.ToString()),
            WaitingContext: null,
            Escalation: null,
            DueAt: meeting.StartAt,
            PrimaryActionCode: "acceptInvite",
            SecondaryActionCodes: ["declineInvite"],
            // "isHolder=true" (pack) — the invitee IS the one this decision belongs to.
            Assignee: new WorkItemPersonDto(actor.UserId.ToString(), IsCurrentUser: true),
            // "isRequester=false" — the ORGANIZER asked for this, never the invitee themselves: an organizer's
            // own row is always written Accepted at creation (S5), so it can never reach this Pending query.
            Requester: new WorkItemPersonDto(meeting.OrganizerUserId.ToString(), organizerName, IsCurrentUser: false),
            RelatedRecords: relatedRecords,
            SlaState: _sla.Resolve(meeting.StartAt, now));
    }

    private static WorkItemActionDto Build(string code, string labelKey, WorkItemActor actor)
    {
        var permitted = actor.Has(MeetingPermissions.Read);
        return new WorkItemActionDto(
            Code: code,
            Label: WorkItemLabelDto.Resource(labelKey),
            SemanticType: code == "acceptInvite" ? "approve" : "reject",
            Enabled: permitted,
            Source: WorkItemContract.ActionSourceProvider,
            DisabledReasonCode: permitted ? null : WorkAggregationReasonCodes.PermissionDenied,
            DisabledReason: permitted ? null : WorkItemLabelDto.Resource("WorkAggregation_ActionDisabled_PermissionDenied"),
            RequiresConfirmation: false,
            RequiresReason: false,
            RequiresEvidence: false,
            SupportsBulk: false,
            RiskLevel: "normal");
    }

    /// <summary>
    /// `relatedRecords` — SOURCE side only: a meeting invite links to the tasks the meeting has already
    /// produced (S4's own `RecordLinkTypes.Preparation`/`BornFromMeeting`), never the reverse. One
    /// <c>RecordLink</c> read for the whole page, one resolver call per distinct far module code — the same
    /// shape <c>TaskWorkItemProvider.ResolveRelatedRecordsAsync</c> already established, simplified to the one
    /// direction a meeting ever needs.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<WorkItemRelatedRecordDto>>> ResolveRelatedRecordsAsync(
        IReadOnlyList<Guid> meetingIds, CancellationToken ct)
    {
        if (_recordLinks is null || _relatedRecordResolvers is null || meetingIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<WorkItemRelatedRecordDto>>();
        }

        var links = await _recordLinks.ListBySourceAsync(meetingIds, ct);
        if (links.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<WorkItemRelatedRecordDto>>();
        }

        var byModule = new Dictionary<string, IReadOnlyDictionary<Guid, RelatedRecordSummary>>(StringComparer.Ordinal);
        foreach (var moduleCode in links.Select(l => l.TargetModuleCode).Distinct(StringComparer.Ordinal))
        {
            if (!_relatedRecordResolvers.TryGet(moduleCode, out var resolver))
            {
                continue; // pack §, "çözücüsü olmayan modül kodu... uydurma başlık yok" — dropped, not invented.
            }

            var ids = links.Where(l => l.TargetModuleCode == moduleCode).Select(l => l.TargetRecordId).Distinct().ToList();
            byModule[moduleCode] = await resolver.ResolveAsync(ids, ct);
        }

        var result = new Dictionary<Guid, IReadOnlyList<WorkItemRelatedRecordDto>>();
        foreach (var group in links.GroupBy(l => l.SourceRecordId))
        {
            var rows = new List<WorkItemRelatedRecordDto>();
            foreach (var link in group)
            {
                if (rows.Count >= WorkItemContract.MaxRelatedRecords) { break; }
                if (!byModule.TryGetValue(link.TargetModuleCode, out var summaries)
                    || !summaries.TryGetValue(link.TargetRecordId, out var summary))
                {
                    continue;
                }

                rows.Add(new WorkItemRelatedRecordDto(
                    link.TargetRecordId.ToString(), link.TargetModuleCode, summary.Title, summary.Link));
            }

            if (rows.Count > 0)
            {
                result[group.Key] = rows;
            }
        }

        return result;
    }
}
