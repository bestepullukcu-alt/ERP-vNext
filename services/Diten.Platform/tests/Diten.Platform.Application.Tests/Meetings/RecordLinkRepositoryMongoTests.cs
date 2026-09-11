using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S1 — <see cref="RecordLinkRepository"/> against a REAL MongoDB, not the in-memory
/// <c>FakeRecordLinkRepository</c> that every other test in this folder (<see cref="MeetingTestDoubles"/>,
/// <see cref="RecordLinkServiceTests"/>) is built on. The fake reproduces the unique-index SHAPE in memory
/// (see its own doc comment) but cannot prove any of the following, because none of it runs a real Mongo
/// query:
///  - the tenant execution filter a <c>TenantRepository{T}</c> applies actually reaches the driver, in BOTH
///    list directions;
///  - <c>PlatformSchemaManifest.Meetings.cs</c>'s unique compound index is really built, in this shape, in the
///    shared test database;
///  - the soft-delete path — <c>IsDeleted</c> joining the unique key — really lets a re-created link past a
///    still-present archived row instead of colliding with it;
///  - <c>FindOrCreateAsync</c>'s <c>catch (MongoWriteException ...)</c> branch is reachable at all, rather than
///    dead code the fake's plain <see cref="InvalidOperationException"/> stand-in never actually exercises.
///
/// Shared-database pattern reused from <c>MongoIntegrationHarness</c> (see its own doc comment and
/// <c>WorkingCalendarCodeUniquenessMongoTests</c> for a sibling repository test built the same way): ONE
/// shared database (<see cref="MongoIntegrationHarness.SharedDatabaseName"/>), isolation by a fresh
/// <see cref="Guid"/> <c>TenantId</c> per harness instance rather than a database per run — the shape
/// <c>MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun</c> forbids adding to this suite. Schema is
/// built once per process via <c>PlatformSchemaManifest.ApplyAsync(SchemaProfile.Meetings)</c> — the same
/// index models <c>PlatformSchemaManifest.Meetings.cs</c> declares for production, never a hand-rolled second
/// definition.
/// </summary>
public sealed class RecordLinkRepositoryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private RecordLinkRepository _repository = null!;

    /// <summary>A second tenant for the isolation test. Always distinct from the harness's own
    /// <see cref="MongoIntegrationHarness.TenantId"/>, which is a freshly generated <see cref="Guid"/> too.</summary>
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new RecordLinkRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync()
    {
        // Hard delete, not soft delete — this shared database is never dropped (CreateAsync never drops on
        // dispose; sharing it without dropping is the whole point), so a test that only soft-deletes its rows
        // would leave them behind forever. Filtered on the raw collection (no tenant filter baked in), scoped
        // to the two TenantIds this class instance ever used, so it can never touch another test's rows —
        // this class's own "test-run marker" is its TenantId, per the harness's own isolation model.
        await RawCollection.DeleteManyAsync(
            Builders<RecordLink>.Filter.In(x => x.TenantId, new[] { _harness.TenantId, _otherTenantId }));
        await _harness.DisposeAsync();
    }

    private IMongoCollection<RecordLink> RawCollection
        => _harness.Database.GetCollection<RecordLink>(PlatformCollections.MeetingRecordLinks);

    /// <summary>The six identity values a test cares about, defaulted to a source=meeting/target=task shape
    /// (matching <c>RecordLinkServiceTests</c>' own <c>Meeting()</c>/<c>Task_()</c> convention). Every path
    /// except the raw-driver insert in <see cref="FindOrCreateAsync_returns_the_existing_document_when_one_already_exists_outside_its_own_FindAsync_call"/>
    /// goes through <c>TenantRepository{T}.CreateAsync</c>, which overwrites <see cref="RecordLink.TenantId"/>
    /// from the ambient <c>TenantContext</c> regardless of what is set here.</summary>
    private RecordLink Candidate(
        Guid sourceRecordId,
        Guid targetRecordId,
        string linkType = RecordLinkTypes.Preparation,
        string sourceModuleCode = RecordLinkModuleCodes.Meetings,
        string targetModuleCode = RecordLinkModuleCodes.Tasks,
        Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? _harness.TenantId,
        SourceModuleCode = sourceModuleCode,
        SourceRecordId = sourceRecordId,
        TargetModuleCode = targetModuleCode,
        TargetRecordId = targetRecordId,
        LinkType = linkType,
        CreatedByUserId = Guid.NewGuid()
    };

    private async Task<long> LiveCountAsync(Guid sourceId, Guid targetId) => await RawCollection.CountDocumentsAsync(
        Builders<RecordLink>.Filter.And(
            Builders<RecordLink>.Filter.Eq(x => x.TenantId, _harness.TenantId),
            Builders<RecordLink>.Filter.Eq(x => x.SourceRecordId, sourceId),
            Builders<RecordLink>.Filter.Eq(x => x.TargetRecordId, targetId),
            Builders<RecordLink>.Filter.Eq(x => x.IsDeleted, false)));

    // ── 1. FindOrCreateAsync is idempotent ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateAsync_returns_the_SAME_document_for_the_SAME_six_values_called_twice()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var first = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));
        var second = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await LiveCountAsync(sourceId, targetId));
    }

    // ── 2. A different LinkType is a different fact, not a duplicate ────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateAsync_writes_a_SECOND_document_when_only_LinkType_differs()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var preparation = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId, RecordLinkTypes.Preparation));
        var bornFromMeeting = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId, RecordLinkTypes.BornFromMeeting));

        Assert.NotEqual(preparation.Id, bornFromMeeting.Id);
        Assert.Equal(2, await LiveCountAsync(sourceId, targetId));
    }

    // ── 3. Both directions, including a record that is both a source and a target ──────────────────────────

    [Fact]
    public async Task ListBySourceAsync_and_ListByTargetAsync_each_find_the_SAME_link_from_their_own_side()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var created = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));

        var bySource = await _repository.ListBySourceAsync([sourceId]);
        var byTarget = await _repository.ListByTargetAsync([targetId]);

        Assert.Equal(created.Id, Assert.Single(bySource).Id);
        Assert.Equal(created.Id, Assert.Single(byTarget).Id);
    }

    [Fact]
    public async Task A_record_that_is_TARGET_of_one_link_and_SOURCE_of_another_is_found_by_both_calls()
    {
        var meetingId = Guid.NewGuid();
        var taskId = Guid.NewGuid(); // TARGET of `incoming` (meeting -> task), SOURCE of `outgoing` (task -> meeting)
        var otherMeetingId = Guid.NewGuid();

        var incoming = await _repository.FindOrCreateAsync(Candidate(
            meetingId, taskId, RecordLinkTypes.Agenda, RecordLinkModuleCodes.Meetings, RecordLinkModuleCodes.Tasks));
        var outgoing = await _repository.FindOrCreateAsync(Candidate(
            taskId, otherMeetingId, RecordLinkTypes.BornFromMeeting, RecordLinkModuleCodes.Tasks, RecordLinkModuleCodes.Meetings));

        var asTarget = await _repository.ListByTargetAsync([taskId]);
        var asSource = await _repository.ListBySourceAsync([taskId]);

        Assert.Equal(incoming.Id, Assert.Single(asTarget).Id);
        Assert.Equal(outgoing.Id, Assert.Single(asSource).Id);
    }

    [Fact]
    public async Task ListBySourceAsync_and_ListByTargetAsync_batch_N_ids_into_ONE_call()
    {
        var pairs = Enumerable.Range(0, 3)
            .Select(_ => (Source: Guid.NewGuid(), Target: Guid.NewGuid()))
            .ToArray();

        foreach (var (source, target) in pairs)
        {
            await _repository.FindOrCreateAsync(Candidate(source, target));
        }

        var bySource = await _repository.ListBySourceAsync(pairs.Select(p => p.Source).ToArray());
        var byTarget = await _repository.ListByTargetAsync(pairs.Select(p => p.Target).ToArray());

        Assert.Equal(3, bySource.Count);
        Assert.Equal(3, byTarget.Count);
    }

    // ── 4. Tenant isolation ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_link_written_under_one_tenant_is_invisible_from_another_tenant_in_both_directions()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));

        _harness.TenantContext.SetTenant(_otherTenantId);
        Assert.Empty(await _repository.ListBySourceAsync([sourceId]));
        Assert.Empty(await _repository.ListByTargetAsync([targetId]));

        // Non-vacuity: switching back must see it again. Without this, the two empty asserts above would pass
        // just as well for a repository that is broken outright as for one that genuinely isolates by tenant.
        _harness.TenantContext.SetTenant(_harness.TenantId);
        Assert.Single(await _repository.ListBySourceAsync([sourceId]));
        Assert.Single(await _repository.ListByTargetAsync([targetId]));
    }

    // ── 5. Soft delete ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task After_soft_delete_the_link_is_hidden_from_both_lists_and_FindOrCreateAsync_makes_a_NEW_document()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var original = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));

        await _repository.DeleteAsync(original.Id);

        // MOD-0357 S2 fix — DeletedAt is a field RecordLink declares specifically for an auditable "when"
        // (its own doc comment), and the base TenantRepository<T>.DeleteAsync this class used to inherit only
        // ever set IsDeleted/UpdatedAt. Read straight off the raw collection so this proves the STORED
        // document, not a value the repository merely echoes back.
        var stored = await RawCollection.Find(Builders<RecordLink>.Filter.Eq(x => x.Id, original.Id)).SingleAsync();
        Assert.NotNull(stored.DeletedAt);

        Assert.Empty(await _repository.ListBySourceAsync([sourceId]));
        Assert.Empty(await _repository.ListByTargetAsync([targetId]));

        // This repository CREATES a new document rather than resurrecting the old one: `IsDeleted` is part of
        // the unique compound index (PlatformSchemaManifest.Meetings.cs, "ux_meeting_record_links_tenant_
        // source_target_type"), so the archived row and a fresh live row for the same six values do not
        // collide — the same reasoning WorkingCalendar's archived-code release already relies on.
        var resurrection = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId));
        Assert.NotEqual(original.Id, resurrection.Id);

        var live = await _repository.ListBySourceAsync([sourceId]);
        Assert.Equal(resurrection.Id, Assert.Single(live).Id);
    }

    // ── 6. Duplicate-key race / unique index ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateAsync_returns_the_existing_document_when_one_already_exists_outside_its_own_FindAsync_call()
    {
        // Fail loudly, not quietly pass: if the unique index is missing from the shared test database, this
        // test's premise — that Mongo itself would refuse a second insert of these six values — does not
        // hold, and nothing asserted below would mean what it appears to mean.
        var indexes = await RawCollection.Indexes.List().ToListAsync();
        var uniqueIndex = indexes.SingleOrDefault(
            i => i.Contains("name") && i["name"].AsString == "ux_meeting_record_links_tenant_source_target_type");
        Assert.True(
            uniqueIndex is not null,
            "Expected the unique compound index declared in PlatformSchemaManifest.Meetings.cs "
            + $"('ux_meeting_record_links_tenant_source_target_type') on '{PlatformCollections.MeetingRecordLinks}' "
            + "— it was not found in the shared test database.");
        Assert.True(
            uniqueIndex!.Contains("unique") && uniqueIndex["unique"].AsBoolean,
            "The index exists but is not declared unique — the duplicate-key guarantee this test relies on does not hold.");

        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Inserted through the RAW collection, bypassing the repository entirely — this document exists in
        // storage BEFORE FindOrCreateAsync's own internal FindAsync call ever runs, standing in for "another
        // request already won the race" without needing real concurrency.
        var preExisting = Candidate(sourceId, targetId);
        await RawCollection.InsertOneAsync(preExisting);

        RecordLink? result = null;
        var exception = await Record.ExceptionAsync(async ()
            => result = await _repository.FindOrCreateAsync(Candidate(sourceId, targetId)));

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Equal(preExisting.Id, result!.Id);
        Assert.Equal(1, await LiveCountAsync(sourceId, targetId));
    }
}
