using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-533 — every meeting write replaces the WHOLE document, so it must be conditional on the version the handler READ,
/// not only on the version the client sent. Each race test stops one handler right after its read (<see cref="ReadBarrier"/>),
/// runs a competing write to the end, then lets the first handler go on with a client version that matches what is
/// stored NOW. Before the fix the stale copy matched the filter and overwrote the winner; the answer must be 409 and the
/// winner's content must stay. A publish writes the minutes, the attendance and the meeting's Completed in ONE Platform
/// transaction. Real handlers, real Mongo repositories (shared itest database on the dev replica set, fresh TenantId).
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
        await Delete<RecordLink>(PlatformCollections.MeetingRecordLinks, tenant);
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

    private static bool InTransaction(object?[] args) => args.Any(a => a is IPlatformTransactionSession);

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

    private async Task<MeetingMinutesVersion> DraftAsync(Guid meetingId, IReadOnlyList<MinutesAttendanceRecord>? attendance = null,
        IReadOnlyList<MinutesDecision>? decisions = null)
        => (await _minutes.TryCreateAsync(new MeetingMinutesVersion
        {
            TenantId = _harness.TenantId,
            MeetingId = meetingId,
            VersionNumber = 1,
            Status = MinutesStatus.Draft,
            Attendance = attendance?.ToList() ?? [],
            Decisions = decisions?.ToList() ?? []
        }))!;

    private SaveMinutesDraftHandler SaveDraft(IMeetingMinutesVersionRepository minutes)
        => new(_meetings, _attendees, minutes, new FakeCurrentUserContext(_organizer), new FakeUserDisplayNameResolver());

    private PublishMinutesHandler Publish(IMeetingMinutesVersionRepository minutes, IMeetingRepository? meetings = null)
        => new(meetings ?? _meetings, _attendees, minutes, new FakeCurrentUserContext(_organizer), new FakeUserDisplayNameResolver(),
            new PlatformTransactionExecutor(_harness.DbContext));

    private static SaveMinutesDraftCommand DraftCommand(Guid meetingId, string decision, int expectedVersion)
        => new(meetingId, new SaveMinutesDraftRequest([], [new MinutesDecisionRequest(decision, null)], expectedVersion), "race");

    private UpdateMeetingHandler UpdateMeeting(IMeetingRepository? meetings = null)
        => new(meetings ?? _meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), new FakeMeetingInviteMailer());

    private CancelMeetingHandler CancelMeeting()
        => new(_meetings, _types, _attendees, new FakeCurrentUserContext(_organizer), new FakeMeetingInviteMailer());

    private CreateTaskFromMeetingHandler Bridge(IAgendaItemRepository? agenda = null, IMeetingMinutesVersionRepository? minutes = null)
        => new(_meetings, _types, agenda ?? _agenda, minutes ?? _minutes,
            new RecordLinkService(new RecordLinkRepository(_harness.DbContext, _harness.TenantContext), _harness.TenantContext,
                new FakeCurrentUserContext(_organizer)),
            new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(_organizer), new TaskCreatingMediator());

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

    /// <summary>
    /// Item 5: the loser read the draft at N, the winner moved it to N+1, and the loser's client names N+1 — the version
    /// stored NOW. Only the store's read-version rule can refuse this (the stored-version filter matches): before
    /// 2fc7deca3 the loser's stale copy replaced the winner's decision.
    /// </summary>
    [Fact]
    public async Task An_edit_that_read_the_draft_before_another_edit_is_refused_even_under_the_version_stored_now()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id);

        var (first, second) = await RaceAsync(
            () => SaveDraft(_barrier.Wrap<IMeetingMinutesVersionRepository>(_minutes, nameof(IMeetingMinutesVersionRepository.GetLatestByMeetingIdAsync)))
                .Handle(DraftCommand(meeting.Id, "İkinci yazan", 2), default),
            () => SaveDraft(_minutes).Handle(DraftCommand(meeting.Id, "İlk yazan", 1), default));

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(MeetingReasonCodes.MinutesConcurrencyConflict, second.ReasonCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Contains(stored.Decisions, d => d.Text == "İlk yazan");
        Assert.DoesNotContain(stored.Decisions, d => d.Text == "İkinci yazan");
        Assert.Equal(2, stored.Version);
    }

    // ── Publish ↔ the meeting it completes (item 4) ──────────────────────────────────────────────────────────────

    /// <summary>A meeting edit lands between the publish's read of the meeting and its transaction: the publish completes
    /// the meeting AS IT IS NOW — the edit stays, the meeting is Completed (before BL-533 the Completed write was refused
    /// and ignored, leaving published minutes on a Scheduled, still-editable meeting).</summary>
    [Fact]
    public async Task A_publish_that_read_the_meeting_before_an_edit_completes_it_and_keeps_the_edit()
    {
        var type = await TypeAsync();
        var meeting = await MeetingAsync(type.Id);
        var draft = await DraftAsync(meeting.Id);

        var (edit, publish) = await RaceAsync(
            () => Publish(_minutes, _barrier.Wrap<IMeetingRepository>(_meetings, nameof(IMeetingRepository.GetByIdAsync)))
                .Handle(new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "race"), default),
            () => UpdateMeeting().Handle(new UpdateMeetingCommand(meeting.Id, new UpdateMeetingRequest(
                "Eşzamanlı başlık", type.Id, meeting.StartAt, meeting.EndAt, meeting.Location, meeting.Description, 1), "race"), default));

        Assert.Equal(200, edit.StatusCode);
        Assert.Equal(200, publish.StatusCode);
        var stored = await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id);
        Assert.Equal(MeetingLifecycle.Completed, stored.Lifecycle);
        Assert.Equal("Eşzamanlı başlık", stored.Title);
        Assert.Equal(3, stored.Version);
        Assert.Equal(MinutesStatus.Published,
            (await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id)).Status);
    }

    /// <summary>The meeting's Completed write is refused: the publish is rolled back with it — the minutes stay a Draft at
    /// the version they were read at, the attendance it would have written is not written, and the answer is 409.</summary>
    [Fact]
    public async Task A_publish_whose_meeting_write_is_refused_is_rolled_back_and_answers_409()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var attendee = Guid.NewGuid();
        await _attendees.CreateAsync(new MeetingAttendee { TenantId = _harness.TenantId, MeetingId = meeting.Id, UserId = attendee });
        var draft = await DraftAsync(meeting.Id,
            attendance: [new MinutesAttendanceRecord { AttendeeUserId = attendee, Status = AttendanceStatus.Present }]);
        var hook = new CallHook();
        hook.Instead(nameof(IMeetingRepository.UpdateAsync), InTransaction, false);

        var publish = await Publish(_minutes, hook.Wrap<IMeetingRepository>(_meetings))
            .Handle(new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "race"), default);

        Assert.Equal(409, publish.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, publish.ReasonCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Equal(MinutesStatus.Draft, stored.Status);
        Assert.Equal(1, stored.Version);
        Assert.Null(stored.PublishedAtUtc);
        Assert.Null((await _attendees.FindAsync(meeting.Id, attendee))!.AttendanceStatus);
        Assert.Equal(MeetingLifecycle.Scheduled, (await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id)).Lifecycle);
    }

    /// <summary>Item 4(b): the meeting is cancelled after the publish read it. A cancelled meeting gets no published minutes
    /// (and never becomes Completed): 409 MEETING_CANCELLED, the minutes stay a Draft.</summary>
    [Fact]
    public async Task A_publish_that_read_the_meeting_before_it_was_cancelled_is_refused_and_the_meeting_stays_cancelled()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id);

        var (cancel, publish) = await RaceAsync(
            () => Publish(_minutes, _barrier.Wrap<IMeetingRepository>(_meetings, nameof(IMeetingRepository.GetByIdAsync)))
                .Handle(new PublishMinutesCommand(meeting.Id, new PublishMinutesRequest(1), "race"), default),
            () => CancelMeeting().Handle(new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Ertelendi", 1), "race"), default));

        Assert.Equal(200, cancel.StatusCode);
        Assert.Equal(409, publish.StatusCode);
        Assert.Equal(MeetingReasonCodes.Cancelled, publish.ReasonCode);
        Assert.Equal(MeetingLifecycle.Cancelled, (await RawAsync<Meeting>(PlatformCollections.MeetingMeetings, meeting.Id)).Lifecycle);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Equal(MinutesStatus.Draft, stored.Status);
        Assert.Equal(1, stored.Version);
    }

    // ── Meeting ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_meeting_edit_that_read_before_a_cancel_is_refused_and_the_meeting_stays_cancelled()
    {
        var type = await TypeAsync();
        var meeting = await MeetingAsync(type.Id);

        var (cancel, update) = await RaceAsync(
            () => UpdateMeeting(_barrier.Wrap<IMeetingRepository>(_meetings, nameof(IMeetingRepository.GetByIdAsync)))
                .Handle(new UpdateMeetingCommand(meeting.Id, new UpdateMeetingRequest(
                    "Güncellenen başlık", type.Id, meeting.StartAt, meeting.EndAt, meeting.Location, meeting.Description, 2), "race"), default),
            () => CancelMeeting().Handle(new CancelMeetingCommand(meeting.Id, new CancelMeetingRequest("Ertelendi", 1), "race"), default));

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

    // ── Agenda order and the task bridge: a write's answer is read (item 7) ─────────────────────────────────────

    /// <summary>A line the reorder writes is refused (it moved under the reorder): 409, never a 200 over a half-applied
    /// order.</summary>
    [Fact]
    public async Task A_reorder_whose_line_write_is_refused_answers_409()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        AgendaItem Line(string text, int order) => new() { TenantId = _harness.TenantId, MeetingId = meeting.Id, Text = text, SortOrder = order };
        var opening = await _agenda.CreateAsync(Line("Açılış", 0));
        var budget = await _agenda.CreateAsync(Line("Bütçe", 1));
        var closing = await _agenda.CreateAsync(Line("Kapanış", 2));
        var hook = new CallHook();
        hook.Instead(nameof(IAgendaItemRepository.UpdateAsync), args => CallHook.Arg<AgendaItem>(args)?.Id == opening.Id, false);

        var reorder = await new ReorderAgendaHandler(_meetings, hook.Wrap<IAgendaItemRepository>(_agenda))
            .Handle(new ReorderAgendaCommand(meeting.Id, new ReorderAgendaRequest([closing.Id, budget.Id, opening.Id]), "race"), default);

        Assert.Equal(409, reorder.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, reorder.ReasonCode);
        Assert.Equal(0, (await RawAsync<AgendaItem>(PlatformCollections.MeetingAgendaItems, opening.Id)).SortOrder);
    }

    /// <summary>Someone edits the agenda line between the bridge's read and its link write: the link is attached to the line
    /// as it is NOW (the edit stays), instead of being silently lost.</summary>
    [Fact]
    public async Task A_task_link_whose_agenda_line_moved_meanwhile_lands_on_the_line_as_it_is_now()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var line = await _agenda.CreateAsync(new AgendaItem { TenantId = _harness.TenantId, MeetingId = meeting.Id, Text = "Bütçe", SortOrder = 0 });
        var hook = new CallHook();
        hook.Before(nameof(IAgendaItemRepository.UpdateAsync), args => CallHook.Arg<AgendaItem>(args)?.Id == line.Id,
            () => _harness.Database.GetCollection<AgendaItem>(PlatformCollections.MeetingAgendaItems).UpdateOneAsync(
                x => x.Id == line.Id, Builders<AgendaItem>.Update.Set(x => x.Text, "Bütçe (düzeltildi)").Inc(x => x.Version, 1)));

        var created = await Bridge(agenda: hook.Wrap<IAgendaItemRepository>(_agenda)).Handle(new CreateTaskFromMeetingCommand(meeting.Id,
            new CreateTaskFromMeetingRequest("Bütçe taslağı", null, null, null, line.Id, null, Guid.NewGuid().ToString("N")), "race"), default);

        Assert.Equal(201, created.StatusCode);
        var stored = await RawAsync<AgendaItem>(PlatformCollections.MeetingAgendaItems, line.Id);
        Assert.Equal(created.Data!.RecordLinkId, stored.RecordLinkId);
        Assert.Equal("Bütçe (düzeltildi)", stored.Text);
    }

    /// <summary>The minutes draft moves between the bridge's read and its decision-link write: the link lands on the
    /// decision in the draft as it is NOW.</summary>
    [Fact]
    public async Task A_task_link_whose_minutes_draft_moved_meanwhile_lands_on_the_decision_as_it_is_now()
    {
        var meeting = await MeetingAsync((await TypeAsync()).Id);
        var draft = await DraftAsync(meeting.Id, decisions: [new MinutesDecision { Code = "D-1", Text = "Bütçe onaylandı" }]);
        var hook = new CallHook();
        hook.Before(nameof(IMeetingMinutesVersionRepository.UpdateAsync), args => CallHook.Arg<MeetingMinutesVersion>(args)?.Id == draft.Id,
            () => _harness.Database.GetCollection<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions).UpdateOneAsync(
                x => x.Id == draft.Id, Builders<MeetingMinutesVersion>.Update.Inc(x => x.Version, 1)));

        var created = await Bridge(minutes: hook.Wrap<IMeetingMinutesVersionRepository>(_minutes)).Handle(new CreateTaskFromMeetingCommand(meeting.Id,
            new CreateTaskFromMeetingRequest("Bütçe görevi", null, null, null, null, null, Guid.NewGuid().ToString("N"), DecisionCode: "D-1"),
            "race"), default);

        Assert.Equal(201, created.StatusCode);
        var stored = await RawAsync<MeetingMinutesVersion>(PlatformCollections.MeetingMinutesVersions, draft.Id);
        Assert.Equal(created.Data!.RecordLinkId, Assert.Single(stored.Decisions).RecordLinkId);
        Assert.Contains(created.Data.RecordLinkId, stored.ActionReferences);
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

    /// <summary>Answers ONLY <see cref="CreateTaskItemCommand"/>: a new task id, as MOD-0024's create would — the bridge's
    /// own writes are what these tests watch.</summary>
    private sealed class TaskCreatingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is CreateTaskItemCommand create
                ? (Task<TResponse>)(object)Task.FromResult(Response<Guid>.Success(Guid.NewGuid(), 201, create.CorrelationId))
                : throw new NotSupportedException($"TaskCreatingMediator does not support {request.GetType().Name}.");

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
