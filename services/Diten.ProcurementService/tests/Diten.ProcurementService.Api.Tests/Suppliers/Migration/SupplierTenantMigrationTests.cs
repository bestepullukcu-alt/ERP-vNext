using Diten.ProcurementService.Api.Tests.TestInfrastructure;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Persistence;
using Diten.ProcurementService.Persistence.Suppliers;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Migration;

/// <summary>
/// MOD-0140 DL-2 (§8 "Göç", §16, §17): göç işi gerçek bir replika kümesinde — birleştirme kuralı, vergi no normalizasyonu,
/// farklı ülke aynı no, kod çakışması, idempotent ikinci koşu, verify sayımı, geri alma. Her test boş bir veritabanıyla başlar.
/// </summary>
[Collection(ProcurementMongoCollection.Name)]
public sealed class SupplierTenantMigrationTests(ProcurementMongoReplicaSet mongo)
{
    private static readonly Guid TenantA = Guid.Parse("0a000000-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("0b000000-0000-0000-0000-00000000000b");
    private static readonly Guid Le1 = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid Le2 = Guid.Parse("00000000-0000-0000-0002-000000000002");
    private static readonly Guid Le3 = Guid.Parse("00000000-0000-0000-0003-000000000003");
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task One_firm_in_two_companies_becomes_one_master_and_two_relationships()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-AAAA0001", "Acme", "tr", "1234567890", T0),
            Legacy(TenantA, Le2, "SUP-AAAA0002", "Acme", "tr", "1234567890", T0.AddDays(1)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(1, report.Plan.Groups);
        Assert.Equal(1, report.Plan.MergedRecords);
        var masters = await AllAsync(Masters(db));
        var survivor = Assert.Single(masters, m => !m.IsDeleted);
        Assert.Equal("SUP-AAAA0001", survivor.SupplierId);
        Assert.Null(survivor.LegalEntityId);
        var merged = Assert.Single(masters, m => m.IsDeleted);
        Assert.Equal(survivor.Id, merged.MergedIntoId);
        Assert.NotNull(merged.DeletedAt);

        var relationships = await AllAsync(Relationships(db));
        Assert.Equal(2, relationships.Count);
        Assert.All(relationships, r => Assert.Equal(survivor.Id, r.SupplierRef));
        Assert.Empty(relationships.Single(r => r.LegalEntityId == Le1).LegacySupplierCodes);
        Assert.Equal(["SUP-AAAA0002"], relationships.Single(r => r.LegalEntityId == Le2).LegacySupplierCodes);
    }

    [Fact]
    public async Task The_oldest_record_survives_and_ties_go_to_the_smallest_id()
    {
        var first = Legacy(TenantA, Le2, "SUP-BBBB0002", "Late", "tr", "555", T0.AddDays(5));
        var a = Legacy(TenantA, Le1, "SUP-BBBB0001", "Early", "tr", "555", T0, Guid.Parse("00000000-0000-0000-0000-0000000000f2"));
        var b = Legacy(TenantA, Le3, "SUP-BBBB0003", "Early too", "tr", "555", T0, Guid.Parse("00000000-0000-0000-0000-0000000000f1"));
        var (db, migration) = await NewAsync(first, a, b);

        await migration.RunAsync(SupplierMigrationMode.Apply);

        var survivor = Assert.Single(await AllAsync(Masters(db)), m => !m.IsDeleted);
        Assert.Equal(b.Id, survivor.Id);
        Assert.Equal("SUP-BBBB0003", survivor.SupplierId);
        Assert.Equal("Early too", survivor.Name);
    }

    [Theory]
    [InlineData("12 345.678-90", "1234567890")]
    [InlineData("ab-12.cd", "AB12CD")]
    [InlineData(" 99 ", "99")]
    public async Task Tax_ids_are_normalized_before_grouping(string written, string other)
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-CCCC0001", "X", "de", written, T0),
            Legacy(TenantA, Le2, "SUP-CCCC0002", "X", "de", other, T0.AddHours(1)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(1, report.Plan.Groups);
        var survivor = Assert.Single(await AllAsync(Masters(db)), m => !m.IsDeleted);
        Assert.Equal(SupplierTaxId.Normalize(other), survivor.TaxIdNormalized);
        Assert.Equal(written, survivor.TaxId);
    }

    [Fact]
    public async Task The_same_tax_id_in_two_countries_is_two_firms_but_country_case_is_not()
    {
        var (_, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-DDDD0001", "X", "tr", "777", T0),
            Legacy(TenantA, Le2, "SUP-DDDD0002", "X", "de", "777", T0.AddHours(1)),
            Legacy(TenantA, Le3, "SUP-DDDD0003", "X", " TR ", "777", T0.AddHours(2)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(2, report.Plan.Groups);
        Assert.Equal(1, report.Plan.MergedRecords);
        Assert.Equal(1, report.Plan.CountryNormalized);
    }

    [Fact]
    public async Task Records_without_a_tax_id_never_merge()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-EEEE0001", "Same", "tr", null, T0),
            Legacy(TenantA, Le2, "SUP-EEEE0002", "Same", "tr", " - . ", T0.AddHours(1)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(2, report.Plan.Groups);
        Assert.Equal(2, report.Plan.WithoutTaxId);
        Assert.Equal(2, (await AllAsync(Masters(db))).Count(m => !m.IsDeleted));
    }

    [Fact]
    public async Task Tenants_are_never_merged_with_each_other()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-FFFF0001", "X", "tr", "42", T0),
            Legacy(TenantB, Le1, "SUP-FFFF0001", "X", "tr", "42", T0));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(2, report.Tenants);
        Assert.Equal(0, report.Plan.MergedRecords);
        Assert.Equal(0, report.Plan.CodeConflicts);
        var masters = await AllAsync(Masters(db));
        Assert.Equal([TenantA, TenantB], masters.Select(m => m.TenantId!.Value).Order());
        var relationships = await AllAsync(Relationships(db));
        Assert.All(relationships, r => Assert.Equal(masters.Single(m => m.Id == r.SupplierRef).TenantId, r.TenantId));
    }

    [Fact]
    public async Task A_code_held_by_two_different_firms_gives_the_later_firm_a_new_code_and_keeps_the_old_one_as_its_alias()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-12345678", "First firm", "tr", "100", T0),
            Legacy(TenantA, Le2, "SUP-12345678", "Second firm", "tr", "200", T0.AddDays(1)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(1, report.Plan.CodeConflicts);
        var masters = (await AllAsync(Masters(db))).Where(m => !m.IsDeleted).ToList();
        Assert.Equal("SUP-12345678", masters.Single(m => m.Name == "First firm").SupplierId);
        var second = masters.Single(m => m.Name == "Second firm");
        Assert.Matches("^SUP-[0-9A-F]{8}$", second.SupplierId);
        Assert.NotEqual("SUP-12345678", second.SupplierId);
        var relationship = Assert.Single(await AllAsync(Relationships(db)), r => r.SupplierRef == second.Id);
        Assert.Equal(["SUP-12345678"], relationship.LegacySupplierCodes);
        Assert.True((await migration.RunAsync(SupplierMigrationMode.Verify)).Passed);
    }

    [Fact]
    public async Task A_second_apply_changes_nothing()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-GGGG0001", "X", "tr", "1", T0),
            Legacy(TenantA, Le2, "SUP-GGGG0002", "X", "tr", "1", T0.AddDays(1)),
            Legacy(TenantA, Le1, "SUP-GGGG0003", "Y", null, null, T0.AddDays(2)));

