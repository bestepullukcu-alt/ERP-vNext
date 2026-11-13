using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Persistence.WavePlanning;
using Diten.InventoryService.Tests.TestInfrastructure;
using Diten.InventoryService.Tests.Warehouse;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// The REAL host for MOD-0180 (Program.cs, real middleware, real MediatR pipeline with the central-audit behaviour, real Mongo repositories on
/// the suite's own replica set) on top of <see cref="WarehouseApiFactory"/> (its fake AUTH user directory). It opens ONLY seams around production
/// code and changes no decision: MOD-0180's own clock, a failing record counter (AC-10), a barrier / failing timeline insert around the wave
/// transaction (AC-2, AC-7, AC-11) and a failing warehouse-data read (503). The <c>wms_wave_*</c> records of its tenants are removed on dispose.
/// </summary>
public sealed class WaveApiFactory : WarehouseApiFactory
{
    private readonly ConcurrentBag<Guid> _waveTenants = [];

    /// <summary>MOD-0180's own clock (only this one moves).</summary>
    public MovableClock WaveClock { get; } = new();

    /// <summary>tenants whose record counter fails (503 path, AC-10).</summary>
    public ConcurrentDictionary<Guid, bool> CounterDown { get; } = new();

    /// <summary>tenant → runs before each wave transaction of that tenant (a barrier for the concurrency cases).</summary>
    public ConcurrentDictionary<Guid, Func<Task>> BeforeWaveTransaction { get; } = new();

    /// <summary>tenants whose next timeline insert throws inside the transaction (AC-2 "same transaction").</summary>
    public ConcurrentDictionary<Guid, bool> FailNextEventInsert { get; } = new();

    /// <summary>tenants whose MOD-0178 / LOCATION reads fail (503 <c>WAVE_INVENTORY_UNAVAILABLE</c>).</summary>
    public ConcurrentDictionary<Guid, bool> InventoryDown { get; } = new();

    public void TrackWaveTenant(Guid tenant)
    {
        _waveTenants.Add(tenant);
        TrackWarehouseTenant(tenant);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new WaveTime(WaveClock));
            services.AddScoped<WarehouseRecordCodeAllocator>();
            services.AddScoped<IWarehouseRecordCodeAllocator>(sp => new SeamedWaveAllocator(sp.GetRequiredService<WarehouseRecordCodeAllocator>(),
                sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<WaveRepository>();
            services.AddScoped<IWaveRepository>(sp => new SeamedWaveRepository(sp.GetRequiredService<WaveRepository>(), sp.GetRequiredService<ITenantContext>(), this));
            services.AddScoped<WaveInventoryReader>();
            services.AddScoped<IWaveInventoryReader>(sp => new SeamedWaveInventoryReader(sp.GetRequiredService<WaveInventoryReader>(),
                sp.GetRequiredService<ITenantContext>(), this));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_waveTenants.IsEmpty)
        {
            var tenants = new BsonArray(_waveTenants.Select(t => t.ToString()));
            foreach (var name in WaveCollections.All)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}

public sealed class SeamedWaveAllocator(WarehouseRecordCodeAllocator inner, ITenantContext tenant, WaveApiFactory factory) : IWarehouseRecordCodeAllocator
{
    public Task<string> NextAsync(string companyCodePrefix, string typePrefix, CancellationToken cancellationToken = default)
        => factory.CounterDown.ContainsKey(tenant.TenantId)
            ? throw new WarehouseRecordCodeUnavailableException("counter down (test seam)")
            : inner.NextAsync(companyCodePrefix, typePrefix, cancellationToken);
}

/// <summary>The production reader; only an outage can be staged.</summary>
public sealed class SeamedWaveInventoryReader(WaveInventoryReader inner, ITenantContext tenant, WaveApiFactory factory) : IWaveInventoryReader
{
    private void Guard()
    {
        if (factory.InventoryDown.ContainsKey(tenant.TenantId)) throw new WaveInventoryUnavailableException("warehouse data down (test seam)");
    }

    public Task<WaveWarehouse?> WarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        Guard();
        return inner.WarehouseAsync(warehouseId, cancellationToken);
    }

    public Task<IReadOnlyList<WaveOutbound>> OutboundsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
    {
        Guard();
        return inner.OutboundsAsync(outboundIds, cancellationToken);
    }

    public Task<WaveCandidatePage> CandidatesAsync(WaveCandidateQuery query, CancellationToken cancellationToken = default)
    {
        Guard();
        return inner.CandidatesAsync(query, cancellationToken);
    }

    public Task<long> DeferredCountAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        Guard();
        return inner.DeferredCountAsync(warehouseId, cancellationToken);
    }

