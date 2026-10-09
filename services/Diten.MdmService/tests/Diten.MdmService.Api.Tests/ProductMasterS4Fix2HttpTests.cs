using System.Net;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Features.ProductMaster.Handlers;
using Diten.MdmService.Application.Features.ProductMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX2 (P-2026-10-09-FU04-S4-FIX2) — the FIX1 review's findings over HTTP on the throwaway mongod:
/// R1 a page with nothing readable is a 503 and every skipped row is named; R2 one unreadable policy decides only its own
/// product in a batch; R4 a control character in q is 400; R5 the store's copies of the production rules held by a
/// table (every status × every completeness branch compared with the production functions, no hand-written list), and
/// rows under a deleted parent are never listed; M1 the search finds the name the screen shows.
/// </summary>
public sealed partial class ProductMasterS4HttpTests
{
    // ------------------------------------------------------------------------------------------- R1

    // The sabotage target "a page with every row unreadable answers 200": a systematic read failure is a 503.
    [Fact]
    public async Task S4_FIX2_R1_A_page_with_nothing_readable_is_503()
    {
        var first = await SeedItemAsync("Broken one");
        var second = await SeedItemAsync("Broken two");
        foreach (var id in new[] { first.Id, second.Id })
            await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(
                new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
                new BsonDocument("$set", new BsonDocument("Name", 42)));

        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item"), 503,
            "PRODUCT_MASTER_UNAVAILABLE", null);
        Assert.Contains(host.Logs.Entries, entry => entry.Message.Contains(first.Id.ToString("D"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task S4_FIX2_R1_One_unreadable_row_is_named_in_the_log_and_the_page_answers()
    {
        var good = await SeedItemAsync("Readable excipient");
        var broken = await SeedItemAsync("Unreadable excipient");
        await host.Database.GetCollection<BsonDocument>("mdm_items").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(broken.Id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("Name", 42)));

        var ids = Ids(await OkAsync(await Reader().GetAsync($"{Base}/stockable-items?skuLevel=Item"), "searchStockableItems"));

        Assert.Equal([good.Id.ToString("D")], ids);
        Assert.Contains(host.Logs.Entries, entry =>
            entry.Message.Contains("PRODUCT_MASTER_SEARCH_ROW_UNREADABLE", StringComparison.Ordinal)
            && entry.Message.Contains(broken.Id.ToString("D"), StringComparison.Ordinal)
            && !entry.Message.Contains("Unreadable excipient", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------- R2

    // The sabotage target "a batch read failure denies the whole batch": an unreadable policy of product X decides only
    // X — A answers the same in a bulk read next to X as on its own.
    [Fact]
    public async Task S4_FIX2_R2_One_unreadable_policy_decides_only_its_own_product_in_a_batch()
    {
        var inScope = await SeedWorldAsync();
        var broken = await SeedWorldAsync(itemName: "Other product item");
        await EnforceScopeAsync(inScope.Product.Id);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, broken.Product.Id, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        // The policy store is strict by design: an element it does not know makes the document unreadable.
        var marked = await host.Database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_policies").UpdateOneAsync(
            new BsonDocument("GlobalProductId", new BsonBinaryData(broken.Product.Id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("NotAPolicyField", 1)));
        Assert.Equal(1, marked.ModifiedCount);
        var client = Reader();

        await OkAsync(await client.GetAsync($"{Base}/skus/{inScope.Gsku.Id:D}?level=Gsku"), "getSku");
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { inScope.Gsku.Id.ToString("D"), broken.Gsku.Id.ToString("D") } })), "bulkReadSkus");

        Assert.Equal([inScope.Gsku.Id.ToString("D")],
            bulk.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("skuId").GetString()!));
        Assert.Equal([broken.Gsku.Id.ToString("D")], Strings(bulk.GetProperty("notFound")));
    }

    // ------------------------------------------------------------------------------------------- R4

    // draft.13, the sabotage target "a control character reaches the store": U+0000–U+001F in q is 400. (A q that is
    // ONLY whitespace — a lone newline included — is no value to ASP.NET's binder: no filter, as the contract says.)
    [Theory]
    [InlineData("%00")]
    [InlineData("a%0Ab")]
    [InlineData("a%1Fb")]
    [InlineData("%00%0A")]
    public async Task S4_FIX2_R4_A_control_character_in_q_is_400(string q)
    {
        await ErrorAsync(await Reader().GetAsync($"{Base}/stockable-items?q={q}"), 400, "INVALID_PARAMETER",
            "searchStockableItems");
    }

