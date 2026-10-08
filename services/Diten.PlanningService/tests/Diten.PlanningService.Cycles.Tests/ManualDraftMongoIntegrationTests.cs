using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

// These tests are skipped unless a loopback replica-set URI is explicitly supplied.
// They never fall back to an operational 27017 connection.
public sealed class ManualDraftMongoFactAttribute : FactAttribute
{
    public ManualDraftMongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")))
            Skip = "Set MOD0188_TEST_MONGO_URI to an explicit loopback test replica set.";
    }
}

public sealed class ManualDraftMongoIntegrationTests
{
    private static readonly DateOnly FirstMonday = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static (ManualDraftMongoStore Store, DemandPlanningMongoContext Context) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("An explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 ||
            url.Username is not null || url.Password is not null ||
            url.ReplicaSetName != "rs-mod0188")
            throw new InvalidOperationException("Only the explicit local rs-mod0188 test replica set is allowed.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        var context = new DemandPlanningMongoContext(configuration);
        return (new ManualDraftMongoStore(context), context);
    }

    private static DemandRevisionDraft Draft(Guid tenant, Guid legalEntity,
        decimal firstQuantity = 0m, Guid? cycleId = null)
    {
        var cycle = new PlanningCycle
        {
            Id = cycleId ?? Guid.NewGuid(),
            TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = new DateOnly(2026, 10, 2),
            CalendarId = "ISO-8601", CalendarVersion = "1",
            TimeZoneId = "Asia/Baku", HorizonStart = FirstMonday,
            HorizonEnd = FirstMonday.AddDays(363),
            PlanningPeriodKey = "2026-10-05",
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1,
                WeekStart = FirstMonday.AddDays(index * 7),
                WeekEnd = FirstMonday.AddDays(index * 7 + 6)
            }).ToList()
        };
        VerifiedDraftSeries Series(string warehouse) => new(
            Guid.NewGuid(), warehouse, "EA",
            Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                number, FirstMonday.AddDays((number - 1) * 7),
                FirstMonday.AddDays((number - 1) * 7 + 6),
                number == 2 ? DraftWeekValueKind.Missing :
                    number == 3 ? DraftWeekValueKind.Unknown : DraftWeekValueKind.Known,
                number is 2 or 3 ? null : number == 1 ? firstQuantity : 10m)).ToArray());
        return Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, legalEntity, Guid.NewGuid(), "Verified manual fixture", Now,
            [Series("WH-1"), Series("WH-2")]).Data);
    }

    [ManualDraftMongoFact]
    public async Task BsonRoundTrip_PreservesTwoParts52WeeksValueKindsAndAudit()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "create-1", default)).Outcome);
        var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(2, restored.Series.Count);
        Assert.All(restored.Series, series => Assert.Equal(52, series.Weeks.Count));
        Assert.Equal(0m, restored.Series[0].Weeks[0].Quantity);
        Assert.Equal(DraftWeekValueKind.Missing, restored.Series[0].Weeks[1].ValueKind);
        Assert.Null(restored.Series[0].Weeks[1].Quantity);
        Assert.Equal(DraftWeekValueKind.Unknown, restored.Series[0].Weeks[2].ValueKind);
        Assert.Equal("EA", restored.Series[0].BaseUomId);
        Assert.Equal(52, restored.Weeks.Count);
        Assert.Equal(draft.CalendarVersion, restored.CalendarVersion);
        Assert.Equal(draft.TimeZoneId, restored.TimeZoneId);

        var sku = restored.Series[0].SkuId;
        var actor = Guid.NewGuid();
        var edited = await store.EditWeekAsync(tenant, legalEntity, draft.Id,
            actor, sku, restored.Series[0].WarehouseId, 1,
            DraftWeekValueKind.Known, 7m, " Adjustment ", "edit-1", 0,
            Now.AddHours(1), default);
        Assert.Equal(ManualDraftStoreOutcome.Changed, edited.Outcome);
        var after = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(1, after.Version);
        Assert.Equal(7m, after.Series[0].Weeks[0].Quantity);
        var change = Assert.Single(after.Changes);
        Assert.Equal(0m, change.OldQuantity);
        Assert.Equal(7m, change.NewQuantity);
        Assert.Equal("Adjustment", change.Reason);
        Assert.Equal(actor, change.ActorId);
        Assert.Equal(Now.AddHours(1), change.OccurredAt);
        Assert.Equal(2, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task CreateRetry_OneRevisionAndChangedContentConflicts()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "same-key", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Replayed,
            (await store.CreateAsync(draft, "same-key", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await store.CreateAsync(Draft(tenant, legalEntity, 4m,
                draft.PlanningCycleId),
                "same-key", default)).Outcome);
        Assert.Equal(1, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task EditRetryAndStaleVersion_AreDurableAndDistinct()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        await store.CreateAsync(draft, "create", default);
        var sku = draft.Series[0].SkuId;
        var warehouse = draft.Series[0].WarehouseId;
        var actor = Guid.NewGuid();
        Task<ManualDraftStoreResult> Edit(decimal quantity, string key, int version,
            string reason = "Adjustment") => store.EditWeekAsync(tenant, legalEntity,
            draft.Id, actor, sku, warehouse, 1, DraftWeekValueKind.Known,
            quantity, reason, key, version, Now.AddHours(1), default);
        Assert.Equal(ManualDraftStoreOutcome.Changed, (await Edit(5m, "edit", 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Replayed,
            (await Edit(5m, "edit", 0, "  Adjustment  ")).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict, (await Edit(6m, "edit", 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict, (await Edit(6m, "other", 0)).Outcome);
        Assert.Equal(2, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentEdits_WithSameVersion_AtMostOneCommits()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        await store.CreateAsync(draft, "create", default);
        var sku = draft.Series[0].SkuId;
        var warehouse = draft.Series[0].WarehouseId;
        var actor = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            store.EditWeekAsync(tenant, legalEntity, draft.Id, actor, sku,
                warehouse, 1, DraftWeekValueKind.Known, index + 1,
                "Adjustment", $"race-{index}", 0, Now.AddHours(1), default)));
        Assert.Single(results, x => x.Outcome == ManualDraftStoreOutcome.Changed);
        Assert.Equal(7, results.Count(x => x.Outcome == ManualDraftStoreOutcome.Conflict));
        Assert.Equal(2, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(1, (await store.ReadAsync(tenant, legalEntity, draft.Id, default))?.Version);
    }

    [ManualDraftMongoFact]
    public async Task TenantAndLegalEntityScopes_DoNotLeakDraftOrAudit()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        await store.CreateAsync(draft, "create", default);
        Assert.Null(await store.ReadAsync(Guid.NewGuid(), legalEntity, draft.Id, default));
        Assert.Null(await store.ReadAsync(tenant, Guid.NewGuid(), draft.Id, default));
        var denied = await store.EditWeekAsync(tenant, Guid.NewGuid(), draft.Id,
            Guid.NewGuid(), draft.Series[0].SkuId, draft.Series[0].WarehouseId,
            1, DraftWeekValueKind.Known, 5m, "Adjustment", "denied", 0,
            Now.AddHours(1), default);
        Assert.Equal(ManualDraftStoreOutcome.ScopeDenied, denied.Outcome);
        Assert.Equal(1, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task MongoAuditInsertFailure_RollsBackManifestAndAllSeriesParts()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        // A test-only stricter index makes the mandatory audit insert fail at Mongo,
        // after manifest and series insertions have already been attempted.
        var keys = Builders<ManualDraftAuditRecord>.IndexKeys;
        await context.ManualDraftAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftAuditRecord>(
                keys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RequestKey),
                new CreateIndexOptions<ManualDraftAuditRecord>
                {
                    Unique = true,
                    Name = "mod0188_test_audit_failure",
                    PartialFilterExpression = Builders<ManualDraftAuditRecord>.Filter
                        .Eq(x => x.TenantId, tenant)
                }));
        try
        {
            await context.ManualDraftAudit.InsertOneAsync(new ManualDraftAuditRecord
            {
                TenantId = tenant, LegalEntityId = legalEntity,
                RevisionId = Guid.NewGuid(), RequestKey = "blocked-audit",
                Action = "TestCollision", ActorId = Guid.NewGuid(),
                OccurredAt = Now, Reason = "Controlled Mongo audit failure"
            });
            Assert.Equal(ManualDraftStoreOutcome.Conflict,
                (await store.CreateAsync(draft, "blocked-audit", default)).Outcome);
            Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity && x.Id == draft.Id));
            Assert.Equal(0, await context.ManualDraftSeriesParts.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity && x.RevisionId == draft.Id));
            Assert.Equal(0, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity && x.RevisionId == draft.Id));
        }
        finally
        {
            await context.ManualDraftAudit.Indexes.DropOneAsync("mod0188_test_audit_failure");
        }
    }

    [ManualDraftMongoFact]
    public async Task MongoEditAuditFailure_RollsBackVersionAndWeekChange()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "create", default)).Outcome);
        var keys = Builders<ManualDraftAuditRecord>.IndexKeys;
        await context.ManualDraftAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftAuditRecord>(
                keys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.RequestKey),
                new CreateIndexOptions<ManualDraftAuditRecord>
                {
                    Unique = true,
                    Name = "mod0188_test_edit_audit_failure",
                    PartialFilterExpression = Builders<ManualDraftAuditRecord>.Filter
                        .Eq(x => x.TenantId, tenant)
                }));
        try
        {
            await context.ManualDraftAudit.InsertOneAsync(new ManualDraftAuditRecord
            {
                TenantId = tenant, LegalEntityId = legalEntity,
                RevisionId = Guid.NewGuid(), RequestKey = "blocked-edit",
                Action = "TestCollision", ActorId = Guid.NewGuid(),
                OccurredAt = Now, Reason = "Controlled edit audit failure"
            });
            var result = await store.EditWeekAsync(tenant, legalEntity, draft.Id,
                Guid.NewGuid(), draft.Series[0].SkuId, draft.Series[0].WarehouseId,
                1, DraftWeekValueKind.Known, 9m, "Adjustment", "blocked-edit",
                0, Now.AddHours(1), default);
            Assert.Equal(ManualDraftStoreOutcome.Conflict, result.Outcome);
            var after = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
                tenant, legalEntity, draft.Id, default));
            Assert.Equal(0, after.Version);
            Assert.Equal(0m, after.Series[0].Weeks[0].Quantity);
            Assert.Empty(after.Changes);
            Assert.Equal(1, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id));
        }
        finally
        {
            await context.ManualDraftAudit.Indexes.DropOneAsync(
                "mod0188_test_edit_audit_failure");
        }
    }

    [ManualDraftMongoFact]
    public async Task CorruptPhysicalPartIdentity_NeverReturnsPartialDraft()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        foreach (var field in new[]
                 { "TenantId", "LegalEntityId", "RevisionId", "SkuId", "WarehouseId" })
        {
            var tenant = Guid.NewGuid();
            var legalEntity = Guid.NewGuid();
            var draft = Draft(tenant, legalEntity);
            Assert.Equal(ManualDraftStoreOutcome.Created,
                (await store.CreateAsync(draft, $"create-{field}", default)).Outcome);
            var part = await context.ManualDraftSeriesParts.Find(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id).FirstAsync();
            var updates = Builders<ManualDraftSeriesPart>.Update;
            var corruption = field switch
            {
                "TenantId" => updates.Set(x => x.TenantId, Guid.NewGuid()),
                "LegalEntityId" => updates.Set(x => x.LegalEntityId, Guid.NewGuid()),
                "RevisionId" => updates.Set(x => x.RevisionId, Guid.NewGuid()),
                "SkuId" => updates.Set(x => x.SkuId, Guid.NewGuid()),
                "WarehouseId" => updates.Set(x => x.WarehouseId, "CORRUPT-WAREHOUSE"),
                _ => throw new InvalidOperationException("Unexpected test field.")
            };
            await context.ManualDraftSeriesParts.UpdateOneAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id && x.Id == part.Id, corruption);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.ReadAsync(tenant, legalEntity, draft.Id, default));
        }
    }

    [ManualDraftMongoFact]
    public async Task MissingDuplicateOrUnreadablePart_NeverReturnsPartialDraft()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        foreach (var corruptionKind in new[] { "Missing", "DuplicateContent", "Unreadable" })
        {
            var tenant = Guid.NewGuid();
            var legalEntity = Guid.NewGuid();
            var draft = Draft(tenant, legalEntity);
            Assert.Equal(ManualDraftStoreOutcome.Created,
                (await store.CreateAsync(draft, $"create-{corruptionKind}", default)).Outcome);
            var parts = await context.ManualDraftSeriesParts.Find(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id).SortBy(x => x.WarehouseId).ToListAsync();
            Assert.Equal(2, parts.Count);
            var updates = Builders<ManualDraftSeriesPart>.Update;
            var corruption = corruptionKind switch
            {
                "Missing" => updates.Set(x => x.IsDeleted, true),
                "DuplicateContent" => updates.Set(x => x.InitialSeriesJson,
                    parts[1].InitialSeriesJson),
                "Unreadable" => updates.Set(x => x.InitialSeriesJson, "{malformed"),
                _ => throw new InvalidOperationException("Unexpected test corruption.")
            };
            await context.ManualDraftSeriesParts.UpdateOneAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id && x.Id == parts[0].Id, corruption);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.ReadAsync(tenant, legalEntity, draft.Id, default));
        }
    }

    [ManualDraftMongoFact]
    public async Task CorruptCreationAuditFields_NeverReturnsDraft()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        foreach (var field in new[]
                 { "RequestKey", "Fingerprint", "ActorId", "Reason", "OccurredAt" })
        {
            var tenant = Guid.NewGuid();
            var legalEntity = Guid.NewGuid();
            var draft = Draft(tenant, legalEntity);
            Assert.Equal(ManualDraftStoreOutcome.Created,
                (await store.CreateAsync(draft, $"create-{field}", default)).Outcome);
            var audit = await context.ManualDraftAudit.Find(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id && x.Action == "Created").FirstAsync();
            var updates = Builders<ManualDraftAuditRecord>.Update;
            var corruption = field switch
            {
                "RequestKey" => updates.Set(x => x.RequestKey, "altered-key"),
                "Fingerprint" => updates.Set(x => x.Fingerprint, "altered-fingerprint"),
                "ActorId" => updates.Set(x => x.ActorId, Guid.NewGuid()),
                "Reason" => updates.Set(x => x.Reason, "altered-reason"),
                "OccurredAt" => updates.Set(x => x.OccurredAt, Now.AddDays(1)),
                _ => throw new InvalidOperationException("Unexpected test field.")
            };
            await context.ManualDraftAudit.UpdateOneAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id && x.Id == audit.Id, corruption);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.ReadAsync(tenant, legalEntity, draft.Id, default));
        }
    }

    [ManualDraftMongoFact]
    public async Task EditAuditVersionGap_NeverReturnsDraft()
    {
        var (store, context) = Open();
        await store.EnsureIndexesAsync();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "create", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id,
                Guid.NewGuid(), draft.Series[0].SkuId, draft.Series[0].WarehouseId,
                1, DraftWeekValueKind.Known, 8m, "Adjustment", "edit", 0,
                Now.AddHours(1), default)).Outcome);
        await context.ManualDraftAudit.UpdateOneAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id && x.Action == "Edited",
            Builders<ManualDraftAuditRecord>.Update.Set(x => x.VersionAfter, 3));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.ReadAsync(tenant, legalEntity, draft.Id, default));
    }
}
