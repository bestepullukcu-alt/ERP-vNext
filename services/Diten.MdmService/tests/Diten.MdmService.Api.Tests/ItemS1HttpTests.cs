using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Inventory;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S1 (P-2026-10-07-FU04-S1): the item server core over HTTP and a throwaway replica-set mongod — the IT
/// code from the product identity family's counter, the transactional insert touch and the burn gate (CT decision 1),
/// the §13 refusals, the lifecycle (Draft / Active / Deactivated / Retired; activation is S2), the retirement stock
/// check (§8.3, fail-closed) and path-b audit of every write command, refused ones included.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class ItemS1HttpTests(MdmHttpHost host) : IAsyncLifetime
{
    private static readonly string[] All =
    [
        "mdm.items.read", "mdm.items.create", "mdm.items.update", "mdm.items.submit", "mdm.items.deactivate",
        "mdm.items.retire"
    ];

    private const string Items = "api/mdm/items";
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    public Task InitializeAsync()
    {
        host.ItemReferences.Reset();
        host.Stock.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        host.ItemReferences.Reset();
        host.Stock.Reset();
        host.Audit.ThrowOnForward = false;
        host.Audit.Inner = null;
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------- code (RCS-001 §4)

    [Fact]
    public async Task S1_Create_gives_an_IT_code_from_the_product_familys_one_counter()
    {
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var gp = await codes.ReserveAsync(CodeBearingEntityType.GlobalProduct, "gp-" + Guid.NewGuid(), "t", "t");

        var created = await CreateAsync(Body("Amoxicillin trihydrate"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await ItemAsync(await IdAsync(created));
        Assert.Matches("^IT-[0-9]{12}$", item.CanonicalCode);
        Assert.Equal(15, item.CanonicalCode.Length);
        Assert.True(long.Parse(item.CanonicalCode[3..]) > long.Parse(gp.ReservedCode[3..]), "not the family's counter");
        Assert.Equal(ItemLifecycleStatus.Draft, item.LifecycleStatus);
        Assert.Equal(0, item.Version);
        Assert.Equal("KGM", item.BaseUomSelection!.ValueCode);
        Assert.Equal(new[] { "AMBIENT_15_25", "PROTECT_FROM_LIGHT" }, item.StorageConditionSelections.Select(x => x.ValueCode));
        var code = await CodeAsync(item.CodeReservationId);
        Assert.Equal(CodeBearingEntityType.Item, code.EntityType);
        Assert.Equal(CodeReservationBindingState.Confirmed, code.BindingState);
        Assert.Equal(item.Id, code.ConsumedEntityId);
        Assert.Equal(item.CanonicalCode, code.ReservedCode);
    }

    [Fact]
    public async Task S1_Two_concurrent_creates_get_two_different_codes()
    {
        var responses = await Task.WhenAll(CreateAsync(Body("Lactose monohydrate")), CreateAsync(Body("Magnesium stearate")));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var items = await Task.WhenAll(responses.Select(async response => await ItemAsync(await IdAsync(response))));
        Assert.NotEqual(items[0].CanonicalCode, items[1].CanonicalCode);
    }

    [Fact]
    public async Task S1_The_code_is_the_servers_and_the_request_cannot_carry_it()
    {
        var create = await CreateAsync(Body("Povidone K30", extra: ("canonicalCode", "IT-000000000001")));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Contains(ItemErrors.FieldForbidden, await ErrorsAsync(create));

        var id = await CreatedAsync("Povidone K30");
        var update = await PutAsync(id, UpdateBody("Povidone K30", 0, extra: ("canonicalCode", "IT-999999999999")));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Contains(ItemErrors.FieldForbidden, await ErrorsAsync(update));
        Assert.StartsWith("IT-", (await ItemAsync(id)).CanonicalCode);
        Assert.NotEqual("IT-999999999999", (await ItemAsync(id)).CanonicalCode);
    }

    // ------------------------------------------------------------------------------------------- §13 access

    [Fact]
    public async Task S1_No_token_is_401_and_no_permission_is_403_on_every_door()
    {
        var id = await CreatedAsync("Cellulose microcrystalline");
        // No token but a tenant header (as through the gateway): 401. Without the header too, the service's tenant
        // middleware answers 400 "Missing Tenant" before authorization — service-wide, not the item's (S1 report).
        var anonymous = host.Client(null);
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client(null).GetAsync(Items)).StatusCode);
        var none = host.Client(host.Token(_tenant, _user));
        foreach (var client in new[] { anonymous, none })
        {
            var expected = client == anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(expected, (await client.GetAsync(Items)).StatusCode);
            Assert.Equal(expected, (await client.GetAsync($"{Items}/{id}")).StatusCode);
            Assert.Equal(expected, (await client.PostAsJsonAsync(Items, Body("X"))).StatusCode);
            Assert.Equal(expected, (await client.PutAsJsonAsync($"{Items}/{id}", UpdateBody("X", 0))).StatusCode);
            foreach (var door in new[] { "submit", "reactivation-requests", "deactivate", "retire" })
                Assert.Equal(expected, (await client.PostAsJsonAsync($"{Items}/{id}/{door}", Lifecycle(0))).StatusCode);
        }
    }

    [Fact]
    public async Task S1_Another_tenants_item_is_not_found()
    {
        var id = await CreatedAsync("Talc");
        var other = host.Client(host.Token(Guid.NewGuid(), _user, All));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Items}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{Items}/{id}", UpdateBody("Talc", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{Items}/{id}/retire", Lifecycle(0, "x"))).StatusCode);
        var list = await Envelope.ReadAsync(await other.GetAsync(Items));
        Assert.Equal(0, list.GetProperty("data").GetProperty("total").GetInt64());
    }

    // ------------------------------------------------------------------------------------------- §12 / §13 refusals

    [Fact]
    public async Task S1_The_name_is_unique_per_material_type_among_items_not_retired()
    {
        await CreatedAsync("Sodium chloride");
        var same = await CreateAsync(Body("  SODIUM CHLORIDE "));
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal(ItemErrors.NameConflict, await FirstErrorAsync(same));
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(Body("Sodium chloride", materialType: "EXCIPIENT"))).StatusCode);
    }

    [Theory]
    [InlineData("baseUom", 422, ItemErrors.ReferenceValueInvalid)]
    [InlineData("storage", 422, ItemErrors.ReferenceValueInvalid)]
    [InlineData("materialType", 422, ItemErrors.ReferenceValueInvalid)]
    [InlineData("retired", 422, ItemErrors.ReferenceValueInvalid)]
    [InlineData("two-temperatures", 422, ItemErrors.StorageConditionConflict)]
    [InlineData("repeated-storage", 422, ItemErrors.StorageConditionConflict)]
    [InlineData("exponent", 422, ItemErrors.UomConversionInvalid)]
    [InlineData("same-uom", 422, ItemErrors.UomConversionInvalid)]
    [InlineData("conversion-unit", 422, ItemErrors.ReferenceValueInvalid)]
    [InlineData("stockable-no-shelf-life", 422, ItemErrors.StockBehaviourIncomplete)]
    [InlineData("stockable-no-storage", 422, ItemErrors.StockBehaviourIncomplete)]
    [InlineData("outage", 503, ItemErrors.ReferenceListUnavailable)]
    [InlineData("provider-throws", 503, ItemErrors.ReferenceListUnavailable)]
    public async Task S1_A_refused_create_writes_nothing_and_reserves_no_code(string fault, int status, string error)
    {
        var body = fault switch
        {
            "baseUom" => Body("Refused", baseUom: "XYZ"),
            "storage" => Body("Refused", storage: ["BELOW_30"]),
            "materialType" => Body("Refused", materialType: "CONSUMABLE"),
            "retired" => Body("Refused", baseUom: "MGM"),
            "two-temperatures" => Body("Refused", storage: ["AMBIENT_15_25", "COOL_2_8"]),
            "repeated-storage" => Body("Refused", storage: ["PROTECT_FROM_LIGHT", "PROTECT_FROM_LIGHT", "COOL_2_8"]),
            "exponent" => Body("Refused", conversions: [Conv("KGM", "GRM", "1e3", "1")]),
            "same-uom" => Body("Refused", conversions: [Conv("KGM", "KGM", "1", "1")]),
            "conversion-unit" => Body("Refused", conversions: [Conv("KGM", "LBR", "2.2", "1")]),
            "stockable-no-shelf-life" => Body("Refused", shelfLife: null, retest: null),
            "stockable-no-storage" => Body("Refused", storage: []),
            _ => Body("Refused")
        };
        if (fault == "retired") host.ItemReferences.Retired.Add("MGM");
        host.ItemReferences.Outage = fault == "outage";
        host.ItemReferences.Throw = fault == "provider-throws";

        var response = await CreateAsync(body);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(error, await FirstErrorAsync(response));
        Assert.False(await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.TenantId == _tenant).AnyAsync());
        Assert.False(await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant).AnyAsync());
    }

    [Fact]
    public async Task S1_Packaging_may_be_stockable_without_storage_or_shelf_life()
    {
        var response = await CreateAsync(Body("Blister foil", materialType: "PACKAGING_PRIMARY", storage: [],
            shelfLife: null, retest: null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task S1_A_write_on_an_old_version_is_a_concurrency_conflict()
    {
        var id = await CreatedAsync("Croscarmellose sodium");
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(id, UpdateBody("Croscarmellose sodium", 0, description: "first"))).StatusCode);
        var stale = await PutAsync(id, UpdateBody("Croscarmellose sodium", 0, description: "second"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ItemErrors.ConcurrencyConflict, await FirstErrorAsync(stale));
        Assert.Equal("first", (await ItemAsync(id)).Description);
    }

    // ------------------------------------------------------------------------------------------- idempotency

    [Fact]
    public async Task S1_The_same_key_gives_the_first_answer_and_other_facts_conflict()
    {
        var key = "ITEM-" + Guid.NewGuid().ToString("N");
        var first = await CreateAsync(Body("Silica colloidal", key: key));
        var again = await CreateAsync(Body("Silica colloidal", key: key));
        var other = await CreateAsync(Body("Silica colloidal anhydrous", key: key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await IdAsync(first), await IdAsync(again));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal(ItemErrors.IdempotencyKeyConflict, await FirstErrorAsync(other));
        Assert.Equal(1, await host.Database.GetCollection<Item>(ItemRepository.CollectionName)
            .CountDocumentsAsync(x => x.TenantId == _tenant));
    }

    // An item key never collides with another kind's creation key in the shared reservation ledger.
    [Fact]
    public async Task S1_An_item_key_and_another_kinds_key_never_meet()
    {
        var key = "SHARED-" + Guid.NewGuid().ToString("N");
        await new CodeReservationRepository(host.Database, new Tenant(_tenant))
            .ReserveAsync(CodeBearingEntityType.GlobalProduct, key.ToUpperInvariant(), "t", "t");
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(Body("Mannitol", key: key))).StatusCode);
    }

    // ------------------------------------------------------------------------------------------- O-8 lock

    [Fact]
    public async Task S1_On_an_active_item_a_filled_locked_field_is_refused_an_empty_one_filled_once()
    {
        var id = await CreatedAsync("Purified water", retest: 30, shelfLife: null, storage: ["AMBIENT_15_25"],
            stockable: true);
        await SetStatusAsync(id, ItemLifecycleStatus.Active);

        var locked = await PutAsync(id, UpdateBody("Purified water", 0, retest: 60, storage: ["AMBIENT_15_25"], stockable: true));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal(new[] { ItemErrors.CriticalFieldLocked, "RetestDays" }, await ErrorsAsync(locked));

        var firstFill = await PutAsync(id, UpdateBody("Purified water", 0, retest: 30, shelfLife: 365,
            storage: ["AMBIENT_15_25"], stockable: true));
        Assert.Equal(HttpStatusCode.OK, firstFill.StatusCode);
        var renamed = await PutAsync(id, UpdateBody("Purified water (EP)", 1, retest: 30, shelfLife: 365,
            storage: ["AMBIENT_15_25"], stockable: true, description: "edited"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var stored = await ItemAsync(id);
        Assert.Equal((365, "Purified water (EP)", 2), (stored.ShelfLifeDays!.Value, stored.Name, stored.Version));

        var changed = await PutAsync(id, UpdateBody("Purified water (EP)", 2, retest: 30, shelfLife: 400,
            storage: ["AMBIENT_15_25"], stockable: true));
        Assert.Equal(new[] { ItemErrors.CriticalFieldLocked, "ShelfLifeDays" }, await ErrorsAsync(changed));
    }

    // ------------------------------------------------------------------------------------------- lifecycle + §8.3

    [Fact]
    public async Task S1_Retirement_needs_deactivation_and_zero_stock_and_every_refusal_is_audited()
    {
        var id = await CreatedAsync("Ethanol 96%");
        await SetStatusAsync(id, ItemLifecycleStatus.Active);

        var direct = await PostAsync(id, "retire", Lifecycle(0, "obsolete"));
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
        Assert.Equal(ItemErrors.RetireRequiresDeactivation, await FirstErrorAsync(direct));

        var deactivated = await PostAsync(id, "deactivate", Lifecycle(0, "supplier stopped"));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(ItemLifecycleStatus.Deactivated, (await ItemAsync(id)).LifecycleStatus);

        host.Stock.Answer = InventoryStockCheck.NonZero();
        var nonZero = await PostAsync(id, "retire", Lifecycle(1, "obsolete"));
        Assert.Equal(HttpStatusCode.Conflict, nonZero.StatusCode);
        Assert.Equal(ItemErrors.RetireStockNotZero, await FirstErrorAsync(nonZero));
        Assert.Contains(host.Stock.Calls, call => call == (id, "Item"));

        host.Stock.Answer = InventoryStockCheck.Unavailable("PARTIAL_LEGAL_ENTITY_COVERAGE");
        var partial = await PostAsync(id, "retire", Lifecycle(1, "obsolete"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, partial.StatusCode);
        Assert.Equal(new[] { ItemErrors.RetireStockCheckUnavailable, "PARTIAL_LEGAL_ENTITY_COVERAGE" }, await ErrorsAsync(partial));

        host.Stock.Answer = InventoryStockCheck.Zero();
        host.Stock.Throw = true;
        var down = await PostAsync(id, "retire", Lifecycle(1, "obsolete"));
        Assert.Equal(new[] { ItemErrors.RetireStockCheckUnavailable, "STOCK_CHECK_FAILED" }, await ErrorsAsync(down));

        host.Stock.Throw = false;
        host.Stock.Delay = RetireItemHandler.StockCheckBudget + TimeSpan.FromSeconds(1);
        var slow = await PostAsync(id, "retire", Lifecycle(1, "obsolete"));
        Assert.Equal(new[] { ItemErrors.RetireStockCheckUnavailable, "STOCK_CHECK_BUDGET_EXHAUSTED" }, await ErrorsAsync(slow));
        Assert.Equal((ItemLifecycleStatus.Deactivated, 1), await StatusAsync(id));

        host.Stock.Delay = TimeSpan.Zero;
        var retired = await PostAsync(id, "retire", Lifecycle(1, "obsolete"));
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var stored = await ItemAsync(id);
        Assert.Equal(ItemLifecycleStatus.Retired, stored.LifecycleStatus);
        Assert.Null(stored.NameKey);

        // Retired is final; its code is never reused; its name may be used again.
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(id, "retire", Lifecycle(2, "again"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(id, "deactivate", Lifecycle(2, "again"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(id, UpdateBody("Ethanol 96%", 2))).StatusCode);
        var reuse = await CreateAsync(Body("Ethanol 96%"));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
        Assert.NotEqual(stored.CanonicalCode, (await ItemAsync(await IdAsync(reuse))).CanonicalCode);

        var trail = Trail(id, nameof(RetireItemCommand));
        Assert.Equal(new[] { 2, 2, 2, 2, 2, 1, 2 }, trail.Select(x => x.Outcome));
        Assert.All(trail, x => Assert.Equal(ItemAudit.EntityType, x.EntityType));
    }

    // The production client (no tenant-wide stock query yet, O-11) refuses — measured over the real class.
    [Fact]
    public async Task S1_Without_the_inventory_query_retirement_is_refused()
    {
        var id = await CreatedAsync("Starch maize");
        await SetStatusAsync(id, ItemLifecycleStatus.Deactivated);
        var handler = new RetireItemHandler(new ItemRepository(host.Database, new Tenant(_tenant)),
            new Diten.MdmService.Infrastructure.Inventory.UnavailableInventoryStockAvailabilityClient(), new Actor("t"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RetireItemHandler>.Instance);

        var refused = await handler.Handle(new RetireItemCommand(id, new ItemModels.ItemLifecycleRequest
            { ExpectedVersion = 0, Reason = "obsolete" }), CancellationToken.None);

        Assert.Equal(503, refused.StatusCode);
        Assert.Equal(new[] { ItemErrors.RetireStockCheckUnavailable, "INVENTORY_TENANT_WIDE_QUERY_NOT_AVAILABLE" }, refused.Errors);
        Assert.Equal((ItemLifecycleStatus.Deactivated, 0), await StatusAsync(id));
    }

    [Fact]
    public async Task S1_Submit_and_reactivation_wait_for_the_approval_and_write_nothing()
    {
        var draft = await CreatedAsync("Hypromellose");
        var submit = await PostAsync(draft, "submit", Lifecycle(0));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, submit.StatusCode);
        Assert.Equal(ItemErrors.ActivationApprovalUnavailable, await FirstErrorAsync(submit));
        Assert.Equal((ItemLifecycleStatus.Draft, 0), await StatusAsync(draft));
        var wrongState = await PostAsync(draft, "reactivation-requests", Lifecycle(0, "back"));
        Assert.Equal(ItemErrors.TransitionInvalid, await FirstErrorAsync(wrongState));

        await SetStatusAsync(draft, ItemLifecycleStatus.Deactivated);
        var reactivate = await PostAsync(draft, "reactivation-requests", Lifecycle(0, "back"));
        Assert.Equal(ItemErrors.ActivationApprovalUnavailable, await FirstErrorAsync(reactivate));
        Assert.Equal((ItemLifecycleStatus.Deactivated, 0), await StatusAsync(draft));
        Assert.Equal(ItemErrors.TransitionInvalid, await FirstErrorAsync(await PostAsync(draft, "submit", Lifecycle(0))));
        Assert.Equal(new[] { 2, 2 }, Trail(draft, nameof(RequestItemReactivationCommand)).Select(x => x.Outcome));
    }

    // ------------------------------------------------------------------------------------------- AUD-001 path b

    [Fact]
    public async Task S1_Every_item_write_is_forwarded_on_path_b_and_a_failed_hand_over_never_breaks_it()
    {
        host.Audit.ThrowOnForward = true;
        var id = await CreatedAsync("Glycerol");
        host.Audit.ThrowOnForward = false;
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(id, UpdateBody("Glycerol", 0, description: "x"))).StatusCode);
        await SetStatusAsync(id, ItemLifecycleStatus.Active);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(id, "deactivate", Lifecycle(1, "x"))).StatusCode);
        await PostAsync(id, "submit", Lifecycle(2));

        Assert.Single(Trail(id, nameof(UpdateItemCommand)), x => x.Outcome == 1);
        Assert.Single(Trail(id, nameof(DeactivateItemCommand)), x => x.Outcome == 1);
        Assert.Single(Trail(id, nameof(SubmitItemCommand)), x => x.Outcome == 2);
        Assert.All(host.Audit.Forwarded.Where(x => x.EntityId == id), x =>
        {
            Assert.Equal(ItemAudit.EntityType, x.EntityType);
            Assert.Equal(ItemAudit.SourceModule, x.SourceModule);
        });
        var next = await CreateAsync(Body("Glycerol 85%"));
        Assert.Single(Trail(await IdAsync(next), nameof(CreateItemCommand)), x => x.Outcome == 1);
    }

    // ------------------------------------------------------------------------------------------- legacy document

    [Fact]
    public async Task S1_A_document_without_the_later_fields_reads_and_lists()
    {
        var id = Guid.NewGuid();
        await host.Database.GetCollection<BsonDocument>(ItemRepository.CollectionName).InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard) },
            { "CanonicalCode", "IT-000000000777" },
            { "Name", "Legacy item" },
            { "MaterialType", (int)ItemMaterialType.RawMaterial },
            { "BaseUomCode", "KGM" },
            { "LifecycleStatus", (int)ItemLifecycleStatus.Draft },
            { "Version", 0 },
            { "WrittenByALaterSlice", true }
        });

        var read = await Client().GetAsync($"{Items}/{id}");
        Assert.True(read.StatusCode == HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
        var list = await Envelope.ReadAsync(await Client().GetAsync($"{Items}?q=IT-000000000777"));
        Assert.Equal(1, list.GetProperty("data").GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task S1_The_list_is_paged_ordered_by_code_and_rejects_bad_parameters()
    {
        foreach (var name in new[] { "Alpha excipient", "Beta excipient", "Gamma excipient" })
            await CreatedAsync(name, materialType: "EXCIPIENT");
        var page = await Envelope.ReadAsync(await Client().GetAsync($"{Items}?pageSize=2&materialType=EXCIPIENT&q=excipient"));
        var codes = page.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("canonicalCode").GetString()!).ToList();
        Assert.Equal(3, page.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        Assert.Equal(2, codes.Count);
        foreach (var bad in new[] { "pageSize=101", "page=0", "materialType=CONSUMABLE", "status=Archived", "q=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Client().GetAsync($"{Items}?{bad}")).StatusCode);
    }

    // ------------------------------------------------------------------------------------------- CT decision 1

    // No row bound: the code burns. A live row (any status, Retired included) keeps it.
    [Theory]
    [InlineData(null)]
    [InlineData(ItemLifecycleStatus.Draft)]
    [InlineData(ItemLifecycleStatus.Retired)]
    public async Task S1_The_burn_gate_burns_an_unbound_item_code_and_keeps_a_bound_one(ItemLifecycleStatus? row)
    {
        var (item, code) = await CutOffBeforeTheRowAsync("Gate " + row);
        if (row is { } status)
        {
            Assert.True((await new ItemRepository(host.Database, new Tenant(_tenant)).CreateDraftAsync(item)).Succeeded);
            await SetStatusAsync(item.Id, status);
        }

        var burned = await new CodeReservationRepository(host.Database, new Tenant(_tenant))
            .BurnIdentityBindingAsync(code.Id, item.Id, code.Version, "gate:burn", "gate", "gate", "TEST");

        Assert.Equal(row is null, burned.Succeeded);
        if (row is not null) Assert.Equal(CodeReservationBurnCodes.BoundRowExists, burned.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task S1_A_burn_and_an_item_insert_racing_never_leave_a_live_item_with_a_burned_code(bool burnReadsFirst)
    {
        var (item, code) = await CutOffBeforeTheRowAsync("Race " + burnReadsFirst);
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var rows = new ItemRepository(host.Database, new Tenant(_tenant));
        ReservationOperationResult? burned;
        ItemWriteResult? inserted = null;
        if (burnReadsFirst)
        {
            codes.AfterBoundRowCheckForTests = async () => inserted = await rows.CreateDraftAsync(item);
            burned = await codes.BurnIdentityBindingAsync(code.Id, item.Id, code.Version, "race:burn", "race", "race", "TEST");
        }
        else
        {
            burned = await codes.BurnIdentityBindingAsync(code.Id, item.Id, code.Version, "race:burn", "race", "race", "TEST");
            inserted = await rows.CreateDraftAsync(item);
        }

        var finalCode = await CodeAsync(code.Id);
        var finalRow = await host.Database.GetCollection<Item>(ItemRepository.CollectionName)
            .Find(x => x.Id == item.Id).FirstOrDefaultAsync();
        Assert.False(finalRow is not null && finalCode.BindingState == CodeReservationBindingState.Burned,
            "a live item with a burned code");
        if (burnReadsFirst)
        {
            Assert.True(inserted!.Succeeded, inserted.ErrorCode);
            Assert.Equal("CONCURRENCY_CONFLICT", burned.ErrorCode);
        }
        else
        {
            Assert.True(burned.Succeeded, burned.ErrorCode);
            Assert.Contains(inserted!.ErrorCode, new[] { "CODE_RESERVATION_REQUIRED", "CODE_RESERVATION_BURNED" });
            Assert.Null(finalRow);
        }
    }

    // The burn lands between the insert's pre-check and its transaction's reservation write: refused inside it.
    [Fact]
    public async Task S1_A_burn_between_the_precheck_and_the_transaction_is_refused_inside_it()
    {
        var (item, code) = await CutOffBeforeTheRowAsync("Between");
        ReservationOperationResult? burned = null;
        var rows = new ItemRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () => burned ??= await new CodeReservationRepository(host.Database,
                new Tenant(_tenant)).BurnIdentityBindingAsync(code.Id, item.Id, code.Version, "b:burn", "b", "b", "TEST")
        };

        var inserted = await rows.CreateDraftAsync(item);

        Assert.True(burned!.Succeeded, burned.ErrorCode);
        Assert.Equal("CODE_RESERVATION_BURNED", inserted.ErrorCode);
        Assert.Null(await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == item.Id)
            .FirstOrDefaultAsync());
    }

    // A refused insert (here the name lost to a concurrent create) burns its code; the answer is the named conflict.
    [Fact]
    public async Task S1_A_definitely_refused_insert_burns_its_code()
    {
        const string name = "Titanium dioxide";
        var rows = new ItemRepository(host.Database, new Tenant(_tenant));
        rows.BeforeReservationWriteForTests = async () =>
        {
            rows.BeforeReservationWriteForTests = null;
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(Body(name))).StatusCode);
        };
        var key = "ITEM-" + Guid.NewGuid().ToString("N");

        var refused = await Handler(rows).Handle(new CreateItemCommand(CreateRequest(name, key)), CancellationToken.None);

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(ItemErrors.NameConflict, refused.Errors[0]);
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == ItemRules.ReservationCommandPrefix + key.ToUpperInvariant())
            .SingleAsync();
        Assert.Equal(CodeReservationBindingState.Burned, code.BindingState);
    }

    // Every insert attempt meets a transient error: retried with backoff, then 202 — the code stays pending, no row; the
    // same key finishes it (the named exit, §8.4 B).
    [Fact]
    public async Task S1_A_transient_insert_error_to_the_last_attempt_is_ambiguous_and_the_same_key_finishes_it()
    {
        var key = "ITEM-" + Guid.NewGuid().ToString("N");
        var armed = false;
        var rows = new ItemRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                await FailPointAsync("insert", 112, "TransientTransactionError", ItemRepository.InsertAttempts);
            }
        };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Diten.Shared.Core.Response<Guid> first;
        try { first = await Handler(rows).Handle(new CreateItemCommand(CreateRequest("Iron oxide red", key)), CancellationToken.None); }
        finally { await FailPointOffAsync(); }

        Assert.True(armed);
        Assert.Equal(202, first.StatusCode);
        Assert.Equal(ItemErrors.BindingReconciliationRequired, first.Errors[0]);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(650), $"no backoff: {clock.Elapsed.TotalMilliseconds} ms");
        var reservationKey = ItemRules.ReservationCommandPrefix + key.ToUpperInvariant();
        var pending = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == reservationKey).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pending.BindingState);
        Assert.False(await host.Database.GetCollection<Item>(ItemRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).AnyAsync());

        var again = await CreateAsync(Body("Iron oxide red", key: key));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var item = await ItemAsync(await IdAsync(again));
        Assert.Equal(pending.ReservedCode, item.CanonicalCode);
        Assert.Equal(CodeReservationBindingState.Confirmed, (await CodeAsync(pending.Id)).BindingState);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private HttpClient Client() => host.Client(host.Token(_tenant, _user, All));

    private Task<HttpResponseMessage> CreateAsync(object body) => Client().PostAsJsonAsync(Items, body);

    private Task<HttpResponseMessage> PutAsync(Guid id, object body) => Client().PutAsJsonAsync($"{Items}/{id}", body);

    private Task<HttpResponseMessage> PostAsync(Guid id, string door, object body) =>
        Client().PostAsJsonAsync($"{Items}/{id}/{door}", body);

    private async Task<Guid> CreatedAsync(string name, string materialType = "RAW_MATERIAL", int? shelfLife = 730,
        int? retest = null, string[]? storage = null, bool stockable = true)
    {
        var response = await CreateAsync(Body(name, materialType: materialType, shelfLife: shelfLife, retest: retest,
            storage: storage, stockable: stockable));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await IdAsync(response);
    }

    private static object Conv(string from, string to, string numerator, string denominator) =>
        new { fromUom = from, toUom = to, numerator, denominator };

    private static Dictionary<string, object?> Fields(string name, string materialType, string baseUom, object[]? conversions,
        string[]? storage, int? shelfLife, int? retest, bool stockable, string? description)
        => new()
        {
            ["name"] = name,
            ["description"] = description,
            ["materialType"] = materialType,
            ["baseUomCode"] = baseUom,
            ["uomConversions"] = conversions ?? [Conv("KGM", "GRM", "1000", "1")],
            ["storageConditionCodes"] = storage ?? ["AMBIENT_15_25", "PROTECT_FROM_LIGHT"],
            ["lotControlled"] = true,
            ["stockable"] = stockable,
            ["shelfLifeDays"] = shelfLife,
            ["retestDays"] = retest,
            ["minRemainingShelfLifeDays"] = null
        };

    private static Dictionary<string, object?> Body(string name, string materialType = "RAW_MATERIAL", string baseUom = "KGM",
        object[]? conversions = null, string[]? storage = null, int? shelfLife = 730, int? retest = null,
        bool stockable = true, string? key = null, (string Name, object Value)? extra = null)
    {
        var body = Fields(name, materialType, baseUom, conversions, storage, shelfLife, retest, stockable, null);
        body["idempotencyKey"] = key ?? "ITEM-" + Guid.NewGuid().ToString("N");
        if (extra is { } field) body[field.Name] = field.Value;
        return body;
    }

    private static Dictionary<string, object?> UpdateBody(string name, int version, int? shelfLife = 730, int? retest = null,
        string[]? storage = null, bool stockable = true, string? description = null, (string Name, object Value)? extra = null)
    {
        var body = Fields(name, "RAW_MATERIAL", "KGM", null, storage, shelfLife, retest, stockable, description);
        body["expectedVersion"] = version;
        if (extra is { } field) body[field.Name] = field.Value;
        return body;
    }

    private static object Lifecycle(int version, string? reason = null) => new { expectedVersion = version, reason };

    private static ItemModels.CreateItemRequest CreateRequest(string name, string key) => new()
    {
        IdempotencyKey = key, Name = name, MaterialType = "RAW_MATERIAL", BaseUomCode = "KGM",
        StorageConditionCodes = ["AMBIENT_15_25"], LotControlled = true, Stockable = true, ShelfLifeDays = 730
    };

    private CreateItemHandler Handler(ItemRepository rows) =>
        new(rows, new CodeReservationRepository(host.Database, new Tenant(_tenant)), host.ItemReferences, new Actor(_user.ToString("D")));

    // A code reserved and consumed for a new item whose row is not written yet (the moment between consume and insert).
    private async Task<(Item Item, CodeReservation Code)> CutOffBeforeTheRowAsync(string name)
    {
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var command = ItemRules.ReservationCommandPrefix + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var reserved = await codes.ReserveAsync(CodeBearingEntityType.Item, command, "t", "t");
        var consumed = await codes.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Item, Guid.NewGuid(),
            reserved.Version, command, "t", "t");
        Assert.True(consumed.Succeeded, consumed.ErrorCode);
        var code = consumed.Reservation!;
        var item = new Item
        {
            Id = code.ConsumedEntityId!.Value, CanonicalCode = code.ReservedCode, CodeReservationId = code.Id,
            CreationCommandId = command[ItemRules.ReservationCommandPrefix.Length..], CreationFingerprint = "F",
            Name = name, NameNormalized = ItemRules.NormalizeName(name),
            NameKey = ItemRules.NameKey(ItemMaterialType.RawMaterial, ItemRules.NormalizeName(name)),
            MaterialType = ItemMaterialType.RawMaterial, BaseUomCode = "KGM"
        };
        return (item, code);
    }

    private async Task SetStatusAsync(Guid id, ItemLifecycleStatus status) =>
        await host.Database.GetCollection<Item>(ItemRepository.CollectionName).UpdateOneAsync(
            x => x.Id == id, Builders<Item>.Update.Set(x => x.LifecycleStatus, status));

    private async Task<Item> ItemAsync(Guid id) =>
        await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == id).SingleAsync();

    private async Task<(ItemLifecycleStatus, int)> StatusAsync(Guid id)
    {
        var item = await ItemAsync(id);
        return (item.LifecycleStatus, item.Version);
    }

    private async Task<CodeReservation> CodeAsync(Guid id) =>
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == id).SingleAsync();

    private List<Diten.MdmService.Application.Contracts.Audit.AuditForwardRequest> Trail(Guid id, string requestType) =>
        host.Audit.Forwarded.Where(x => x.EntityId == id && x.RequestType == requestType).ToList();

    private static async Task<Guid> IdAsync(HttpResponseMessage response) =>
        (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response) =>
        Envelope.FirstError(await Envelope.ReadAsync(response));

    private static async Task<List<string>> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await Envelope.ReadAsync(response);
        return body.ValueKind == JsonValueKind.Object && body.TryGetProperty("errors", out var errors)
            ? errors.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
    }

    private async Task FailPointAsync(string command, int errorCode, string label, int times) =>
        await host.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" },
            { "mode", new BsonDocument("times", times) },
            { "data", new BsonDocument
                {
                    { "failCommands", new BsonArray { command } },
                    { "errorCode", errorCode },
                    { "errorLabels", new BsonArray { label } }
                }
            }
        });

    private async Task FailPointOffAsync() =>
        await host.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", "off" }
        });

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class Actor(string id) : IProductIdentityActorContext
    {
        public string ActorId { get; } = id;
    }
}