    // ------------------------------------------------------------------------------------------- R5

    // The sabotage targets "a completeness branch is dropped" (CT D1–D3) and "a status mapping changes": the search
    // answers EXACTLY the rows the production rules say — every stored status × every completeness branch, under every
    // status filter. The oracle is the production code itself (ProductMasterContract.Status / *Stockable, i.e.
    // StockBehaviourRules.GskuIsComplete / FinishedGoodIsComplete / LskuEffectiveStockable), read over what the store holds.
    [Fact]
    public async Task S4_FIX2_R5_The_search_answers_exactly_what_the_production_rules_say()
    {
        await SeedRuleTableAsync();
        var items = await host.Database.GetCollection<Item>("mdm_items").Find(x => x.TenantId == _tenant).ToListAsync();
        var gskus = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.TenantId == _tenant).ToListAsync();
        var lskus = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.TenantId == _tenant).ToListAsync();
        var fgs = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").Find(x => x.TenantId == _tenant).ToListAsync();
        var parent = gskus.ToDictionary(x => x.Id);
        var rows = items.Select(x => (x.Id, Stockable: x.Stockable, Status: ProductMasterContract.Status(x)))
            .Concat(gskus.Select(x => (x.Id, Stockable: ProductMasterContract.GskuStockable(x),
                Status: ProductMasterContract.Status(x.LifecycleStatus))))
            .Concat(lskus.Select(x => (x.Id, Stockable: ProductMasterContract.LskuStockable(parent[x.GskuId], x),
                Status: ProductMasterContract.Status(x.LifecycleStatus))))
            .Concat(fgs.Select(x => (x.Id, Stockable: ProductMasterContract.FinishedGoodStockable(parent[x.GskuId], x),
                Status: ProductMasterContract.Status(x.LifecycleStatus))))
            .ToList();
        Assert.True(rows.Count > 150, $"only {rows.Count} rows seeded");
        Assert.Contains(rows, x => x.Stockable);
        Assert.Contains(rows, x => !x.Stockable);

        var findings = new List<string>();
        foreach (var filter in new string?[] { null, "Draft", "Active", "Deactivated", "Retired", "Archived" })
        {
            var expected = rows.Where(x => x.Stockable && (filter is null ? x.Status != "Retired" : x.Status == filter))
                .Select(x => x.Id.ToString("D")).ToHashSet();
            var actual = await AllIdsAsync(filter);
            foreach (var missing in expected.Except(actual)) findings.Add($"status={filter ?? "(none)"}: {missing} not listed");
            foreach (var extra in actual.Except(expected)) findings.Add($"status={filter ?? "(none)"}: {extra} listed");
        }
        Assert.True(findings.Count == 0, string.Join("\n", findings.Take(40)));
    }

    private async Task<HashSet<string>> AllIdsAsync(string? status)
    {
        var ids = new HashSet<string>();
        for (var page = 1; page <= 10; page++)
        {
            var body = await OkAsync(await Reader().GetAsync(
                $"{Base}/stockable-items?pageSize=100&page={page}" + (status is null ? "" : $"&status={status}")),
                "searchStockableItems");
            var batch = Ids(body);
            ids.UnionWith(batch);
            if (batch.Length < 100) break;
        }
        return ids;
    }

    // Every branch of every rule, at every stored status. Parent GSKUs are varied too, since an LSKU / FG reads its
    // GSKU's fields (O-3 / O-14 / D-2).
    private async Task SeedRuleTableAsync()
    {
        var (product, revision) = await SeedProductAsync();
        var productStatuses = Enum.GetValues<ProductIdentityLifecycleStatus>();
        var pack = 100;

        // GSKU: complete, then each required field missing / empty, and the Stockable flag false / unset.
        var gskuVariants = new (string Name, Action<Gsku> Change)[]
        {
            ("complete", _ => { }), ("stockable-false", g => g.Stockable = false), ("stockable-null", g => g.Stockable = null),
            ("base-null", g => g.BaseUomCode = null), ("base-empty", g => g.BaseUomCode = ""),
            ("shelf-null", g => g.ShelfLifeDays = null), ("storage-null", g => g.StorageConditionCodes = null),
            ("storage-empty", g => g.StorageConditionCodes = []), ("lot-null", g => g.LotControlled = null)
        };
        foreach (var (_, change) in gskuVariants)
        foreach (var status in productStatuses)
        {
            var gsku = RuleGsku(revision.Id, pack++, status);
            change(gsku);
            await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        }

        // LSKU: its serial flag × its GSKU's completeness × status.
        var market = 0;
        foreach (var parentVariant in new[] { "complete", "lot-null", "storage-null", "stockable-false" })
        {
            var gsku = RuleGsku(revision.Id, pack++, ProductIdentityLifecycleStatus.IdentityApproved);
            gskuVariants.Single(x => x.Name == parentVariant).Change(gsku);
            await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            foreach (var serial in new bool?[] { true, false, null })
            foreach (var status in productStatuses)
            {
                var lsku = await SeedLskuAsync(gsku.Id, $"M{market++:D3}", serial: true);
                await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku.Id,
                    Builders<Lsku>.Update.Set(x => x.SerialControlled, serial).Set(x => x.LifecycleStatus, status));
            }
        }

        // Finished Good: each own branch (flags, GTIN, storage, case ↔ box either way, shelf life own or the GSKU's) under
        // a complete GSKU, a GSKU without a base unit and one without a shelf life.
        var fgVariants = new (string Name, Action<FinishedGood> Change)[]
        {
            ("complete", _ => { }), ("stockable-false", f => f.Stockable = false), ("stockable-null", f => f.Stockable = null),
            ("lot-null", f => f.LotControlled = null), ("serial-null", f => f.SerialControlled = null),
            ("gtin-null", f => f.Gtin = null), ("gtin-empty", f => f.Gtin = ""),
            ("storage-null", f => f.StorageConditionCodes = null), ("storage-empty", f => f.StorageConditionCodes = []),
            ("conversions-null", f => f.UomConversions = null),
            ("no-case-box", f => f.UomConversions = [new ItemUomConversion { FromUom = "XBX", ToUom = "C62", Numerator = "16", Denominator = "1" }]),
            ("box-case-reversed", f => f.UomConversions = [new ItemUomConversion { FromUom = "XBX", ToUom = "XCS", Numerator = "1", Denominator = "48" }]),
            ("own-shelf", f => f.ShelfLifeDays = 365)
        };
        var index = 0;
        foreach (var parentVariant in new[] { "complete", "base-null", "shelf-null" })
        foreach (var (_, change) in fgVariants)
        {
            var gsku = RuleGsku(revision.Id, pack++, ProductIdentityLifecycleStatus.IdentityApproved);
            gskuVariants.Single(x => x.Name == parentVariant).Change(gsku);
            await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            var fg = new FinishedGood
            {
                TenantId = _tenant, GskuId = gsku.Id, CanonicalCode = Code("FG"), CodeReservationId = Guid.NewGuid(),
                CreationCommandId = "fg-" + Guid.NewGuid().ToString("N"),
                LifecycleStatus = productStatuses[index++ % productStatuses.Length],
                UomConversions = [new ItemUomConversion { FromUom = "XCS", ToUom = "XBX", Numerator = "48", Denominator = "1" }],
                StorageConditionCodes = ["AMBIENT_15_25"], LotControlled = true, SerialControlled = true, Stockable = true,
                Gtin = Gtin()
            };
            change(fg);
            await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(fg);
        }

        // Items: the flag × every stored status × every activation kind.
        foreach (var stockable in new[] { true, false })
        foreach (var status in Enum.GetValues<ItemLifecycleStatus>())
        foreach (var kind in new ItemActivationKind?[] { null, ItemActivationKind.Activation, ItemActivationKind.Reactivation })
            await SeedItemAsync($"Rule item {stockable} {status} {kind}", stockable: stockable, status: status, activationKind: kind);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    private Gsku RuleGsku(Guid revisionId, int pack, ProductIdentityLifecycleStatus status) => new()
    {
        TenantId = _tenant, ProductDefinitionRevisionId = revisionId, CanonicalCode = Code("GS"),
        CodeReservationId = Guid.NewGuid(), CreationCommandId = "gs-" + Guid.NewGuid().ToString("N"),
        PackApplicabilityCode = "PACK-" + Guid.NewGuid().ToString("N")[..8], PackQuantity = pack, PackUomCode = "C62",
        LifecycleStatus = status, BaseUomCode = "C62", ShelfLifeDays = 730, StorageConditionCodes = ["AMBIENT_15_25"],
        LotControlled = true, Stockable = true
    };

    // T4 in the search: a row whose Global Product, revision or parent GSKU is deleted is never listed — and getSku
    // answers it as none too (the same live chain on both paths).
    [Theory]
    [InlineData("global-product")]
    [InlineData("revision")]
    [InlineData("parent-gsku")]
    public async Task S4_FIX2_R5_Rows_under_a_deleted_parent_are_never_listed(string parent)
    {
        var world = await SeedWorldAsync();
        var (collection, id) = parent switch
        {
            "global-product" => ("mdm_global_products", world.Product.Id),
            "revision" => ("mdm_product_definition_revisions", world.Revision.Id),
            _ => ("mdm_gskus", world.Gsku.Id)
        };
        var client = Reader();
        var before = await AllIdsAsync(null);
        Assert.Contains(world.Lsku.Id.ToString("D"), before);
        Assert.Contains(world.FinishedGood.Id.ToString("D"), before);

        await host.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("IsDeleted", true)));

        var after = await AllIdsAsync(null);
        foreach (var (row, level) in new[] { (world.Gsku.Id, "Gsku"), (world.Lsku.Id, "Lsku"), (world.FinishedGood.Id, "FinishedGood") })
        {
            Assert.DoesNotContain(row.ToString("D"), after);
            await ErrorAsync(await client.GetAsync($"{Base}/skus/{row:D}?level={level}"), 404, "UNKNOWN_SKU", "getSku");
        }
        Assert.Contains(world.Item.Id.ToString("D"), after);
    }

    // ------------------------------------------------------------------------------------------- CT D6 / D7

    // CT sabotage D6: the search's parent-GSKU join must be bound to the tenant. Tenant A's LSKU names a GSKU id that
    // tenant A does not have; tenant B holds a complete, stockable GSKU with that very _id, pointing at tenant A's own
    // revision (so only the tenant condition of the join can keep it out). A's search never lists the LSKU.
    [Fact]
    public async Task S4_FIX2_D6_Cross_tenant_same_id_parent_gsku_is_never_joined()
    {
        var (_, revision) = await SeedProductAsync();
        var foreignId = Guid.NewGuid();
        var foreign = RuleGsku(revision.Id, 41, ProductIdentityLifecycleStatus.IdentityApproved);
        foreign.Id = foreignId;
        foreign.TenantId = Guid.NewGuid();
        await host.Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(foreign);
        var lsku = await SeedLskuAsync(foreignId, "XT", serial: true);

        var ids = await AllIdsAsync(null);

        Assert.DoesNotContain(lsku.Id.ToString("D"), ids);
        Assert.DoesNotContain(foreignId.ToString("D"), ids);
        await ErrorAsync(await Reader().GetAsync($"{Base}/skus/{lsku.Id:D}?level=Lsku"), 404, "UNKNOWN_SKU", "getSku");
    }

    // CT sabotage D7: the batched policy read must be bound to the tenant. Tenant A is Enforced and its product has no
    // policy (out of scope); tenant B holds a group-wide policy for the SAME Global Product id. A's bulk read and validate
    // never use B's policy, and they agree with getSku.
    [Fact]
    public async Task S4_FIX2_D7_Cross_tenant_same_id_policy_is_never_read()
    {
        var world = await SeedWorldAsync();
        var other = await SeedWorldAsync(itemName: "Scoped product item");
        await EnforceScopeAsync(other.Product.Id);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(Guid.NewGuid(), world.Product.Id, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        var client = Reader();

        await ErrorAsync(await client.GetAsync($"{Base}/skus/{world.Gsku.Id:D}?level=Gsku"), 404, "UNKNOWN_SKU", "getSku");
        var bulk = await OkAsync(await client.PostAsync($"{Base}/skus/bulk-read",
            Json(new { skuIds = new[] { world.Gsku.Id.ToString("D"), other.Gsku.Id.ToString("D") } })), "bulkReadSkus");
        Assert.Equal([world.Gsku.Id.ToString("D")], Strings(bulk.GetProperty("notFound")));
        var validate = await OkAsync(await client.PostAsync($"{Base}/validate", Json(new
        {
            refs = new object[] { new { skuId = world.Gsku.Id.ToString("D") }, new { itemId = world.Product.Id.ToString("D") } }
        })), "validateRefs");
        Assert.All(validate.GetProperty("results").EnumerateArray(), x => Assert.False(x.GetProperty("exists").GetBoolean()));
    }

    // D7 at the read itself: the evaluator also refuses a policy of another tenant (ProductLegalEntityScopeEvaluator), so
    // the HTTP answer above cannot show a read that lost its tenant — this pins the batched read's own tenant bound.
    [Fact]
    public async Task S4_FIX2_D7_Cross_tenant_same_id_policy_is_not_returned_by_the_batched_read()
    {
        var productId = Guid.NewGuid();
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(Guid.NewGuid(), productId, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        var policies = scope.ServiceProvider.GetRequiredService<IProductLegalEntityScopePolicyRepository>();

        var found = await policies.GetByGlobalProductIdsAsync([productId]);

        Assert.Empty(found);
        Assert.Null(await policies.GetByGlobalProductIdAsync(productId));
    }

    // ------------------------------------------------------------------------------------------- M1

    // The pack quantity is stored with its scale ("16.50"); the screen shows "16.5" — the search finds what is shown.
    [Fact]
    public async Task S4_FIX2_M1_The_search_finds_the_pack_quantity_as_it_is_shown()
    {
        var world = await SeedWorldAsync();
        var scaled = await SeedGskuAsync(world.Revision.Id, 16.50m, complete: true, ProductIdentityLifecycleStatus.IdentityApproved);
        var stored = await host.Database.GetCollection<BsonDocument>("mdm_gskus")
            .Find(new BsonDocument("_id", new BsonBinaryData(scaled.Id, GuidRepresentation.Standard))).SingleAsync();
        Assert.Equal("16.50", stored["PackQuantity"].ToString());

        var body = await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?q={Uri.EscapeDataString("16.5 c62")}&skuLevel=Gsku"), "searchStockableItems");

        var row = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(scaled.Id.ToString("D"), row.GetProperty("skuId").GetString());
        Assert.EndsWith(" · 16.5 C62", row.GetProperty("name").GetString(), StringComparison.Ordinal);
        // The stored text with its trailing zero is not what the screen shows: not a match.
        Assert.Empty(Ids(await OkAsync(await Reader().GetAsync(
            $"{Base}/stockable-items?q={Uri.EscapeDataString("16.50 C62")}&skuLevel=Gsku"), "searchStockableItems")));
    }

    // ------------------------------------------------------------------------------------------- M4, M6

    // Only a DATA read failure is a 503: a defect (an InvalidOperationException that is not the driver's or the BSON
    // library's) stays an exception — never hidden behind "unavailable".
    [Fact]
    public async Task S4_FIX2_M4_A_defect_is_not_turned_into_a_503()
    {
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        var handler = ActivatorUtilities.CreateInstance<ProductMasterQueryHandler>(scope.ServiceProvider,
            (IItemRepository)new DefectiveItems());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetProductMasterItemQuery(Guid.NewGuid().ToString("D")), default));
        Assert.True(ProductMasterQueryHandler.IsStoreReadFailure(new TimeoutException()));
        Assert.False(ProductMasterQueryHandler.IsStoreReadFailure(new InvalidOperationException("a defect")));
        Assert.False(ProductMasterQueryHandler.IsStoreReadFailure(new FormatException("a defect")));
    }

    [Fact]
    public void S4_FIX2_M6_The_read_store_refuses_an_unresolved_tenant()
    {
        Assert.Throws<InvalidOperationException>(() => new ProductMasterReadStore(host.Database, new NoTenant()));
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public bool IsResolved => false;
        public void SetTenant(Guid tenantId) { }
    }

    private sealed class DefectiveItems : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("a defect, not a read failure");
        public Task<Item?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<Item?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> NameKeyTakenAsync(string nameKey, Guid? exceptItemId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ItemPage> GetPageAsync(ItemPageRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ItemWriteResult> CreateDraftAsync(Item item, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ItemWriteResult> ReplaceAsync(Item item, int expectedVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
