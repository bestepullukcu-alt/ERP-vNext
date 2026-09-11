using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

// MOD-0357 S2, AC11 — one Mongo-backed test class per new repository, the SAME MongoIntegrationHarness pattern
// RecordLinkRepositoryMongoTests (S1) established: ONE shared database, isolation by a fresh Guid TenantId per
// harness instance, schema built once per process via PlatformSchemaManifest.ApplyAsync(SchemaProfile.Meetings)
// — never a database per test class (the shape MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun
// forbids).

public sealed class MeetingRepositoryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingRepository _repository = null!;
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        await RawCollection.DeleteManyAsync(
            Builders<Meeting>.Filter.In(x => x.TenantId, new[] { _harness.TenantId, _otherTenantId }));
        await _harness.DisposeAsync();
    }

    private IMongoCollection<Meeting> RawCollection => _harness.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings);

    private Meeting Candidate(string idempotencyKey, Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _harness.TenantId,
        Title = "Aylık QA Toplantısı",
        MeetingTypeId = Guid.NewGuid(),
        StartAt = DateTimeOffset.UtcNow,
        EndAt = DateTimeOffset.UtcNow.AddHours(1),
        OrganizerUserId = Guid.NewGuid(),
        IdempotencyKey = idempotencyKey
    };

    [Fact]
    public async Task FindOrCreateAsync_returns_the_SAME_document_for_the_SAME_idempotency_key()
    {
        var key = Guid.NewGuid().ToString("N");
        var first = await _repository.FindOrCreateAsync(Candidate(key));
        var second = await _repository.FindOrCreateAsync(Candidate(key));

        Assert.Equal(first.Id, second.Id);
        var count = await RawCollection.CountDocumentsAsync(
            Builders<Meeting>.Filter.And(
                Builders<Meeting>.Filter.Eq(x => x.TenantId, _harness.TenantId),
                Builders<Meeting>.Filter.Eq(x => x.IdempotencyKey, key)));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FindOrCreateAsync_returns_the_existing_document_when_one_already_exists_outside_its_own_check()
    {
        var indexes = await RawCollection.Indexes.List().ToListAsync();
        var uniqueIndex = indexes.SingleOrDefault(
            i => i.Contains("name") && i["name"].AsString == "ux_meeting_meetings_tenant_idempotency_key");
        Assert.True(uniqueIndex is not null, "Expected the unique idempotency-key index declared in PlatformSchemaManifest.Meetings.cs.");
        Assert.True(uniqueIndex!.Contains("unique") && uniqueIndex["unique"].AsBoolean);

        var key = Guid.NewGuid().ToString("N");
        var preExisting = Candidate(key);
        await RawCollection.InsertOneAsync(preExisting);

        var exception = await Record.ExceptionAsync(async () => await _repository.FindOrCreateAsync(Candidate(key)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task UpdateAsync_with_a_stale_ExpectedVersion_returns_false_and_writes_nothing()
    {
        var meeting = await _repository.CreateAsync(Candidate(Guid.NewGuid().ToString("N")));

        meeting.Title = "Değişti";
        var ok = await _repository.UpdateAsync(meeting, expectedVersion: meeting.Version + 5);

        Assert.False(ok);
        var reread = await _repository.GetByIdAsync(meeting.Id);
        Assert.NotEqual("Değişti", reread!.Title);
    }

    [Fact]
    public async Task UpdateAsync_with_the_correct_ExpectedVersion_persists_and_advances_the_version()
    {
        var meeting = await _repository.CreateAsync(Candidate(Guid.NewGuid().ToString("N")));
        var originalVersion = meeting.Version;

        meeting.Title = "Yeni Başlık";
        var ok = await _repository.UpdateAsync(meeting, originalVersion);

        Assert.True(ok);
        var reread = await _repository.GetByIdAsync(meeting.Id);
        Assert.Equal("Yeni Başlık", reread!.Title);
        Assert.Equal(originalVersion + 1, reread.Version);
    }

    [Fact]
    public async Task A_meeting_written_under_one_tenant_is_invisible_from_another_tenant()
    {
        var meeting = await _repository.CreateAsync(Candidate(Guid.NewGuid().ToString("N")));

        _harness.TenantContext.SetTenant(_otherTenantId);
        Assert.Null(await _repository.GetByIdAsync(meeting.Id));
        Assert.Empty(await _repository.ListByIdsAsync([meeting.Id]));

        _harness.TenantContext.SetTenant(_harness.TenantId);
        Assert.NotNull(await _repository.GetByIdAsync(meeting.Id));
    }

    [Fact]
    public async Task AnyByMeetingTypeIdAsync_is_true_only_while_a_live_meeting_references_the_type()
    {
        var typeId = Guid.NewGuid();
        var candidate = Candidate(Guid.NewGuid().ToString("N"));
        candidate.MeetingTypeId = typeId;

        Assert.False(await _repository.AnyByMeetingTypeIdAsync(typeId));

        await _repository.CreateAsync(candidate);
        Assert.True(await _repository.AnyByMeetingTypeIdAsync(typeId));
    }
}

public sealed class MeetingAttendeeRepositoryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingAttendeeRepository _repository = null!;
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        await RawCollection.DeleteManyAsync(
            Builders<MeetingAttendee>.Filter.In(x => x.TenantId, new[] { _harness.TenantId, _otherTenantId }));
        await _harness.DisposeAsync();
    }

    private IMongoCollection<MeetingAttendee> RawCollection => _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees);

    private MeetingAttendee Candidate(Guid meetingId, Guid userId, Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _harness.TenantId,
        MeetingId = meetingId,
        UserId = userId
    };

    [Fact]
    public async Task The_unique_index_on_meeting_and_user_exists_and_refuses_a_duplicate_row()
    {
        var indexes = await RawCollection.Indexes.List().ToListAsync();
        var uniqueIndex = indexes.SingleOrDefault(
            i => i.Contains("name") && i["name"].AsString == "ux_meeting_attendees_tenant_meeting_user");
        Assert.True(uniqueIndex is not null, "Expected the unique (tenant, meeting, user) index declared in PlatformSchemaManifest.Meetings.cs.");
        Assert.True(uniqueIndex!.Contains("unique") && uniqueIndex["unique"].AsBoolean);

        var meetingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _repository.CreateAsync(Candidate(meetingId, userId));

        var exception = await Record.ExceptionAsync(async () => await _repository.CreateAsync(Candidate(meetingId, userId)));
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task After_soft_delete_the_same_meeting_and_user_can_be_re_invited()
    {
        var meetingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var original = await _repository.CreateAsync(Candidate(meetingId, userId));

        await _repository.DeleteAsync(original.Id);
        Assert.Empty(await _repository.ListByMeetingIdAsync(meetingId));

        var recreated = await _repository.CreateAsync(Candidate(meetingId, userId));
        Assert.NotEqual(original.Id, recreated.Id);
        Assert.Single(await _repository.ListByMeetingIdAsync(meetingId));
    }

    [Fact]
    public async Task ListByMeetingIdsAsync_batches_several_meetings_worth_of_attendees_into_ONE_call()
    {
        var meetingA = Guid.NewGuid();
        var meetingB = Guid.NewGuid();
        await _repository.CreateAsync(Candidate(meetingA, Guid.NewGuid()));
        await _repository.CreateAsync(Candidate(meetingB, Guid.NewGuid()));

        var all = await _repository.ListByMeetingIdsAsync([meetingA, meetingB]);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task An_attendee_written_under_one_tenant_is_invisible_from_another_tenant()
    {
        var meetingId = Guid.NewGuid();
        var attendee = await _repository.CreateAsync(Candidate(meetingId, Guid.NewGuid()));

        _harness.TenantContext.SetTenant(_otherTenantId);
        Assert.Empty(await _repository.ListByMeetingIdAsync(meetingId));
        Assert.Null(await _repository.FindAsync(meetingId, attendee.UserId));

        _harness.TenantContext.SetTenant(_harness.TenantId);
        Assert.Single(await _repository.ListByMeetingIdAsync(meetingId));
    }
}

public sealed class AgendaItemRepositoryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private AgendaItemRepository _repository = null!;
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new AgendaItemRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        await RawCollection.DeleteManyAsync(
            Builders<AgendaItem>.Filter.In(x => x.TenantId, new[] { _harness.TenantId, _otherTenantId }));
        await _harness.DisposeAsync();
    }

    private IMongoCollection<AgendaItem> RawCollection => _harness.Database.GetCollection<AgendaItem>(PlatformCollections.MeetingAgendaItems);

    private AgendaItem Candidate(Guid meetingId, int sortOrder, Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _harness.TenantId,
        MeetingId = meetingId,
        Text = "Gündem maddesi",
        SortOrder = sortOrder
    };

    [Fact]
    public async Task ListByMeetingIdAsync_returns_items_ordered_by_SortOrder()
    {
        var meetingId = Guid.NewGuid();
        await _repository.CreateAsync(Candidate(meetingId, 2));
        await _repository.CreateAsync(Candidate(meetingId, 0));
        await _repository.CreateAsync(Candidate(meetingId, 1));

        var items = await _repository.ListByMeetingIdAsync(meetingId);
        Assert.Equal([0, 1, 2], items.Select(x => x.SortOrder));
    }

    [Fact]
    public async Task UpdateAsync_with_a_stale_ExpectedVersion_returns_false()
    {
        var item = await _repository.CreateAsync(Candidate(Guid.NewGuid(), 0));
        item.Text = "Değişti";

        var ok = await _repository.UpdateAsync(item, item.Version + 5);
        Assert.False(ok);
    }

    [Fact]
    public async Task An_agenda_item_written_under_one_tenant_is_invisible_from_another_tenant()
    {
        var meetingId = Guid.NewGuid();
        await _repository.CreateAsync(Candidate(meetingId, 0));

        _harness.TenantContext.SetTenant(_otherTenantId);
        Assert.Empty(await _repository.ListByMeetingIdAsync(meetingId));

        _harness.TenantContext.SetTenant(_harness.TenantId);
        Assert.Single(await _repository.ListByMeetingIdAsync(meetingId));
    }

    [Fact]
    public async Task After_soft_delete_the_item_is_hidden_from_the_list()
    {
        var meetingId = Guid.NewGuid();
        var item = await _repository.CreateAsync(Candidate(meetingId, 0));

        await _repository.DeleteAsync(item.Id);
        Assert.Empty(await _repository.ListByMeetingIdAsync(meetingId));
    }
}

