using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Api.Audit;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Contracts.Inventory;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.Inventory;
using Diten.MdmService.Infrastructure.ReferenceData;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S1 FIX1 (P-2026-10-07-FU04-S1-FIX1): O-2 on an untrusted list answer, the audit record's before / after
/// / metadata measured on the PRODUCTION forwarder's HTTP body, every door's own permission key (CT I1 / I2), the
/// conversion lock (CT I6) and M3 – M12.
/// </summary>
public sealed partial class ItemS1HttpTests
{
    // ------------------------------------------------------------------------------------------- item 1 (O-2)

    [Theory]
    [InlineData("omit-kind")]
    [InlineData("null-attributes")]
    public async Task Fix1_A_storage_answer_without_its_kind_is_untrusted_never_a_pass(string fault)
    {
        host.ItemReferences.OmitConditionKind = fault == "omit-kind";
        host.ItemReferences.NullAttributes = fault == "null-attributes";

        var response = await CreateAsync(Body("Untrusted kind", storage: ["AMBIENT_15_25", "COOL_2_8"]));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(new[] { ItemErrors.ReferenceListUnavailable, ItemReferenceCheck.UntrustedAnswer }, await ErrorsAsync(response));
        Assert.False(await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.TenantId == _tenant).AnyAsync());
    }

    // ------------------------------------------------------------------------------------------- item 2 (AUD-001 §3 #10)

    [Fact]
    public async Task Fix1_The_record_on_the_wire_carries_only_the_changed_fields_and_why()
    {
        var wire = CaptureTheProductionForwarder();
        var id = await CreatedAsync("Wire item", retest: 30, shelfLife: null, storage: ["AMBIENT_15_25"]);
        var created = WireBody(wire, nameof(CreateItemCommand), id);
        Assert.False(created.TryGetProperty("beforeState", out _));
        Assert.Equal("Wire item", created.GetProperty("afterState").GetProperty("Name").GetString());

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(id, UpdateBody("Wire item", 0, retest: 30, shelfLife: null,
            storage: ["AMBIENT_15_25"], description: "now described"))).StatusCode);
        var updated = WireBody(wire, nameof(UpdateItemCommand), id);
        Assert.Equal(new[] { "Description" }, Keys(updated.GetProperty("beforeState")));
        Assert.Equal(new[] { "Description" }, Keys(updated.GetProperty("afterState")));
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("beforeState").GetProperty("Description").ValueKind);
        Assert.Equal("now described", updated.GetProperty("afterState").GetProperty("Description").GetString());
        Assert.False(updated.TryGetProperty("metadata", out _));

        // First fill on an Active item: named.
        await SetStatusAsync(id, ItemLifecycleStatus.Active);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(id, UpdateBody("Wire item", 1, retest: 30, shelfLife: 365,
            storage: ["AMBIENT_15_25"], description: "now described"))).StatusCode);
        var filled = WireBody(wire, nameof(UpdateItemCommand), id);
        Assert.Equal(new[] { "ShelfLifeDays" }, Keys(filled.GetProperty("afterState")));
        Assert.Equal(new[] { "ShelfLifeDays" },
            filled.GetProperty("metadata").GetProperty(ItemAudit.FirstFill).EnumerateArray().Select(x => x.GetString()));

        // A refused retire (Active): the refusal code, the reason, the lifecycle — operation Deactivate (M7).
        await PostAsync(id, "retire", Lifecycle(2, "OBSOLETE"));
        var refused = WireBody(wire, nameof(RetireItemCommand), id);
        Assert.Equal(2, refused.GetProperty("outcome").GetInt32());
        Assert.Equal((int)AuditOperation.Deactivate, refused.GetProperty("operation").GetInt32());
        var metadata = refused.GetProperty("metadata");
        Assert.Equal(ItemErrors.RetireRequiresDeactivation, metadata.GetProperty(AuditDetails.RefusalCodeKey).GetString());
        Assert.True(metadata.GetProperty(ItemAudit.ReasonProvided).GetBoolean());
        Assert.Equal("Retired", metadata.GetProperty(ItemAudit.Lifecycle).GetString());

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(id, "deactivate", Lifecycle(2, "SUPPLIER_STOPPED"))).StatusCode);
        var deactivated = WireBody(wire, nameof(DeactivateItemCommand), id);
        Assert.Equal((int)AuditOperation.Deactivate, deactivated.GetProperty("operation").GetInt32());
        Assert.Equal("Active", deactivated.GetProperty("beforeState").GetProperty("LifecycleStatus").GetString());
        Assert.Equal("Deactivated", deactivated.GetProperty("afterState").GetProperty("LifecycleStatus").GetString());
        Assert.True(deactivated.GetProperty("metadata").GetProperty(ItemAudit.ReasonProvided).GetBoolean());

        await PostAsync(id, "reactivation-requests", Lifecycle(3, "BACK"));
        Assert.Equal((int)AuditOperation.Reactivate, WireBody(wire, nameof(RequestItemReactivationCommand), id)
            .GetProperty("operation").GetInt32());
    }

    // FIX1-EK (CT decision): a reason that names a person never reaches the record — only that a reason was given.
    [Fact]
    public async Task Fix1Ek_The_reason_text_never_reaches_the_audit_record()
    {
        const string reason = "Retired at the request of Jane Doe (jane.doe@example.com)";
        var wire = CaptureTheProductionForwarder();
        var id = await CreatedAsync("Reason privacy");
        await SetStatusAsync(id, ItemLifecycleStatus.Deactivated);

        var retired = await PostAsync(id, "retire", Lifecycle(0, reason));

        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var text = Assert.Single(wire, body => body.Contains(nameof(RetireItemCommand), StringComparison.Ordinal));
        Assert.DoesNotContain("Jane", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jane.doe@example.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(JsonDocument.Parse(text).RootElement.GetProperty("metadata").GetProperty(ItemAudit.ReasonProvided).GetBoolean());
        Assert.Equal(reason, (await ItemAsync(id)).RetirementReason);
    }

    // M7 on the recorded requests too, and (item 2) only allow-listed names ever reach the record.
    [Fact]
    public async Task Fix1_Item_records_carry_only_allow_listed_names_and_the_lifecycle_operations()
    {
        var id = await CreatedAsync("Allow-listed");
        await PutAsync(id, UpdateBody("Allow-listed", 0, description: "x"));
        await SetStatusAsync(id, ItemLifecycleStatus.Active);
        await PostAsync(id, "deactivate", Lifecycle(1, "R"));
        await PostAsync(id, "retire", Lifecycle(2, "R"));
        await PostAsync(id, "reactivation-requests", Lifecycle(3, "R"));

        var records = host.Audit.Forwarded.Where(x => x.EntityId == id).ToList();
        Assert.All(records, record =>
        {
            Assert.All((record.BeforeState?.Keys ?? []).Concat(record.AfterState?.Keys ?? []),
                key => Assert.Contains(key, ItemAudit.Fields));
            Assert.All(record.Metadata?.Keys ?? [], key => Assert.Contains(key, ItemAudit.MetadataKeys));
        });
        Assert.All(Trail(id, nameof(DeactivateItemCommand)), x => Assert.Equal(5, x.Operation));
        Assert.All(Trail(id, nameof(RetireItemCommand)), x => Assert.Equal(5, x.Operation));
        Assert.All(Trail(id, nameof(RequestItemReactivationCommand)), x => Assert.Equal(7, x.Operation));
    }

    // Backward compatible: a command without details sends exactly the body it sent before (no new key).
    [Fact]
    public async Task Fix1_A_record_without_details_keeps_its_old_body()
    {
        var wire = CaptureTheProductionForwarder();
        await host.Audit.Inner!.ForwardAsync(new AuditForwardRequest("CreateBrandCommand", 4, 1, "Brand", "brand-product-master",
            Guid.NewGuid(), 1));
        var body = JsonDocument.Parse(Assert.Single(wire)).RootElement;
        Assert.Equal(new[]
            {
                "actorEmail", "actorId", "actorType", "category", "correlationId", "entityId", "entityType",
                "isPlatformGlobal", "occurredAtUtc", "operation", "outcome", "requestType", "sourceModule",
                "sourceService", "targetTenantId"
            },
            Keys(body).Order(StringComparer.Ordinal));
    }

    // ------------------------------------------------------------------------------------------- CT I1 / I2

    public static TheoryData<string, string, string, string> Doors => new()
    {
        { "GET", "", "mdm.items.read", "mdm.items.create" },
        { "GET", "/{id}", "mdm.items.read", "mdm.items.update" },
        { "POST", "", "mdm.items.create", "mdm.items.read" },
        { "PUT", "/{id}", "mdm.items.update", "mdm.items.read" },
        { "POST", "/{id}/submit", "mdm.items.submit", "mdm.items.update" },
        { "POST", "/{id}/reactivation-requests", "mdm.items.submit", "mdm.items.deactivate" },
        { "POST", "/{id}/deactivate", "mdm.items.deactivate", "mdm.items.retire" },
        { "POST", "/{id}/retire", "mdm.items.retire", "mdm.items.deactivate" }
    };

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task Fix1_Every_door_asks_its_own_key_and_a_neighbouring_key_is_403(string method, string path,
        string own, string neighbour)
    {
        var id = await CreatedAsync("Door " + Guid.NewGuid().ToString("N")[..8]);
        Task<HttpResponseMessage> Send(string key)
        {
            var client = host.Client(host.Token(_tenant, _user, key));
            var url = Items + path.Replace("{id}", id.ToString());
            return method switch
            {
                "GET" => client.GetAsync(url),
                "PUT" => client.PutAsJsonAsync(url, UpdateBody("Door", 0)),
                _ => path.Length == 0
                    ? client.PostAsJsonAsync(url, Body("Door other " + Guid.NewGuid().ToString("N")[..8]))
                    : client.PostAsJsonAsync(url, Lifecycle(0, "R"))
            };
        }

        var neighbourAnswer = await Send(neighbour);
        var ownAnswer = await Send(own);

        Assert.Equal(HttpStatusCode.Forbidden, neighbourAnswer.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, ownAnswer.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, ownAnswer.StatusCode);
    }

    // ------------------------------------------------------------------------------------------- CT I6 (O-8)

    [Fact]
    public async Task Fix1_On_an_active_item_a_filled_conversion_list_is_locked_and_its_order_is_not_a_change()
    {
        object[] two = [Conv("KGM", "GRM", "1000", "1"), Conv("XBX", "KGM", "25", "1")];
        object[] swapped = [Conv("XBX", "KGM", "25", "1"), Conv("KGM", "GRM", "1000", "1")];
        object[] changed = [Conv("KGM", "GRM", "1000", "1"), Conv("XBX", "KGM", "20", "1")];
        var empty = await CreatedWithAsync("Empty conversions", []);
        var filled = await CreatedWithAsync("Filled conversions", two);
        await SetStatusAsync(empty, ItemLifecycleStatus.Active);
        await SetStatusAsync(filled, ItemLifecycleStatus.Active);

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(empty, Update("Empty conversions", two))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(filled, Update("Filled conversions", swapped))).StatusCode);
        var locked = await PutAsync(filled, Update("Filled conversions", changed, version: 1));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal(new[] { ItemErrors.CriticalFieldLocked, "UomConversions" }, await ErrorsAsync(locked));
    }

    // ------------------------------------------------------------------------------------------- M3 / M4

    [Theory]
    [InlineData("page=2147483647")]
    [InlineData("page=1000001")]
    public async Task Fix1_A_page_past_the_ceiling_is_400_never_an_overflow(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Client().GetAsync($"{Items}?{query}")).StatusCode);

    [Theory]
    [InlineData("baseUom", "ITEM_BASE_UOM_INVALID")]
    [InlineData("storage", "ITEM_STORAGE_CONDITION_INVALID")]
    [InlineData("conversion", ItemErrors.UomConversionInvalid)]
    public async Task Fix1_A_code_longer_than_40_is_400(string field, string error)
    {
        var tooLong = new string('X', 41);
        var body = field switch
        {
            "baseUom" => Body("Long code", baseUom: tooLong),
            "storage" => Body("Long code", storage: [tooLong]),
            _ => Body("Long code", conversions: [Conv(tooLong, "GRM", "1", "1")])
        };
        var response = await CreateAsync(body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await ErrorsAsync(response));
    }

    // ------------------------------------------------------------------------------------------- M6

    [Fact]
    public async Task Fix1_During_a_list_outage_an_active_item_can_still_be_renamed()
    {
        var id = await CreatedAsync("Before the outage");
        await SetStatusAsync(id, ItemLifecycleStatus.Active);
        host.ItemReferences.Outage = true;
        var calls = host.ItemReferences.Calls.Count;

        var renamed = await PutAsync(id, UpdateBody("After the outage", 0));

        Assert.True(renamed.StatusCode == HttpStatusCode.OK, await renamed.Content.ReadAsStringAsync());
        Assert.Equal(calls, host.ItemReferences.Calls.Count);
        Assert.Equal("After the outage", (await ItemAsync(id)).Name);
    }

    // ------------------------------------------------------------------------------------------- M8 / M9 / M10

    [Fact]
    public async Task Fix1_A_same_key_replay_is_recorded_as_a_replay()
    {
        var key = "ITEM-" + Guid.NewGuid().ToString("N");
        var first = await CreateAsync(Body("Replayed", key: key));
        Assert.Equal(HttpStatusCode.OK, (await CreateAsync(Body("Replayed", key: key))).StatusCode);
        var trail = Trail(await IdAsync(first), nameof(CreateItemCommand));
        Assert.Equal(2, trail.Count);
        Assert.Null(trail[0].Metadata);
        Assert.Equal(true, trail[1].Metadata![ItemAudit.Replay]);
    }

    [Fact]
    public async Task Fix1_The_key_of_a_deleted_row_is_spent()
    {
        var key = "ITEM-" + Guid.NewGuid().ToString("N");
        await host.Database.GetCollection<BsonDocument>(ItemRepository.CollectionName).InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard) },
            { "CanonicalCode", "IT-" + Random.Shared.NextInt64(100_000_000_000, 999_999_999_999) },
            { "CreationCommandId", key.ToUpperInvariant() },
            { "Name", "Deleted" }, { "IsDeleted", true }, { "Version", 0 }
        });

        var response = await CreateAsync(Body("Deleted", key: key));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ItemErrors.CreationCommandTombstoned, await FirstErrorAsync(response));
    }

    [Fact]
    public async Task Fix1_A_deactivated_item_is_not_edited()
    {
        var id = await CreatedAsync("Deactivated edit");
        await SetStatusAsync(id, ItemLifecycleStatus.Deactivated);
        var response = await PutAsync(id, UpdateBody("Deactivated edit", 0, description: "x"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ItemErrors.DeactivatedNotEditable, await FirstErrorAsync(response));
        Assert.Null((await ItemAsync(id)).Description);
    }

    // ------------------------------------------------------------------------------------------- M11

    // (a) an unknown commit, a lost connection, a server timeout: 202, the code pending, no row.
    [Theory]
    [InlineData("commit-unknown")]
    [InlineData("connection")]
    [InlineData("timeout")]
    public async Task Fix1_An_unknown_write_outcome_is_202_and_burns_nothing(string fault)
    {
        var key = "ITEM-" + Guid.NewGuid().ToString("N");
        var armed = false;
        var rows = new ItemRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                await (fault switch
                {
                    "commit-unknown" => FailCommandAsync("commitTransaction", 91, "UnknownTransactionCommitResult", 2),
                    "connection" => FailCommandAsync("insert", null, null, ItemRepository.InsertAttempts, closeConnection: true),
                    _ => FailCommandAsync("insert", 50, null, 1)
                });
            }
        };
        Diten.Shared.Core.Response<Guid> response;
        try { response = await Handler(rows).Handle(new CreateItemCommand(CreateRequest("Unknown " + fault, key)), CancellationToken.None); }
        finally { await FailPointOffAsync(); }

        Assert.True(armed);
        Assert.Equal(202, response.StatusCode);
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == ItemRules.ReservationCommandPrefix + key.ToUpperInvariant())
            .SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.False(await host.Database.GetCollection<Item>(ItemRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).AnyAsync());
    }

    // (b) the name is unique per TENANT: the same name in two tenants is two items.
    [Fact]
    public async Task Fix1_The_same_name_in_two_tenants_is_two_items()
    {
        await CreatedAsync("Shared across tenants");
        var other = host.Client(host.Token(Guid.NewGuid(), _user, All));
        Assert.Equal(HttpStatusCode.Created, (await other.PostAsJsonAsync(Items, Body("Shared across tenants"))).StatusCode);
    }

    // (c) production wiring: both providers fail closed, resolved from the real registration.
    [Fact]
    public void Fix1_Production_wires_both_fail_closed_providers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.IsType<UnavailableItemReferenceResolver>(provider.GetRequiredService<IItemReferenceResolver>());
        Assert.IsType<UnavailableInventoryStockAvailabilityClient>(provider.GetRequiredService<IInventoryStockAvailabilityClient>());
    }

    // ------------------------------------------------------------------------------------------- helpers (FIX1)

    private async Task<Guid> CreatedWithAsync(string name, object[] conversions)
    {
        var response = await CreateAsync(Body(name, conversions: conversions));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await IdAsync(response);
    }

    private static Dictionary<string, object?> Update(string name, object[] conversions, int version = 0)
    {
        var body = Fields(name, "RAW_MATERIAL", "KGM", conversions, null, 730, null, true, null);
        body["expectedVersion"] = version;
        return body;
    }

    // The production forwarder under the recorder; its HTTP body to Platform is captured.
    private ConcurrentQueue<string> CaptureTheProductionForwarder()
    {
        var wire = new ConcurrentQueue<string>();
        host.Audit.Inner = new PlatformAuditForwarder(
            new CapturingFactory(wire),
            Options.Create(new PlatformRegistrationOptions { BaseUrl = "http://platform.test", InternalApiKey = "test-only" }),
            host.Factory.Services.GetRequiredService<IHttpContextAccessor>(),
            NullLogger<PlatformAuditForwarder>.Instance);
        return wire;
    }

    private static JsonElement WireBody(ConcurrentQueue<string> wire, string requestType, Guid id) =>
        wire.Select(text => JsonDocument.Parse(text).RootElement)
            .Last(body => body.GetProperty("requestType").GetString() == requestType
                          && body.GetProperty("entityId").GetString() == id.ToString());

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(x => x.Name).ToArray();

    private async Task FailCommandAsync(string command, int? errorCode, string? label, int times, bool closeConnection = false)
    {
        var data = new BsonDocument { { "failCommands", new BsonArray { command } } };
        if (errorCode is { } code) data["errorCode"] = code;
        if (label is not null) data["errorLabels"] = new BsonArray { label };
        if (closeConnection) data["closeConnection"] = true;
        await host.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", new BsonDocument("times", times) }, { "data", data }
        });
    }

    private sealed class CapturingFactory(ConcurrentQueue<string> wire) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Capturing(wire));
    }

    private sealed class Capturing(ConcurrentQueue<string> wire) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            wire.Enqueue(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