    public Task<IReadOnlyList<WaveBin>> BinsAsync(IReadOnlyCollection<Guid> binIds, CancellationToken cancellationToken = default)
    {
        Guard();
        return inner.BinsAsync(binIds, cancellationToken);
    }
}

/// <summary>The production repository; only a barrier before the transaction and a failing timeline insert inside it can be staged.</summary>
public sealed class SeamedWaveRepository(WaveRepository inner, ITenantContext tenant, WaveApiFactory factory) : IWaveRepository
{
    public Task<Wave?> FindAsync(Guid waveId, CancellationToken cancellationToken = default) => inner.FindAsync(waveId, cancellationToken);

    public Task<Wave?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<WavePage> ListAsync(WaveQuery query, CancellationToken cancellationToken = default) => inner.ListAsync(query, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, WaveTotals>> TotalsAsync(IReadOnlyCollection<Guid> waveIds, CancellationToken cancellationToken = default)
        => inner.TotalsAsync(waveIds, cancellationToken);

    public Task<WaveOrderPage> OrdersAsync(Guid waveId, IReadOnlyCollection<WaveOrderState> states, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.OrdersAsync(waveId, states, page, pageSize, cancellationToken);

    public Task<IReadOnlyList<WaveOrder>> ActiveOrdersAsync(Guid waveId, CancellationToken cancellationToken = default) => inner.ActiveOrdersAsync(waveId, cancellationToken);

    public Task<IReadOnlyList<WaveOrder>> MembershipsAsync(Guid waveId, IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => inner.MembershipsAsync(waveId, outboundIds, cancellationToken);

    public Task<IReadOnlyList<WaveOrder>> ActiveMembershipsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => inner.ActiveMembershipsAsync(outboundIds, cancellationToken);

    public Task<IReadOnlyList<Guid>> ActiveOutboundIdsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => inner.ActiveOutboundIdsAsync(warehouseId, cancellationToken);

    public Task<long> ReleasingOrderCountAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => inner.ReleasingOrderCountAsync(warehouseId, cancellationToken);

    public Task<WaveEventPage> EventsAsync(Guid streamId, WaveEventStream stream, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.EventsAsync(streamId, stream, page, pageSize, cancellationToken);

    public Task<WaveEvent?> FindKeyEventAsync(Guid streamId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindKeyEventAsync(streamId, actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<WaveWarehouseSettings?> FindSettingsAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => inner.FindSettingsAsync(warehouseId, cancellationToken);

    public async Task<WaveWriteOutcome> InTransactionAsync(Func<IWaveWriteSession, CancellationToken, Task<WaveWriteOutcome>> work,
        CancellationToken cancellationToken = default)
    {
        if (factory.BeforeWaveTransaction.TryGetValue(tenant.TenantId, out var before)) await before();
        var fail = factory.FailNextEventInsert.TryRemove(tenant.TenantId, out _);
        return await inner.InTransactionAsync((session, ct) => work(fail ? new FailingEventsSession(session) : session, ct), cancellationToken);
    }

    private sealed class FailingEventsSession(IWaveWriteSession inner) : IWaveWriteSession
    {
        public Task InsertWaveAsync(Wave wave, CancellationToken cancellationToken = default) => inner.InsertWaveAsync(wave, cancellationToken);

        public Task<bool> ReplaceWaveAsync(Wave wave, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.ReplaceWaveAsync(wave, expectedVersion, cancellationToken);

        public Task InsertOrdersAsync(IReadOnlyList<WaveOrder> orders, CancellationToken cancellationToken = default) => inner.InsertOrdersAsync(orders, cancellationToken);

        public Task<long> RemovePlannedOrdersAsync(Guid waveId, IReadOnlyCollection<Guid>? outboundIds, Guid closedBy, DateTimeOffset at, string? reason,
            CancellationToken cancellationToken = default)
            => inner.RemovePlannedOrdersAsync(waveId, outboundIds, closedBy, at, reason, cancellationToken);

        public Task InsertEventsAsync(IReadOnlyList<WaveEvent> events, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("timeline insert failed (test seam)");

        public Task InsertSettingsAsync(WaveWarehouseSettings settings, CancellationToken cancellationToken = default)
            => inner.InsertSettingsAsync(settings, cancellationToken);

        public Task<bool> UpdateSettingsAsync(WaveWarehouseSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.UpdateSettingsAsync(settings, expectedVersion, cancellationToken);

        public Task PassGateAsync(Guid warehouseId, CancellationToken cancellationToken = default) => inner.PassGateAsync(warehouseId, cancellationToken);
    }
}

/// <summary>
/// One isolated wave world: a company with code <c>GMG</c> in the MDM stub, the inventory scenario's warehouse (wave managed,
/// <c>Europe/Istanbul</c>, set through the REAL setting endpoint unless asked otherwise) and MOD-0178 outbound documents seeded straight into
/// <c>wms_outbound_shipments</c> — in DL-1 MOD-0178 cannot yet produce a deferred outbound (pack AC-15 header; X-1 is DL-2), so the fixture
/// writes the document MOD-0178 will write. Every <c>/api/warehouse/waves*</c> answer is checked against the WMS-WAVE-BUNDLE draft
/// (<see cref="WaveDraftContractHandler"/>).
/// </summary>
public sealed class WaveScenario
{
    public const string Read = "warehouse.waves.read";
    public const string Create = "warehouse.waves.create";
    public const string Update = "warehouse.waves.update";
    public const string Configure = "warehouse.waves.settings.configure";
    public const string Cancel = "warehouse.waves.cancel";
    public const string Reason = "Plan degisti - DEV-2026-180";
    public const string Istanbul = "Europe/Istanbul";

    public static readonly string[] AllKeys = [Read, Create, Update, Configure, Cancel];

    private readonly HttpClient _http;
    private int _outboundSeq;

    public WaveScenario(WaveApiFactory factory, string companyCode = "GMG")
    {
        Factory = factory;
        Inventory = new InventoryScenario(factory, Diten.InventoryService.Domain.Enums.SkuLevel.Lsku, false);
        factory.TrackWaveTenant(Inventory.Tenant);
        factory.LegalEntities.RegisterName(LegalEntity, companyCode, "Grand Medical " + companyCode);
        _http = factory.CreateDefaultClient(new WaveDraftContractHandler());
        Token = TokenFor(User, AllKeys);
    }

    /// <summary>A scenario whose warehouse is already wave managed in <paramref name="timeZoneId"/>.</summary>
    public static async Task<WaveScenario> ManagedAsync(WaveApiFactory factory, string timeZoneId = Istanbul, string companyCode = "GMG")
    {
        var s = new WaveScenario(factory, companyCode);
        await s.ConfiguredAsync(true, timeZoneId);
        return s;
    }

    public WaveApiFactory Factory { get; }

    public InventoryScenario Inventory { get; }

    public Guid Tenant => Inventory.Tenant;

    public Guid LegalEntity => Inventory.LegalEntity;

    public Guid User => Inventory.User;

    public Guid Warehouse => Inventory.WarehouseNode.Id;

    public Guid Warehouse2 => Inventory.Warehouse2Node.Id;

    public string Token { get; }

    public string TokenFor(Guid user, params string[] permissions) => TestTokens.Mint(Tenant, LegalEntity, user, permissions);

    public static string NewKey() => "wav-" + Guid.NewGuid().ToString("N");

    /// <summary>A local cutoff comfortably in the future in any zone: the day after tomorrow, 14:30.</summary>
    public string FutureCutoff => Day(2) + "T14:30";

    /// <summary>The calendar day <paramref name="daysAhead"/> days after MOD-0180's own clock (the clock a test may have moved).</summary>
    public string Day(int daysAhead) => Factory.WaveClock.GetUtcNow().AddDays(daysAhead).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ── MOD-0178 outbound documents (seeded; DL-1 fixture) ─────────────────────────────────────────────────

    /// <summary>
    /// A MOD-0178 outbound document of this company. Default: <c>Allocated</c> with deferred pick issuance — the eligible order of pack §8.2
    /// rule 2 — with <paramref name="lines"/> lines, one allocation each, <paramref name="binsPerLine"/> planned bins per allocation.
    /// </summary>
    public Guid SeedOutbound(string status = "Allocated", string? pickIssuance = "Deferred", int lines = 1, int binsPerLine = 1, Guid? warehouse = null,
        string shipToName = "Acme Eczane", string? city = "Ankara", string? postalCode = "06100", string country = "TR", DateTime? createdAt = null,
        Guid? bin = null, Guid? tenant = null, Guid? legalEntity = null, string? orderRef = null)
    {
        var id = Guid.NewGuid();
        var seq = Interlocked.Increment(ref _outboundSeq);
        var location = bin ?? Guid.Parse(Inventory.LocationA);
        var document = new BsonDocument
        {
            ["_id"] = id.ToString("D"),
            ["TenantId"] = (tenant ?? Tenant).ToString("D"),
            ["LegalEntityId"] = (legalEntity ?? LegalEntity).ToString("D"),
            ["CreatedAt"] = new BsonDateTime(createdAt ?? DateTime.UtcNow.AddMinutes(-60 + seq)),
            ["Version"] = 1,
            ["IsDeleted"] = false,
            ["OutboundNumber"] = $"GMG-OUT-{seq:D6}-{id.ToString("N")[..6]}",
            ["CompanyCodePrefix"] = "GMG",
            ["WarehouseId"] = (warehouse ?? Warehouse).ToString("D"),
            ["ShipTo"] = new BsonDocument { ["Name"] = shipToName, ["Country"] = country, ["City"] = city is null ? BsonNull.Value : city, ["PostalCode"] = postalCode is null ? BsonNull.Value : postalCode, ["Line1"] = "Gizli Sokak 1" },
            ["Status"] = status,
            ["Lines"] = new BsonArray(Enumerable.Range(1, lines).Select(l => new BsonDocument
            {
                ["LineSeq"] = l,
                ["Allocations"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["AllocSeq"] = l,
                        ["PlannedBins"] = new BsonArray(Enumerable.Range(0, binsPerLine).Select(_ => new BsonDocument { ["LocationId"] = location.ToString("D"), ["Quantity"] = new BsonDecimal128(1m) }))
                    }
                }
            }))
        };
        if (orderRef is not null) document["OrderRef"] = orderRef;
        if (pickIssuance is not null) document["PickIssuance"] = new BsonDocument { ["State"] = pickIssuance };
        Factory.Collection(WarehouseCollections.OutboundShipments).InsertOne(document);
        return id;
    }

    public BsonDocument StoredOutbound(Guid id) => Factory.Collection(WarehouseCollections.OutboundShipments).Find(new BsonDocument("_id", id.ToString("D"))).Single();

    // ── setting API ─────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> SettingsBody(bool managed = true, string? timeZoneId = Istanbul, int riskMinutes = 90, int riskPercent = 70,
        int pendingMinutes = 30, string? reason = Reason)
    {
        var body = new Dictionary<string, object?>
        {
            ["waveManaged"] = managed, ["riskMinutesBeforeCutoff"] = riskMinutes, ["riskProgressPercent"] = riskPercent, ["pendingTaskMinutes"] = pendingMinutes
        };
        if (timeZoneId is not null) body["timeZoneId"] = timeZoneId;
        if (reason is not null) body["reason"] = reason;
        return body;
    }

    public Task<HttpResponseMessage> PutSettingsAsync(Dictionary<string, object?> settings, string ifMatch, Guid? warehouse = null, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(settings) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, $"/api/warehouse/waves/settings/{(warehouse ?? Warehouse):D}/update", body, (string)body["idempotencyKey"]!, ifMatch, token);
    }

    public Task<HttpResponseMessage> GetSettingsAsync(Guid? warehouse = null, string? token = null)
        => GetAsync($"/api/warehouse/waves/settings/{(warehouse ?? Warehouse):D}", token);

    /// <summary>Writes the warehouse setting through the REAL endpoint (reads the current version first).</summary>
    public async Task<JsonElement> ConfiguredAsync(bool managed, string? timeZoneId = Istanbul, Guid? warehouse = null)
    {
        var current = await Expect(GetSettingsAsync(warehouse), 200);
        return await Expect(PutSettingsAsync(SettingsBody(managed, timeZoneId), ETag(current), warehouse), 200);
    }

    // ── wave API ────────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Form(string waveType = "Cargo", string? cutoffLocal = null, Guid? warehouse = null, string? shipToFilter = null,
        int? maxOrders = null, int? maxLines = null, Guid? responsible = null, string? note = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["warehouseId"] = (warehouse ?? Warehouse).ToString("D"), ["waveType"] = waveType, ["cutoffLocal"] = cutoffLocal ?? FutureCutoff
        };
        if (shipToFilter is not null) body["shipToFilter"] = shipToFilter;
        if (maxOrders is { } o) body["maxOrders"] = o;
        if (maxLines is { } l) body["maxLines"] = l;
        if (responsible is { } r) body["responsibleUserId"] = r.ToString("D");
        if (note is not null) body["note"] = note;
        return body;
    }

    public Task<HttpResponseMessage> CreateAsync(Dictionary<string, object?>? form = null, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(form ?? Form()) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, "/api/warehouse/waves", body, (string)body["idempotencyKey"]!, null, token);
    }

    public async Task<JsonElement> CreatedAsync(Dictionary<string, object?>? form = null) => await Expect(CreateAsync(form), 201);

    public Task<HttpResponseMessage> UpdateAsync(string waveId, Dictionary<string, object?> form, string ifMatch, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(form) { ["idempotencyKey"] = key ?? NewKey() };
        return Send(HttpMethod.Post, $"/api/warehouse/waves/{waveId}/update", body, (string)body["idempotencyKey"]!, ifMatch, token);
    }

    public Task<HttpResponseMessage> AddAsync(string waveId, IEnumerable<Guid> outbounds, string ifMatch, string? via = null, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k, ["outboundIds"] = outbounds.Select(x => x.ToString("D")).ToArray() };
        if (via is not null) body["via"] = via;
        return Send(HttpMethod.Post, $"/api/warehouse/waves/{waveId}/orders", body, k, ifMatch, token);
    }

    /// <summary>Adds the orders and answers the result (its <c>wave</c> carries the new version).</summary>
    public async Task<JsonElement> AddedAsync(JsonElement wave, params Guid[] outbounds)
        => (await Expect(AddAsync(Id(wave), outbounds, ETag(wave)), 200)).GetProperty("wave");

    public Task<HttpResponseMessage> RemoveAsync(string waveId, IEnumerable<Guid> outbounds, string ifMatch, string? reason = null, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k, ["outboundIds"] = outbounds.Select(x => x.ToString("D")).ToArray() };
        if (reason is not null) body["reason"] = reason;
        return Send(HttpMethod.Post, $"/api/warehouse/waves/{waveId}/orders/remove", body, k, ifMatch, token);
    }

    public Task<HttpResponseMessage> CancelAsync(string waveId, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k };
        if (reason is not null) body["reason"] = reason;
        return Send(HttpMethod.Post, $"/api/warehouse/waves/{waveId}/cancel", body, k, ifMatch, token);
    }

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null) => Send(HttpMethod.Get, pathAndQuery, null, null, null, token);

    public async Task<JsonElement> WaveAsync(string waveId) => await Expect(GetAsync($"/api/warehouse/waves/{waveId}"), 200);

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

    public static string Id(JsonElement wave) => wave.GetProperty("waveId").GetString()!;

    public static string ETag(JsonElement record) => $"W/\"{record.GetProperty("version").GetInt32()}\"";

    // ── stored rows ─────────────────────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Stored(string collection) => Inventory.Persisted(collection);

    public BsonDocument StoredWave(string waveId) => Factory.Collection(WaveCollections.Waves).Find(new BsonDocument("_id", waveId)).Single();

    public List<BsonDocument> Events(string streamId)
        => Stored(WaveCollections.Events).Where(e => e["WaveId"].AsString == streamId).OrderBy(e => e["Seq"].AsInt32).ToList();

    public List<BsonDocument> Orders(string waveId) => Stored(WaveCollections.Orders).Where(o => o["WaveId"].AsString == waveId).ToList();

    public List<BsonDocument> CentralRows(string requestType)
        => Factory.Collection("central_audit_outbox").Find(new BsonDocument { ["TenantId"] = Tenant.ToString("D"), ["RequestType"] = requestType }).ToList();

    /// <summary>Puts a stored wave into a state DL-1 cannot reach through the API (Released comes with DL-2) — fixture only.</summary>
    public void ForceStatus(string waveId, string status)
        => Factory.Collection(WaveCollections.Waves).UpdateOne(new BsonDocument("_id", waveId), new BsonDocument("$set", new BsonDocument("Status", status)));

    /// <summary>Runs <paramref name="work"/> on the PRODUCTION repository in a scope bound to this scenario's tenant, company and user.</summary>
    public async Task<T> WithRepositoryAsync<T>(Func<WaveRepository, Task<T>> work, Guid? tenant = null, Guid? legalEntity = null)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(tenant ?? Tenant, legalEntity ?? LegalEntity, User);
        return await work(scope.ServiceProvider.GetRequiredService<WaveRepository>());
    }
}
