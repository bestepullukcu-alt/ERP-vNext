using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Persistence.Traceability;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using Diten.InventoryService.Tests.Warehouse;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// The REAL host for MOD-0181 (Program.cs, real middleware, real MediatR pipeline with the central-audit behaviour, real Mongo repositories on the
/// suite's own replica set) on top of <see cref="WarehouseApiFactory"/> (its fake AUTH assignee directory and PRODUCT-MASTER wrapper). It opens
/// ONLY seams around production code and changes no decision: the release probe between the two snapshot reads (AC-52 (a), AC-13 (c)), MOD-0181's
/// own clock, a failing record counter (AC-12), a failure / barrier around the count transaction (AC-1, AC-9, SB3/F-1) and — DL-2 — a driver error
/// before the submit's ledger read (AC-20). The <c>wms_count_*</c> records of its tenants, and the lots a test registered for them, are removed on
/// dispose (DB-010).
/// </summary>
public sealed class CycleCountApiFactory : WarehouseApiFactory
{
    private readonly ConcurrentBag<Guid> _countTenants = [];

    /// <summary>tenant → runs between the release's balance read and its movement-id read (the production probe is a no-op).</summary>
    public ConcurrentDictionary<Guid, Func<Task>> AfterBalanceRead { get; } = new();

    /// <summary>MOD-0181's own clock (only this one moves — pack §4.1 r5 SB1/F-3).</summary>
    public MovableClock CountClock { get; } = new();

    /// <summary>tenants whose record counter fails (503 path, AC-12).</summary>
    public ConcurrentDictionary<Guid, bool> CounterDown { get; } = new();

    /// <summary>tenant → runs before each count transaction of that tenant (a barrier for the concurrency cases).</summary>
    public ConcurrentDictionary<Guid, Func<Task>> BeforeCountTransaction { get; } = new();

    /// <summary>tenants whose next timeline insert throws inside the transaction (AC-1 "same transaction", SB3/F-1 skipped number).</summary>
    public ConcurrentDictionary<Guid, bool> FailNextEventInsert { get; } = new();

    /// <summary>DL-2 AC-20: tenants whose submit meets a driver error where the ledger is read (production code must answer 503, write nothing).</summary>
    public ConcurrentDictionary<Guid, bool> LedgerReadDown { get; } = new();

