using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S7 (K6) — <see cref="MeetingRepository.FindByFollowUpOfMeetingIdAsync"/> and the carry-forward
/// round trip (<see cref="AgendaItem.CarriedFromMeetingId"/>, a NEW field this slice adds) against a REAL
/// MongoDB, not the in-memory fakes every handler test in this folder is built on. Neither fake can prove:
///  - the real query actually sorts by <c>CreatedAt</c> ascending to pick the EARLIEST follow-up when more
///    than one exists (a LINQ <c>OrderBy</c> over an in-memory list would pass even if the real Mongo query
///    had no sort at all);
///  - <see cref="AgendaItem.CarriedFromMeetingId"/> — a field added in this same slice — actually round-trips
///    through the real BSON mapping rather than being silently dropped by an unmapped-field default;
///  - the tenant execution filter really reaches the driver for both the forward and reverse cross-link reads.
/// Shared-database pattern reused from <c>MongoIntegrationHarness</c>, exactly as
/// <c>MeetingMinutesVersionMongoTests</c>/<c>RecordLinkRepositoryMongoTests</c> already establish for this module.
/// </summary>
public sealed class MeetingFollowUpMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _meetings = null!;
    private AgendaItemRepository _agendaItems = null!;
    private RecordLinkRepository _links = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _meetings = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
        _agendaItems = new AgendaItemRepository(_harness.DbContext, _harness.TenantContext);
        _links = new RecordLinkRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        await _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings)
            .DeleteManyAsync(Builders<Meeting>.Filter.Eq(x => x.TenantId, _harness.TenantId));
        await _harness.Database.GetCollection<AgendaItem>(PlatformCollections.MeetingAgendaItems)
            .DeleteManyAsync(Builders<AgendaItem>.Filter.Eq(x => x.TenantId, _harness.TenantId));
        await _harness.Database.GetCollection<RecordLink>(PlatformCollections.MeetingRecordLinks)
            .DeleteManyAsync(Builders<RecordLink>.Filter.Eq(x => x.TenantId, _harness.TenantId));
        await _harness.DisposeAsync();
    }

    private Meeting NewMeeting(string title, Guid? followUpOf = null, string idempotencyKey = "k") => new()
    {
        Id = Guid.NewGuid(), TenantId = _harness.TenantId, Title = title, MeetingTypeId = Guid.NewGuid(),
        StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
        OrganizerUserId = Guid.NewGuid(), FollowUpOfMeetingId = followUpOf,
        IdempotencyKey = idempotencyKey, CreatedBy = "test"
    };

    [Fact]
    public async Task FindByFollowUpOfMeetingId_returns_null_when_no_meeting_follows_up_on_it()
    {
        var source = await _meetings.CreateAsync(NewMeeting("Kaynak", idempotencyKey: "k1"));

        Assert.Null(await _meetings.FindByFollowUpOfMeetingIdAsync(source.Id));
    }

    [Fact]
    public async Task FindByFollowUpOfMeetingId_returns_the_EARLIEST_follow_up_when_more_than_one_exists()
    {
        var source = await _meetings.CreateAsync(NewMeeting("Kaynak", idempotencyKey: "k2"));
        var earlier = await _meetings.CreateAsync(NewMeeting("Devam 1", source.Id, "k3"));
        await Task.Delay(15); // CreatedAt granularity — force a real, observable ordering gap
        var later = await _meetings.CreateAsync(NewMeeting("Devam 2", source.Id, "k4"));

        var found = await _meetings.FindByFollowUpOfMeetingIdAsync(source.Id);

        Assert.NotNull(found);
        Assert.Equal(earlier.Id, found!.Id);
        Assert.NotEqual(later.Id, found.Id);
    }

    [Fact]
    public async Task Another_tenants_context_cannot_see_this_tenants_follow_up_cross_link()
    {
        var source = await _meetings.CreateAsync(NewMeeting("Kaynak", idempotencyKey: "k5"));
        await _meetings.CreateAsync(NewMeeting("Devam", source.Id, "k6"));

        var otherTenantId = Guid.NewGuid();
        _harness.TenantContext.SetTenant(otherTenantId);
        try
        {
            Assert.Null(await _meetings.FindByFollowUpOfMeetingIdAsync(source.Id));
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }

    /// <summary>The carry-forward write shape end to end: a NEW agenda line per still-open source task, each
    /// with its OWN <see cref="RecordLink"/> and <see cref="AgendaItem.CarriedFromMeetingId"/> set to the
    /// source — read back through the SAME real repositories <c>ScheduleFollowUpMeetingHandler</c> uses, in
    /// <see cref="AgendaItem.SortOrder"/> order.</summary>
    [Fact]
    public async Task Carried_agenda_items_round_trip_with_their_own_RecordLink_and_CarriedFromMeetingId()
    {
        var source = await _meetings.CreateAsync(NewMeeting("Kaynak", idempotencyKey: "k7"));
        var target = await _meetings.CreateAsync(NewMeeting("Devam", source.Id, "k8"));
        var taskA = Guid.NewGuid();
        var taskB = Guid.NewGuid();

        var linkA = await _links.CreateAsync(new RecordLink
        {
            TenantId = _harness.TenantId, SourceModuleCode = RecordLinkModuleCodes.Meetings, SourceRecordId = target.Id,
            TargetModuleCode = RecordLinkModuleCodes.Tasks, TargetRecordId = taskA, LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = Guid.NewGuid(), CreatedBy = "test"
        });
        var linkB = await _links.CreateAsync(new RecordLink
        {
            TenantId = _harness.TenantId, SourceModuleCode = RecordLinkModuleCodes.Meetings, SourceRecordId = target.Id,
            TargetModuleCode = RecordLinkModuleCodes.Tasks, TargetRecordId = taskB, LinkType = RecordLinkTypes.Agenda,
            CreatedByUserId = Guid.NewGuid(), CreatedBy = "test"
        });

        await _agendaItems.CreateAsync(new AgendaItem
        {
            TenantId = _harness.TenantId, MeetingId = target.Id, Text = "Görev A", SortOrder = 0,
            RecordLinkId = linkA.Id, CarriedFromMeetingId = source.Id, CreatedBy = "test"
        });
        await _agendaItems.CreateAsync(new AgendaItem
        {
            TenantId = _harness.TenantId, MeetingId = target.Id, Text = "Görev B", SortOrder = 1,
            RecordLinkId = linkB.Id, CarriedFromMeetingId = source.Id, CreatedBy = "test"
        });

        var read = await _agendaItems.ListByMeetingIdAsync(target.Id);

        Assert.Equal(2, read.Count);
        Assert.Equal("Görev A", read[0].Text);
        Assert.Equal(source.Id, read[0].CarriedFromMeetingId);
        Assert.Equal(linkA.Id, read[0].RecordLinkId);
        Assert.Equal("Görev B", read[1].Text);
        Assert.Equal(source.Id, read[1].CarriedFromMeetingId);
        Assert.Equal(linkB.Id, read[1].RecordLinkId);
    }

    /// <summary>A manually typed line (never carried) has <see cref="AgendaItem.CarriedFromMeetingId"/> null —
    /// the field is not defaulted to the meeting's own <c>FollowUpOfMeetingId</c> or anything else that would
    /// make an ordinary line look carried.</summary>
    [Fact]
    public async Task A_plain_agenda_item_has_no_CarriedFromMeetingId()
    {
        var target = await _meetings.CreateAsync(NewMeeting("Devam", idempotencyKey: "k9"));

        await _agendaItems.CreateAsync(new AgendaItem
        {
            TenantId = _harness.TenantId, MeetingId = target.Id, Text = "Elle yazılan", SortOrder = 0, CreatedBy = "test"
        });

        var read = await _agendaItems.ListByMeetingIdAsync(target.Id);

        Assert.Null(Assert.Single(read).CarriedFromMeetingId);
    }
}
