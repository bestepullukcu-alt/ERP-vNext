using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using YamlDotNet.RepresentationModel;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

// ── the fake MVP-6 SHIPMENT-BUNDLE (behind the REAL MOD-0177 ShipmentBundleHttpReader) ─────────────────────────────────────────

/// <summary>One fake MVP-6 shipment (SHIPMENT-BUNDLE 3.4.0 <c>ShipmentSummary</c> / <c>ShipmentDetail</c>).</summary>
public sealed class FakeShipment
{
    public string ShipmentId { get; init; } = Guid.NewGuid().ToString("D");

    public string ShipmentNumber { get; init; } = "SHP-2026-000001";

    public string Status { get; set; } = "Dispatched";

    public string SourceModule { get; init; } = "MOD-0178";

    public string SourceType { get; init; } = "WAREHOUSE_OUTBOUND";

    public bool? StockIssued { get; init; } = true;

    /// <summary>Detail lines: (skuId, lotId?, fromLocationId?, quantity).</summary>
    public List<(Guid Sku, string? Lot, Guid? From, decimal Quantity)> Lines { get; init; } = [];

    /// <summary>A forced detail answer (403 / 404 / 429 …) instead of the body.</summary>
    public HttpStatusCode? DetailStatus { get; init; }
}

/// <summary>The fake SHIPMENT-BUNDLE of one company: the query answer, its candidates, failure modes and the calls it saw (path + forwarded headers).</summary>
public sealed class FakeShipments
{
    public List<FakeShipment> Items { get; } = [];

    /// <summary>A forced query answer (401 / 403 / 500 / 400 …) instead of the page.</summary>
    public HttpStatusCode? QueryStatus { get; set; }

    /// <summary>Every read answers after this delay (the reader's own 2 s bound).</summary>
    public TimeSpan? Delay { get; set; }

    public bool ConnectionFails { get; set; }

    public bool UnreadableBody { get; set; }

    public ConcurrentQueue<(string Path, string? Authorization, string? LegalEntity)> Calls { get; } = new();

    public int DetailCalls => Calls.Count(c => Regex.IsMatch(c.Path, "/shipments/[0-9a-f-]{36}$"));

    public int QueryCalls => Calls.Count(c => c.Path.EndsWith("/shipments", StringComparison.Ordinal));
}