    public void TrackCountTenant(Guid tenant)
    {
        _countTenants.Add(tenant);
        TrackWarehouseTenant(tenant);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new CycleCountTime(CountClock));
            services.AddScoped<ICycleCountReleaseProbe>(sp => new SeamedReleaseProbe(sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<ICycleCountSubmitProbe>(sp => new SeamedSubmitProbe(sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<WarehouseRecordCodeAllocator>();
            services.AddScoped<IWarehouseRecordCodeAllocator>(sp => new SeamedAllocator(sp.GetRequiredService<WarehouseRecordCodeAllocator>(),
                sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<CycleCountRepository>();
            services.AddScoped<ICycleCountRepository>(sp => new SeamedCountRepository(sp.GetRequiredService<CycleCountRepository>(),
                sp.GetRequiredService<ITenantContext>(), this));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_countTenants.IsEmpty)
        {
            var tenants = new BsonArray(_countTenants.Select(t => t.ToString()));
            foreach (var name in CycleCountCollections.All.Append(TraceCollections.Lots))
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}

public sealed class SeamedReleaseProbe(ITenantContext tenant, CycleCountApiFactory factory) : ICycleCountReleaseProbe
{
    public Task AfterBalanceReadAsync(CancellationToken cancellationToken = default)
        => factory.AfterBalanceRead.TryGetValue(tenant.TenantId, out var seam) ? seam() : Task.CompletedTask;
}

public sealed class SeamedSubmitProbe(ITenantContext tenant, CycleCountApiFactory factory) : ICycleCountSubmitProbe
{
    public Task BeforeLedgerReadAsync(CancellationToken cancellationToken = default)
        => factory.LedgerReadDown.ContainsKey(tenant.TenantId) ? throw new MongoClientException("ledger unreadable (test seam)") : Task.CompletedTask;
}

public sealed class SeamedAllocator(WarehouseRecordCodeAllocator inner, ITenantContext tenant, CycleCountApiFactory factory) : IWarehouseRecordCodeAllocator
{
    public Task<string> NextAsync(string companyCodePrefix, string typePrefix, CancellationToken cancellationToken = default)
        => factory.CounterDown.ContainsKey(tenant.TenantId)
            ? throw new WarehouseRecordCodeUnavailableException("counter down (test seam)")
            : inner.NextAsync(companyCodePrefix, typePrefix, cancellationToken);
}

/// <summary>The production repository; only a barrier before the transaction and a failing timeline insert inside it can be staged.</summary>
public sealed class SeamedCountRepository(CycleCountRepository inner, ITenantContext tenant, CycleCountApiFactory factory) : ICycleCountRepository
{
    public Task<CycleCount?> FindAsync(Guid countId, CancellationToken cancellationToken = default) => inner.FindAsync(countId, cancellationToken);

    public Task<CycleCount?> FindIncludingDeletedAsync(Guid countId, CancellationToken cancellationToken = default) => inner.FindIncludingDeletedAsync(countId, cancellationToken);

    public Task<IReadOnlyList<CycleCount>> FindManyAsync(IReadOnlyCollection<Guid> countIds, CancellationToken cancellationToken = default)
        => inner.FindManyAsync(countIds, cancellationToken);

    public Task<CycleCount?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<CycleCountPage> ListAsync(CycleCountQuery query, CancellationToken cancellationToken = default) => inner.ListAsync(query, cancellationToken);

    public Task<CycleCountLinePage> LinesAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.LinesAsync(countId, page, pageSize, cancellationToken);

    public Task<CycleCountEventPage> EventsAsync(Guid countId, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.EventsAsync(countId, page, pageSize, cancellationToken);

    public Task<CycleCountLine?> FindLineAsync(Guid countId, Guid lineId, CancellationToken cancellationToken = default)
        => inner.FindLineAsync(countId, lineId, cancellationToken);

    public Task<CycleCountEvent?> FindKeyEventAsync(Guid countId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindKeyEventAsync(countId, actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<CycleCountEvent>> FindBulkKeyEventsAsync(Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindBulkKeyEventsAsync(actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<(Guid ItemId, Guid SkuId, SkuLevel Level)>> ScopeSkusAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default)
        => inner.ScopeSkusAsync(request, cancellationToken);

    public async Task<CycleCountWriteOutcome> InTransactionAsync(Func<ICycleCountWriteSession, CancellationToken, Task<CycleCountWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        if (factory.BeforeCountTransaction.TryGetValue(tenant.TenantId, out var before)) await before();
        var fail = factory.FailNextEventInsert.TryRemove(tenant.TenantId, out _);
        return await inner.InTransactionAsync((session, ct) => work(fail ? new FailingEventsSession(session) : session, ct), cancellationToken);
    }

    private sealed class FailingEventsSession(ICycleCountWriteSession inner) : ICycleCountWriteSession
    {
        public Task InsertCountAsync(CycleCount count, CancellationToken cancellationToken = default) => inner.InsertCountAsync(count, cancellationToken);

        public Task<bool> ReplaceCountAsync(CycleCount count, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.ReplaceCountAsync(count, expectedVersion, cancellationToken);

        public Task InsertLinesAsync(IReadOnlyList<CycleCountLine> lines, CancellationToken cancellationToken = default) => inner.InsertLinesAsync(lines, cancellationToken);

        public Task<bool> ReplaceLinesAsync(IReadOnlyList<(CycleCountLine Line, int ExpectedVersion)> lines, CancellationToken cancellationToken = default)
            => inner.ReplaceLinesAsync(lines, cancellationToken);

        public Task<IReadOnlyList<CycleCountLine>> ReadLinesAsync(Guid countId, CancellationToken cancellationToken = default) => inner.ReadLinesAsync(countId, cancellationToken);

        public Task<CycleCountLedgerRead> ReadVarianceLedgerAsync(CycleCountLedgerRequest request, CancellationToken cancellationToken = default)
            => inner.ReadVarianceLedgerAsync(request, cancellationToken);

        public Task InsertEventsAsync(IReadOnlyList<CycleCountEvent> events, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("timeline insert failed (test seam)");

        public Task InsertBinClaimsAsync(IReadOnlyList<CycleCountBinClaim> claims, CancellationToken cancellationToken = default)
            => inner.InsertBinClaimsAsync(claims, cancellationToken);

        public Task<long> ReleaseBinClaimsAsync(Guid countId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => inner.ReleaseBinClaimsAsync(countId, at, cancellationToken);

        public Task<CycleCountScopeSnapshot> ReadScopeSnapshotAsync(CycleCountScopeRequest request, CancellationToken cancellationToken = default)
            => inner.ReadScopeSnapshotAsync(request, cancellationToken);
    }
}

/// <summary>
/// One isolated cycle-count world: a company with code <c>GMG</c> in the MDM stub, the inventory scenario's warehouse (bins A and B under the
/// storage location; bin C in the second warehouse), stock put in by the REAL ledger API, and calls to the REAL count API — every
/// <c>/api/warehouse/cycle-counts*</c> answer checked against the WMS-COUNT-BUNDLE draft (<see cref="CycleCountDraftContractHandler"/>).
/// </summary>
public sealed class CycleCountScenario
{
    public const string Read = "cycle-count.counts.read";
    public const string Create = "cycle-count.counts.create";
    public const string Update = "cycle-count.counts.update";
    public const string BulkDelete = "cycle-count.counts.bulk-delete";
    public const string Cancel = "cycle-count.counts.cancel";
    public const string CountsRecord = "cycle-count.counts.record";
    public const string VariancesRead = "cycle-count.variances.read";
    public const string Reason = "Plan degisti - DEV-2026-181";

    private readonly HttpClient _http;

    public CycleCountScenario(CycleCountApiFactory factory, bool lotControlled = false, string companyCode = "GMG")
    {
        Factory = factory;
        Inventory = new InventoryScenario(factory, SkuLevel.Lsku, lotControlled);
        factory.TrackCountTenant(Inventory.Tenant);
        factory.LegalEntities.RegisterName(LegalEntity, companyCode, "Grand Medical " + companyCode);
        _http = factory.CreateDefaultClient(new CycleCountDraftContractHandler());
        Token = TokenFor(User, Read, Create, Update, BulkDelete, Cancel, CountsRecord);
    }

    public CycleCountApiFactory Factory { get; }

    public InventoryScenario Inventory { get; }

    public Guid Tenant => Inventory.Tenant;

    public Guid LegalEntity => Inventory.LegalEntity;

    public Guid User => Inventory.User;

    public Guid Warehouse => Inventory.WarehouseNode.Id;

    public Guid Storage => Inventory.StorageNode.Id;

    public Guid BinA => Guid.Parse(Inventory.LocationA);

    public Guid BinB => Guid.Parse(Inventory.LocationB);

    public string Token { get; }

    public string TokenFor(Guid user, params string[] permissions) => TestTokens.Mint(Tenant, LegalEntity, user, permissions);

    public static string NewKey() => "cnt-" + Guid.NewGuid().ToString("N");

    public static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A second SKU of this company (own item), known to the PRODUCT-MASTER stub.</summary>
    public (Guid Item, Guid Sku) AddSku(bool lotControlled = false)
    {
        var item = Guid.NewGuid();
        var sku = Guid.NewGuid();
        Factory.Products.AddItem(item, lotControlled);
        Factory.Products.AddSku(sku, SkuLevel.Lsku, item);
        Factory.StorageConditions[sku] = [];
        return (item, sku);
    }

    /// <summary>A new node of this company (default: a bin under the storage location).</summary>
    public Guid Node(LocationType type = LocationType.Bin, Location? parent = null, bool? stockable = null)
        => Guid.Parse(Inventory.AddLocation(type, parent ?? Inventory.StorageNode, stockable));

    // ── stock (REAL ledger API) ─────────────────────────────────────────────────────────────────────────

    public async Task<JsonElement> ReceiveAsync(decimal quantity, Guid bin, (Guid Item, Guid Sku)? sku = null, string? status = null, string? lot = null)
    {
        var body = Inventory.Receipt(Q(quantity), toStatus: status, location: bin.ToString("D"));
        if (sku is { } s)
        {
            body["itemId"] = s.Item.ToString("D");
            body["skuId"] = s.Sku.ToString("D");
        }

        if (lot is not null) body["lotId"] = lot;
        return await Inventory.PostExpectingAsync(body, 201);
    }

    public async Task<JsonElement> TransferAsync(decimal quantity, Guid from, Guid to)
    {
        var body = Inventory.Movement("LOCATION_TRANSFER", Q(quantity));
        body["fromWarehouseId"] = Inventory.Warehouse;
        body["fromLocationId"] = from.ToString("D");
        body["toWarehouseId"] = Inventory.Warehouse;
        body["toLocationId"] = to.ToString("D");
        return await Inventory.PostExpectingAsync(body, 201);
    }

    public async Task<JsonElement> PostAsync(string movementType, decimal quantity, Guid bin, bool inbound)
    {
        var body = Inventory.Movement(movementType, Q(quantity));
        body[inbound ? "toWarehouseId" : "fromWarehouseId"] = Inventory.Warehouse;
        body[inbound ? "toLocationId" : "fromLocationId"] = bin.ToString("D");
        return await Inventory.PostExpectingAsync(body, 201);
    }

    public static string Q(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    // ── count API ───────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Form(Guid[]? scope = null, string countType = "Cycle", bool blind = false, Guid[]? skus = null, string? plannedDate = null,
        Guid? warehouse = null, Guid? assignedTo = null, string? note = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["warehouseId"] = (warehouse ?? Warehouse).ToString("D"),
            ["countType"] = countType,
            ["blind"] = blind,
            ["scopeLocationIds"] = (scope ?? (countType == "Full" ? [] : [BinA])).Select(x => x.ToString("D")).ToArray(),
            ["scopeSkuIds"] = (skus ?? []).Select(x => x.ToString("D")).ToArray(),
            ["plannedDate"] = plannedDate ?? Today
        };
        if (assignedTo is { } a) body["assignedTo"] = a.ToString("D");
        if (note is not null) body["note"] = note;
        return body;
    }

    public Task<HttpResponseMessage> CreateAsync(Dictionary<string, object?>? form = null, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(form ?? Form()) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, "/api/warehouse/cycle-counts", body, (string)body["idempotencyKey"]!, null, token);
    }

    public async Task<JsonElement> CreatedAsync(Dictionary<string, object?>? form = null) => await Expect(CreateAsync(form), 201);

    public Task<HttpResponseMessage> UpdateAsync(string countId, Dictionary<string, object?> form, string ifMatch, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(form) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/update", body, (string)body["idempotencyKey"]!, ifMatch, token);
    }

    public Task<HttpResponseMessage> ReleaseAsync(string countId, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/release", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k, ifMatch, token);
    }

    public async Task<JsonElement> ReleasedAsync(JsonElement count) => await Expect(ReleaseAsync(Id(count), ETag(count)), 200);

    public Task<HttpResponseMessage> CancelAsync(string countId, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k };
        if (reason is not null) body["reason"] = reason;
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/cancel", body, k, ifMatch, token);
    }

    public Task<HttpResponseMessage> BulkDeleteAsync(IEnumerable<(string CountId, int Version)> items, string? reason = Reason, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = k,
            ["items"] = items.Select(i => new Dictionary<string, object?> { ["countId"] = i.CountId, ["version"] = i.Version }).ToArray()
        };
        if (reason is not null) body["reason"] = reason;
        return Send(HttpMethod.Post, "/api/warehouse/cycle-counts/bulk-delete", body, k, null, token);
    }

    // ── DL-2: entry, found stock, submit ────────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> RecordAsync(string countId, string lineId, object? countedQuantity, string ifMatch, string? key = null, string? token = null,
        string? scanRef = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k, ["countedQuantity"] = countedQuantity };
        if (scanRef is not null) body["scanRef"] = scanRef;
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/lines/{lineId}/count", body, k, ifMatch, token);
    }

    /// <summary>Counts <paramref name="line"/> (as listed) with <paramref name="quantity"/>.</summary>
    public async Task<JsonElement> CountedAsync(string countId, JsonElement line, decimal quantity)
        => await Expect(RecordAsync(countId, LineId(line), Q(quantity), ETag(line)), 200);

    public Dictionary<string, object?> FoundLine(Guid bin, (Guid Item, Guid Sku)? sku = null, decimal quantity = 1m, string? lot = null, string skuLevel = "Lsku")
    {
        var s = sku ?? (Inventory.Item, Inventory.Sku);
        var body = new Dictionary<string, object?>
        {
            ["locationId"] = bin.ToString("D"),
            ["item"] = new Dictionary<string, object?> { ["itemId"] = s.Item.ToString("D"), ["skuId"] = s.Sku.ToString("D"), ["skuLevel"] = skuLevel },
            ["countedQuantity"] = Q(quantity)
        };
        if (lot is not null) body["lotId"] = lot;
        return body;
    }

    public Task<HttpResponseMessage> AddLineAsync(string countId, Dictionary<string, object?> line, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(line) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/lines", body, (string)body["idempotencyKey"]!, null, token);
    }

    public Task<HttpResponseMessage> SubmitAsync(string countId, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{countId}/submit", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k, ifMatch, token);
    }

    /// <summary>The count as it is now (its ETag moves with every entry).</summary>
    public async Task<JsonElement> CurrentAsync(string countId) => await Expect(GetAsync($"/api/warehouse/cycle-counts/{countId}"), 200);

    public async Task<JsonElement> SubmittedAsync(string countId) => await Expect(SubmitAsync(countId, ETag(await CurrentAsync(countId))), 200);

    /// <summary>
    /// A lot REGISTERED in MOD-0174 for this tenant (staged straight into the lot store — the production read under test is
    /// <c>ILotRepository.FindAsync</c>); returns its canonical id.
    /// </summary>
    public string RegisterLot(Guid item, Guid sku, SkuLevel level = SkuLevel.Lsku)
    {
        var id = Guid.NewGuid();
        Factory.Database.GetCollection<Lot>(TraceCollections.Lots).InsertOne(new Lot
        {
            Id = id, TenantId = Tenant, LegalEntityId = LegalEntity, CreatedAt = DateTimeOffset.UtcNow, Version = 1, ItemId = item, SkuId = sku,
            SkuLevel = level, LotNumber = "CNT-" + id.ToString("N")[..10], IdentityKey = "cnt-test-" + id.ToString("N"), Status = LotStatus.Released
        });
        return id.ToString("D");
    }

    public static string LineId(JsonElement line) => line.GetProperty("lineId").GetString()!;

    /// <summary>The line of <paramref name="bin"/> (and SKU, when given) in a line list.</summary>
    public static JsonElement LineOf(IEnumerable<JsonElement> lines, Guid bin, Guid? sku = null)
        => lines.Single(l => l.GetProperty("locationId").GetString() == bin.ToString("D")
                             && (sku is null || l.GetProperty("item").GetProperty("skuId").GetString() == sku.Value.ToString("D")));

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null) => Send(HttpMethod.Get, pathAndQuery, null, null, null, token);

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? key, string? ifMatch, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        var effective = token ?? Token;
        if (effective.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        return _http.SendAsync(request);
    }

    public static async Task<JsonElement> Expect(Task<HttpResponseMessage> call, int status)
    {
        using var response = await call;
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == (int)response.StatusCode, $"expected {status}, got {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<string> ErrorCode(Task<HttpResponseMessage> call, int status)
        => (await Expect(call, status)).GetProperty("error").GetProperty("code").GetString()!;

    public static string Id(JsonElement count) => count.GetProperty("countId").GetString()!;

    public static string ETag(JsonElement count) => $"W/\"{count.GetProperty("version").GetInt32()}\"";

    public async Task<List<JsonElement>> LinesAsync(string countId, string? token = null)
        => (await Expect(GetAsync($"/api/warehouse/cycle-counts/{countId}/lines?pageSize=200", token), 200)).GetProperty("items").EnumerateArray().ToList();

    public async Task<List<JsonElement>> EventListAsync(string countId, string? token = null)
        => (await Expect(GetAsync($"/api/warehouse/cycle-counts/{countId}/events?pageSize=200", token), 200)).GetProperty("items").EnumerateArray().ToList();

    public List<BsonDocument> StoredLines(string countId)
        => Stored(CycleCountCollections.Lines).Where(l => l["CountId"].AsString == countId).ToList();

    /// <summary>The ledger movements MOD-0181 wrote for this company (DL-2: always none).</summary>
    public long CountMovements()
        => Factory.Collection("inventory_transactions").CountDocuments(new BsonDocument { ["TenantId"] = Tenant.ToString("D"), ["SourceModule"] = "MOD-0181" });

    // ── stored rows ─────────────────────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Stored(string collection) => Inventory.Persisted(collection);

    public BsonDocument StoredCount(string countId)
        => Factory.Collection(CycleCountCollections.Counts).Find(new BsonDocument("_id", countId)).Single();

    public List<BsonDocument> Events(string countId)
        => Stored(CycleCountCollections.Events).Where(e => e["CountId"].AsString == countId).OrderBy(e => e["Seq"].AsInt32).ToList();

    public List<BsonDocument> Claims(string countId) => Stored(CycleCountCollections.BinClaims).Where(c => c["CountId"].AsString == countId).ToList();

    public List<BsonDocument> CentralRows(string requestType)
        => Factory.Collection("central_audit_outbox").Find(new BsonDocument { ["TenantId"] = Tenant.ToString("D"), ["RequestType"] = requestType }).ToList();

    /// <summary>The stored ledger movement of a posting answer (its id is the transaction id).</summary>
    public static Guid MovementId(JsonElement posted) => Guid.Parse(posted.GetProperty("transactionId").GetString()!);

    public async Task<decimal> OnHandAsync(Guid bin) => await Inventory.OnHandAsync(bin.ToString("D"));
}
