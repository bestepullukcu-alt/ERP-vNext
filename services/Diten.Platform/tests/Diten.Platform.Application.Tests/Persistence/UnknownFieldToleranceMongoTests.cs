using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-384 — a document written by a NEWER build reads back through the production repository of this build,
/// against a REAL MongoDB.
///
/// <para><b>The failure.</b> 2026-09-13: an older Platform build read <c>meeting_record_links</c> rows written by
/// a newer one and threw <c>FormatException: Element 'IdempotencyKey' does not match any field</c>. A rollback puts
/// exactly that older build in front of newer data.</para>
///
/// <para><b>How the "newer build" is simulated.</b> The entity is serialized with this build's class map, an element
/// this build does not know is added to the raw <see cref="BsonDocument"/>, and the document is inserted with the
/// raw driver. The read goes through the production repository, so the class map that has to tolerate the element
/// is the one production uses.</para>
///
/// <para><b>Sabotage.</b> Remove the <c>ConventionRegistry.Register</c> call from
/// <c>PlatformBsonConventions.Register</c> and both round-trip tests fail with that FormatException. The test
/// assembly's <c>[ModuleInitializer]</c> calls that production method; it does not keep its own copy.</para>
///
/// <para>Isolation follows <see cref="MongoIntegrationHarness"/>: one shared database, a fresh TenantId per test
/// (DB-010). The rows are hard-deleted by that TenantId on dispose, because the shared database is never dropped.</para>
/// </summary>
public sealed class UnknownFieldToleranceMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings, SchemaProfile.WorkflowWorkCenter);
    }

    public async Task DisposeAsync()
    {
        var mine = Builders<BsonDocument>.Filter.Eq(
            "TenantId", new BsonBinaryData(_harness.TenantId, GuidRepresentation.Standard));
        await Raw(PlatformCollections.MeetingRecordLinks).DeleteManyAsync(mine);
        await Raw(PlatformCollections.TaskItems).DeleteManyAsync(mine);
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task A_record_link_carrying_an_unknown_element_reads_back_through_RecordLinkRepository()
    {
        var link = new RecordLink
        {
            TenantId = _harness.TenantId,
            SourceModuleCode = RecordLinkModuleCodes.Meetings,
            SourceRecordId = Guid.NewGuid(),
            TargetModuleCode = RecordLinkModuleCodes.Tasks,
            TargetRecordId = Guid.NewGuid(),
            LinkType = RecordLinkTypes.Preparation,
            CreatedByUserId = Guid.NewGuid()
        };

        var document = link.ToBsonDocument();
        document["IdempotencyKey"] = $"bl384-{link.Id:N}"; // the element measured in the 2026-09-13 crash
        document["AddedByALaterRelease"] = new BsonDocument { { "Shape", "nested" }, { "Depth", 2 } };
        await Raw(PlatformCollections.MeetingRecordLinks).InsertOneAsync(document);

        await AssertStoredWithAsync(PlatformCollections.MeetingRecordLinks, link.Id, "IdempotencyKey");

        var repository = new RecordLinkRepository(_harness.DbContext, _harness.TenantContext);

        var byId = await repository.GetByIdAsync(link.Id);
        Assert.NotNull(byId);
        Assert.Equal(link.TargetRecordId, byId!.TargetRecordId);

        var found = await repository.FindAsync(
            link.SourceModuleCode, link.SourceRecordId,
            link.TargetModuleCode, link.TargetRecordId,
            link.LinkType);
        Assert.Equal(link.Id, found?.Id);

        Assert.Equal(link.Id, Assert.Single(await repository.ListBySourceAsync([link.SourceRecordId])).Id);
    }

    [Fact]
    public async Task A_task_item_carrying_unknown_elements_at_the_top_and_in_an_embedded_value_reads_back_through_TaskItemRepository()
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.TenantId,
            Title = "BL-384 rollback read",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            OrganizationUnitId = Guid.NewGuid(),
            FieldValues =
            [
                new TaskFieldValue { DefinitionCode = "BL384", ValueType = TaskFieldValueType.Text, Value = "kept" }
            ]
        };

        var document = task.ToBsonDocument();
        document["IdempotencyKey"] = $"bl384-{task.Id:N}";
        // An embedded type has its own class map; a convention that reached only the root would still crash here.
        document["FieldValues"][0].AsBsonDocument["AddedByALaterRelease"] = true;
        await Raw(PlatformCollections.TaskItems).InsertOneAsync(document);

        await AssertStoredWithAsync(PlatformCollections.TaskItems, task.Id, "IdempotencyKey");

        var repository = new TaskItemRepository(
            _harness.DbContext,
            _harness.TenantContext,
            new TaskTransitionRepository(_harness.DbContext, _harness.TenantContext));

        var byId = await repository.GetByIdAsync(task.Id);
        Assert.NotNull(byId);
        Assert.Equal(task.Title, byId!.Title);
        Assert.Equal("kept", Assert.Single(byId.FieldValues).Value);

        Assert.Equal(task.Id, Assert.Single(await repository.ListByIdsAsync([task.Id])).Id);
    }

    [Fact]
    public void An_explicit_attribute_still_opts_a_type_out()
    {
        // The escape hatch PlatformBsonConventions documents: a type that must reject unknown elements says so,
        // and the attribute wins over the process-wide default.
        var withUnknown = new BsonDocument { { "Name", "n" }, { "Unknown", 1 } };

        Assert.Equal("n", BsonSerializer.Deserialize<TolerantProbe>(withUnknown).Name);

        var rejected = Assert.Throws<FormatException>(() => BsonSerializer.Deserialize<StrictProbe>(withUnknown));
        Assert.Contains("Unknown", rejected.Message);
    }

    private IMongoCollection<BsonDocument> Raw(string collectionName)
        => _harness.Database.GetCollection<BsonDocument>(collectionName);

    /// <summary>Non-vacuity: the unknown element really is on disk, so a green read is not a read of a clean row.</summary>
    private async Task AssertStoredWithAsync(string collectionName, Guid id, string element)
    {
        var stored = await Raw(collectionName)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .FirstOrDefaultAsync();
        Assert.NotNull(stored);
        Assert.True(stored!.Contains(element), $"'{element}' was not stored on {collectionName}/{id}.");
    }

    private sealed class TolerantProbe
    {
        public string Name { get; set; } = string.Empty;
    }

    [BsonIgnoreExtraElements(false)]
    private sealed class StrictProbe
    {
        public string Name { get; set; } = string.Empty;
    }
}
