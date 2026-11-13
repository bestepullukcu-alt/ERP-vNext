using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Persistence.Configurations;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.CompetitorProducts;

/// <summary>
/// WP-SYNC-144 BL-640 — the competitor product, its proposal decision and the event inbox's record are on
/// <see cref="MdmDocumentClassMaps"/> (the one registry), and their updates are field-level. Measured on the run's own
/// mongod (<see cref="MdmTestMongo"/>, the run's own replica set — never the shared one) with documents written in TODAY's stored shape by hand
/// (<see cref="BsonDocument"/>, standard UUIDs — independent of the new map): (a) the registry covers the three types and
/// everything they embed; (b) the old shape reads; (c) a field a later binary added survives an update (the BL-640 risk);
/// (d) the compare-and-swap and the tenant fence are unchanged; (e) Guid filters match the stored UUIDs.
/// </summary>
public sealed class CompetitorDocumentMapsBl640Tests
{
    private static readonly Guid Tenant = CompetitorHarness.Tenant;
    private static readonly Guid OtherTenant = CompetitorHarness.OtherTenant;

    private static readonly Type[] Roots =
    [
        typeof(CompetitorProduct), typeof(CompetitorProposalDecision), typeof(MdmEventInboxRepository.ProcessedEvent)
    ];

    private static readonly Type[] Embedded =
    [
        typeof(CompetitorCountryPrice), typeof(CompetitorPriceHistoryEntry), typeof(CompetitorMergeTrace), typeof(CompetitorChange),
        typeof(CompetitorProductFields), typeof(WorkflowApprovalRound), typeof(WorkflowApprovalDecision)
    ];

    [Fact]
    public void A_the_registry_covers_the_three_documents_and_all_they_embed_and_verify_holds()
    {
        MdmDocumentClassMaps.Register();
        Assert.All(Roots, root => Assert.Contains(root, MdmDocumentClassMaps.PersistedRoots));
        Assert.All(Roots.Concat(Embedded), type =>
        {
            Assert.Contains(type, MdmDocumentClassMaps.CoveredTypes);
            Assert.True(BsonClassMap.IsClassMapRegistered(type), type.Name + " registered");
            Assert.True(BsonClassMap.LookupClassMap(type).IgnoreExtraElements, type.Name + " ignores extra elements");
        });
        MdmDocumentClassMaps.Verify(Roots.Concat(Embedded));

        // one registry: no other class-map source for the competitor documents is left in Persistence
        var persistence = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Persistence");
        var others = Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("RegisterClassMap<Competitor", StringComparison.Ordinal)
                           || File.ReadAllText(path).Contains("RegisterClassMap<WorkflowApproval", StringComparison.Ordinal))
            .Select(Path.GetFileName).ToArray();
        Assert.Empty(others);
        Assert.False(File.Exists(Path.Combine(persistence, "Configurations", "CompetitorProductClassMaps.cs")));

