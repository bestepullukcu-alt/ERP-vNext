using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// The development platform administrator seed (<see cref="PlatformAdministratorSeed"/>) and the first-administrator record
/// (<see cref="FirstPlatformAdministratorRecord"/>), on the production code over a test-owned replica set.
/// <para>BL-521 FIX2 item 5 measured what the seed did (two skipped tests: every start forced the record back to
/// PlatformAdmin + Active + [SuperAdmin] + not deleted; a changed address stopped the next start on the fixed id). The owner
/// decided (WP-AUTH-ADMIN-BOOTSTRAP-01, BL-534, 2026-10-15): Development only, create when missing, never change an existing
/// record. These are now the rules.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class PlatformAdministratorSeedMeasurementTests
{
    private static readonly Guid SeedId = PlatformAdministratorSeed.DevelopmentAdministratorId;

    [Fact]
    public async Task Outside_development_an_empty_database_gets_no_administrator()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: false, optedIn: true);

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<PlatformAdministrator>.Empty));
    }

    // v1.1 (owner decision 2026-10-15) — DevSeeds:PlatformAdmin is required too: without it Development creates nothing.
    [Fact]
    public async Task In_development_without_the_opt_in_key_an_empty_database_gets_no_administrator()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: false);

        Assert.Equal(0, await Collection(database).CountDocumentsAsync(FilterDefinition<PlatformAdministrator>.Empty));
    }

    [Fact]
    public async Task In_development_an_empty_database_gets_the_development_administrator()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true);

        var record = await Collection(database).Find(x => x.Id == SeedId).SingleAsync();
        Assert.Equal(PlatformAdministratorSeed.DevelopmentAdministratorEmail, record.NormalizedEmail);
        Assert.Equal(AdministratorStatus.Active, record.Status);
        Assert.Equal(new[] { AdministratorRole.SuperAdmin }, record.Roles);
    }

    /// <summary>
    /// Whatever an operator did to the seed record — demoted it to partner, suspended it, stripped its roles, deleted it —
    /// the next start leaves it so (it used to restore it: the account could not be lastingly demoted, suspended or removed).
    /// </summary>
    [Fact]
    public async Task The_next_start_leaves_a_suspended_demoted_or_deleted_seed_administrator_as_it_is()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true);
        var collection = Collection(database);
        await collection.UpdateOneAsync(x => x.Id == SeedId, Builders<PlatformAdministrator>.Update
            .Set(x => x.ActorType, ActorType.PartnerAdmin)
            .Set(x => x.Status, AdministratorStatus.Suspended)
            .Set(x => x.Roles, new List<AdministratorRole> { AdministratorRole.ReadOnly })
            .Set(x => x.IsDeleted, true));
        var before = await Raw(database).Find(ById(SeedId)).SingleAsync();

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true); // the next start

        Assert.Equal(before, await Raw(database).Find(ById(SeedId)).SingleAsync());
        Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<PlatformAdministrator>.Empty));
    }

    /// <summary>
    /// If the seed record's e-mail is changed, the next start finds the record by its fixed id and leaves it: it no longer
    /// inserts a second one with the same id (which failed on the duplicate id and stopped the Platform from starting).
    /// </summary>
    [Fact]
    public async Task After_the_seed_administrators_email_is_changed_the_next_start_neither_fails_nor_adds_a_record()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true);
        var collection = Collection(database);
        await collection.UpdateOneAsync(x => x.Id == SeedId, Builders<PlatformAdministrator>.Update
            .Set(x => x.Email, "renamed.seed@bl521.test")
            .Set(x => x.NormalizedEmail, "renamed.seed@bl521.test"));

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true);

        var only = await collection.Find(FilterDefinition<PlatformAdministrator>.Empty).SingleAsync();
        Assert.Equal("renamed.seed@bl521.test", only.NormalizedEmail);
    }

    /// <summary>A record under the seeded address with another id is never touched, and no second record is added.</summary>
    [Fact]
    public async Task A_record_with_another_id_under_the_seeded_address_is_kept_and_nothing_is_added()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        var otherId = Guid.NewGuid();
        await Collection(database).InsertOneAsync(new PlatformAdministrator
        {
            Id = otherId,
            Email = PlatformAdministratorSeed.DevelopmentAdministratorEmail,
            NormalizedEmail = PlatformAdministratorSeed.DevelopmentAdministratorEmail,
            UserName = "real.admin",
            NormalizedUserName = "real.admin",
            Status = AdministratorStatus.Suspended,
            Roles = [AdministratorRole.ReadOnly]
        });
        var before = await Raw(database).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

        await PlatformAdministratorSeed.EnsureSeededAsync(database, isDevelopment: true, optedIn: true);

        Assert.Equal(before, await Raw(database).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
    }

    // ── the first platform administrator (the command's Platform half) ─────────────────────────────────────────────────

    [Fact]
    public async Task The_first_administrator_record_is_read_by_Platform_as_an_active_super_administrator_and_is_written_once()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        var request = new FirstPlatformAdministratorRecord.Request(Guid.NewGuid(), " First.Admin@Bootstrap.Test ", "first.admin", "First Admin");

        Assert.Null(await FirstPlatformAdministratorRecord.FindBlockerAsync(database, request));
        Assert.True(await FirstPlatformAdministratorRecord.TryCreateAsync(database, request, DateTimeOffset.UtcNow));

        var repository = new PlatformAdministratorRepository(new PlatformDbContext(mongo.Client, database), new TenantContext());
        var record = await repository.GetByNormalizedEmailAsync("first.admin@bootstrap.test");
        Assert.NotNull(record);
        Assert.Equal(request.Id, record!.Id);
        Assert.Equal(AdministratorStatus.Active, record.Status);
        Assert.Equal(ActorType.PlatformAdmin, record.ActorType);
        Assert.Equal(new[] { AdministratorRole.SuperAdmin }, record.Roles);
        Assert.Equal(AdministratorInvitationStatus.PendingInvitation, record.InvitationStatus);
        Assert.False(record.IsDeleted);

        // A second administrator by the same path — another id and address — is refused while one exists; nothing changes.
        var before = await Raw(database).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var second = new FirstPlatformAdministratorRecord.Request(Guid.NewGuid(), "second.admin@bootstrap.test", "second.admin", "Second");
        Assert.NotNull(await FirstPlatformAdministratorRecord.FindBlockerAsync(database, second));
        Assert.False(await FirstPlatformAdministratorRecord.TryCreateAsync(database, second, DateTimeOffset.UtcNow));
        Assert.False(await FirstPlatformAdministratorRecord.TryCreateAsync(database, request, DateTimeOffset.UtcNow));
        Assert.Equal(before, await Raw(database).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
    }

    private static IMongoCollection<PlatformAdministrator> Collection(IMongoDatabase database) =>
        database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);

    private static IMongoCollection<BsonDocument> Raw(IMongoDatabase database) =>
        database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators);

    private static FilterDefinition<BsonDocument> ById(Guid id) =>
        Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private static async Task<IMongoDatabase> DatabaseAsync(DisposableMongoReplicaSet mongo)
    {
        var database = mongo.CreateDatabase();
        foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                     .Where(c => c.Name == PlatformCollections.PlatformAdministrators))
        {
            await collection.ApplyAsync(database, CancellationToken.None);
        }

        return database;
    }
}