public sealed class FakeShipmentBundleHandler(WarehouseApiFactory factory) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var le = request.Headers.TryGetValues("X-Legal-Entity-Id", out var values) ? values.Single() : null;
        var auth = request.Headers.Authorization?.ToString();
        var path = request.RequestUri!.AbsolutePath;
        if (le is null || !Guid.TryParse(le, out var company) || !factory.Shipments.TryGetValue(company, out var fake))
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        fake.Calls.Enqueue((path, auth, le));
        if (fake.Delay is { } delay) await Task.Delay(delay, cancellationToken);
        if (fake.ConnectionFails) throw new HttpRequestException("test: connection refused");
        if (path.EndsWith("/api/shipment-bundle/shipments", StringComparison.Ordinal))
        {
            if (fake.QueryStatus is { } forced) return new HttpResponseMessage(forced);
            if (fake.UnreadableBody) return Text("{ not json");
            return Json(new
            {
                items = fake.Items.Select(s => new { shipmentId = s.ShipmentId, shipmentNumber = s.ShipmentNumber, sourceDocumentId = "x", status = s.Status }).ToList(),
                page = 1, pageSize = 200, total = fake.Items.Count, contractVersion = "v1"
            });
        }

        var id = path[(path.LastIndexOf('/') + 1)..];
        var shipment = fake.Items.FirstOrDefault(s => s.ShipmentId == id);
        if (shipment is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (shipment.DetailStatus is { } detailForced) return new HttpResponseMessage(detailForced);
        var detail = new Dictionary<string, object?>
        {
            ["shipmentId"] = shipment.ShipmentId, ["shipmentNumber"] = shipment.ShipmentNumber, ["sourceDocumentId"] = "x", ["status"] = shipment.Status,
            ["sourceModule"] = shipment.SourceModule, ["sourceType"] = shipment.SourceType, ["warehouseReferenceId"] = "w", ["shipToReference"] = "s",
            ["lines"] = shipment.Lines.Select((l, i) =>
            {
                var line = new Dictionary<string, object?>
                {
                    ["lineNumber"] = (i + 1).ToString(CultureInfo.InvariantCulture), ["itemId"] = Guid.NewGuid().ToString("D"), ["skuId"] = l.Sku.ToString("D"),
                    ["quantity"] = l.Quantity.ToString("0.000", CultureInfo.InvariantCulture), ["uomId"] = "EA"
                };
                if (l.Lot is not null) line["lotId"] = l.Lot;
                if (l.From is { } from) line["fromLocationId"] = from.ToString("D");
                return line;
            }).ToList(),
            ["stockIssued"] = shipment.StockIssued, ["contractVersion"] = "v1"
        };
        return Json(detail);
    }

    private static HttpResponseMessage Json(object body) => Text(JsonSerializer.Serialize(body));

    private static HttpResponseMessage Text(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

// ── the outbound store with the outbox seam (AC-30 K2) ────────────────────────────────────────────────────────────────────────

public sealed class SeamedOutboundRepository(OutboundShipmentRepository inner, Application.Common.ITenantContext tenant, WarehouseApiFactory factory)
    : IOutboundShipmentRepository
{
    public Task<OutboundShipment?> FindAsync(Guid outboundId, CancellationToken cancellationToken = default) => inner.FindAsync(outboundId, cancellationToken);

    public Task<OutboundShipment?> FindByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<OutboundShipmentPage> ListAsync(OutboundShipmentQuery query, CancellationToken cancellationToken = default) => inner.ListAsync(query, cancellationToken);

    public Task<OutboundEventPage> EventsAsync(Guid outboundId, int page, int pageSize, CancellationToken cancellationToken = default)
        => inner.EventsAsync(outboundId, page, pageSize, cancellationToken);

    public Task<OutboundShipmentEvent?> FindKeyEventAsync(Guid outboundId, Guid actorUserId, string requestOp, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindKeyEventAsync(outboundId, actorUserId, requestOp, idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<WarehouseTask>> PickTasksAsync(Guid outboundId, CancellationToken cancellationToken = default) => inner.PickTasksAsync(outboundId, cancellationToken);

    public Task<IReadOnlyList<WarehouseTask>> PickTasksOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => inner.PickTasksOfManyAsync(outboundIds, cancellationToken);

    public Task<IReadOnlyList<HandlingUnit>> PackagesAsync(Guid outboundId, CancellationToken cancellationToken = default) => inner.PackagesAsync(outboundId, cancellationToken);

    public Task<IReadOnlyList<HandlingUnit>> PackagesOfManyAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
        => inner.PackagesOfManyAsync(outboundIds, cancellationToken);

    public Task<HandlingUnit?> FindPackageAsync(Guid packageId, CancellationToken cancellationToken = default) => inner.FindPackageAsync(packageId, cancellationToken);

    public Task<HandlingUnit?> FindPackageByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => inner.FindPackageByCreateKeyAsync(createdBy, idempotencyKey, cancellationToken);

    public Task<WarehouseWriteOutcome> InTransactionAsync(Func<IOutboundWriteSession, CancellationToken, Task<WarehouseWriteOutcome>> work, CancellationToken cancellationToken = default)
        => factory.FailOutbox.ContainsKey(tenant.TenantId)
            ? inner.InTransactionAsync((session, ct) => work(new FailingOutbox(session), ct), cancellationToken)
            : inner.InTransactionAsync(work, cancellationToken);

    private sealed class FailingOutbox(IOutboundWriteSession inner) : IOutboundWriteSession
    {
        public Task InsertOutboundAsync(OutboundShipment outbound, CancellationToken cancellationToken = default) => inner.InsertOutboundAsync(outbound, cancellationToken);

        public Task<bool> ReplaceOutboundAsync(OutboundShipment outbound, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.ReplaceOutboundAsync(outbound, expectedVersion, cancellationToken);

        public Task InsertEventsAsync(IReadOnlyList<OutboundShipmentEvent> events, CancellationToken cancellationToken = default) => inner.InsertEventsAsync(events, cancellationToken);

        public Task InsertPickTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default)
            => inner.InsertPickTasksAsync(tasks, events, cancellationToken);

        public Task<bool> ReplaceTaskAsync(WarehouseTask task, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.ReplaceTaskAsync(task, expectedVersion, cancellationToken);

        public Task InsertTasksAsync(IReadOnlyList<WarehouseTask> tasks, IReadOnlyList<WarehouseTaskEvent> events, CancellationToken cancellationToken = default)
            => inner.InsertTasksAsync(tasks, events, cancellationToken);

        public Task InsertPackageAsync(HandlingUnit package, CancellationToken cancellationToken = default) => inner.InsertPackageAsync(package, cancellationToken);

        public Task<bool> ReplacePackageAsync(HandlingUnit package, int expectedVersion, CancellationToken cancellationToken = default)
            => inner.ReplacePackageAsync(package, expectedVersion, cancellationToken);

        public Task InsertOutboxAsync(WarehouseOutboxMessage message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("test: the outbox write failed");
    }
}

// ── the frozen WAREHOUSE-OUTBOUND 1.0.0 schema, derived from the PRODUCTION contract file (Çekirdek 10) ──────────────────────────────

/// <summary>
/// AC-30 / AC-33 (v): the rules MVP-6's <c>FrozenReadSchema</c> applies, derived member by member from
/// <c>docs/analysis/contracts/warehouse-outbound.openapi.yaml</c> (the frozen 1.0.0 file — not a copy): every <c>required</c> member present, NO JSON
/// null anywhere (OAS 3.1 <c>nullable</c> does not permit null — GAP-0183-03), the declared types, every <c>format: uuid</c> a canonical "D" UUID,
/// <c>date-time</c> an RFC 3339 instant, enums (<c>SkuLevel</c> three values, <c>OutboundStatus</c> six) from the file, <c>Decimal</c> by its
/// pattern. Members the file does not name are tolerated (MVP-6 tolerates the WMS draft §2.3 additive members).
/// </summary>
public static class FrozenOutboundSchema
{
    public static readonly string FilePath = Path.Combine(RepoPaths.Root, "docs", "analysis", "contracts", "warehouse-outbound.openapi.yaml");

    /// <summary>The frozen file's SHA-256 (git blob of HEAD, LF) — AC-30 "byte-eşit (AC-18)".</summary>
    public const string Sha256 = "2389ce2064570812b8f7efda249af0c1d261f21841443bd88414eda8fb72ba6c";

    private static readonly Lazy<YamlMappingNode> Schemas = new(() =>
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(FilePath)));
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        return (YamlMappingNode)((YamlMappingNode)root["components"])["schemas"];
    });

    public static string FileSha256()
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(FilePath).Replace("\r\n", "\n")))).ToLowerInvariant();

    public static IReadOnlyList<string> EnumOf(string schema)
        => ((YamlSequenceNode)((YamlMappingNode)Schemas.Value[schema])["enum"]).Children.Select(c => ((YamlScalarNode)c).Value!).ToList();

    public static List<string> ValidateShipment(JsonElement e) => Validate(e, (YamlMappingNode)Schemas.Value["OutboundShipment"], "$");

    /// <summary>The list body: <c>items</c> of <c>OutboundShipment</c>, <c>nextCursor</c> / <c>contractVersion</c> strings — no null member.</summary>
    public static List<string> ValidatePage(JsonElement e)
    {
        var errors = new List<string>();
        if (e.ValueKind != JsonValueKind.Object) return ["$: not an object"];
        foreach (var p in e.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Null) errors.Add($"$.{p.Name}: null");
        }

        if (e.TryGetProperty("nextCursor", out var c) && c.ValueKind != JsonValueKind.String) errors.Add("$.nextCursor: not a string");
        if (!e.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return [.. errors, "$.items: missing"];
        var i = 0;
        foreach (var item in items.EnumerateArray()) errors.AddRange(Validate(item, (YamlMappingNode)Schemas.Value["OutboundShipment"], $"$.items[{i++}]"));
        return errors;
    }

    private static List<string> Validate(JsonElement e, YamlMappingNode schema, string at)
    {
        var errors = new List<string>();
        if (schema.Children.TryGetValue(new YamlScalarNode("$ref"), out var reference))
            return Validate(e, (YamlMappingNode)Schemas.Value[((YamlScalarNode)reference).Value!.Split('/')[^1]], at);
        if (e.ValueKind == JsonValueKind.Null) return [$"{at}: JSON null (GAP-0183-03)"];
        var type = schema.Children.TryGetValue(new YamlScalarNode("type"), out var t) ? ((YamlScalarNode)t).Value : null;
        switch (type)
        {
            case "object":
                if (e.ValueKind != JsonValueKind.Object) return [$"{at}: not an object"];
                if (schema.Children.TryGetValue(new YamlScalarNode("required"), out var req))
                {
                    foreach (var r in ((YamlSequenceNode)req).Children.Select(n => ((YamlScalarNode)n).Value!))
                    {
                        if (!e.TryGetProperty(r, out _)) errors.Add($"{at}.{r}: required member missing");
                    }
                }

                var props = schema.Children.TryGetValue(new YamlScalarNode("properties"), out var p) ? (YamlMappingNode)p : new YamlMappingNode();
                foreach (var member in e.EnumerateObject())
                {
                    if (member.Value.ValueKind == JsonValueKind.Null) errors.Add($"{at}.{member.Name}: JSON null (GAP-0183-03)");
                    else if (props.Children.TryGetValue(new YamlScalarNode(member.Name), out var ms)) errors.AddRange(Validate(member.Value, (YamlMappingNode)ms, $"{at}.{member.Name}"));
                }

                return errors;
            case "array":
                if (e.ValueKind != JsonValueKind.Array) return [$"{at}: not an array"];
                var itemSchema = (YamlMappingNode)schema["items"];
                var i = 0;
                foreach (var x in e.EnumerateArray()) errors.AddRange(Validate(x, itemSchema, $"{at}[{i++}]"));
                return errors;
            case "string":
                if (e.ValueKind != JsonValueKind.String) return [$"{at}: not a string"];
                var value = e.GetString()!;
                if (schema.Children.TryGetValue(new YamlScalarNode("enum"), out var en) && !((YamlSequenceNode)en).Children.Any(c => ((YamlScalarNode)c).Value == value))
                    errors.Add($"{at}: '{value}' not in the enum");
                if (schema.Children.TryGetValue(new YamlScalarNode("pattern"), out var pat) && !Regex.IsMatch(value, ((YamlScalarNode)pat).Value!))
                    errors.Add($"{at}: '{value}' does not match the pattern");
                var format = schema.Children.TryGetValue(new YamlScalarNode("format"), out var f) ? ((YamlScalarNode)f).Value : null;
                if (format == "uuid" && !Guid.TryParseExact(value, "D", out _)) errors.Add($"{at}: '{value}' is not a canonical UUID");
                if (format == "date-time" && !Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")) errors.Add($"{at}: '{value}' is not date-time");
                return errors;
            default:
                return errors;
        }
    }

    public static string Hash(string body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
}

// ── the S2 flow on top of OutboundScenario: pick, pack, release, sync, cancel through the REAL API ───────────────────────────────

public sealed class OutboundFlow
{
    public const string PickRead = "warehouse.pick-tasks.read";
    public const string PickUpdate = "warehouse.pick-tasks.update";
    public const string PickConfirm = "warehouse.pick-tasks.confirm";
    public const string PickResolve = "warehouse.pick-tasks.resolve";
    public const string PackagesRead = "warehouse.packages.read";
    public const string PackagesCreate = "warehouse.packages.create";
    public const string PackagesUpdate = "warehouse.packages.update";
    public const string OutboundCancel = "warehouse.outbound-shipments.cancel";
    public const string OutboundRelease = "warehouse.outbound-shipments.release";
    public const string Reason = "Musteri iptal etti - 2026";

    public OutboundFlow(OutboundScenario s)
    {
        S = s;
        // the supervisor: every warehouse key (pick update = supervisor half of the assignee rule)
        Supervisor = s.TokenFor(OutboundScenario.OutboundRead, OutboundScenario.OutboundCreate, OutboundScenario.OutboundUpdate, OutboundCancel, OutboundRelease, PickRead, PickUpdate,
            PickConfirm, PickResolve, PackagesRead, PackagesCreate, PackagesUpdate, PutawayScenario.TaskRead, PutawayScenario.TaskCreate, PutawayScenario.TaskUpdate,
            PutawayScenario.TaskConfirm, PutawayScenario.TaskCancel, "warehouse.location-roles.read", "warehouse.location-roles.manage");
        // the operator: pick + packages only, NO outbound read (Q-10a)
        Operator = s.TokenFor(PickRead, PickConfirm, PackagesRead, PackagesCreate, PackagesUpdate);
        s.Factory.Shipments[s.LegalEntity] = Mvp6;
    }

    public OutboundScenario S { get; }

    public string Supervisor { get; }

    public string Operator { get; }

    public FakeShipments Mvp6 { get; } = new();

    private WarehouseScenario W => S.P.W;

    // ── tasks ──

    public async Task<List<JsonElement>> PickTasksAsync(string outboundId, string? token = null)
        => (await Expect(W.GetAsync($"/api/warehouse/pick-tasks?outboundId={outboundId}&pageSize=200&orderBy=createdAt", token ?? Supervisor), 200))
            .GetProperty("items").EnumerateArray().OrderBy(t => t.GetProperty("allocSeq").GetInt32()).ThenBy(t => t.GetProperty("number").GetString()).ToList();

    public async Task<JsonElement> PickTaskAsync(string taskId, string? token = null) => await Expect(W.GetAsync($"/api/warehouse/pick-tasks/{taskId}", token ?? Supervisor), 200);

    public Task<HttpResponseMessage> ConfirmAsync(string taskId, decimal quantity, string ifMatch, string? key = null, string? token = null, Dictionary<string, object?>? extra = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? NewKey(), ["quantity"] = PutawayScenario.Q(quantity) };
        foreach (var (k, v) in extra ?? []) body[k] = v;
        return W.Send(HttpMethod.Post, $"/api/warehouse/pick-tasks/{taskId}/confirmations", body, (string)body["idempotencyKey"]!, ifMatch, token ?? Supervisor);
    }

    public async Task<JsonElement> ConfirmedAsync(JsonElement task, decimal? quantity = null)
        => await Expect(ConfirmAsync(PutawayScenario.TaskId(task), quantity ?? PutawayScenario.D(task, "remainingQuantity"), PutawayScenario.ETagOf(task)), 201);

    public Task<HttpResponseMessage> RetryAsync(string taskId, int seq, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, $"/api/warehouse/pick-tasks/{taskId}/confirmations/{seq}/retry", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k, null, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> ReasonedAsync(string path, string? reason, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, path, new Dictionary<string, object?> { ["idempotencyKey"] = k, ["reason"] = reason }, k, ifMatch, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> CloseShortAsync(JsonElement task, string? reason = PutawayScenario.Reason, string? key = null, string? token = null)
        => ReasonedAsync($"/api/warehouse/pick-tasks/{PutawayScenario.TaskId(task)}/close-short", reason, PutawayScenario.ETagOf(task), key, token);

    public Task<HttpResponseMessage> ClaimAsync(JsonElement task, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, $"/api/warehouse/pick-tasks/{PutawayScenario.TaskId(task)}/claim", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k,
            PutawayScenario.ETagOf(task), token ?? Supervisor);
    }

    /// <summary>Confirms every pick task of the outbound in full.</summary>
    public async Task PickAllAsync(string outboundId)
    {
        foreach (var task in await PickTasksAsync(outboundId)) await ConfirmedAsync(task);
    }

    // ── packages ──

    public static Dictionary<string, object?> Contents(params (int AllocSeq, decimal Quantity)[] rows)
        => new() { ["contents"] = rows.Select(r => new Dictionary<string, object?> { ["allocSeq"] = r.AllocSeq, ["quantity"] = PutawayScenario.Q(r.Quantity) }).ToList() };

    public Task<HttpResponseMessage> CreatePackageAsync(string outboundId, Dictionary<string, object?> body, string? key = null, string? token = null)
    {
        var b = new Dictionary<string, object?>(body) { ["idempotencyKey"] = key ?? NewKey() };
        return W.Send(HttpMethod.Post, $"/api/warehouse/outbound-shipments/{outboundId}/packages", b, (string)b["idempotencyKey"]!, null, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> UpdatePackageAsync(string packageId, Dictionary<string, object?> body, string ifMatch, string? key = null, string? token = null)
    {
        var b = new Dictionary<string, object?>(body) { ["idempotencyKey"] = key ?? NewKey() };
        return W.Send(HttpMethod.Put, $"/api/warehouse/packages/{packageId}", b, (string)b["idempotencyKey"]!, ifMatch, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> ClosePackageAsync(string packageId, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, $"/api/warehouse/packages/{packageId}/close", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k, ifMatch, token ?? Supervisor);
    }

    public async Task<JsonElement> PackingAsync(string outboundId, string? token = null)
        => await Expect(W.GetAsync($"/api/warehouse/outbound-shipments/{outboundId}/packages", token ?? Supervisor), 200);

    /// <summary>One package with every picked quantity, closed → the outbound Packed.</summary>
    public async Task PackAllAsync(string outboundId)
    {
        var packing = await PackingAsync(outboundId);
        var rows = packing.GetProperty("allocations").EnumerateArray().Select(a => (a.GetProperty("allocSeq").GetInt32(), PutawayScenario.D(a, "picked"))).Where(r => r.Item2 > 0m).ToArray();
        var package = await Expect(CreatePackageAsync(outboundId, Contents(rows)), 201);
        await Expect(ClosePackageAsync(package.GetProperty("packageId").GetString()!, ETag(package)), 200);
    }

    public static string ETag(JsonElement e) => $"W/\"{e.GetProperty("version").GetInt32()}\"";

    // ── outbound ──

    public async Task<JsonElement> ViewAsync(string outboundId) => await Expect(S.ViewAsync(outboundId, Supervisor), 200);

    public async Task<string> ETagOfOutboundAsync(string outboundId) => ETag(await ViewAsync(outboundId));

    public async Task<HttpResponseMessage> ReleaseAsync(string outboundId, IEnumerable<(int AllocSeq, string Reason)>? reasons = null, string? ifMatch = null, string? key = null,
        string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k };
        if (reasons is not null) body["fefoBypassReasons"] = reasons.Select(r => new Dictionary<string, object?> { ["allocSeq"] = r.AllocSeq, ["reason"] = r.Reason }).ToList();
        return await W.Send(HttpMethod.Post, $"/api/warehouse/outbound-shipments/{outboundId}/release", body, k, ifMatch ?? await ETagOfOutboundAsync(outboundId), token ?? Supervisor);
    }

    public async Task<HttpResponseMessage> SyncAsync(string outboundId, string? ifMatch = null, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return await W.Send(HttpMethod.Post, $"/api/warehouse/outbound-shipments/{outboundId}/sync-dispatch", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k,
            ifMatch ?? await ETagOfOutboundAsync(outboundId), token ?? Supervisor);
    }

    public async Task<HttpResponseMessage> CancelAsync(string outboundId, string? reason = Reason, string? ifMatch = null, string? key = null, string? token = null)
        => await ReasonedAsync($"/api/warehouse/outbound-shipments/{outboundId}/cancel", reason, ifMatch ?? await ETagOfOutboundAsync(outboundId), key, token);

    public Task<HttpResponseMessage> FrozenAsync(string outboundId, string? token = null) => W.GetAsync($"/api/warehouse/outbound-shipments/{outboundId}", token ?? Supervisor);

    public async Task<string> FrozenTextAsync(string outboundId)
    {
        using var response = await FrozenAsync(outboundId);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"frozen detail: {(int)response.StatusCode} {text}");
        return text;
    }

    public async Task<JsonElement> FrozenListAsync(string query = "") => await Expect(W.GetAsync($"/api/warehouse/outbound-shipments{query}", Supervisor), 200);

    /// <summary>Allocated → Picked → Packed (every task in full, one package).</summary>
    public async Task<string> PackedAsync(params Dictionary<string, object?>[] lines)
    {
        var id = OutboundScenario.Id(await S.CreatedAsync(lines));
        await PickAllAsync(id);
        await PackAllAsync(id);
        Assert.Equal("Packed", (await ViewAsync(id)).GetProperty("status").GetString());
        return id;
    }

    public async Task<string> ReadyAsync(params Dictionary<string, object?>[] lines)
    {
        var id = await PackedAsync(lines);
        await Expect(ReleaseAsync(id), 200);
        return id;
    }

    /// <summary>The MVP-6 step of the fixture: a <c>GOODS_ISSUE_SALES</c> on the REAL ledger API (source MOD-0183) from <paramref name="from"/>.</summary>
    public async Task<JsonElement> IssueAsync(decimal quantity, Guid from, string? lotId, string? reservationId = null)
    {
        var body = S.P.Inventory.Issue(PutawayScenario.Q(quantity), location: from.ToString("D"));
        body["sourceModule"] = "MOD-0183";
        body["sourceType"] = "SHIPMENT";
        body["sourceDocumentId"] = Guid.NewGuid().ToString("D");
        if (lotId is null) body.Remove("lotId");
        else body["lotId"] = lotId;
        if (reservationId is not null) body["reservationId"] = reservationId;
        return await S.P.Inventory.PostExpectingAsync(body, 201);
    }

    /// <summary>A LOCATION_TRANSFER on the REAL ledger API (outside MOD-0178).</summary>
    public async Task<JsonElement> TransferAsync(decimal quantity, Guid from, Guid to, string? lotId)
    {
        var body = S.P.Inventory.Movement("LOCATION_TRANSFER", PutawayScenario.Q(quantity));
        body["fromWarehouseId"] = S.Warehouse.ToString("D");
        body["fromLocationId"] = from.ToString("D");
        body["toWarehouseId"] = S.Warehouse.ToString("D");
        body["toLocationId"] = to.ToString("D");
        if (lotId is null) body.Remove("lotId");
        else body["lotId"] = lotId;
        return await S.P.Inventory.PostExpectingAsync(body, 201);
    }

    public FakeShipment Dispatched(params (Guid Sku, string? Lot, Guid? From, decimal Quantity)[] lines)
    {
        var shipment = new FakeShipment { Lines = [.. lines] };
        Mvp6.Items.Add(shipment);
        return shipment;
    }

    public List<BsonDocument> Movements() => S.P.Inventory.Persisted("inventory_transactions");

    public List<BsonDocument> Outbox(string? outboundId = null)
        => W.Stored(WarehouseCollections.Outbox).Where(o => outboundId is null || o["AggregateId"].ToString() == outboundId).ToList();

    public List<BsonDocument> Packages(string outboundId) => W.Stored(WarehouseCollections.HandlingUnits).Where(p => p["OutboundId"].ToString() == outboundId).ToList();

    public List<BsonDocument> PutawayTasks(string? origin = null)
        => W.Stored(WarehouseCollections.Tasks).Where(t => t["Kind"].AsString == "Putaway" && (origin is null || t["Origin"].AsString == origin)).ToList();

    public string[] OutboundEventKinds(string outboundId) => S.OutboundEvents(outboundId).Select(e => e["Kind"].AsString).ToArray();

    public static List<JsonElement> Allocs(JsonElement view) => OutboundScenario.Allocations(view);

    public static JsonElement Alloc(JsonElement view, int seq) => Allocs(view).Single(a => a.GetProperty("allocSeq").GetInt32() == seq);

    /// <summary>A recall block of <paramref name="lotId"/> in this tenant (the REAL recall reader).</summary>
    public void Recall(string lotId) => S.P.Recall(lotId);

    public void LotStatus(string lotId, Diten.InventoryService.Domain.Traceability.Enums.LotStatus status)
        => S.Factory.Database.GetCollection<Diten.InventoryService.Domain.Traceability.Entities.Lot>("trace_lots")
            .UpdateOne(l => l.Id == Guid.Parse(lotId), Builders<Diten.InventoryService.Domain.Traceability.Entities.Lot>.Update.Set(l => l.Status, status));
}