        // the stored shape is unchanged: _id, standard UUIDs, the computed IsSelectable never written
        var record = new CompetitorProduct { TenantId = Tenant, Name = "x", CompetesWithGlobalProductIds = [CompetitorHarness.Tutukon] };
        var written = record.ToBsonDocument();
        Assert.Equal(BsonBinarySubType.UuidStandard, written["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(BsonBinarySubType.UuidStandard, written["TenantId"].AsBsonBinaryData.SubType);
        Assert.Equal(BsonBinarySubType.UuidStandard, written["CompetesWithGlobalProductIds"][0].AsBsonBinaryData.SubType);
        Assert.False(written.Contains("IsSelectable"));
    }

    [Fact]
    public async Task B_C_E_a_product_in_todays_stored_shape_reads_its_update_keeps_a_later_field_and_guid_filters_match()
    {
        await WithDatabase(async database =>
        {
            var id = Guid.NewGuid();
            var merged = Guid.NewGuid();
            var raw = database.GetCollection<BsonDocument>(CompetitorProductRepository.CollectionName);
            await raw.InsertOneAsync(OldProduct(id, Tenant, merged)
                .Add("FutureField", "kept")
                .Add("FutureObject", new BsonDocument("Nested", 7)));
            var products = new CompetitorProductRepository(database);

            // (b) the old shape reads — the nested lists, decimals and dates included; (e) the Guid filters match
            var loaded = await products.GetAsync(Tenant, id, default);
            Assert.NotNull(loaded);
            Assert.Equal(("Vastarel MR", 3), (loaded!.Name, loaded.Version));
            Assert.Equal(123.4567m, Assert.Single(loaded.CountryPrices).BoxPrice);
            Assert.Equal("Vastrel", Assert.Single(loaded.MergedFrom).SourceName);
            Assert.Equal(merged, loaded.MergedFrom[0].SourceId);
            Assert.Equal(CompetitorHarness.Tutukon, Assert.Single(loaded.CompetesWithGlobalProductIds));
            Assert.Equal("Status", Assert.Single(loaded.ChangeLog).Field);
            Assert.Single(await products.ListAsync(Tenant, default));

            // (c) the update keeps what this binary does not know, and writes what it does
            loaded.Status = CompetitorProductStatuses.Inactive;
            loaded.CountryPrices[0].BoxPrice = 130m;
            Assert.True(await products.ReplaceAsync(loaded, 3, default));
            var after = await raw.Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync();
            Assert.Equal("kept", after["FutureField"].AsString);
            Assert.Equal(7, after["FutureObject"]["Nested"].AsInt32);
            Assert.Equal(("inactive", 4), (after["Status"].AsString, after["Version"].AsInt32));
            Assert.Equal(130m, after["CountryPrices"][0]["BoxPrice"].ToDecimal());
            // (e) after the write the stored Guids are still standard UUIDs a raw filter matches
            Assert.Equal(1, await raw.CountDocumentsAsync(new BsonDocument
            {
                ["TenantId"] = new BsonBinaryData(Tenant, GuidRepresentation.Standard),
                ["CompetesWithGlobalProductIds"] = new BsonBinaryData(CompetitorHarness.Tutukon, GuidRepresentation.Standard),
                ["MergedFrom.SourceId"] = new BsonBinaryData(merged, GuidRepresentation.Standard)
            }));
        });
    }

    [Fact]
    public async Task D_the_compare_and_swap_and_the_tenant_fence_are_unchanged()
    {
        await WithDatabase(async database =>
        {
            var id = Guid.NewGuid();
            var raw = database.GetCollection<BsonDocument>(CompetitorProductRepository.CollectionName);
            await raw.InsertOneAsync(OldProduct(id, Tenant, Guid.NewGuid()));
            var products = new CompetitorProductRepository(database);
            var first = (await products.GetAsync(Tenant, id, default))!;
            var stale = (await products.GetAsync(Tenant, id, default))!;

            first.Name = "first";
            Assert.True(await products.ReplaceAsync(first, 3, default));
            stale.Name = "stale";
            Assert.False(await products.ReplaceAsync(stale, 3, default));            // a lost race writes nothing
            Assert.Equal(3, stale.Version);                                          // and leaves the caller's version
            Assert.Null(await products.GetAsync(OtherTenant, id, default));          // another tenant does not see it

            // a record pointed at another tenant (the filter names the record's tenant) updates nothing
            var moved = (await products.GetAsync(Tenant, id, default))!;
            moved.TenantId = OtherTenant;
            moved.Name = "moved";
            Assert.False(await products.ReplaceAsync(moved, 4, default));
            var stored = await raw.Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync();
            Assert.Equal(("first", 4), (stored["Name"].AsString, stored["Version"].AsInt32));
            Assert.Equal(Tenant, stored["TenantId"].AsGuid);
        });
    }

    [Fact]
    public async Task B_C_D_E_a_decision_in_todays_shape_reads_by_proposal_and_its_approval_store_update_keeps_a_later_field()
    {
        await WithDatabase(async database =>
        {
            var id = Guid.NewGuid();
            var proposal = Guid.NewGuid();
            var target = Guid.NewGuid();
            var instance = Guid.NewGuid();
            var raw = database.GetCollection<BsonDocument>(CompetitorProposalDecisionRepository.CollectionName);
            await raw.InsertOneAsync(OldDecision(id, Tenant, proposal, target, instance).Add("FutureField", "kept"));
            var decisions = new CompetitorProposalDecisionRepository(database);

            // (b) + (e): every read path — by id (the approval store), by proposal, by a proposal list
            var loaded = await decisions.GetAsync(Tenant, id, default);
            Assert.NotNull(loaded);
            Assert.Equal((proposal, target, "merge", 2), (loaded!.ProposalId, loaded.TargetId, loaded.Action, loaded.Version));
            Assert.Equal(instance, Assert.Single(loaded.ReviewRounds).WorkflowInstanceId);
            Assert.Equal("approve", Assert.Single(loaded.Decisions).Outcome);
            Assert.Equal("Vastarel MR", loaded.Complete!.Name);
            Assert.Equal(id, (await decisions.GetByProposalAsync(Tenant, proposal, default))!.Id);
            Assert.Single(await decisions.ListByProposalsAsync(Tenant, [proposal], default));
            Assert.Null(await decisions.GetAsync(OtherTenant, id, default));

            // (c) the MongoWorkflowApprovalStore<T> update keeps the later field; (d) its compare-and-swap is unchanged
            var stale = (await decisions.GetAsync(Tenant, id, default))!;
            loaded.Status = CompetitorDecisionStatuses.Applied;
            Assert.True(await decisions.ReplaceAsync(loaded, 2, default));
            stale.Status = CompetitorDecisionStatuses.Returned;
            Assert.False(await decisions.ReplaceAsync(stale, 2, default));
            var after = await raw.Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync();
            Assert.Equal(("kept", "applied", 3), (after["FutureField"].AsString, after["Status"].AsString, after["Version"].AsInt32));
            Assert.Equal(BsonBinarySubType.UuidStandard, after["ProposalId"].AsBsonBinaryData.SubType);
        });
    }

    [Fact]
    public async Task B_an_inbox_record_in_todays_shape_reads_and_still_blocks_its_duplicate()
    {
        await WithDatabase(async database =>
        {
            var eventId = Guid.NewGuid();
            var raw = database.GetCollection<BsonDocument>(MdmEventInboxRepository.CollectionName);
            await raw.InsertOneAsync(new BsonDocument
            {
                ["_id"] = eventId.ToString("D"), ["EventName"] = "workflow.completed", ["TenantId"] = Tenant.ToString("D"),
                ["ProcessedAt"] = new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc), ["FutureField"] = "kept"
            });

            var typed = await database.GetCollection<MdmEventInboxRepository.ProcessedEvent>(MdmEventInboxRepository.CollectionName)
                .Find(Builders<MdmEventInboxRepository.ProcessedEvent>.Filter.Eq(x => x.Id, eventId.ToString("D"))).SingleAsync();
            Assert.Equal(("workflow.completed", Tenant.ToString("D")), (typed.EventName, typed.TenantId));
            var inbox = new MdmEventInboxRepository(database);
            Assert.False(await inbox.TryInsertAsync(eventId, "workflow.completed", Tenant));
            Assert.True(await inbox.TryInsertAsync(Guid.NewGuid(), "workflow.completed", Tenant));
        });
    }