public sealed class MeetingTypeRepositoryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingTypeRepository _repository = null!;
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingTypeRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        await RawCollection.DeleteManyAsync(
            Builders<MeetingType>.Filter.In(x => x.TenantId, new[] { _harness.TenantId, _otherTenantId }));
        await _harness.DisposeAsync();
    }

    private IMongoCollection<MeetingType> RawCollection => _harness.Database.GetCollection<MeetingType>(PlatformCollections.MeetingTypes);

    private MeetingType Candidate(string name, Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _harness.TenantId,
        Name = name
    };

    [Fact]
    public async Task The_unique_index_on_name_exists_and_refuses_a_duplicate()
    {
        var indexes = await RawCollection.Indexes.List().ToListAsync();
        var uniqueIndex = indexes.SingleOrDefault(
            i => i.Contains("name") && i["name"].AsString == "ux_meeting_types_tenant_name");
        Assert.True(uniqueIndex is not null, "Expected the unique (tenant, name) index declared in PlatformSchemaManifest.Meetings.cs.");
        Assert.True(uniqueIndex!.Contains("unique") && uniqueIndex["unique"].AsBoolean);

        var name = "Aylık QA Toplantısı " + Guid.NewGuid();
        await _repository.CreateAsync(Candidate(name));

        var exception = await Record.ExceptionAsync(async () => await _repository.CreateAsync(Candidate(name)));
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task After_soft_delete_the_name_can_be_reused()
    {
        var name = "Tekrar Kullanılabilir " + Guid.NewGuid();
        var original = await _repository.CreateAsync(Candidate(name));

        await _repository.DeleteAsync(original.Id);
        Assert.Null(await _repository.FindByNameAsync(name));

        var recreated = await _repository.CreateAsync(Candidate(name));
        Assert.NotEqual(original.Id, recreated.Id);
    }

    [Fact]
    public async Task UpdateAsync_with_a_stale_ExpectedVersion_returns_false()
    {
        var type = await _repository.CreateAsync(Candidate("Sürüm testi " + Guid.NewGuid()));
        type.IsQualityRecord = true;

        var ok = await _repository.UpdateAsync(type, type.Version + 5);
        Assert.False(ok);
    }

    [Fact]
    public async Task A_type_written_under_one_tenant_is_invisible_from_another_tenant()
    {
        var name = "Kiracı izolasyonu " + Guid.NewGuid();
        await _repository.CreateAsync(Candidate(name));

        _harness.TenantContext.SetTenant(_otherTenantId);
        Assert.Null(await _repository.FindByNameAsync(name));

        _harness.TenantContext.SetTenant(_harness.TenantId);
        Assert.NotNull(await _repository.FindByNameAsync(name));
    }
}