        await new SupplierTenantMigration(mongo.Client, db, new FixedClock(T0.AddMonths(1))).RunAsync(SupplierMigrationMode.Apply);
        var before = await SnapshotAsync(db);

        var second = await new SupplierTenantMigration(mongo.Client, db, new FixedClock(T0.AddMonths(2))).RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(0, second.MastersInserted);
        Assert.Equal(0, second.RelationshipsInserted);
        Assert.Equal(3, second.MastersAlreadyPresent);
        Assert.Equal(3, second.RelationshipsAlreadyPresent);
        Assert.Equal(before, await SnapshotAsync(db));
    }

    [Fact]
    public async Task Dry_run_writes_nothing_and_plans_what_apply_writes()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-HHHH0001", "X", "tr", "1", T0),
            Legacy(TenantA, Le2, "SUP-HHHH0002", "X", "tr", "1", T0.AddDays(1)));

        var dry = await migration.RunAsync(SupplierMigrationMode.DryRun);

        Assert.Equal(0, await Masters(db).CountDocumentsAsync(FilterDefinition<SupplierMaster>.Empty));
        Assert.Equal(0, await Relationships(db).CountDocumentsAsync(FilterDefinition<SupplierCompanyRelationship>.Empty));
        var applied = await migration.RunAsync(SupplierMigrationMode.Apply);
        Assert.Equal(dry.Plan.Masters.Count, applied.MastersInserted);
        Assert.Equal(dry.Plan.Relationships.Count, applied.RelationshipsInserted);
        Assert.Equal((dry.Plan.Groups, dry.Plan.MergedRecords), (applied.Plan.Groups, applied.Plan.MergedRecords));
    }

    [Fact]
    public async Task Verify_resolves_every_code_in_the_five_document_collections()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-JJJJ0001", "X", "tr", "9", T0),
            Legacy(TenantA, Le2, "SUP-JJJJ0002", "X", "tr", "9", T0.AddDays(1)));
        await SeedReferencesAsync(db, Le2, "SUP-JJJJ0002");
        await SeedReferencesAsync(db, Le1, "SUP-JJJJ0001");

        await migration.RunAsync(SupplierMigrationMode.Apply);
        var verify = await migration.RunAsync(SupplierMigrationMode.Verify);

        Assert.True(verify.Passed);
        Assert.Equal(
            ["bids", "contracts", "invoices", "purchase_orders", "rfx_events", "suppliers"],
            verify.UnresolvedReferences.Keys.Order());
        Assert.All(verify.UnresolvedReferences.Values, v => Assert.Equal(0, v));
        Assert.Equal(0, verify.MissingMasters);
        Assert.Equal(0, verify.DanglingRelationships);
        Assert.Equal(0, verify.DanglingMerges);
    }

    [Theory]
    [InlineData("purchase_orders")]
    [InlineData("invoices")]
    [InlineData("bids")]
    [InlineData("contracts")]
    [InlineData("rfx_events")]
    public async Task Verify_counts_a_code_that_no_relationship_resolves(string collection)
    {
        var (db, migration) = await NewAsync(Legacy(TenantA, Le1, "SUP-KKKK0001", "X", "tr", "9", T0));
        await migration.RunAsync(SupplierMigrationMode.Apply);
        // Aynı kod, ilişkisi olmayan şirkette: (kiracı, şirket, kod) çözülmez.
        await db.GetCollection<BsonDocument>(collection).InsertOneAsync(Reference(collection, Le2, "SUP-KKKK0001"));

        var verify = await migration.RunAsync(SupplierMigrationMode.Verify);

        Assert.False(verify.Passed);
        Assert.Equal(1, verify.UnresolvedReferences[collection]);
    }

    [Fact]
    public async Task Verify_fails_before_apply()
    {
        var (_, migration) = await NewAsync(Legacy(TenantA, Le1, "SUP-LLLL0001", "X", "tr", "9", T0));

        var verify = await migration.RunAsync(SupplierMigrationMode.Verify);

        Assert.False(verify.Passed);
        Assert.Equal(1, verify.MissingMasters);
        Assert.Equal(1, verify.UnresolvedReferences["suppliers"]);
    }

    [Fact]
    public async Task Apply_never_writes_to_the_legacy_collection_so_switching_back_restores_the_old_reads()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-MMMM0001", "X", "tr", "1", T0),
            Legacy(TenantA, Le2, "SUP-MMMM0002", "X", "tr", "1", T0.AddDays(1)),
            Legacy(TenantA, Le2, "SUP-MMMM0003", "Gone", "tr", "2", T0, deleted: true));
        var legacy = db.GetCollection<BsonDocument>(SupplierIndexConfiguration.CollectionName);
        var before = (await legacy.Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync()).ToJson();

        await migration.RunAsync(SupplierMigrationMode.Apply);
        await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(before, (await legacy.Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync()).ToJson());
    }

    [Fact]
    public async Task Shared_fields_merge_contacts_and_documents_and_take_the_latest_screening()
    {
        var older = Legacy(TenantA, Le1, "SUP-NNNN0001", "Acme", "tr", "5", T0);
        older.Contacts = [new() { Type = "primary", Email = "a@x.io" }, new() { Type = "billing", Phone = "1" }];
        older.Documents = [new() { Type = "tax", EvidenceRef = "ev-1" }];
        older.KycOutcome = KycOutcome.Pending;
        older.UpdatedAt = T0.AddDays(1);
        var newer = Legacy(TenantA, Le2, "SUP-NNNN0002", "Acme Ltd", "tr", "5", T0.AddHours(1));
        newer.Contacts = [new() { Type = "primary", Email = "A@X.IO" }, new() { Type = "billing", Phone = "2" }];
        newer.Documents = [new() { Type = "tax", EvidenceRef = "ev-1" }, new() { Type = "iso", EvidenceRef = "ev-2" }];
        newer.KycOutcome = KycOutcome.Passed;
        newer.SanctionsOutcome = SanctionsOutcome.Clear;
        newer.UpdatedAt = T0.AddDays(3);
        var (db, migration) = await NewAsync(older, newer);

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        var master = Assert.Single(await AllAsync(Masters(db)), m => !m.IsDeleted);
        Assert.Equal("Acme", master.Name);
        Assert.Equal(1, report.Plan.NameDifferences);
        Assert.Equal(3, master.Contacts.Count);
        Assert.Equal(["ev-1", "ev-2"], master.Documents.Select(d => d.EvidenceRef).Order());
        Assert.Equal(KycOutcome.Passed, master.Screening.KycOutcome);
        Assert.Equal(SanctionsOutcome.Clear, master.Screening.SanctionsOutcome);
        Assert.Equal(T0.AddDays(3), master.Screening.ScreenedAt);
        Assert.Equal(1, report.Plan.ScreeningDifferences);
    }

    [Fact]
    public async Task Relationship_fields_stay_with_their_company()
    {
        var a = Legacy(TenantA, Le1, "SUP-PPPP0001", "X", "tr", "3", T0);
        a.Status = SupplierStatus.Blocked;
        a.OnboardingStatus = OnboardingStatus.Approved;
        a.Approval = new OnboardingApproval { WorkflowInstanceId = "WF-1", DecidedBy = "u1", DecidedAt = T0 };
        a.SourceSystem = "sap";
        a.ExternalRef = "V100";
        var b = Legacy(TenantA, Le2, "SUP-PPPP0002", "X", "tr", "3", T0.AddDays(1));
        var (db, migration) = await NewAsync(a, b);

        await migration.RunAsync(SupplierMigrationMode.Apply);

        var relationships = await AllAsync(Relationships(db));
        var first = relationships.Single(r => r.LegalEntityId == Le1);
        Assert.Equal(a.Id, first.Id);
        Assert.Equal(SupplierStatus.Blocked, first.Status);
        Assert.Equal(OnboardingStatus.Approved, first.OnboardingStatus);
        Assert.Equal(1, first.Approval!.SubmissionRound);
        Assert.Equal(("sap", "V100"), (first.SourceSystem, first.ExternalRef));
        var second = relationships.Single(r => r.LegalEntityId == Le2);
        Assert.Equal(SupplierStatus.Active, second.Status);
        Assert.Null(second.Approval);
        Assert.Null(second.SourceSystem);
    }

    [Fact]
    public async Task Two_records_of_one_firm_in_one_company_keep_one_relationship_with_an_alias()
    {
        var (db, migration) = await NewAsync(
            Legacy(TenantA, Le1, "SUP-QQQQ0001", "X", "tr", "12-34", T0),
            Legacy(TenantA, Le1, "SUP-QQQQ0002", "X", "tr", "1234", T0.AddHours(1)));

        var report = await migration.RunAsync(SupplierMigrationMode.Apply);

        Assert.Equal(1, report.Plan.SameCompanyDuplicates);
        var relationship = Assert.Single(await AllAsync(Relationships(db)));
        Assert.Equal(["SUP-QQQQ0002"], relationship.LegacySupplierCodes);
        Assert.True((await migration.RunAsync(SupplierMigrationMode.Verify)).Passed);
    }

    [Fact]
    public async Task A_deleted_legacy_record_stays_deleted_does_not_merge_and_its_code_still_resolves()
    {
        var live = Legacy(TenantA, Le1, "SUP-RRRR0001", "X", "tr", "8", T0.AddDays(1));
        var gone = Legacy(TenantA, Le2, "SUP-RRRR0002", "X", "tr", "8", T0, deleted: true);
        var (db, migration) = await NewAsync(live, gone);
        await db.GetCollection<BsonDocument>("purchase_orders").InsertOneAsync(Reference("purchase_orders", Le2, "SUP-RRRR0002"));

        await migration.RunAsync(SupplierMigrationMode.Apply);
        var verify = await migration.RunAsync(SupplierMigrationMode.Verify);

        var masters = await AllAsync(Masters(db));
        Assert.Equal(live.Id, Assert.Single(masters, m => !m.IsDeleted).Id);
        var deletedMaster = masters.Single(m => m.Id == gone.Id);
        Assert.True(deletedMaster.IsDeleted);
        Assert.Null(deletedMaster.MergedIntoId);
        Assert.Equal(gone.DeletedAt, deletedMaster.DeletedAt);
        var deletedRelationship = (await AllAsync(Relationships(db))).Single(r => r.Id == gone.Id);
        Assert.True(deletedRelationship.IsDeleted);
        Assert.True(verify.Passed);
        // Eski kaydın kendi (kiracı, şirket, kod) üçlüsü ve siparişteki aynı üçlü — ikisi de yalnız silinmiş ilişkiye çözülür.
        Assert.Equal(2, verify.ResolvedToDeletedOnly);
    }

    // ----------------------------------------------------------------------------------------------- indexes (§12)
    // Yalnız test edilen koleksiyonun indeks modelleri kurulur (servisin bütün şeması değil — MongoTestDatabaseGuardTests).
    [Fact]
    public async Task Master_indexes_enforce_tenant_wide_code_and_tax_id_uniqueness_among_live_records()
    {
        var db = await mongo.FreshDatabaseAsync();
        var masters = Masters(db);
        await masters.Indexes.CreateManyAsync(SupplierTenantModelIndexConfiguration.BuildMasterIndexes());
        await masters.InsertOneAsync(Master("SUP-00000001", "tr", "111"));

        await AssertDuplicateAsync(() => masters.InsertOneAsync(Master("SUP-00000001", "de", "222")));
        await AssertDuplicateAsync(() => masters.InsertOneAsync(Master("SUP-00000002", "tr", "111")));
        await masters.InsertOneAsync(Master("SUP-00000003", "de", "111"));
        await masters.InsertOneAsync(Master("SUP-00000004", "tr", null));
        await masters.InsertOneAsync(Master("SUP-00000005", "tr", null));
        await masters.InsertOneAsync(Master("SUP-00000001", "tr", "111", deleted: true));
        var otherTenant = Master("SUP-00000001", "tr", "111");
        otherTenant.TenantId = TenantB;
        await masters.InsertOneAsync(otherTenant);
    }

    [Fact]
    public async Task Relationship_indexes_allow_one_live_relationship_per_company_and_unique_aliases()
    {
        var db = await mongo.FreshDatabaseAsync();
        var relationships = Relationships(db);
        await relationships.Indexes.CreateManyAsync(SupplierTenantModelIndexConfiguration.BuildRelationshipIndexes());
        var supplier = Guid.NewGuid();
        await relationships.InsertOneAsync(Relationship(Le1, supplier));

        await AssertDuplicateAsync(() => relationships.InsertOneAsync(Relationship(Le1, supplier)));
        await relationships.InsertOneAsync(Relationship(Le2, supplier));
        await relationships.InsertOneAsync(Relationship(Le1, supplier, deleted: true));

        await relationships.InsertOneAsync(Relationship(Le1, Guid.NewGuid(), "SUP-OLD00001"));
        await AssertDuplicateAsync(() => relationships.InsertOneAsync(Relationship(Le1, Guid.NewGuid(), "SUP-OLD00001")));
        await relationships.InsertOneAsync(Relationship(Le2, Guid.NewGuid(), "SUP-OLD00001"));
        // Boş eski kod dizileri birbirine çarpmaz.
        await relationships.InsertOneAsync(Relationship(Le3, Guid.NewGuid()));
        await relationships.InsertOneAsync(Relationship(Le3, Guid.NewGuid()));
    }

    // ----------------------------------------------------------------------------------------------- helpers
    private async Task<(IMongoDatabase Db, SupplierTenantMigration Migration)> NewAsync(params Supplier[] legacy)
    {
        var db = await mongo.FreshDatabaseAsync();
        await db.GetCollection<Supplier>(SupplierIndexConfiguration.CollectionName).InsertManyAsync(legacy);
        return (db, new SupplierTenantMigration(mongo.Client, db, new FixedClock(T0.AddMonths(1))));
    }

    private static Supplier Legacy(
        Guid tenant, Guid company, string code, string name, string? country, string? taxId, DateTimeOffset createdAt,
        Guid? id = null, bool deleted = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenant,
        LegalEntityId = company,
        SupplierId = code,
        Name = name,
        Country = country,
        TaxId = taxId,
        CreatedAt = createdAt,
        IsDeleted = deleted,
        DeletedAt = deleted ? createdAt.AddDays(2) : null
    };

    private static SupplierMaster Master(string code, string? country, string? taxId, bool deleted = false) => new()
    {
        TenantId = TenantA,
        SupplierId = code,
        Name = "n",
        Country = country,
        TaxId = taxId,
        TaxIdNormalized = SupplierTaxId.Normalize(taxId),
        IsDeleted = deleted
    };

    private static SupplierCompanyRelationship Relationship(Guid company, Guid supplier, string? alias = null, bool deleted = false) => new()
    {
        TenantId = TenantA,
        LegalEntityId = company,
        SupplierRef = supplier,
        LegacySupplierCodes = alias is null ? [] : [alias],
        IsDeleted = deleted
    };

    private static async Task SeedReferencesAsync(IMongoDatabase db, Guid company, string code)
    {
        foreach (var collection in new[] { "purchase_orders", "invoices", "bids", "contracts", "rfx_events" })
        {
            await db.GetCollection<BsonDocument>(collection).InsertOneAsync(Reference(collection, company, code));
        }
    }

    /// <summary>Belge, üretim varlıklarının yazdığı alan adlarıyla (Guid'ler metin; EntityBase).</summary>
    private static BsonDocument Reference(string collection, Guid company, string code)
    {
        var document = new BsonDocument
        {
            { "_id", Guid.NewGuid().ToString() },
            { "TenantId", TenantA.ToString() },
            { "LegalEntityId", company.ToString() }
        };
        if (collection == "rfx_events")
        {
            document["InvitedSupplierIds"] = new BsonArray { code };
            document["Award"] = new BsonDocument { { "AwardedBidId", "B-1" }, { "AwardedSupplierId", code } };
        }
        else
        {
            document["SupplierId"] = code;
        }

        return document;
    }

    private static IMongoCollection<SupplierMaster> Masters(IMongoDatabase db) =>
        db.GetCollection<SupplierMaster>(SupplierTenantModelIndexConfiguration.MasterCollectionName);

    private static IMongoCollection<SupplierCompanyRelationship> Relationships(IMongoDatabase db) =>
        db.GetCollection<SupplierCompanyRelationship>(SupplierTenantModelIndexConfiguration.RelationshipCollectionName);

    private static Task<List<T>> AllAsync<T>(IMongoCollection<T> collection) =>
        collection.Find(FilterDefinition<T>.Empty).ToListAsync();

    private static async Task<string> SnapshotAsync(IMongoDatabase db)
    {
        var parts = new List<string>();
        foreach (var name in new[] { SupplierTenantModelIndexConfiguration.MasterCollectionName, SupplierTenantModelIndexConfiguration.RelationshipCollectionName })
        {
            var documents = await db.GetCollection<BsonDocument>(name).Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync();
            parts.Add(name + ":" + documents.ToJson());
        }

        return string.Join("\n", parts);
    }

    private static async Task AssertDuplicateAsync(Func<Task> insert)
    {
        var error = await Assert.ThrowsAsync<MongoWriteException>(insert);
        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