    // ── the stored shapes of today, written by hand ────────────────────────────────────────────────────────

    private static BsonBinaryData Uuid(Guid value) => new(value, GuidRepresentation.Standard);

    private static BsonArray Offset(DateTime utc) => new() { utc.Ticks, 0 };

    private static BsonDocument OldProduct(Guid id, Guid tenant, Guid mergedSource) => new()
    {
        ["_id"] = Uuid(id), ["TenantId"] = Uuid(tenant), ["IsDeleted"] = false, ["DeletedAt"] = BsonNull.Value,
        ["CreatedAt"] = Offset(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)), ["UpdatedAt"] = BsonNull.Value, ["Version"] = 3,
        ["Name"] = "Vastarel MR", ["NormalizedKey"] = "servier|vastarelmr", ["Aliases"] = new BsonArray { "Vastrel" },
        ["Company"] = "Servier", ["ActiveIngredients"] = new BsonArray { "trimetazidine" }, ["FormCode"] = "tablet",
        ["Strength"] = "35 mg", ["AtcCode"] = BsonNull.Value,
        ["CompetesWithGlobalProductIds"] = new BsonArray { Uuid(CompetitorHarness.Tutukon) }, ["IndicationNodeIds"] = new BsonArray(),
        ["Status"] = "active", ["MergedIntoId"] = BsonNull.Value, ["MergedAt"] = BsonNull.Value, ["MergedBy"] = BsonNull.Value,
        ["MergedFrom"] = new BsonArray
        {
            new BsonDocument
            {
                ["SourceId"] = Uuid(mergedSource), ["SourceName"] = "Vastrel", ["SourceCompany"] = BsonNull.Value, ["ProposedBy"] = "rep-1",
                ["ProposedAt"] = BsonNull.Value, ["MergedAt"] = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), ["MergedBy"] = "ops-1",
                ["Kind"] = "proposal", ["MovedMarketCodes"] = new BsonArray()
            }
        },
        ["CountryPrices"] = new BsonArray
        {
            new BsonDocument
            {
                ["MarketCode"] = "TR", ["BoxPrice"] = new BsonDecimal128(123.4567m), ["CurrencyCode"] = "TRY",
                ["ReimbursementStatusCode"] = "reimbursed", ["ValidFrom"] = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), ["Source"] = BsonNull.Value
            }
        },
        ["CountryPriceHistory"] = new BsonArray(),
        ["ProposedBy"] = BsonNull.Value, ["ProposedAt"] = BsonNull.Value, ["ProposedFromReportId"] = BsonNull.Value,
        ["ProposedForProductId"] = BsonNull.Value, ["RejectedReason"] = BsonNull.Value, ["DeactivatedAt"] = BsonNull.Value,
        ["DeactivatedBy"] = BsonNull.Value,
        ["ChangeLog"] = new BsonArray
        {
            new BsonDocument { ["At"] = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), ["By"] = "ops-1", ["Field"] = "Status", ["OldValue"] = "proposed", ["NewValue"] = "active" }
        },
        ["CreatedBy"] = "ops-1", ["UpdatedBy"] = BsonNull.Value
    };

    private static BsonDocument OldDecision(Guid id, Guid tenant, Guid proposal, Guid target, Guid instance) => new()
    {
        ["_id"] = Uuid(id), ["TenantId"] = Uuid(tenant), ["IsDeleted"] = false, ["DeletedAt"] = BsonNull.Value,
        ["CreatedAt"] = Offset(new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc)), ["UpdatedAt"] = BsonNull.Value, ["Version"] = 2,
        ["ProposalId"] = Uuid(proposal), ["Action"] = "merge",
        ["Complete"] = new BsonDocument
        {
            ["Name"] = "Vastarel MR", ["Aliases"] = new BsonArray(), ["Company"] = "Servier", ["ActiveIngredients"] = new BsonArray { "trimetazidine" },
            ["FormCode"] = BsonNull.Value, ["Strength"] = BsonNull.Value, ["AtcCode"] = BsonNull.Value,
            ["CompetesWithGlobalProductIds"] = new BsonArray { Uuid(CompetitorHarness.Tutukon) }, ["IndicationNodeIds"] = new BsonArray(),
            ["CountryPrices"] = new BsonArray()
        },
        ["TargetId"] = Uuid(target), ["MoveMarketCodes"] = new BsonArray { "TR" }, ["Reason"] = BsonNull.Value, ["Status"] = "in_review",
        ["CreatedBy"] = "ops-1", ["UpdatedBy"] = BsonNull.Value, ["AppliedBy"] = BsonNull.Value, ["AppliedAt"] = BsonNull.Value,
        ["ReviewRounds"] = new BsonArray { RoundOf(instance) },
        ["Decisions"] = new BsonArray { DecisionOf() }
    };

    // the embedded approval round / decision in the shape the driver's auto-map writes them today (members, names, types)
    private static BsonDocument RoundOf(Guid instance) => new()
    {
        ["WorkflowInstanceId"] = Uuid(instance), ["SubmittedAt"] = Offset(new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc)),
        ["SubmittedBy"] = "ops-1", ["Outcome"] = "approved", ["ClosedAt"] = Offset(new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc)),
        ["RoundNo"] = 1, ["TemplateCode"] = "MDM-COMP-PROPOSAL", ["CompletedBy"] = "approver-1", ["ReasonCode"] = BsonNull.Value
    };

    private static BsonDocument DecisionOf() => new()
    {
        ["RoundNo"] = 1, ["By"] = "approver-1", ["Outcome"] = "approve", ["Comment"] = BsonNull.Value,
        ["At"] = Offset(new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc))
    };

    private static async Task WithDatabase(Func<IMongoDatabase, Task> body)
    {
        MdmDocumentClassMaps.Register();
        var settings = MongoClientSettings.FromConnectionString(MdmTestMongo.ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var name = $"diten_mdm_bl640_tests_{Guid.NewGuid():N}";
        try
        {
            await body(client.GetDatabase(name));
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private static string RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
