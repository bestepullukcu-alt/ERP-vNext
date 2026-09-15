using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S2 — in-memory doubles for the four new repositories, the SAME shape every other Fake*Repository in
// this suite takes (tenant-filtered, soft-delete-aware), plus a tiny FakeMediator that answers ONLY
// GetTaskAssignmentPersonLookupQuery with a test-controlled eligible list — the attendee/organizer eligibility
// SEAM this slice reuses (NE #3), never a second resolution path. The real GetTaskAssignmentPersonLookupHandler
// is proven separately by its own MOD-0024 test suite; doubling it here keeps these handler tests isolated to
// MOD-0357's own logic.

internal sealed class FakeMeetingRepository : IMeetingRepository
{
    private readonly List<Meeting> _items = [];
    public Guid Tenant { get; init; }

    public void Seed(Meeting meeting) => _items.Add(meeting);

    public Task<Meeting> CreateAsync(Meeting meeting, CancellationToken ct = default)
    {
        _items.Add(meeting);
        return Task.FromResult(meeting);
    }

    public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == Tenant && !x.IsDeleted));

    public Task<Meeting?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == Tenant && !x.IsDeleted && x.IdempotencyKey == idempotencyKey));

    public Task<Meeting> FindOrCreateAsync(Meeting candidate, CancellationToken ct = default)
    {
        var existing = _items.FirstOrDefault(
            x => x.TenantId == candidate.TenantId && !x.IsDeleted && x.IdempotencyKey == candidate.IdempotencyKey);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        _items.Add(candidate);
        return Task.FromResult(candidate);
    }

    public Task<bool> UpdateAsync(Meeting meeting, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == meeting.Id && x.TenantId == Tenant && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        _items.Remove(stored);
        meeting.Version = expectedVersion + 1;
        _items.Add(meeting);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Meeting>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Meeting>>(_items.Where(x => x.TenantId == Tenant && !x.IsDeleted).ToList());

    public Task<IReadOnlyList<Meeting>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Meeting>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted && ids.Contains(x.Id)).ToList());

    public Task<bool> AnyByMeetingTypeIdAsync(Guid meetingTypeId, CancellationToken ct = default)
        => Task.FromResult(_items.Any(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingTypeId == meetingTypeId));

    public Task<Meeting?> FindByFollowUpOfMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult(_items
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && x.FollowUpOfMeetingId == meetingId)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefault());
}

internal sealed class FakeMeetingAttendeeRepository : IMeetingAttendeeRepository
{
    private readonly List<MeetingAttendee> _items = [];
    public Guid Tenant { get; init; }

    public IReadOnlyList<MeetingAttendee> Items => _items;

    public void Seed(MeetingAttendee attendee) => _items.Add(attendee);

    public Task<MeetingAttendee> CreateAsync(MeetingAttendee attendee, CancellationToken ct = default)
    {
        _items.Add(attendee);
        return Task.FromResult(attendee);
    }

    public Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingAttendee>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId).ToList());

    public Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdsAsync(IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingAttendee>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted && meetingIds.Contains(x.MeetingId)).ToList());

    public Task<MeetingAttendee?> FindAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(
            x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId && x.UserId == userId));

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null) { item.IsDeleted = true; }
        return Task.CompletedTask;
    }

    public Task UpdateInvitationResponseAsync(Guid id, InvitationResponse response, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null) { item.InvitationResponse = response; }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MeetingAttendee>> ListPendingByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingAttendee>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted
                              && x.UserId == userId && x.InvitationResponse == InvitationResponse.Pending).ToList());

    public Task UpdateAttendanceStatusAsync(Guid meetingId, Guid userId, AttendanceStatus status, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId && x.UserId == userId);
        if (item is not null) { item.AttendanceStatus = status; }
        return Task.CompletedTask;
    }

    public Task<bool> MarkMailUndeliveredAsync(Guid meetingId, Guid userId, DateTimeOffset failedAt, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId && x.UserId == userId);
        if (item is null) { return Task.FromResult(false); }
        item.MailUndeliveredAt = failedAt;
        return Task.FromResult(true);
    }
}

/// <summary>MOD-0357 S6 — in-memory double for <see cref="IMeetingMinutesVersionRepository"/>. No unique-index
/// enforcement here (that guarantee is proven against a REAL Mongo, in <c>MeetingMinutesVersionMongoTests</c>) —
/// this fake exists to test the COMMAND HANDLERS' own logic in isolation, the same division of labour every
/// other Fake*Repository in this suite already draws.</summary>
internal sealed class FakeMeetingMinutesVersionRepository : IMeetingMinutesVersionRepository
{
    private readonly List<MeetingMinutesVersion> _items = [];
    public Guid Tenant { get; init; }

    public void Seed(MeetingMinutesVersion version) => _items.Add(version);

    /// <summary>What THIS repository currently has stored for <paramref name="id"/>, before any in-flight
    /// caller mutation — the "before" half of a source-guard test that watches <c>UpdateAsync</c> calls.</summary>
    public MinutesStatus? StoredStatusOf(Guid id) => _items.FirstOrDefault(x => x.Id == id)?.Status;

