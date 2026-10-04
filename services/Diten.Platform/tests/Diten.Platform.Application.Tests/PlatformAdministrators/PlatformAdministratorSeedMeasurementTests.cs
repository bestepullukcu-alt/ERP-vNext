using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX2 item 5 — A MEASUREMENT, NOT A RULE. The production <see cref="PlatformAdministratorSeed"/> runs on every
/// Platform start, in every environment (DependencyInjection.AddInfrastructure calls it unconditionally). These tests
/// pin what it does TODAY to the admin@diten.com record so the owner can decide on it (the decision is pending; the seed
/// was deliberately not changed). When the owner decides, these expectations change with the code.
///
/// <para>Measured on the production seed over a test-owned replica set — never the shared database.</para>
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class PlatformAdministratorSeedMeasurementTests
{
    private static readonly Guid SeedId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Whatever an operator did to the seed record — demoted it to partner, suspended it, stripped its roles, deleted it —
    /// the next start makes it PlatformAdmin + Active + [SuperAdmin] + not deleted again: the account cannot be lastingly
    /// demoted, suspended or removed.
    /// </summary>
    // BL-521 FIX3 item 8 — a measurement of two HARMFUL behaviours does not belong in the gate suite as a pass condition: the
    // day the seed is fixed it would turn red as if that were a regression. Kept as evidence, not run, until the owner decides.
    [Fact(Skip = "BL-534: 2026-10-04 ölçümü; sahip kararı bekliyor")]
    public async Task Measured_the_next_start_restores_the_seed_administrator_whatever_was_done_to_it()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        await PlatformAdministratorSeed.EnsureSeededAsync(database);
        var collection = database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);
        await collection.UpdateOneAsync(x => x.Id == SeedId, Builders<PlatformAdministrator>.Update
            .Set(x => x.ActorType, ActorType.PartnerAdmin)
            .Set(x => x.Status, AdministratorStatus.Suspended)
            .Set(x => x.Roles, new List<AdministratorRole> { AdministratorRole.ReadOnly })
            .Set(x => x.IsDeleted, true));

        await PlatformAdministratorSeed.EnsureSeededAsync(database); // the next start

        var record = await collection.Find(x => x.Id == SeedId).SingleAsync();
        Assert.Equal(ActorType.PlatformAdmin, record.ActorType);
        Assert.Equal(AdministratorStatus.Active, record.Status);
        Assert.Equal(new[] { AdministratorRole.SuperAdmin }, record.Roles);
        Assert.False(record.IsDeleted);
    }

    /// <summary>
    /// If the seed record's e-mail is changed (the update endpoint allowed it for another administrator until FIX3 item 2(c);
    /// a direct database edit still can), the next start
    /// finds no admin@diten.com record and inserts one with the SAME fixed id: the insert fails on the duplicate id and the
    /// exception escapes the seed — at the unguarded call in AddInfrastructure that stops the Platform from starting.
    /// </summary>
    [Fact(Skip = "BL-534: 2026-10-04 ölçümü; sahip kararı bekliyor")]
    public async Task Measured_after_the_seed_administrators_email_is_changed_the_next_start_fails_on_a_duplicate_id()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = await DatabaseAsync(mongo);
        await PlatformAdministratorSeed.EnsureSeededAsync(database);
        var collection = database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);
        await collection.UpdateOneAsync(x => x.Id == SeedId, Builders<PlatformAdministrator>.Update
            .Set(x => x.Email, "renamed.seed@bl521.test")
            .Set(x => x.NormalizedEmail, "renamed.seed@bl521.test"));

        var failure = await Assert.ThrowsAsync<MongoWriteException>(() => PlatformAdministratorSeed.EnsureSeededAsync(database));

        Assert.Equal(ServerErrorCategory.DuplicateKey, failure.WriteError.Category);
        Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<PlatformAdministrator>.Empty));
    }

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
