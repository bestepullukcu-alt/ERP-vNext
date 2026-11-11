using Diten.ProcurementService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Suppliers;

public enum SupplierMigrationMode
{
    /// <summary>Yalnız sayılar; hiçbir koleksiyona yazmaz.</summary>
    DryRun,

    /// <summary>Yeni koleksiyonlara yazar; var olan kayda dokunmaz — aynı girdiyle ikinci koşu bir şey değiştirmez.</summary>
    Apply,

    /// <summary>Eski kayıt ↔ yeni kayıt dengesi ve eski belgelerdeki kodların çözümü; yazmaz.</summary>
    Verify
}

/// <summary>Göç raporu. Yalnız sayı taşır — ad, vergi no, kiracı adı yok (öneri §2.3a).</summary>
public sealed class SupplierMigrationReport
{
    public SupplierMigrationMode Mode { get; init; }
    public int Tenants { get; init; }
    public required SupplierMigrationPlan Plan { get; init; }

    public int MastersInserted { get; init; }
    public int RelationshipsInserted { get; init; }
    public int MastersAlreadyPresent { get; init; }
    public int RelationshipsAlreadyPresent { get; init; }

    public int MissingMasters { get; init; }
    public int DanglingRelationships { get; init; }
    public int DanglingMerges { get; init; }

    /// <summary>Koleksiyon başına çözülemeyen (kiracı, şirket, kod) üçlüsü sayısı. Verify yalnız hepsi 0 ise geçer.</summary>
    public IReadOnlyDictionary<string, int> UnresolvedReferences { get; init; } = new Dictionary<string, int>();

    /// <summary>Yalnız silinmiş bir ilişkiye çözülen üçlüler (bilgi; bugün de silinmiş tedarikçiye 404 dönüyor).</summary>
    public int ResolvedToDeletedOnly { get; init; }

    public bool Passed => Mode != SupplierMigrationMode.Verify
        || (MissingMasters == 0 && DanglingRelationships == 0 && DanglingMerges == 0
            && UnresolvedReferences.Values.All(v => v == 0) && Plan.SkippedWithoutTenantOrCompany == 0);
}