    /// <summary>
    /// A real Mongo read deserializes a NEW object every call; a fake that hands back the SAME reference lets a
    /// handler's in-place field mutation retroactively corrupt what this repository "has stored", which is
    /// exactly backwards for a test that watches for an update against an ALREADY-published row (measured: the
    /// naive version of this fake made <c>PublishMinutesHandler</c>'s own `latest.Status = Published;` — set
    /// BEFORE its own `UpdateAsync` call — appear to have already been stored as Published, because `latest`
    /// WAS the stored object). Every read below returns one of these instead.
    /// </summary>
    private static MeetingMinutesVersion Clone(MeetingMinutesVersion source) => new()
    {
        Id = source.Id, TenantId = source.TenantId, CreatedBy = source.CreatedBy, CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt, UpdatedBy = source.UpdatedBy, IsDeleted = source.IsDeleted, Version = source.Version,
        MeetingId = source.MeetingId, VersionNumber = source.VersionNumber, Status = source.Status,
        Attendance = source.Attendance.Select(a => new MinutesAttendanceRecord { AttendeeUserId = a.AttendeeUserId, Status = a.Status }).ToList(),
        Decisions = source.Decisions.Select(d => new MinutesDecision { Code = d.Code, Text = d.Text, DecidedByUserId = d.DecidedByUserId, RecordLinkId = d.RecordLinkId }).ToList(),
        ActionReferences = [.. source.ActionReferences],
        PublishedAtUtc = source.PublishedAtUtc, PublishedByUserId = source.PublishedByUserId,
        CorrectionOfVersionNumber = source.CorrectionOfVersionNumber, CorrectionReason = source.CorrectionReason
    };

    public Task<MeetingMinutesVersion?> TryCreateAsync(MeetingMinutesVersion version, CancellationToken ct = default)
    {
        if (_items.Any(x => x.TenantId == version.TenantId && !x.IsDeleted
                             && x.MeetingId == version.MeetingId && x.VersionNumber == version.VersionNumber))
        {
            return Task.FromResult<MeetingMinutesVersion?>(null);
        }

        _items.Add(Clone(version));
        return Task.FromResult<MeetingMinutesVersion?>(version);
    }

    public Task<MeetingMinutesVersion?> GetLatestByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult(_items
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(Clone)
            .FirstOrDefault());

    public Task<IReadOnlyList<MeetingMinutesVersion>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingMinutesVersion>>(_items
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(Clone)
            .ToList());

    public Task<IReadOnlyList<MeetingMinutesVersion>> ListPublishedByMeetingIdsAsync(
        IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingMinutesVersion>>(_items
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && meetingIds.Contains(x.MeetingId)
                        && x.Status == MinutesStatus.Published)
            .Select(Clone)
            .ToList());

    public Task<bool> UpdateAsync(MeetingMinutesVersion version, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == version.Id && x.TenantId == Tenant && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        _items.Remove(stored);
        version.Version = expectedVersion + 1;
        _items.Add(Clone(version));
        return Task.FromResult(true);
    }
}

internal sealed class FakeAgendaItemRepository : IAgendaItemRepository
{
    private readonly List<AgendaItem> _items = [];
    public Guid Tenant { get; init; }

    public Task<AgendaItem> CreateAsync(AgendaItem item, CancellationToken ct = default)
    {
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<AgendaItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<AgendaItem>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgendaItem>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted && x.MeetingId == meetingId)
                .OrderBy(x => x.SortOrder).ToList());

    public Task<bool> UpdateAsync(AgendaItem item, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == item.Id && x.TenantId == Tenant && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.Text = item.Text;
        stored.SortOrder = item.SortOrder;
        stored.Version = expectedVersion + 1;
        return Task.FromResult(true);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null) { item.IsDeleted = true; }
        return Task.CompletedTask;
    }
}

internal sealed class FakeMeetingTypeRepository : IMeetingTypeRepository
{
    private readonly List<MeetingType> _items = [];
    public Guid Tenant { get; init; }

    public void Seed(MeetingType type) => _items.Add(type);

    public Task<MeetingType> CreateAsync(MeetingType type, CancellationToken ct = default)
    {
        _items.Add(type);
        return Task.FromResult(type);
    }

    public Task<MeetingType?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<MeetingType>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingType>>(_items.Where(x => x.TenantId == Tenant && !x.IsDeleted).ToList());

    public Task<MeetingType?> FindByNameAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == Tenant && !x.IsDeleted && x.Name == name));

    public Task<bool> UpdateAsync(MeetingType type, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == type.Id && x.TenantId == Tenant && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        _items.Remove(stored);
        type.Version = expectedVersion + 1;
        _items.Add(type);
        return Task.FromResult(true);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null) { item.IsDeleted = true; }
        return Task.CompletedTask;
    }
}

