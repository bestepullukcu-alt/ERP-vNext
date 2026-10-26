using System.Net;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU05 slice 2 over HTTP on the throwaway mongod: an approved Global Product's brand changes by its own key
/// (mdm.global-products.update-brand), directly and audited (GlobalProductBrandChanged, in the same conditional write),
/// only while approved with no lifecycle operation; the same brand is no write; an exact repeat is a replay. Also the
/// slice-1 follow-up (CT §7.3): a create's repeat is not a new link.
/// </summary>
public sealed partial class GlobalProductBrandLinkHttpTests
{
    private const string UpdateBrand = "mdm.global-products.update-brand";

    private string BrandStewardToken() => host.Token(_tenant, _steward, Read, Create, Update, UpdateBrand);

    [Fact]
    public async Task Change_brand_on_an_approved_product_writes_once_with_its_intent_and_the_change_record()
    {
        var before = await SeedBrandAsync("CHG-FROM", "From Brand");
        var after = await SeedBrandAsync("CHG-TO", "To Brand");
        var id = await SeedProductAsync("GP-CHG-0001", "Approved Product", ProductIdentityLifecycleStatus.IdentityApproved, before);

        var response = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = after });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        Assert.True(data.GetProperty("changed").GetBoolean());
        Assert.Equal(1, data.GetProperty("version").GetInt32());
        Assert.Equal("CHG-TO", data.GetProperty("brandCode").GetString());
        var stored = await StoredAsync(id);
        Assert.Equal(after, stored.BrandId);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, stored.LifecycleStatus);
        Assert.Equal("Approved Product", stored.GlobalProductName);
        var intent = Assert.Single(stored.AuditIntents);
        Assert.Equal(ProductAuditOperation.GlobalProductBrandChanged, intent.Operation);
        Assert.Equal((0, 1), (intent.PreVersion, intent.PostVersion));
        Assert.Equal(_steward.ToString("D"), intent.ActorId);
        var change = Assert.Single(stored.BrandChanges);
        Assert.Equal((before, after, 0, intent.IdempotencyKey), (change.FromBrandId, change.ToBrandId, change.PreVersion, change.OperationKey));
        Assert.Equal("To Brand", (await DetailAsync(id)).GetProperty("brandName").GetString());
    }

    [Fact]
    public async Task Change_brand_to_null_removes_it_and_a_product_without_a_brand_takes_one()
    {
        var brand = await SeedBrandAsync("CHG-NULL", "Removed");
        var branded = await SeedProductAsync("GP-CHG-0002", "Branded Approved", ProductIdentityLifecycleStatus.IdentityApproved, brand);
        var plain = await SeedLegacyProductWithoutBrandFieldAsync("GP-CHG-0003", "Legacy Approved");
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            Builders<GlobalProduct>.Filter.Eq(x => x.Id, plain),
            Builders<GlobalProduct>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved));

        var removed = await ChangeBrandAsync(branded, new() { ["expectedVersion"] = 0, ["brandId"] = null });
        var linked = await ChangeBrandAsync(plain, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Null((await StoredAsync(branded)).BrandId);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal(brand, (await StoredAsync(plain)).BrandId);
    }

    [Fact]
    public async Task The_same_brand_is_200_and_writes_nothing()
    {
        var brand = await SeedBrandAsync("CHG-SAME", "Same");
        var id = await SeedProductAsync("GP-CHG-0004", "Same Brand", ProductIdentityLifecycleStatus.IdentityApproved, brand);

        var response = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("changed").GetBoolean());
        var stored = await StoredAsync(id);
        Assert.Equal(0, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Empty(stored.BrandChanges);
    }

    [Fact]
    public async Task Change_brand_without_update_brand_permission_is_403()
    {
        var brand = await SeedBrandAsync("CHG-403", "Forbidden");
        var id = await SeedProductAsync("GP-CHG-0005", "Forbidden Change", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);

        // Every other Global Product key, update included — not the brand key.
        var response = await host.Client(host.Token(_tenant, _steward, Read, Create, Update, "mdm.global-products.submit",
                "mdm.global-products.request-correction", "mdm.global-products.request-retirement"))
            .SendAsync(ChangeRequest(id, new() { ["expectedVersion"] = 0, ["brandId"] = brand }, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await StoredAsync(id);
        Assert.Equal(0, stored.Version);
        Assert.Null(stored.BrandId);
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.Draft)]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task Change_brand_on_a_product_that_is_not_approved_is_409(ProductIdentityLifecycleStatus status)
    {
        var brand = await SeedBrandAsync("CHG-STATE", "State");
        var id = await SeedProductAsync("GP-CHG-0006", "Not Approved", status, brandId: null);

        var response = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_STATE_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(0, (await StoredAsync(id)).Version);
    }

    [Fact]
    public async Task Change_brand_while_a_lifecycle_operation_runs_is_409()
    {
        var brand = await SeedBrandAsync("CHG-OP", "Operation");
        var id = await SeedProductAsync("GP-CHG-0007", "Under Correction", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            Builders<GlobalProduct>.Filter.Eq(x => x.Id, id),
            Builders<GlobalProduct>.Update.Set(x => x.ActiveLifecycleOperation,
                new GlobalProductActiveLifecycleOperationBinding(GlobalProductLifecycleOperationKind.Correction, Guid.NewGuid(), 0)));

        var response = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null((await StoredAsync(id)).BrandId);
    }

    [Fact]
    public async Task Change_brand_follows_the_link_rule_archived_409_not_linkable_409_foreign_404()
    {
        var archived = await SeedBrandAsync("CHG-ARC", "Archived", archived: true);
        var inactive = await SeedBrandAsync("CHG-INA", "Inactive", status: "inactive");
        var foreign = await SeedBrandAsync("CHG-FOR", "Foreign", tenantId: Guid.NewGuid());
        var id = await SeedProductAsync("GP-CHG-0008", "Link Rule", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);

        var a = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = archived });
        var n = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = inactive });
        var f = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = foreign });

        Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        Assert.StartsWith("brand_archived:", Envelope.FirstError(await Envelope.ReadAsync(a)));
        Assert.Equal(HttpStatusCode.Conflict, n.StatusCode);
        Assert.StartsWith("brand_not_linkable:", Envelope.FirstError(await Envelope.ReadAsync(n)));
        Assert.Equal(HttpStatusCode.NotFound, f.StatusCode);
        Assert.StartsWith("brand_not_found:", Envelope.FirstError(await Envelope.ReadAsync(f)));
        var stored = await StoredAsync(id);
        Assert.Equal(0, stored.Version);
        Assert.Empty(stored.AuditIntents);
    }

    [Fact]
    public async Task Change_brand_with_a_stale_version_is_409()
    {
        var one = await SeedBrandAsync("CHG-V1", "One");
        var two = await SeedBrandAsync("CHG-V2", "Two");
        var id = await SeedProductAsync("GP-CHG-0009", "Stale Change", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        Assert.Equal(HttpStatusCode.OK, (await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = one })).StatusCode);

        var stale = await ChangeBrandAsync(id, new() { ["expectedVersion"] = 0, ["brandId"] = two });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_CONCURRENCY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        Assert.Equal(one, (await StoredAsync(id)).BrandId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("brandName")]
    [InlineData("brandCode")]
    [InlineData("empty")]
    [InlineData("no-key")]
    public async Task A_malformed_brand_change_is_400(string kind)
    {
        var brand = await SeedBrandAsync("CHG-400", "Malformed");
        var id = await SeedProductAsync("GP-CHG-0010", "Malformed Change", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        var body = kind switch
        {
            "missing" => new Dictionary<string, object?> { ["expectedVersion"] = 0 },
            "empty" => new() { ["expectedVersion"] = 0, ["brandId"] = Guid.Empty },
            "no-key" => new() { ["expectedVersion"] = 0, ["brandId"] = brand },
            _ => new() { ["expectedVersion"] = 0, ["brandId"] = brand, [kind] = "Injected" }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/global-products/{id:D}/brand") { Content = Envelope.Json(body) };
        if (kind != "no-key") request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

        var response = await host.Client(BrandStewardToken()).SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await StoredAsync(id)).Version);
    }

    [Fact]
    public async Task An_exact_repeat_is_a_replay_and_another_brand_under_the_same_key_is_a_conflict()
    {
        var one = await SeedBrandAsync("CHG-R1", "Repeat One");
        var two = await SeedBrandAsync("CHG-R2", "Repeat Two");
        var three = await SeedBrandAsync("CHG-R3", "Repeat Three");
        var id = await SeedProductAsync("GP-CHG-0011", "Repeated Change", ProductIdentityLifecycleStatus.IdentityApproved, one);
        var key = Guid.NewGuid();
        var body = new Dictionary<string, object?> { ["expectedVersion"] = 0, ["brandId"] = two };

        var first = await ChangeBrandAsync(id, body, key);
        var repeat = await ChangeBrandAsync(id, body, key);
        var otherBrand = await ChangeBrandAsync(id, new(body) { ["brandId"] = three }, key);
        // A later change by another key, then the first command once more: still its own replay (its "before" is recorded).
        Assert.Equal(HttpStatusCode.OK, (await ChangeBrandAsync(id, new() { ["expectedVersion"] = 1, ["brandId"] = three })).StatusCode);
        var lateRepeat = await ChangeBrandAsync(id, body, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.True((await Envelope.ReadAsync(repeat)).GetProperty("data").GetProperty("isReplay").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, otherBrand.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(otherBrand)));
        Assert.Equal(HttpStatusCode.OK, lateRepeat.StatusCode);
        Assert.True((await Envelope.ReadAsync(lateRepeat)).GetProperty("data").GetProperty("isReplay").GetBoolean());
        var stored = await StoredAsync(id);
        Assert.Equal(2, stored.Version);
        Assert.Equal(three, stored.BrandId);
        Assert.Single(stored.AuditIntents, intent => intent.IdempotencyKey == key.ToString("D"));
    }

    [Fact]
    public async Task Another_tenants_product_is_404_for_a_brand_change()
    {
        var brand = await SeedBrandAsync("CHG-XT", "Cross Tenant Change");
        var foreign = await SeedProductAsync("GP-CHG-0012", "Foreign Approved", ProductIdentityLifecycleStatus.IdentityApproved,
            brandId: null, tenantId: Guid.NewGuid());

        var response = await ChangeBrandAsync(foreign, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_detail_offers_CHANGE_BRAND_only_to_the_brand_key_on_an_approved_product()
    {
        var approved = await SeedProductAsync("GP-CHG-0013", "Offered", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        var draft = await SeedProductAsync("GP-CHG-0014", "Not Offered", ProductIdentityLifecycleStatus.Draft, brandId: null);

        async Task<string[]> ActionsAsync(string token, Guid id)
        {
            var response = await host.Client(token).GetAsync($"/api/global-products/{id:D}");
            return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("availableActions")
                .EnumerateArray().Select(x => x.GetString()!).ToArray();
        }

        Assert.Contains("CHANGE_BRAND", await ActionsAsync(BrandStewardToken(), approved));
        Assert.DoesNotContain("CHANGE_BRAND", await ActionsAsync(StewardToken(), approved));
        Assert.DoesNotContain("CHANGE_BRAND", await ActionsAsync(BrandStewardToken(), draft));
    }

    [Fact]
    public async Task Brand_options_admit_the_brand_key_alone()
    {
        await SeedBrandAsync("CHG-OPT", "Option For Brand Key");

        var response = await host.Client(host.Token(_tenant, _steward, UpdateBrand)).GetAsync("/api/global-products/brand-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // CT decision A: the brand change is a write inside the tenant's Legal Entity scope (born fenced). In an Enforced tenant a
    // product the caller's legal entities do not reach is "no such product"; one they reach changes through the fence.
    [Fact]
    public async Task Change_brand_on_a_product_outside_the_callers_legal_entity_scope_is_404()
    {
        var brand = await SeedBrandAsync("CHG-SCOPE", "Scoped Brand");
        var outside = await SeedProductAsync("GP-CHG-0015", "Outside Scope", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        var inside = await SeedProductAsync("GP-CHG-0016", "Inside Scope", ProductIdentityLifecycleStatus.IdentityApproved, brandId: null);
        var callerEntity = Guid.NewGuid();
        var otherEntity = Guid.NewGuid();
        foreach (var entity in new[] { callerEntity, otherEntity })
        {
            await host.Database.GetCollection<LegalEntity>("mdm_legal_entities").InsertOneAsync(new LegalEntity
            {
                Id = entity, TenantId = _tenant, Code = "LE-" + entity.ToString("N")[..8], LegalName = "Scope",
                OperationalStatus = LegalEntityOperationalStatus.Active, IsDeleted = false
            });
        }
        host.Scopes.Answer(_tenant, callerEntity);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        var policies = host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies");
        await policies.InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, outside, Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped, [otherEntity], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
        await policies.InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, inside, Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped, [callerEntity], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));

        var refused = await ChangeBrandAsync(outside, new() { ["expectedVersion"] = 0, ["brandId"] = brand });
        var admitted = await ChangeBrandAsync(inside, new() { ["expectedVersion"] = 0, ["brandId"] = brand });

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_NOT_FOUND", Envelope.FirstError(await Envelope.ReadAsync(refused)));
        var stored = await StoredAsync(outside);
        Assert.Equal(0, stored.Version);
        Assert.Null(stored.BrandId);
        Assert.True(admitted.StatusCode == HttpStatusCode.OK, await admitted.Content.ReadAsStringAsync());
        Assert.Equal(brand, (await StoredAsync(inside)).BrandId);
    }

    // CT decision (slice 1 §7.3): the repeat of a create that already wrote its product is not a new link.
    [Fact]
    public async Task Create_repeat_after_the_brand_was_archived_returns_the_first_answer()
    {
        var brand = await SeedBrandAsync("CHG-RETRY", "Retry Brand");
        var client = host.Client(StewardToken());
        var key = Guid.NewGuid().ToString("D");
        var reserve = await client.PostAsync("/api/global-products/code-reservations",
            Envelope.Json(new { globalProductName = "Retried Create", idempotencyKey = key }));
        var reservation = (await Envelope.ReadAsync(reserve)).GetProperty("data");
        var body = new Dictionary<string, object?>
        {
            ["globalProductName"] = "Retried Create",
            ["reservationId"] = reservation.GetProperty("reservationId").GetGuid(),
            ["expectedReservationVersion"] = reservation.GetProperty("version").GetInt32(),
            ["idempotencyKey"] = key,
            ["brandId"] = brand
        };
        var first = await client.PostAsync("/api/global-products/drafts", Envelope.Json(body));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = (await Envelope.ReadAsync(first)).GetProperty("data").GetProperty("globalProductId").GetGuid();
        await host.Database.GetCollection<Brand>("mdm_brands").UpdateOneAsync(
            Builders<Brand>.Filter.Eq(x => x.Id, brand),
            Builders<Brand>.Update.Set(x => x.IsArchived, true).Set(x => x.BrandStatus, "archived"));

        var repeat = await client.PostAsync("/api/global-products/drafts", Envelope.Json(body));

        Assert.True(repeat.StatusCode is HttpStatusCode.Created or HttpStatusCode.Accepted, await repeat.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(repeat)).GetProperty("data");
        Assert.Equal(id, data.GetProperty("globalProductId").GetGuid());
        Assert.Equal(brand, data.GetProperty("brandId").GetGuid());
        Assert.Equal(1, await ProductCountAsync());

        // Another brand under the same reservation is still a different create.
        var other = await SeedBrandAsync("CHG-RETRY2", "Retry Other");
        var mismatch = await client.PostAsync("/api/global-products/drafts", Envelope.Json(new Dictionary<string, object?>(body) { ["brandId"] = other }));
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("CODE_RESERVATION_MISMATCH", Envelope.FirstError(await Envelope.ReadAsync(mismatch)));
        Assert.Equal(1, await ProductCountAsync());
    }

    private static HttpRequestMessage ChangeRequest(Guid id, Dictionary<string, object?> body, Guid key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/global-products/{id:D}/brand") { Content = Envelope.Json(body) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return request;
    }

    private Task<HttpResponseMessage> ChangeBrandAsync(Guid id, Dictionary<string, object?> body, Guid? key = null) =>
        host.Client(BrandStewardToken()).SendAsync(ChangeRequest(id, body, key ?? Guid.NewGuid()));

    private async Task<GlobalProduct> StoredAsync(Guid id) => BsonSerializer.Deserialize<GlobalProduct>(await RawProductAsync(id));
}
