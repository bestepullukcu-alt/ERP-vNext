using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b D8 / T-16 — meeting suggestions: only the person's ACCEPTED, ended meetings are offered, at their
/// scheduled length rounded to 15; nothing becomes time without "I attended"; the minutes' CURRENT word decides the badge
/// (confirmed / withdrawn / conflict) on every read, so a minutes correction is picked up without anything stored.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeSuggestionHttpMongoTests : TimerScenario
{
    private const string MeetingCategory = "INTERNAL_MEETING";

    public TimeSuggestionHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedCategoryAsync(Tenant, MeetingCategory);
    }

    /// <summary>A meeting on Tuesday 2026-10-06, 10:00–10:50 Istanbul (ended before the Wednesday clock).</summary>
    private async Task<Guid> SeedMeetingAsync(
        InvitationResponse response, AttendanceStatus? attendance = null, int startHour = 10, int minutes = 50,
        MeetingLifecycle lifecycle = MeetingLifecycle.Scheduled, int day = 6, Guid? tenant = null)
    {
        var id = Guid.NewGuid();
        var start = IstanbulLocal(2026, 10, day, startHour, 0);
        await Collection<Meeting>(PlatformCollections.MeetingMeetings).InsertOneAsync(new Meeting
        {
            Id = id,
            TenantId = tenant ?? Tenant,
            Title = "Weekly sync",
            MeetingTypeId = Guid.NewGuid(),
            StartAt = start,
            EndAt = start.AddMinutes(minutes),
            OrganizerUserId = Manager,
            IdempotencyKey = "k-" + id.ToString("N"),
            Lifecycle = lifecycle
        });
        await Collection<MeetingAttendee>(PlatformCollections.MeetingAttendees).InsertOneAsync(new MeetingAttendee
        {
            TenantId = tenant ?? Tenant, MeetingId = id, UserId = Person, InvitationResponse = response, AttendanceStatus = attendance
        });
        return id;
    }

    private Task SetAttendanceAsync(Guid meetingId, AttendanceStatus? status)
        => Collection<MeetingAttendee>(PlatformCollections.MeetingAttendees).UpdateOneAsync(
            a => a.MeetingId == meetingId && a.UserId == Person,
            Builders<MeetingAttendee>.Update.Set(a => a.AttendanceStatus, status));

    private async Task<List<JsonElement>> SuggestionsAsync()
    {
        var week = await GetWeekAsync();
        Ok(week);
        return week.Data.GetProperty("suggestions").EnumerateArray().ToList();
    }

    private async Task<ApiResult> AcceptAsync(Guid suggestionId, object? body = null)
        => await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/suggestions/{suggestionId}/accept", PersonToken(),
            body ?? new { expectedVersion = await VersionAsync() });

    [Fact]
    public async Task Only_accepted_ended_uncancelled_meetings_are_suggested_and_reading_writes_nothing()
    {
        var accepted = await SeedMeetingAsync(InvitationResponse.Accepted);
        await SeedMeetingAsync(InvitationResponse.Pending);
        await SeedMeetingAsync(InvitationResponse.Declined);
        await SeedMeetingAsync(InvitationResponse.Accepted, lifecycle: MeetingLifecycle.Cancelled);
        await SeedMeetingAsync(InvitationResponse.Accepted, startHour: 13, day: 7); // today 13:00 — not ended at 12:00
        await SeedMeetingAsync(InvitationResponse.Accepted, tenant: OtherTenant);

        var first = await SuggestionsAsync();
        var again = await SuggestionsAsync();

        var suggestion = Assert.Single(first);
        Assert.Equal(accepted, suggestion.GetProperty("meetingId").GetGuid());
        Assert.Equal(45, suggestion.GetProperty("proposedMinutes").GetInt32()); // 50 → nearest 15
        Assert.Equal("2026-10-06", suggestion.GetProperty("localDate").GetString());
        Assert.Equal("Open", suggestion.GetProperty("state").GetString());
        Assert.Equal(suggestion.GetProperty("id").GetGuid(), Assert.Single(again).GetProperty("id").GetGuid()); // stable id

        Assert.Equal(0, await Collection<TimeSuggestion>(PlatformCollections.TimeEntrySuggestions).CountDocumentsAsync(s => s.TenantId == Tenant));
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task Accepting_writes_a_meeting_row_to_the_draft_and_a_second_decision_is_refused()
    {
        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);
        var id = Assert.Single(await SuggestionsAsync()).GetProperty("id").GetGuid();

        var accepted = await AcceptAsync(id);
        Ok(accepted);

        var row = Assert.Single((await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray());
        Assert.Equal("Meeting", row.GetProperty("source").GetString());
        Assert.Equal(MeetingCategory, row.GetProperty("categoryCode").GetString());
        Assert.Equal(45, row.GetProperty("durationMinutes").GetInt32());
        var stored = Assert.Single(await Collection<TimeSuggestion>(PlatformCollections.TimeEntrySuggestions)
            .Find(s => s.TenantId == Tenant).ToListAsync());
        Assert.Equal((id, meeting), (stored.Id, stored.MeetingId));
        Assert.Single(Host.Audit.Requests, r => r.EntityType == "TimeSuggestion");

        var twice = await AcceptAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, twice.Status);
        Assert.Equal(TimeEntryReasonCodes.SuggestionAlreadyDecided, twice.ReasonCode);
    }

    [Fact]
    public async Task Minutes_saying_absent_withdraw_an_unaccepted_suggestion()
    {
        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);
        var id = Assert.Single(await SuggestionsAsync()).GetProperty("id").GetGuid();

        await SetAttendanceAsync(meeting, AttendanceStatus.Absent);

        Assert.Empty(await SuggestionsAsync());
        var accept = await AcceptAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, accept.Status);
        Assert.Equal(TimeEntryReasonCodes.SuggestionWithdrawn, accept.ReasonCode);
    }

    [Fact]
    public async Task An_accepted_row_the_minutes_contradict_is_flagged_to_the_person_and_a_correction_is_picked_up()
    {
        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);
        var id = Assert.Single(await SuggestionsAsync()).GetProperty("id").GetGuid();
        Ok(await AcceptAsync(id));

        await SetAttendanceAsync(meeting, AttendanceStatus.Excused);
        var flagged = (await GetWeekAsync()).Data;
        Assert.True(Assert.Single(flagged.GetProperty("entries").EnumerateArray()).GetProperty("minutesConflict").GetBoolean());
        Assert.Equal("conflict", Assert.Single(flagged.GetProperty("suggestions").EnumerateArray()).GetProperty("minutesStatus").GetString());

        // The minutes are corrected to Present (MOD-0357 re-runs its attendance sync): nothing here stored the badge.
        await SetAttendanceAsync(meeting, AttendanceStatus.Present);
        var corrected = (await GetWeekAsync()).Data;
        Assert.False(Assert.Single(corrected.GetProperty("entries").EnumerateArray()).GetProperty("minutesConflict").GetBoolean());
        Assert.Equal("confirmed", Assert.Single(corrected.GetProperty("suggestions").EnumerateArray()).GetProperty("minutesStatus").GetString());
    }

    [Fact]
    public async Task Dismissing_writes_no_time_and_the_suggestion_is_not_offered_again_as_open()
    {
        await SeedMeetingAsync(InvitationResponse.Accepted);
        var id = Assert.Single(await SuggestionsAsync()).GetProperty("id").GetGuid();

        Ok(await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/suggestions/{id}/dismiss", PersonToken()));

        Assert.Equal("Dismissed", Assert.Single(await SuggestionsAsync()).GetProperty("state").GetString());
        Assert.Empty(await StoredWeeksAsync());
        var accept = await AcceptAsync(id, new { expectedVersion = 0 });
        Assert.Equal(TimeEntryReasonCodes.SuggestionAlreadyDecided, accept.ReasonCode);
    }

    [Fact]
    public async Task Two_meetings_on_one_day_share_one_meeting_row_whose_minutes_are_their_sum()
    {
        await SeedMeetingAsync(InvitationResponse.Accepted, startHour: 10, minutes: 30);
        await SeedMeetingAsync(InvitationResponse.Accepted, startHour: 14, minutes: 60);
        var ids = (await SuggestionsAsync()).Select(s => s.GetProperty("id").GetGuid()).ToList();

        Ok(await AcceptAsync(ids[0]));
        Ok(await AcceptAsync(ids[1]));

        var row = Assert.Single((await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray());
        Assert.Equal(90, row.GetProperty("durationMinutes").GetInt32());
    }
}