/// <summary>MOD-0357 S11 — in-memory double for <see cref="IMeetingSeriesRepository"/>, the same shape
/// <see cref="FakeMeetingTypeRepository"/> already takes.</summary>
internal sealed class FakeMeetingSeriesRepository : IMeetingSeriesRepository
{
    private readonly List<MeetingSeries> _items = [];
    public Guid Tenant { get; init; }

    public void Seed(MeetingSeries series) => _items.Add(series);

    public Task<MeetingSeries> CreateAsync(MeetingSeries series, CancellationToken ct = default)
    {
        _items.Add(series);
        return Task.FromResult(series);
    }

    public Task<MeetingSeries?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id && x.TenantId == Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<MeetingSeries>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingSeries>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted).OrderBy(x => x.Name, StringComparer.Ordinal).ToList());

    public Task<IReadOnlyList<MeetingSeries>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MeetingSeries>>(
            _items.Where(x => x.TenantId == Tenant && !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name, StringComparer.Ordinal).ToList());

    public Task<MeetingSeries?> FindByNameAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == Tenant && !x.IsDeleted && x.Name == name));

    public Task<bool> UpdateAsync(MeetingSeries series, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == series.Id && x.TenantId == Tenant && !x.IsDeleted);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        series.Version = expectedVersion + 1;
        if (!ReferenceEquals(stored, series))
        {
            _items.Remove(stored);
            _items.Add(series);
        }

        return Task.FromResult(true);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null) { item.IsDeleted = true; }
        return Task.CompletedTask;
    }
}

/// <summary>S5 — records every call so a test can assert WHO was mailed and WHICH event, without a real
/// <c>INotificationEventDispatchAdapter</c>. <see cref="NextResult"/> is read at call time, not fixed at
/// construction, so a K12 test can flip it to a failure mid-scenario.</summary>
internal sealed class FakeMeetingInviteMailer : IMeetingInviteMailer
{
    public sealed record Call(string Kind, Guid MeetingId, IReadOnlyList<Guid> RecipientUserIds, Guid ActingUserId);

    public List<Call> Calls { get; } = [];
    public MeetingInviteDeliveryResult NextResult { get; set; } = new(Sent: true, Failed: false, Reason: null);

    public Task<MeetingInviteDeliveryResult> SendInviteAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => Record("invite", meeting, recipients, actingUserId);

    public Task<MeetingInviteDeliveryResult> SendChangeAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => Record("change", meeting, recipients, actingUserId);

    public Task<MeetingInviteDeliveryResult> SendCancelAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => Record("cancel", meeting, recipients, actingUserId);

    public Task<MeetingInviteDeliveryResult> SendRemovedAsync(
        Meeting meeting, string meetingTypeName, MeetingAttendee removedAttendee, Guid actingUserId, CancellationToken ct = default)
        => Record("removed", meeting, [removedAttendee], actingUserId);

    public Task<MeetingInviteDeliveryResult> SendOrganizerReassignedAsync(
        Meeting meeting, string meetingTypeName, Guid newOrganizerUserId, Guid actingUserId, CancellationToken ct = default)
    {
        Calls.Add(new Call("organizer-reassigned", meeting.Id, [newOrganizerUserId], actingUserId));
        return Task.FromResult(NextResult);
    }

    private Task<MeetingInviteDeliveryResult> Record(
        string kind, Meeting meeting, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId)
    {
        Calls.Add(new Call(kind, meeting.Id, recipients.Select(r => r.UserId).ToList(), actingUserId));
        return Task.FromResult(NextResult);
    }
}

/// <summary>Answers ONLY <see cref="GetTaskAssignmentPersonLookupQuery"/>, with a test-controlled eligible-user
/// set — the "same seam" MOD-0357's attendee/organizer eligibility checks call through <see cref="IMediator"/>.</summary>
internal sealed class FakeEligibilityMediator : IMediator
{
    public HashSet<Guid> EligibleUserIds { get; } = [];

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        if (request is GetTaskAssignmentPersonLookupQuery query)
        {
            var people = EligibleUserIds
                .Select(id => new AssignablePersonDto(id, null, Guid.NewGuid(), "POS", "Position", Guid.NewGuid(), "OU", "Unit", Guid.NewGuid()))
                .ToList();
            var result = Response<AssignablePersonLookupDto>.Success(
                new AssignablePersonLookupDto(people, ExcludedCandidateSummary.None), correlationId: query.CorrelationId);
            return (Task<TResponse>)(object)Task.FromResult(result);
        }

        throw new NotSupportedException($"FakeEligibilityMediator does not support {request.GetType().Name}.");
    }

    public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

    public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification => throw new NotSupportedException();
}

/// <summary>A permission set a test dictates directly — no claims, no HTTP context.</summary>
internal sealed class FakeActorPermissionContext : IActorPermissionContext
{
    private readonly HashSet<string> _granted;

    public FakeActorPermissionContext(params string[] granted) => _granted = new HashSet<string>(granted, StringComparer.Ordinal);

    public bool IsPlatformActor { get; init; }

    public bool Has(string? permissionKey) => permissionKey is null || _granted.Contains(permissionKey);
}
