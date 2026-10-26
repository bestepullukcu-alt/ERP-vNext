using System.Net;
using System.Text.Json;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU05 slice 1 over HTTP on the throwaway mongod: a Global Product links to one brand of its tenant — only a
/// linkable one (FU02's rule, the same refusals as the legacy product link) — and every read (list, detail, selector) says
/// the brand as id + code + name, the code and name read from the brand itself (nothing copied onto the product). Pack §13
/// is one test per row here; each test is its own tenant.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class GlobalProductBrandLinkHttpTests(MdmHttpHost host)
{
    private const string Read = "mdm.global-products.read";
    private const string Create = "mdm.global-products.create";
    private const string Update = "mdm.global-products.update";
    private static readonly DateTimeOffset From2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _steward = Guid.NewGuid();
    private string StewardToken() => host.Token(_tenant, _steward, Read, Create, Update);

    // ── create ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_a_linkable_brand_reads_the_brand_and_stores_only_its_id()
    {
        var brand = await SeedBrandAsync("BRD-LINK", "Linkable Brand");

        var created = await CreateAsync("Brand Linked Product", new() { ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await Envelope.ReadAsync(created)).GetProperty("data");
        Assert.Equal(brand, data.GetProperty("brandId").GetGuid());
        Assert.Equal("BRD-LINK", data.GetProperty("brandCode").GetString());
        var id = data.GetProperty("globalProductId").GetGuid();

        var detail = await DetailAsync(id);
        Assert.Equal(brand, detail.GetProperty("brandId").GetGuid());
        Assert.Equal("BRD-LINK", detail.GetProperty("brandCode").GetString());
        Assert.Equal("Linkable Brand", detail.GetProperty("brandName").GetString());

        var stored = await RawProductAsync(id);
        Assert.Equal(BsonBinarySubType.UuidStandard, stored["BrandId"].AsBsonBinaryData.SubType);
        Assert.Equal(brand, stored["BrandId"].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
        Assert.DoesNotContain(stored.Names, name => name.Contains("BrandCode", StringComparison.OrdinalIgnoreCase)
                                                    || name.Contains("BrandName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Read_model_contract_is_flat_brandId_brandCode_brandName_and_all_null_without_a_brand()
    {
        var brand = await SeedBrandAsync("BRD-SHAPE", "Shape Brand");
        var branded = await CreateIdAsync("Shape Branded", brand);
        var plain = await CreateIdAsync("Shape Plain", brandId: null);

        foreach (var (item, expected) in new[]
                 {
                     (await DetailAsync(branded), (Guid?)brand), (await DetailAsync(plain), null),
                     (await ListItemAsync(branded), (Guid?)brand), (await ListItemAsync(plain), null),
                     (await SelectorItemAsync(branded), (Guid?)brand), (await SelectorItemAsync(plain), null)
                 })
        {
            Assert.False(item.TryGetProperty("brand", out _), "the read model is flat: no nested brand object");
            if (expected is { } id)
            {
                Assert.Equal(id, item.GetProperty("brandId").GetGuid());
                Assert.Equal("BRD-SHAPE", item.GetProperty("brandCode").GetString());
                Assert.Equal("Shape Brand", item.GetProperty("brandName").GetString());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, item.GetProperty("brandId").ValueKind);
                Assert.Equal(JsonValueKind.Null, item.GetProperty("brandCode").ValueKind);
                Assert.Equal(JsonValueKind.Null, item.GetProperty("brandName").ValueKind);
            }
        }
    }

    [Fact]
    public async Task Create_without_a_brand_works_as_before_and_reads_null()
    {
        var created = await CreateAsync("No Brand Product", []);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await Envelope.ReadAsync(created)).GetProperty("data").GetProperty("globalProductId").GetGuid();
        var stored = await RawProductAsync(id);
        Assert.True(!stored.Contains("BrandId") || stored["BrandId"].IsBsonNull);
        Assert.Equal(JsonValueKind.Null, (await DetailAsync(id)).GetProperty("brandId").ValueKind);
    }

    [Theory]
    [InlineData("draft", false, false)]
    [InlineData("inactive", false, false)]
    [InlineData("active", true, false)]  // expired: the window ended yesterday
    [InlineData("active", false, true)]  // not yet valid: the window starts tomorrow
    public async Task Create_with_not_linkable_brand_is_409(string status, bool expired, bool future)
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var brand = await SeedBrandAsync("BRD-NOLINK", "Not Linkable", status: status,
            from: future ? today.AddDays(1) : From2025, to: expired ? today.AddDays(-1) : null);

        var response = await CreateAsync("Refused Product", new() { ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("brand_not_linkable:", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(0, await ProductCountAsync());
    }

    [Fact]
    public async Task Create_with_archived_brand_is_409_and_writes_nothing()
    {
        var brand = await SeedBrandAsync("BRD-ARC", "Archived", archived: true);

        var response = await CreateAsync("Archived Brand Product", new() { ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("brand_archived:", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(0, await ProductCountAsync());
    }

    [Fact]
    public async Task Foreign_tenant_brand_id_is_404()
    {
        var foreign = await SeedBrandAsync("BRD-FOREIGN", "Other Tenant's Brand", tenantId: Guid.NewGuid());

        var create = await CreateAsync("Foreign Brand Product", new() { ["brandId"] = foreign });
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        Assert.StartsWith("brand_not_found:", Envelope.FirstError(await Envelope.ReadAsync(create)));
        Assert.Equal(0, await ProductCountAsync());

        var draft = await CreateIdAsync("Foreign Brand Edit", brandId: null);
        var edit = await UpdateAsync(draft, new() { ["globalProductName"] = "Foreign Brand Edit", ["expectedVersion"] = 0, ["brandId"] = foreign });
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.StartsWith("brand_not_found:", Envelope.FirstError(await Envelope.ReadAsync(edit)));
        Assert.Equal(0, (await RawProductAsync(draft))["Version"].ToInt32());
    }

    [Fact]
    public async Task Create_with_an_empty_brand_id_is_400()
    {
        var response = await CreateAsync("Empty Brand Id", new() { ["brandId"] = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await ProductCountAsync());
    }

    [Theory]
    [InlineData("brandCode")]
    [InlineData("brandName")]
    public async Task Create_with_a_brand_code_or_name_field_is_400(string field)
    {
        var response = await CreateAsync("Unknown Field Create", new() { [field] = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await ProductCountAsync());
    }

    [Fact]
    public async Task Create_without_the_create_permission_is_403()
    {
        var brand = await SeedBrandAsync("BRD-403", "Forbidden");

        var response = await host.Client(host.Token(_tenant, _steward, Read, Update))
            .PostAsync("/api/global-products/drafts", Envelope.Json(new Dictionary<string, object?>
            {
                ["globalProductName"] = "Forbidden", ["reservationId"] = Guid.NewGuid(), ["expectedReservationVersion"] = 0,
                ["idempotencyKey"] = Guid.NewGuid().ToString("D"), ["brandId"] = brand
            }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_token_the_brand_options_and_the_create_are_401()
    {
        var client = host.Client(null);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/global-products/brand-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/global-products/drafts", Envelope.Json(new { globalProductName = "X" }))).StatusCode);
    }

    // ── draft edit (T-4) ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_draft_without_the_brand_field_keeps_the_brand()
    {
        var brand = await SeedBrandAsync("BRD-KEEP", "Kept");
        var id = await CreateIdAsync("Keep Brand", brand);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Keep Brand Renamed", ["expectedVersion"] = 0 });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var data = (await Envelope.ReadAsync(edit)).GetProperty("data");
        Assert.Equal(brand, data.GetProperty("brandId").GetGuid());
        Assert.Equal("BRD-KEEP", data.GetProperty("brandCode").GetString());
        Assert.Equal(brand, (await DetailAsync(id)).GetProperty("brandId").GetGuid());
    }

    [Fact]
    public async Task Update_draft_with_a_null_brand_removes_the_brand()
    {
        var brand = await SeedBrandAsync("BRD-DROP", "Dropped");
        var id = await CreateIdAsync("Drop Brand", brand);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Drop Brand", ["expectedVersion"] = 0, ["brandId"] = null });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var detail = await DetailAsync(id);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("brandId").ValueKind);
        Assert.Equal(1, detail.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Update_draft_with_a_brand_links_it()
    {
        var brand = await SeedBrandAsync("BRD-ADD", "Added");
        var id = await CreateIdAsync("Add Brand", brandId: null);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Add Brand", ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var detail = await DetailAsync(id);
        Assert.Equal(brand, detail.GetProperty("brandId").GetGuid());
        Assert.Equal("Added", detail.GetProperty("brandName").GetString());
    }

    [Fact]
    public async Task Update_draft_to_archived_brand_is_409()
    {
        var id = await CreateIdAsync("Archive Target", brandId: null);
        var archived = await SeedBrandAsync("BRD-ARC2", "Archived Too", archived: true);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Archive Target", ["expectedVersion"] = 0, ["brandId"] = archived });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.StartsWith("brand_archived:", Envelope.FirstError(await Envelope.ReadAsync(edit)));
        var stored = await RawProductAsync(id);
        Assert.Equal(0, stored["Version"].ToInt32());
        Assert.True(!stored.Contains("BrandId") || stored["BrandId"].IsBsonNull);
    }

    [Fact]
    public async Task Update_draft_to_not_linkable_brand_is_409()
    {
        var id = await CreateIdAsync("Inactive Target", brandId: null);
        var inactive = await SeedBrandAsync("BRD-INA", "Inactive", status: "inactive");

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Inactive Target", ["expectedVersion"] = 0, ["brandId"] = inactive });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.StartsWith("brand_not_linkable:", Envelope.FirstError(await Envelope.ReadAsync(edit)));
        Assert.Equal(0, (await RawProductAsync(id))["Version"].ToInt32());
    }

    [Fact]
    public async Task A_brand_archived_after_the_link_stays_readable_and_the_same_value_saves_but_moving_to_another_archived_brand_is_409()
    {
        var brand = await SeedBrandAsync("BRD-LATER", "Archived Later");
        var id = await CreateIdAsync("Archived Later Product", brand);
        await host.Database.GetCollection<Brand>("mdm_brands").UpdateOneAsync(
            Builders<Brand>.Filter.Eq(x => x.Id, brand),
            Builders<Brand>.Update.Set(x => x.IsArchived, true).Set(x => x.BrandStatus, "archived"));

        var detail = await DetailAsync(id);
        Assert.Equal(brand, detail.GetProperty("brandId").GetGuid());
        Assert.Equal("BRD-LATER", detail.GetProperty("brandCode").GetString());

        var same = await UpdateAsync(id, new() { ["globalProductName"] = "Archived Later Product", ["expectedVersion"] = 0, ["brandId"] = brand });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        var other = await SeedBrandAsync("BRD-OTHERARC", "Other Archived", archived: true);
        var moved = await UpdateAsync(id, new() { ["globalProductName"] = "Archived Later Product", ["expectedVersion"] = 1, ["brandId"] = other });
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.StartsWith("brand_archived:", Envelope.FirstError(await Envelope.ReadAsync(moved)));
    }

    [Fact]
    public async Task Update_draft_with_an_empty_brand_id_is_400()
    {
        var id = await CreateIdAsync("Empty Id Edit", brandId: null);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Empty Id Edit", ["expectedVersion"] = 0, ["brandId"] = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal(0, (await RawProductAsync(id))["Version"].ToInt32());
    }

    [Theory]
    [InlineData("brandName")]
    [InlineData("brandCode")]
    [InlineData("foo")]
    public async Task Unknown_field_brandName_is_400(string field)
    {
        var brand = await SeedBrandAsync("BRD-UNK", "Unknown Field");
        var id = await CreateIdAsync("Unknown Field Edit", brandId: null);

        var edit = await UpdateAsync(id, new()
        {
            ["globalProductName"] = "Unknown Field Edit", ["expectedVersion"] = 0, ["brandId"] = brand, [field] = "Injected"
        });

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_LIFECYCLE_REQUEST_INVALID", Envelope.FirstError(await Envelope.ReadAsync(edit)));
        Assert.Equal(0, (await RawProductAsync(id))["Version"].ToInt32());
    }

    [Fact]
    public async Task Update_draft_with_a_stale_version_is_409_and_writes_nothing()
    {
        var brand = await SeedBrandAsync("BRD-STALE", "Stale");
        var id = await CreateIdAsync("Stale Edit", brandId: null);
        Assert.Equal(HttpStatusCode.OK,
            (await UpdateAsync(id, new() { ["globalProductName"] = "Stale Edit", ["expectedVersion"] = 0 })).StatusCode);

        var stale = await UpdateAsync(id, new() { ["globalProductName"] = "Stale Edit", ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_CONCURRENCY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        var stored = await RawProductAsync(id);
        Assert.Equal(1, stored["Version"].ToInt32());
        Assert.True(!stored.Contains("BrandId") || stored["BrandId"].IsBsonNull);
    }

    [Fact]
    public async Task The_same_idempotency_key_repeats_the_first_answer_and_another_brand_under_it_is_a_conflict()
    {
        var brand = await SeedBrandAsync("BRD-IDEM", "Idempotent");
        var other = await SeedBrandAsync("BRD-IDEM2", "Other");
        var id = await CreateIdAsync("Idempotent Edit", brandId: null);
        var key = Guid.NewGuid();
        var body = new Dictionary<string, object?> { ["globalProductName"] = "Idempotent Edit", ["expectedVersion"] = 0, ["brandId"] = brand };

        var first = await UpdateAsync(id, body, key);
        var repeat = await UpdateAsync(id, body, key);
        var otherBrand = await UpdateAsync(id, new(body) { ["brandId"] = other }, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.True((await Envelope.ReadAsync(repeat)).GetProperty("data").GetProperty("isReplay").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, otherBrand.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(otherBrand)));
        var stored = BsonSerializer.Deserialize<GlobalProduct>(await RawProductAsync(id));
        Assert.Equal(1, stored.Version);
        Assert.Equal(brand, stored.BrandId);
        Assert.Single(stored.AuditIntents, intent => intent.IdempotencyKey == key.ToString("D"));
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.IdentityApproved)]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task A_draft_edit_with_a_brand_on_a_product_that_is_not_a_draft_is_409(ProductIdentityLifecycleStatus status)
    {
        var brand = await SeedBrandAsync("BRD-NOTDRAFT", "Not Draft");
        var id = await SeedProductAsync("GP-ND-0001", "Not A Draft", status, brandId: null);

        var edit = await UpdateAsync(id, new() { ["globalProductName"] = "Not A Draft", ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        var stored = await RawProductAsync(id);
        Assert.Equal(0, stored["Version"].ToInt32());
        Assert.True(!stored.Contains("BrandId") || stored["BrandId"].IsBsonNull);
    }

    [Fact]
    public async Task A_draft_edit_without_the_update_permission_is_403()
    {
        var brand = await SeedBrandAsync("BRD-NOUPD", "No Update");
        var id = await CreateIdAsync("No Update Edit", brandId: null);

        var edit = await host.Client(host.Token(_tenant, _steward, Read, Create))
            .SendAsync(UpdateRequest(id, new() { ["globalProductName"] = "No Update Edit", ["expectedVersion"] = 0, ["brandId"] = brand }, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(0, (await RawProductAsync(id))["Version"].ToInt32());
    }

    [Fact]
    public async Task Another_tenants_global_product_is_404_for_a_brand_edit()
    {
        var brand = await SeedBrandAsync("BRD-XT", "Cross Tenant");
        var foreignProduct = await SeedProductAsync("GP-XT-0001", "Foreign Product", ProductIdentityLifecycleStatus.Draft,
            brandId: null, tenantId: Guid.NewGuid());

        var edit = await UpdateAsync(foreignProduct, new() { ["globalProductName"] = "Foreign Product", ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_NOT_FOUND", Envelope.FirstError(await Envelope.ReadAsync(edit)));
    }

    [Fact]
    public async Task Draft_create_and_edit_with_a_brand_each_write_their_audit_intent_with_the_state_change()
    {
        var brand = await SeedBrandAsync("BRD-AUD", "Audited");
        var id = await CreateIdAsync("Audited Product", brandId: null);
        Assert.Equal(HttpStatusCode.OK,
            (await UpdateAsync(id, new() { ["globalProductName"] = "Audited Product", ["expectedVersion"] = 0, ["brandId"] = brand })).StatusCode);

        var stored = BsonSerializer.Deserialize<GlobalProduct>(await RawProductAsync(id));
        Assert.Equal(
            [ProductAuditOperation.GlobalProductDraftCreated, ProductAuditOperation.GlobalProductDraftUpdated],
            stored.AuditIntents.Select(intent => intent.Operation).ToArray());
        var edit = stored.AuditIntents.Single(intent => intent.Operation == ProductAuditOperation.GlobalProductDraftUpdated);
        Assert.Equal(0, edit.PreVersion);
        Assert.Equal(1, edit.PostVersion);
        Assert.Equal(_steward.ToString("D"), edit.ActorId);
    }

    // ── reads (Karar b) ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Renamed_brand_shows_new_name_on_global_product_detail()
    {
        var brand = await SeedBrandAsync("BRD-REN", "Before Rename");
        var id = await CreateIdAsync("Renamed Brand Product", brand);
        Assert.Equal("Before Rename", (await DetailAsync(id)).GetProperty("brandName").GetString());

        await host.Database.GetCollection<Brand>("mdm_brands").UpdateOneAsync(
            Builders<Brand>.Filter.Eq(x => x.Id, brand),
            Builders<Brand>.Update.Set(x => x.BrandName, "After Rename"));

        Assert.Equal("After Rename", (await DetailAsync(id)).GetProperty("brandName").GetString());
        Assert.Equal("After Rename", (await ListItemAsync(id)).GetProperty("brandName").GetString());
        Assert.Equal("After Rename", (await SelectorItemAsync(id)).GetProperty("brandName").GetString());
    }

    [Fact]
    public async Task A_brand_that_cannot_be_read_leaves_the_id_and_null_code_and_name()
    {
        var brand = await SeedBrandAsync("BRD-GONE", "Gone");
        var id = await CreateIdAsync("Gone Brand Product", brand);
        await host.Database.GetCollection<Brand>("mdm_brands").UpdateOneAsync(
            Builders<Brand>.Filter.Eq(x => x.Id, brand), Builders<Brand>.Update.Set(x => x.IsDeleted, true));

        var detail = await DetailAsync(id);

        Assert.Equal(brand, detail.GetProperty("brandId").GetGuid());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("brandCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("brandName").ValueKind);
    }

    [Fact]
    public async Task The_list_filters_by_brand_and_a_product_stored_before_the_field_existed_reads_null()
    {
        var alpha = await SeedBrandAsync("BRD-A", "Alpha");
        var beta = await SeedBrandAsync("BRD-B", "Beta");
        var a1 = await CreateIdAsync("Filter A One", alpha);
        var a2 = await CreateIdAsync("Filter A Two", alpha);
        var b1 = await CreateIdAsync("Filter B One", beta);
        var legacy = await SeedLegacyProductWithoutBrandFieldAsync("GP-LEG-0001", "Filter Legacy");
        // Another tenant's product of the same brand id never shows.
        await SeedProductAsync("GP-OT-0001", "Other Tenant Alpha", ProductIdentityLifecycleStatus.Draft, alpha, Guid.NewGuid());

        var filtered = await ListAsync($"brandId={alpha:D}");
        Assert.Equal(new[] { a1, a2 }.OrderBy(x => x), filtered.Ids.OrderBy(x => x));
        Assert.Equal(2, filtered.Total);
        Assert.Equal([b1], (await ListAsync($"brandId={beta:D}")).Ids);

        Assert.False((await RawProductAsync(legacy)).Contains("BrandId"));
        Assert.Equal(JsonValueKind.Null, (await ListItemAsync(legacy)).GetProperty("brandId").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await DetailAsync(legacy)).GetProperty("brandId").ValueKind);
    }

    [Fact]
    public async Task The_brand_filter_holds_in_an_enforced_legal_entity_scope_too()
    {
        var alpha = await SeedBrandAsync("BRD-ENF-A", "Enforced Alpha");
        var beta = await SeedBrandAsync("BRD-ENF-B", "Enforced Beta");
        var inScopeAlpha = await SeedProductAsync("GP-ENF-0001", "Enforced In Alpha", ProductIdentityLifecycleStatus.Draft, alpha);
        var inScopeBeta = await SeedProductAsync("GP-ENF-0002", "Enforced In Beta", ProductIdentityLifecycleStatus.Draft, beta);
        await SeedProductAsync("GP-ENF-0003", "Enforced Out Alpha", ProductIdentityLifecycleStatus.Draft, alpha);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        foreach (var product in new[] { inScopeAlpha, inScopeBeta })
        {
            await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
                .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, product, Guid.NewGuid(),
                    ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        var legalEntity = Guid.NewGuid();
        await host.Database.GetCollection<LegalEntity>("mdm_legal_entities").InsertOneAsync(new LegalEntity
        {
            Id = legalEntity, TenantId = _tenant, Code = "LE-" + legalEntity.ToString("N")[..8], LegalName = "Scope",
            OperationalStatus = LegalEntityOperationalStatus.Active, IsDeleted = false
        });
        host.Scopes.Answer(_tenant, legalEntity);

        var unfiltered = await ListAsync("pageSize=100");
        var filtered = await ListAsync($"brandId={alpha:D}");

        Assert.Equal(new[] { inScopeAlpha, inScopeBeta }.OrderBy(x => x), unfiltered.Ids.OrderBy(x => x));
        Assert.Equal([inScopeAlpha], filtered.Ids);
        Assert.Equal(1, filtered.Total);
    }

    [Theory]
    [InlineData("brandId=not-a-guid")]
    [InlineData("brandId=00000000-0000-0000-0000-000000000000")]
    public async Task A_bad_brand_filter_is_400(string query)
    {
        var response = await host.Client(StewardToken()).GetAsync($"/api/global-products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_brand_index_exists()
    {
        await CreateIdAsync("Index Probe", brandId: null);

        var indexes = await (await host.Database.GetCollection<BsonDocument>("mdm_global_products").Indexes.ListAsync()).ToListAsync();

        var index = Assert.Single(indexes, x => x["name"] == "ix_mdm_global_products_tenant_brand");
        Assert.Equal(new BsonDocument { { "TenantId", 1 }, { "BrandId", 1 } }, index["key"].AsBsonDocument);
        Assert.False(index.GetValue("unique", false).ToBoolean());
    }

    // ── brand options (T-1) ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Brand_options_offer_only_this_tenants_linkable_brands()
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var linkable = await SeedBrandAsync("OPT-A", "Option Active");
        var endsToday = await SeedBrandAsync("OPT-B", "Option Ends Today", to: today);
        await SeedBrandAsync("OPT-C", "Option Draft", status: "draft");
        await SeedBrandAsync("OPT-D", "Option Inactive", status: "inactive");
        await SeedBrandAsync("OPT-E", "Option Archived", archived: true);
        await SeedBrandAsync("OPT-F", "Option Expired", to: today.AddDays(-1));
        await SeedBrandAsync("OPT-G", "Option Future", from: today.AddDays(1));
        await SeedBrandAsync("OPT-H", "Option Other Tenant", tenantId: Guid.NewGuid());

        var response = await host.Client(StewardToken()).GetAsync("/api/global-products/brand-options?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([linkable, endsToday], items.Select(x => x.GetProperty("id").GetGuid()));
        Assert.Equal(["OPT-A", "OPT-B"], items.Select(x => x.GetProperty("brandCode").GetString()));
        Assert.Equal(2, data.GetProperty("totalCount").GetInt64());

        var searched = (await Envelope.ReadAsync(await host.Client(StewardToken())
            .GetAsync("/api/global-products/brand-options?search=ends"))).GetProperty("data").GetProperty("items");
        Assert.Equal([endsToday], searched.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()));
    }

    [Theory]
    [InlineData(Create)]
    [InlineData(Update)]
    public async Task Brand_options_need_a_global_product_write_key_not_brands_read(string key)
    {
        await SeedBrandAsync("OPT-KEY", "Key Brand");

        var allowed = await host.Client(host.Token(_tenant, _steward, key)).GetAsync("/api/global-products/brand-options");
        var readOnly = await host.Client(host.Token(_tenant, _steward, Read, "mdm.brands.read"))
            .GetAsync("/api/global-products/brand-options");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedBrandAsync(
        string code, string name, string status = "active", bool archived = false,
        DateTimeOffset? from = null, DateTimeOffset? to = null, Guid? tenantId = null)
    {
        var brand = new Brand
        {
            TenantId = tenantId ?? _tenant,
            BrandCode = code,
            BrandName = name,
            BrandStatus = archived ? "archived" : status,
            EffectiveFrom = from ?? From2025,
            EffectiveTo = to,
            IsArchived = archived,
            ArchivedAt = archived ? DateTimeOffset.UtcNow : null
        };
        await host.Database.GetCollection<Brand>("mdm_brands").InsertOneAsync(brand);
        return brand.Id;
    }

    private async Task<Guid> SeedProductAsync(
        string code, string name, ProductIdentityLifecycleStatus status, Guid? brandId, Guid? tenantId = null)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = tenantId ?? _tenant, CanonicalCode = $"{code}-{id:N}"[..24], GlobalProductName = name,
            GlobalProductNameNormalized = $"{name.ToUpperInvariant()} {id:N}", CodeReservationId = Guid.NewGuid(),
            LifecycleStatus = status, BrandId = brandId, CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private async Task<Guid> SeedLegacyProductWithoutBrandFieldAsync(string code, string name)
    {
        var id = await SeedProductAsync(code, name, ProductIdentityLifecycleStatus.Draft, brandId: null);
        await host.Database.GetCollection<BsonDocument>("mdm_global_products").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$unset", new BsonDocument("BrandId", "")));
        return id;
    }

    private async Task<HttpResponseMessage> CreateAsync(string name, Dictionary<string, object?> extra)
    {
        var client = host.Client(StewardToken());
        var key = Guid.NewGuid().ToString("D");
        var reserve = await client.PostAsync("/api/global-products/code-reservations",
            Envelope.Json(new { globalProductName = name, idempotencyKey = key }));
        Assert.True(reserve.StatusCode == HttpStatusCode.Created, await reserve.Content.ReadAsStringAsync());
        var reservation = (await Envelope.ReadAsync(reserve)).GetProperty("data");
        var body = new Dictionary<string, object?>
        {
            ["globalProductName"] = name,
            ["reservationId"] = reservation.GetProperty("reservationId").GetGuid(),
            ["expectedReservationVersion"] = reservation.GetProperty("version").GetInt32(),
            ["idempotencyKey"] = key
        };
        foreach (var (field, value) in extra) body[field] = value;
        return await client.PostAsync("/api/global-products/drafts", Envelope.Json(body));
    }

    private async Task<Guid> CreateIdAsync(string name, Guid? brandId)
    {
        var response = await CreateAsync(name, brandId is { } brand ? new() { ["brandId"] = brand } : []);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("globalProductId").GetGuid();
    }

    private static HttpRequestMessage UpdateRequest(Guid id, Dictionary<string, object?> body, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/global-products/{id:D}") { Content = Envelope.Json(body) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return request;
    }

    private Task<HttpResponseMessage> UpdateAsync(Guid id, Dictionary<string, object?> body, Guid? key = null) =>
        host.Client(StewardToken()).SendAsync(UpdateRequest(id, body, key ?? Guid.NewGuid()));

    private async Task<JsonElement> DetailAsync(Guid id)
    {
        var response = await host.Client(StewardToken()).GetAsync($"/api/global-products/{id:D}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data");
    }

    private async Task<JsonElement> ListItemAsync(Guid id)
    {
        var response = await host.Client(StewardToken()).GetAsync("/api/global-products?pageSize=100");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id);
    }

    private async Task<JsonElement> SelectorItemAsync(Guid id)
    {
        var response = await host.Client(StewardToken()).GetAsync("/api/global-products/selector?pageSize=100");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id);
    }

    private async Task<(Guid[] Ids, long Total)> ListAsync(string query)
    {
        var response = await host.Client(StewardToken()).GetAsync($"/api/global-products?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray(),
            data.GetProperty("totalCount").GetInt64());
    }

    private async Task<BsonDocument> RawProductAsync(Guid id) =>
        await host.Database.GetCollection<BsonDocument>("mdm_global_products")
            .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).SingleAsync();

    private async Task<long> ProductCountAsync() =>
        await host.Database.GetCollection<BsonDocument>("mdm_global_products")
            .CountDocumentsAsync(new BsonDocument("TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard)));
}
