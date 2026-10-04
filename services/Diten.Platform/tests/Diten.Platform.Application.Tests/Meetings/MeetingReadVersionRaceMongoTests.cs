using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-533 — every meeting write replaces the WHOLE document, so it must be conditional on the version the handler READ,
/// not only on the version the client sent. Each test stops one handler right after its read (<see cref="ReadBarrier"/>),
/// runs a competing write to the end, then lets the first handler go on with a client version that matches what is
/// stored NOW. Before the fix the stale copy matched the filter and overwrote the winner; the answer must be 409 and the
/// winner's content must stay. Real handlers, real Mongo repositories (shared itest database, fresh TenantId).
/// </summary>
public sealed class MeetingReadVersionRaceMongoTests : IAsyncLifetime
{
    private readonly Guid _organizer = Guid.NewGuid();
    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _meetings = null!;
    private MeetingTypeRepository _types = null!;
    private MeetingAttendeeRepository _attendees = null!;
    private AgendaItemRepository _agenda = null!;
    private MeetingMinutesVersionRepository _minutes = null!;
    private MeetingSeriesRepository _series = null!;
    private readonly ReadBarrier _barrier = new();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        _types = new MeetingTypeRepository(_harness.DbContext, _harness.TenantContext);
        _attendees = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
        _agenda = new AgendaItemRepository(_harness.DbContext, _harness.TenantContext);
        _minutes = new MeetingMinutesVersionRepository(_harness.DbContext, _harness.TenantContext);
        _series = new MeetingSeriesRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        _barrier.Release();
        var tenant = _harness.TenantId;
        await Delete<Meeting>(PlatformCollections.MeetingMeetings, tenant);
        await Delete<MeetingType>(PlatformCollections.MeetingTypes, tenant);
        await Delete<MeetingAttendee>(PlatformCollections.MeetingAttendees, tenant);
        await Delete<AgendaItem>(PlatformCollections.MeetingAgendaItems, tenant);
        await Delete<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, tenant);
        await Delete<MeetingSeries>(PlatformCollections.MeetingSeries, tenant);
        await _harness.DisposeAsync();
    }

    private Task Delete<T>(string collection, Guid tenant) where T : Diten.Platform.Common.Persistence.TenantScopedEntity
        => _harness.Database.GetCollection<T>(collection).DeleteManyAsync(Builders<T>.Filter.Eq(x => x.TenantId, tenant));

    private async Task<T> RawAsync<T>(string collection, Guid id) where T : Diten.Platform.Common.Persistence.BaseEntity
        => await _harness.Database.GetCollection<T>(collection).Find(Builders<T>.Filter.Eq(x => x.Id, id)).SingleAsync();

    /// <summary>Runs <paramref name="loser"/> until it has read, runs <paramref name="winner"/> to the end, then lets the
    /// loser go on. Returns both answers.</summary>
    private async Task<(TWinner Winner, TLoser Loser)> RaceAsync<TWinner, TLoser>(
        Func<Task<TLoser>> loser, Func<Task<TWinner>> winner)
    {
        var loserRun = Task.Run(loser);
        await _barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
        var winnerAnswer = await winner();
        _barrier.Release();
        return (winnerAnswer, await loserRun);
    }

    // ── World ────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<MeetingType> TypeAsync(string name = "Kalite Gözden Geçirme")
        => await _types.CreateAsync(new MeetingType { TenantId = _harness.TenantId, Name = name + " " + Guid.NewGuid().ToString("N")[..6] });

    private async Task<Meeting> MeetingAsync(Guid typeId)
        => await _meetings.CreateAsync(new Meeting
        {
            TenantId = _harness.TenantId,
            Title = "Yönetim Gözden Geçirme",
            MeetingTypeId = typeId,
            StartAt = DateTimeOffset.UtcNow.AddDays(3),
            EndAt = DateTimeOffset.UtcNow.AddDays(3).AddHours(1),
            OrganizerUserId = _organizer,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        });

    private async Task<MeetingMinutesVersion> DraftAsync(Guid meetingId)
        => (await _minutes.TryCreateAsync(new MeetingMinutesVersion
        {
            TenantId = _harness.TenantId,
            MeetingId = meetingId,
            VersionNumber = 1,
            Status = MinutesStatus.Draft
        }))!;

    private SaveMinutesDraftHandler SaveDraft(IMeetingMinutesVersionRepository minutes)
        => new(_meetings, _attendees, minutes, new FakeCurrentUserContext(_organizer), new FakeUserDisplayNameResolver());

    private PublishMinutesHandler Publish(IMeetingMinutesVersionRepository minutes)
        => new(_meetings, _attendees, minutes, new FakeCurrentUserContext(_organizer), new FakeUserDisplayNameResolver());

    private static SaveMinutesDraftCommand DraftCommand(Guid meetingId, string decision, int expectedVersion)
        => new(meetingId, new SaveMinutesDraftRequest([], [new MinutesDecisionRequest(decision, null)], expectedVersion), "race");

    // ── Minutes ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_publish_that_read_the_draft_before_an_edit_is_refused_and_the_edit_stays_unpublished()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id);

        var (edit, publish) = await RaceAsync(
            () => Publish(_barrier.Wrap<IMeetingMinutesVersionRepository>(_minutes, nameof(IMeetingMinutesVersionRepository.GetLatestByMeetingIdAsync)))
                .Handle(new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(2), "race"), default),
            () => SaveDraft(_minutes).Handle(DraftCommand(meeting.Id, "Bütçe onaylandı", 1), default));

        Assert.Equal(200, edit.StatusCode);
        Assert.Equal(409, publish.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesConcurrencyConflict, publish.ReasonCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Equal(MinutesStatus.Draft, stored.Status);
        Assert.Equal(2, stored.Version);
        Assert.Contains(stored.Decisions, d => d.Text == "Bütçe onaylandı");
        Assert.Equal(MeetingLifecycle.Scheduled, (await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id)).Lifecycle);
    }

    [Fact]
    public async Task An_edit_that_read_the_draft_before_it_was_published_is_refused_and_the_minutes_stay_published()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id);

        var (publish, edit) = await RaceAsync(
            () => SaveDraft(_barrier.Wrap<IMeetingMinutesVersionRepository>(_minutes, nameof(IMeetingMinutesVersionRepository.GetLatestByMeetingIdAsync)))
                .Handle(DraftCommand(meeting.Id, "Sonradan eklenen karar", 2), default),
            () => Publish(_minutes).Handle(new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "race"), default));

        Assert.Equal(200, publish.StatusCode);
        Assert.Equal(409, edit.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesConcurrencyConflict, edit.ReasonCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Equal(MinutesStatus.Published, stored.Status);
        Assert.Equal(2, stored.Version);
        Assert.DoesNotContain(stored.Decisions, d => d.Text == "Sonradan eklenen karar");
    }

    [Fact]
    public async Task Two_edits_that_read_the_same_draft_one_wins_and_the_other_is_refused()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id);

        var (first, second) = await RaceAsync(
            () => SaveDraft(_barrier.Wrap<IMeetingMinutesVersionRepository>(_minutes, nameof(IMeetingMinutesVersionRepository.GetLatestByMeetingIdAsync)))
                .Handle(DraftCommand(meeting.Id, "İkinci yazan", 1), default),
            () => SaveDraft(_minutes).Handle(DraftCommand(meeting.Id, "İlk yazan", 1), default));

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(409, second.StatusCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Contains(stored.Decisions, d => d.Text == "İlk yazan");
        Assert.DoesNotContain(stored.Decisions, d => d.Text == "İkinci yazan");
    }

    // ── Meeting ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_meeting_edit_that_read_before_a_cancel_is_refused_and_the_meeting_stays_cancelled()
    {
        var type = await TypeAsync();
        var meeting = await MeetingAsync(type.Id);
        var mailer = new FakeMeetingInviteMailer();

        var (cancel, update) = await RaceAsync(
            () => new UpdateMeetingHandler(
                    _barrier.Wrap<IMeetingRepository>(_meetings, nameof(IMeetingRepository.GetByIdAsync)),
                    _types, _attendees, new FakeCurrentUserContext(_organizer), mailer)
                .Handle(new UpdateMeetingCommand(meeting.Id, new UpdateMeetingRequest(
                    "Güncellenen başlık", type.Id, meeting.StartAt, meeting.EndAt, meeting.Location, meeting.Description, 2), "race"), default),
            () => new CancelMeetingHandler(_meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), mailer)
                .Handle(new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Ertelendi", 1), "race"), default));

        Assert.Equal(200, cancel.StatusCode);
        Assert.Equal(409, update.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, update.ReasonCode);
        var stored = await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id);
        Assert.Equal(MeetingLifecycle.Cancelled, stored.Lifecycle);
        Assert.Equal("Ertelendi", stored.CancellationReason);
        Assert.Equal("Yönetim Gözden Geçirme", stored.Title);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task An_agenda_line_edit_that_read_before_another_edit_is_refused_and_the_other_text_stays()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var item = await _agenda.CreateAsync(new AgendaItem
        {
            TenantId = _harness.TenantId, MeetingId = meeting.Id, Text = "Açılış", SortOrder = 0
        });

        var (winner, loser) = await RaceAsync(
            () => new UpdateAgendaItemHandler(_meetings, _barrier.Wrap<IAgendaItemRepository>(_agenda, nameof(IAgendaItemRepository.GetByIdAsync)))
                .Handle(new UpdateAgendaItemCommand(meeting.Id, item.Id, new UpdateAgendaItemRequest("Eski kopya", 2), "race"), default),
            () => new UpdateAgendaItemHandler(_meetings, _agenda)
                .Handle(new UpdateAgendaItemCommand(meeting.Id, item.Id, new UpdateAgendaItemRequest("Kazanan metin", 1), "race"), default));

        Assert.Equal(200, winner.StatusCode);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, loser.ReasonCode);
        var stored = await RawAsync<AgendaItem>(PlatformCollections.MeetingAgendaItems, item.Id);
        Assert.Equal("Kazanan metin", stored.Text);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_meeting_type_edit_that_read_before_another_edit_is_refused_and_the_other_name_stays()
    {
        var type = await TypeAsync();
        var winnerName = "Kazanan tür " + Guid.NewGuid().ToString("N")[..6];

        var (winner, loser) = await RaceAsync(
            () => new UpdateMeetingTypeHandler(_barrier.Wrap<IMeetingTypeRepository>(_types, nameof(IMeetingTypeRepository.GetByIdAsync)))
                .Handle(new UpdateMeetingTypeCommand(type.Id, new UpdateMeetingTypeRequest(
                    "Eski kopya " + Guid.NewGuid().ToString("N")[..6], null, null, false, false, false, 2), "race"), default),
            () => new UpdateMeetingTypeHandler(_types)
                .Handle(new UpdateMeetingTypeCommand(type.Id, new UpdateMeetingTypeRequest(
                    winnerName, null, null, true, false, false, 1), "race"), default));

        Assert.Equal(200, winner.StatusCode);
        Assert.Equal(409, loser.StatusCode);
        var stored = await RawAsync<MeetingType>(PlatformCollections.MeetingTypes, type.Id);
        Assert.Equal(winnerName, stored.Name);
        Assert.True(stored.IsQualityRecord);
    }

    // ── Series ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_series_edit_that_read_before_another_edit_is_refused_and_the_other_rule_stays()
    {
        var type = await TypeAsync();
        var series = await _series.CreateAsync(new MeetingSeries
        {
            TenantId = _harness.TenantId,
            Name = "Haftalık kalite " + Guid.NewGuid().ToString("N")[..6],
            MeetingTypeId = type.Id,
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            OrganizerUserId = _organizer
        });
        UpdateMeetingSeriesRequest Request(string name, int interval, int expectedVersion) => new(
            name, type.Id, MeetingSeriesFrequency.Weekly, interval, series.StartsAt, null, 60, null, _organizer, [], 14,
            false, true, expectedVersion);
        var winnerName = "Kazanan seri " + Guid.NewGuid().ToString("N")[..6];

        var (winner, loser) = await RaceAsync(
            () => new UpdateMeetingSeriesHandler(_barrier.Wrap<IMeetingSeriesRepository>(_series, nameof(IMeetingSeriesRepository.GetByIdAsync)), _types)
                .Handle(new UpdateMeetingSeriesCommand(series.Id, Request("Eski kopya " + Guid.NewGuid().ToString("N")[..6], 1, 2), "race"), default),
            () => new UpdateMeetingSeriesHandler(_series, _types)
                .Handle(new UpdateMeetingSeriesCommand(series.Id, Request(winnerName, 2, 1), "race"), default));

        Assert.Equal(200, winner.StatusCode);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, loser.ReasonCode);
        var stored = await RawAsync<MeetingSeries>(PlatformCollections.MeetingSeries, series.Id);
        Assert.Equal(winnerName, stored.Name);
        Assert.Equal(2, stored.Interval);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_refused_write_leaves_the_document_in_hand_at_the_version_it_was_read()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var stale = await _meetings.GetByIdAsync(meeting.Id);
        var fresh = await _meetings.GetByIdAsync(meeting.Id);
        Assert.True(await _meetings.UpdateAsync(fresh!, 1));

        stale!.Title = "Eski kopya";
        Assert.False(await _meetings.UpdateAsync(stale, 2));
        Assert.Equal(1, stale.Version);
        Assert.False(await _meetings.UpdateAsync(stale, 1));
        Assert.Equal(1, stale.Version);
        Assert.Equal("Yönetim Gözden Geçirme", (await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id)).Title);
    }
}
