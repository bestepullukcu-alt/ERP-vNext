using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Entities.Meetings;
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
}

internal sealed class FakeMeetingAttendeeRepository : IMeetingAttendeeRepository
{
    private readonly List<MeetingAttendee> _items = [];
    public Guid Tenant { get; init; }

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
