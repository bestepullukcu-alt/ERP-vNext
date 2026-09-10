using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 — repository persistence against a REAL MongoDB via <see cref="MongoIntegrationHarness"/>.
/// <para>
/// A fake repository would prove nothing here. The two failures this module is exposed to only appear when a
/// real query runs: a Guid that round-trips as the wrong BSON shape silently returns an empty result set, and
/// a partial index whose filter uses <c>$ne</c> makes the service crash-loop at startup rather than failing a
/// test. Both are checked below against the live server.
/// </para>
/// </summary>
public sealed class RepositoryObjectMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private RepositoryObjectRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.DocumentManagement);
        _repository = new RepositoryObjectRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private RepositoryObject NewObject(Guid tenantId, Guid? contentId = null, string objectKey = "tenant/documents/a.txt") => new()
    {
        TenantId = tenantId,
        ContentId = contentId ?? Guid.NewGuid(),
        StorageProvider = "local-filesystem",
        ObjectKey = objectKey,
        Scope = "Documents",
        OwningItemId = Guid.NewGuid(),
        OwningVersionId = Guid.NewGuid(),
        FileName = "a.txt",
        MediaType = "text/plain",
        ByteSize = 3,
        Checksum = "abc",
        CreatedBy = "tester"
    };

    // The Guid round-trip. If the serializer shape were wrong this would pass on write and return null on
    // read — the exact "queries silently return empty" failure the module pack warns about.
    [Fact]
    public async Task Stored_object_round_trips_and_is_queryable_by_content_id()
    {
        var contentId = Guid.NewGuid();
        await _repository.CreateAsync(NewObject(_harness.TenantId, contentId));

        var found = await _repository.GetByContentIdAsync(contentId);

        Assert.NotNull(found);
        Assert.Equal(contentId, found!.ContentId);
        Assert.Equal(_harness.TenantId, found.TenantId);
    }

    // Cross-tenant reads must be INVISIBLE, not merely forbidden: the row of another tenant does not come
    // back at all, which is what lets the service answer 404 instead of a 403 that would confirm existence.
    [Fact]
    public async Task Another_tenants_object_is_invisible_rather_than_forbidden()
    {
        var otherTenant = Guid.NewGuid();
        var contentId = Guid.NewGuid();

        // Written directly, bypassing the tenant-stamping CreateAsync, so the row genuinely belongs elsewhere.
        await _harness.Database
            .GetCollection<RepositoryObject>(PlatformCollections.DocumentRepositoryObjects)
            .InsertOneAsync(NewObject(otherTenant, contentId, "tenant-other/documents/a.txt"));

        var found = await _repository.GetByContentIdAsync(contentId);

        Assert.Null(found);
    }

    [Fact]
    public async Task Objects_are_listed_for_their_owning_item()
    {
        var row = NewObject(_harness.TenantId, objectKey: $"tenant/documents/{Guid.NewGuid():N}.txt");
        await _repository.CreateAsync(row);

        var rows = await _repository.GetByOwningItemAsync(row.OwningItemId);

        Assert.Single(rows);
        Assert.Equal(row.ContentId, rows[0].ContentId);
    }

    // Compensation is a soft mark: the row leaves the live set but is never hard-deleted, because destruction
    // is MOD-0262-FU05's and must be an auditable, hold-verified operation (AD-6).
    [Fact]
    public async Task Compensation_soft_marks_the_row_and_removes_it_from_the_live_set()
    {
        var contentId = Guid.NewGuid();
        await _repository.CreateAsync(NewObject(_harness.TenantId, contentId, $"tenant/documents/{contentId:N}.txt"));

        Assert.True(await _repository.MarkCompensatedAsync(contentId));
        Assert.Null(await _repository.GetByContentIdAsync(contentId));

        // Still physically present in the collection — soft, not hard.
        var raw = await _harness.Database
            .GetCollection<RepositoryObject>(PlatformCollections.DocumentRepositoryObjects)
            .Find(Builders<RepositoryObject>.Filter.Eq(x => x.ContentId, contentId))
            .FirstOrDefaultAsync();

        Assert.NotNull(raw);
        Assert.True(raw!.IsDeleted);
    }

    // The indexes must actually be creatable on the live server. A partial filter using $ne would be rejected
    // here — which is the point: it would otherwise surface as a startup crash-loop in the running fleet.
    [Fact]
    public async Task Declared_indexes_exist_on_the_live_collection()
    {
        var cursor = await _harness.Database
            .GetCollection<RepositoryObject>(PlatformCollections.DocumentRepositoryObjects)
            .Indexes.ListAsync();
        var names = (await cursor.ToListAsync()).Select(i => i["name"].AsString).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ux_document_repository_objects_tenant_content_active", names);
        Assert.Contains("ux_document_repository_objects_tenant_provider_key_active", names);
        Assert.Contains("ix_document_repository_objects_owning_item", names);
        Assert.Contains("ix_document_repository_objects_checksum", names);
    }
}
