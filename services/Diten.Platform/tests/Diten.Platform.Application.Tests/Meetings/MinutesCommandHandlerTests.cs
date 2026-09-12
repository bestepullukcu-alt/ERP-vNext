using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S6 (WP-MG-MOD0357-S6-MINUTES-01) — K4: a draft is freely editable; publishing locks it; the only
/// path forward from a published row is a NEW row (a correction), and the prior published row is never
/// updated. <see cref="A_published_version_is_never_the_target_of_UpdateAsync"/> is the source guard's own
/// proof — it does not read field values, it watches which rows the handlers ever hand to
/// <c>IMeetingMinutesVersionRepository.UpdateAsync</c>.
/// </summary>
public sealed class MinutesCommandHandlerTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid Organizer = Guid.NewGuid();
    private static readonly Guid Attendee1 = Guid.NewGuid();
    private static readonly Guid Attendee2 = Guid.NewGuid();

    /// <summary>Wraps <see cref="FakeMeetingMinutesVersionRepository"/> to record every row passed to
    /// <c>UpdateAsync</c> — the source guard's own witness (see the class doc comment).</summary>
    private sealed class WatchedMinutesRepository(FakeMeetingMinutesVersionRepository inner)
        : Domain.Repositories.IMeetingMinutesVersionRepository
    {
        /// <summary>The STORED status of each row at the moment <c>UpdateAsync</c> was called against it — the
        /// "before" state, read from the repository BEFORE the handler's own in-place mutation of its copy of
        /// the object. A handler that ever calls <c>UpdateAsync</c> on a row already Published shows up here as
        /// <see cref="MinutesStatus.Published"/>, whether or not the call would even succeed.</summary>
        public List<MinutesStatus?> UpdateAttemptsAgainstStoredStatus { get; } = [];

        public Task<MeetingMinutesVersion?> TryCreateAsync(MeetingMinutesVersion version, CancellationToken ct = default)
            => inner.TryCreateAsync(version, ct);

        public Task<MeetingMinutesVersion?> GetLatestByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
            => inner.GetLatestByMeetingIdAsync(meetingId, ct);

        public Task<IReadOnlyList<MeetingMinutesVersion>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default)
            => inner.ListByMeetingIdAsync(meetingId, ct);

        public Task<bool> UpdateAsync(MeetingMinutesVersion version, int expectedVersion, CancellationToken ct = default)
        {
            UpdateAttemptsAgainstStoredStatus.Add(inner.StoredStatusOf(version.Id));
            return inner.UpdateAsync(version, expectedVersion, ct);
        }
    }

    private sealed class Fixture
    {
        public FakeMeetingRepository Meetings { get; } = new() { Tenant = Tenant };
        public FakeMeetingAttendeeRepository Attendees { get; } = new() { Tenant = Tenant };
        public FakeMeetingMinutesVersionRepository MinutesInner { get; } = new() { Tenant = Tenant };
        public WatchedMinutesRepository Minutes { get; }
        public FakeUserDisplayNameResolver DisplayNames { get; } = new();

        public Fixture() => Minutes = new WatchedMinutesRepository(MinutesInner);

        public SaveMinutesDraftHandler DraftHandler(Guid actingUserId)
            => new(Meetings, Attendees, Minutes, new FakeCurrentUserContext(actingUserId), DisplayNames);

        public PublishMinutesHandler PublishHandler(Guid actingUserId)
            => new(Meetings, Attendees, Minutes, new FakeCurrentUserContext(actingUserId), DisplayNames);

        public CorrectPublishedMinutesHandler CorrectHandler(Guid actingUserId)
            => new(Meetings, Attendees, Minutes, new FakeCurrentUserContext(actingUserId), DisplayNames);

        public Meeting SeedMeeting(MeetingLifecycle lifecycle = MeetingLifecycle.Scheduled)
        {
            var meeting = new Meeting
            {
                TenantId = Tenant, Title = "Aylık Yönetim Gözden Geçirmesi", MeetingTypeId = Guid.NewGuid(),
                StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                OrganizerUserId = Organizer, IdempotencyKey = Guid.NewGuid().ToString(), Lifecycle = lifecycle
            };
            Meetings.Seed(meeting);
            return meeting;
        }

        public void SeedAttendee(Guid meetingId, Guid userId)
            => Attendees.Seed(new MeetingAttendee { TenantId = Tenant, MeetingId = meetingId, UserId = userId, CreatedBy = "test" });
    }

    private static SaveMinutesDraftRequest DraftRequest(
        IReadOnlyList<MinutesAttendanceRequest>? attendance = null,
        IReadOnlyList<MinutesDecisionRequest>? decisions = null,
        int? expectedVersion = null)
        => new(attendance ?? [], decisions ?? [], expectedVersion);

    // ── SaveMinutesDraft ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task First_save_creates_version_1_as_Draft()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();

        var response = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(1, response.Data!.VersionNumber);
        Assert.Equal(MinutesStatus.Draft, response.Data.Status);
    }

    [Fact]
    public async Task A_second_save_updates_the_SAME_draft_row_in_place()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        fx.SeedAttendee(meeting.Id, Attendee1);

        var first = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(decisions: [new MinutesDecisionRequest("İlk metin", null)]), "corr"),
            CancellationToken.None);

        var second = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(
                meeting.Id,
                DraftRequest(
                    attendance: [new MinutesAttendanceRequest(Attendee1, AttendanceStatus.Present)],
                    decisions: [new MinutesDecisionRequest("Güncellenmiş metin", null)],
                    expectedVersion: first.Data!.Version),
                "corr"),
            CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(1, second.Data!.VersionNumber);
        Assert.Equal("Güncellenmiş metin", Assert.Single(second.Data.Decisions).Text);
        Assert.Single(await fx.MinutesInner.ListByMeetingIdAsync(meeting.Id));
    }

    [Fact]
    public async Task Decision_codes_are_minted_by_position_D1_D2()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();

        var response = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(
                meeting.Id,
                DraftRequest(decisions: [new MinutesDecisionRequest("Birinci", null), new MinutesDecisionRequest("İkinci", null)]),
                "corr"),
            CancellationToken.None);

        Assert.Equal(["D-1", "D-2"], response.Data!.Decisions.Select(d => d.Code));
    }

    [Fact]
    public async Task Saving_a_draft_against_a_PUBLISHED_version_is_409()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        var created = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);
        await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(created.Data!.Version), "corr"), CancellationToken.None);

        var response = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(expectedVersion: 1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesPublished, response.ReasonCode);
    }

    [Fact]
    public async Task A_stale_ExpectedVersion_on_draft_save_is_409_concurrency()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        var response = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(expectedVersion: 999), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesConcurrencyConflict, response.ReasonCode);
    }

    [Fact]
    public async Task Attendance_for_someone_not_invited_is_refused()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();

        var response = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(
                meeting.Id, DraftRequest(attendance: [new MinutesAttendanceRequest(Guid.NewGuid(), AttendanceStatus.Present)]), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.AttendeeNotFound, response.ReasonCode);
    }

    // ── PublishMinutes ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publishing_locks_the_row_and_stamps_who_and_when()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        var draft = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        var response = await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(draft.Data!.Version), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(MinutesStatus.Published, response.Data!.Status);
        Assert.NotNull(response.Data.PublishedAtUtc);
        Assert.Equal(Organizer, response.Data.PublishedByUserId);
    }

    [Fact]
    public async Task Publishing_sets_the_meetings_lifecycle_to_Completed()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        var draft = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(draft.Data!.Version), "corr"), CancellationToken.None);

        var stored = await fx.Meetings.GetByIdAsync(meeting.Id);
        Assert.Equal(MeetingLifecycle.Completed, stored!.Lifecycle);
    }

    [Fact]
    public async Task Publishing_syncs_attendance_status_onto_MeetingAttendee_one_way()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        fx.SeedAttendee(meeting.Id, Attendee1);
        fx.SeedAttendee(meeting.Id, Attendee2);
        var draft = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(
                meeting.Id,
                DraftRequest(attendance:
                [
                    new MinutesAttendanceRequest(Attendee1, AttendanceStatus.Present),
                    new MinutesAttendanceRequest(Attendee2, AttendanceStatus.Excused)
                ]),
                "corr"),
            CancellationToken.None);

        await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(draft.Data!.Version), "corr"), CancellationToken.None);

        var attendee1 = await fx.Attendees.FindAsync(meeting.Id, Attendee1);
        var attendee2 = await fx.Attendees.FindAsync(meeting.Id, Attendee2);
        Assert.Equal(AttendanceStatus.Present, attendee1!.AttendanceStatus);
        Assert.Equal(AttendanceStatus.Excused, attendee2!.AttendanceStatus);
    }

    [Fact]
    public async Task A_second_publish_is_409()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        var draft = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);
        await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(draft.Data!.Version), "corr"), CancellationToken.None);

        var response = await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesPublished, response.ReasonCode);
    }

    [Fact]
    public async Task Publishing_with_no_draft_at_all_is_400()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();

        var response = await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task A_stale_ExpectedVersion_on_publish_is_409_concurrency()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        var response = await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(999), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesConcurrencyConflict, response.ReasonCode);
    }

    // ── CorrectPublishedMinutes — K4's own boundary ─────────────────────────────────────────────────────────

    private async Task<(Fixture fx, Meeting meeting, int publishedVersion)> PublishedFixtureAsync(string decisionText = "Orijinal karar")
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        var draft = await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(decisions: [new MinutesDecisionRequest(decisionText, null)]), "corr"),
            CancellationToken.None);
        await fx.PublishHandler(Organizer).Handle(
            new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(draft.Data!.Version), "corr"), CancellationToken.None);
        return (fx, meeting, 1);
    }

    [Fact]
    public async Task Correcting_without_a_reason_is_400()
    {
        var (fx, meeting, _) = await PublishedFixtureAsync();

        var response = await fx.CorrectHandler(Organizer).Handle(
            new CorrectPublishedMinutesCommand(
                meeting.Id, new CorrectPublishedMinutesRequest([], [new MinutesDecisionRequest("Düzeltilmiş", null)], ""), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesCorrectionReasonRequired, response.ReasonCode);
    }

    [Fact]
    public async Task A_correction_creates_a_NEW_published_version_and_leaves_v1_untouched()
    {
        var (fx, meeting, _) = await PublishedFixtureAsync("Orijinal karar");

        var response = await fx.CorrectHandler(Organizer).Handle(
            new CorrectPublishedMinutesCommand(
                meeting.Id,
                new CorrectPublishedMinutesRequest([], [new MinutesDecisionRequest("Düzeltilmiş karar", null)], "Yazım hatası düzeltildi"),
                "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(2, response.Data!.VersionNumber);
        Assert.Equal(MinutesStatus.Published, response.Data.Status);
        Assert.Equal(1, response.Data.CorrectionOfVersionNumber);
        Assert.Equal("Yazım hatası düzeltildi", response.Data.CorrectionReason);

        var versions = await fx.MinutesInner.ListByMeetingIdAsync(meeting.Id);
        Assert.Equal(2, versions.Count);
        var v1 = versions.Single(v => v.VersionNumber == 1);
        Assert.Equal(MinutesStatus.Published, v1.Status);
        Assert.Equal("Orijinal karar", Assert.Single(v1.Decisions).Text);
        Assert.Null(v1.CorrectionOfVersionNumber);
    }

    /// <summary>
    /// THE SOURCE GUARD, watched directly rather than inferred from output: no handler in this file ever
    /// hands a row whose <see cref="MeetingMinutesVersion.Status"/> was already <see cref="MinutesStatus.Published"/>
    /// to <c>UpdateAsync</c>. Draft-save updates a Draft row; publish updates a Draft row (into Published, once);
    /// correction never calls <c>UpdateAsync</c> at all — it calls <c>TryCreateAsync</c> on a brand new row.
    /// </summary>
    [Fact]
    public async Task A_published_version_is_never_the_target_of_UpdateAsync()
    {
        var (fx, meeting, _) = await PublishedFixtureAsync();

        await fx.CorrectHandler(Organizer).Handle(
            new CorrectPublishedMinutesCommand(
                meeting.Id, new CorrectPublishedMinutesRequest([], [new MinutesDecisionRequest("v2 metni", null)], "sebep"), "corr"),
            CancellationToken.None);

        Assert.DoesNotContain(MinutesStatus.Published, fx.Minutes.UpdateAttemptsAgainstStoredStatus);
    }

    [Fact]
    public async Task Correcting_when_nothing_is_published_yet_is_409()
    {
        var fx = new Fixture();
        var meeting = fx.SeedMeeting();
        await fx.DraftHandler(Organizer).Handle(
            new SaveMinutesDraftCommand(meeting.Id, DraftRequest(), "corr"), CancellationToken.None);

        var response = await fx.CorrectHandler(Organizer).Handle(
            new CorrectPublishedMinutesCommand(meeting.Id, new CorrectPublishedMinutesRequest([], [], "sebep"), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesNotPublished, response.ReasonCode);
    }
}