/// <summary>
/// Tedarikçi göç işi (MOD-0140 DL-2; §8 "Göç", §16, §17). Eski <c>suppliers</c> koleksiyonu salt okunur kaynaktır — bu iş
/// ona hiç yazmaz. Yeni koleksiyonlar <c>supplier_masters</c> ve <c>supplier_company_relationships</c>; <c>apply</c> kiracı
/// başına tek bir Mongo işleminde yazar (kiracı ya tamamen ya hiç). Geri alma okuma anahtarıdır
/// (<see cref="SupplierModelSwitch"/>): yeni koleksiyonlar silinmez.
/// <para><c>verify</c>, eski kodun çözümünü Procurement'ın kendi beş belge koleksiyonunda sayar: sipariş, fatura, RFx (davet
/// ve ödül), teklif, sözleşme. Başka servislerin belgeleri (ör. Inventory karantina kararları) bu servisin veritabanında
/// değildir; onlar sayılmaz.</para>
/// </summary>
public sealed class SupplierTenantMigration
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly TimeProvider _clock;

    public SupplierTenantMigration(IMongoClient client, IMongoDatabase database, TimeProvider? clock = null)
    {
        _client = client;
        _database = database;
        _clock = clock ?? TimeProvider.System;
    }

    private IMongoCollection<Supplier> Legacy => _database.GetCollection<Supplier>(SupplierIndexConfiguration.CollectionName);

    private IMongoCollection<SupplierMaster> Masters =>
        _database.GetCollection<SupplierMaster>(SupplierTenantModelIndexConfiguration.MasterCollectionName);

    private IMongoCollection<SupplierCompanyRelationship> Relationships =>
        _database.GetCollection<SupplierCompanyRelationship>(SupplierTenantModelIndexConfiguration.RelationshipCollectionName);

    public async Task<SupplierMigrationReport> RunAsync(SupplierMigrationMode mode, CancellationToken ct = default)
    {
        var legacy = await Legacy.Find(FilterDefinition<Supplier>.Empty).ToListAsync(ct);
        var plan = SupplierMigrationPlanner.Plan(legacy, _clock.GetUtcNow());
        var tenants = plan.Masters.Select(m => m.TenantId!.Value).Distinct().ToList();

        return mode switch
        {
            SupplierMigrationMode.DryRun => new SupplierMigrationReport { Mode = mode, Tenants = tenants.Count, Plan = plan },
            SupplierMigrationMode.Apply => await ApplyAsync(plan, tenants, ct),
            SupplierMigrationMode.Verify => await VerifyAsync(plan, legacy, tenants.Count, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private async Task<SupplierMigrationReport> ApplyAsync(SupplierMigrationPlan plan, List<Guid> tenants, CancellationToken ct)
    {
        await SupplierTenantModelIndexConfiguration.EnsureIndexesAsync(_database, ct);

        int mastersInserted = 0, relationshipsInserted = 0, mastersPresent = 0, relationshipsPresent = 0;
        foreach (var tenantId in tenants)
        {
            var masters = plan.Masters.Where(m => m.TenantId == tenantId).ToList();
            var relationships = plan.Relationships.Where(r => r.TenantId == tenantId).ToList();

            using var session = await _client.StartSessionAsync(cancellationToken: ct);
            var counts = await session.WithTransactionAsync(async (s, token) =>
            {
                var existingMasters = await ExistingIdsAsync(Masters, s, tenantId, token);
                var existingRelationships = await ExistingIdsAsync(Relationships, s, tenantId, token);
                var newMasters = masters.Where(m => !existingMasters.Contains(m.Id)).ToList();
                var newRelationships = relationships.Where(r => !existingRelationships.Contains(r.Id)).ToList();
                if (newMasters.Count > 0)
                {
                    await Masters.InsertManyAsync(s, newMasters, cancellationToken: token);
                }

                if (newRelationships.Count > 0)
                {
                    await Relationships.InsertManyAsync(s, newRelationships, cancellationToken: token);
                }

                return (newMasters.Count, masters.Count - newMasters.Count, newRelationships.Count, relationships.Count - newRelationships.Count);
            }, cancellationToken: ct);

            mastersInserted += counts.Item1;
            mastersPresent += counts.Item2;
            relationshipsInserted += counts.Item3;
            relationshipsPresent += counts.Item4;
        }

        return new SupplierMigrationReport
        {
            Mode = SupplierMigrationMode.Apply,
            Tenants = tenants.Count,
            Plan = plan,
            MastersInserted = mastersInserted,
            MastersAlreadyPresent = mastersPresent,
            RelationshipsInserted = relationshipsInserted,
            RelationshipsAlreadyPresent = relationshipsPresent
        };
    }

    private static async Task<HashSet<Guid>> ExistingIdsAsync<T>(IMongoCollection<T> collection, IClientSessionHandle session, Guid tenantId, CancellationToken ct)
        where T : EntityBase
    {
        var ids = await collection.Find(session, Builders<T>.Filter.Eq(x => x.TenantId, tenantId))
            .Project(x => x.Id)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    private async Task<SupplierMigrationReport> VerifyAsync(SupplierMigrationPlan plan, List<Supplier> legacy, int tenantCount, CancellationToken ct)
    {
        var masters = await Masters.Find(FilterDefinition<SupplierMaster>.Empty).ToListAsync(ct);
        var relationships = await Relationships.Find(FilterDefinition<SupplierCompanyRelationship>.Empty).ToListAsync(ct);

        var masterById = masters.ToDictionary(m => m.Id);
        var missingMasters = legacy.Count(l => !masterById.TryGetValue(l.Id, out var m) || m.TenantId != l.TenantId);
        var danglingRelationships = relationships.Count(r => !masterById.TryGetValue(r.SupplierRef, out var m) || m.TenantId != r.TenantId);
        var danglingMerges = masters.Count(m => m.MergedIntoId is { } target
            && (!masterById.TryGetValue(target, out var survivor) || survivor.IsDeleted || survivor.TenantId != m.TenantId));

        // (kiracı, şirket, kod) → çözüldüğü ilişkilerden en az biri silinmemiş mi?
        var resolution = new Dictionary<(Guid Tenant, Guid Company, string Code), bool>();
        foreach (var r in relationships)
        {
            if (r.TenantId is not { } tenant || r.LegalEntityId is not { } company)
            {
                continue;
            }

            var codes = new List<string>(r.LegacySupplierCodes);
            if (masterById.TryGetValue(r.SupplierRef, out var master))
            {
                codes.Add(master.SupplierId);
            }

            foreach (var code in codes)
            {
                var key = (tenant, company, code);
                resolution[key] = (resolution.TryGetValue(key, out var live) && live) || !r.IsDeleted;
            }
        }

        var unresolved = new Dictionary<string, int>();
        var resolvedToDeletedOnly = 0;
        foreach (var (collection, triples) in await ReferencedCodesAsync(legacy, ct))
        {
            var missing = 0;
            foreach (var triple in triples)
            {
                if (!resolution.TryGetValue(triple, out var live))
                {
                    missing++;
                }
                else if (!live)
                {
                    resolvedToDeletedOnly++;
                }
            }

            unresolved[collection] = missing;
        }

        return new SupplierMigrationReport
        {
            Mode = SupplierMigrationMode.Verify,
            Tenants = tenantCount,
            Plan = plan,
            MissingMasters = missingMasters,
            DanglingRelationships = danglingRelationships,
            DanglingMerges = danglingMerges,
            UnresolvedReferences = unresolved,
            ResolvedToDeletedOnly = resolvedToDeletedOnly
        };
    }

    /// <summary>Kodu taşıyan her yerdeki (kiracı, şirket, kod) üçlüleri — tekilleştirilmiş, koleksiyon başına.</summary>
    private async Task<List<(string Collection, HashSet<(Guid, Guid, string)> Triples)>> ReferencedCodesAsync(List<Supplier> legacy, CancellationToken ct)
    {
        var result = new List<(string, HashSet<(Guid, Guid, string)>)>
        {
            (SupplierIndexConfiguration.CollectionName,
                legacy.Where(l => l.TenantId is not null && l.LegalEntityId is not null)
                    .Select(l => (l.TenantId!.Value, l.LegalEntityId!.Value, l.SupplierId)).ToHashSet())
        };

        result.Add((RequisitionPoIndexConfiguration.PurchaseOrderCollectionName,
            await TriplesAsync(RequisitionPoIndexConfiguration.PurchaseOrderCollectionName, d => [d.GetValue("SupplierId", BsonNull.Value)], ct)));
        result.Add((InvoiceMatchIndexConfiguration.InvoiceCollectionName,
            await TriplesAsync(InvoiceMatchIndexConfiguration.InvoiceCollectionName, d => [d.GetValue("SupplierId", BsonNull.Value)], ct)));
        result.Add((SourcingIndexConfiguration.RfxCollectionName,
            await TriplesAsync(SourcingIndexConfiguration.RfxCollectionName, d =>
            {
                var values = d.GetValue("InvitedSupplierIds", new BsonArray()) is BsonArray invited ? invited.ToList() : new List<BsonValue>();
                if (d.GetValue("Award", BsonNull.Value) is BsonDocument award)
                {
                    values.Add(award.GetValue("AwardedSupplierId", BsonNull.Value));
                }

                return values;
            }, ct)));
        result.Add((SourcingIndexConfiguration.BidCollectionName,
            await TriplesAsync(SourcingIndexConfiguration.BidCollectionName, d => [d.GetValue("SupplierId", BsonNull.Value)], ct)));
        result.Add((ContractingIndexConfiguration.ContractCollectionName,
            await TriplesAsync(ContractingIndexConfiguration.ContractCollectionName, d => [d.GetValue("SupplierId", BsonNull.Value)], ct)));
        return result;
    }

    private async Task<HashSet<(Guid, Guid, string)>> TriplesAsync(string collectionName, Func<BsonDocument, IEnumerable<BsonValue>> codes, CancellationToken ct)
    {
        var triples = new HashSet<(Guid, Guid, string)>();
        var documents = await _database.GetCollection<BsonDocument>(collectionName).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        foreach (var d in documents)
        {
            if (!Guid.TryParse(d.GetValue("TenantId", BsonNull.Value).ToString(), out var tenant)
                || !Guid.TryParse(d.GetValue("LegalEntityId", BsonNull.Value).ToString(), out var company))
            {
                continue;
            }

            foreach (var value in codes(d))
            {
                if (value is BsonString { Value: var code } && !string.IsNullOrWhiteSpace(code))
                {
                    triples.Add((tenant, company, code));
                }
            }
        }

        return triples;
    }
}
