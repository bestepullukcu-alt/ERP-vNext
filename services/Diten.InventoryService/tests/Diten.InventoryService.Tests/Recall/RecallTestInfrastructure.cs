using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Recall;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// The test double of the recall e-signature gate (R4 brings the MOD-0022 one): required as configured per action (CT-R16: settings not by
/// default); a signature verifies when it was issued (<see cref="Issue"/>) for exactly that signer, case and action. Everything else is a
/// mismatch. Records every verification it was asked.
/// </summary>
public sealed class TestRecallSignature : IRecallSignature
{
    private readonly ConcurrentDictionary<string, (Guid Signer, Guid Subject, string Action)> _issued = new(StringComparer.Ordinal);

    public bool SettingsRequired { get; set; }

    public ConcurrentQueue<RecallSignatureVerification> Verifications { get; } = new();

    public string Issue(Guid signer, string subjectId, string actionCode)
    {
        var id = "sig-rc-" + Guid.NewGuid().ToString("N");
        _issued[id] = (signer, Guid.Parse(subjectId), actionCode);
        return id;
    }

    public Task<RecallSignatureRequirement> RequirementAsync(string actionCode, Guid subjectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RecallSignatureRequirement(
            actionCode != RecallContract.SettingsUpdateAction || SettingsRequired, RecallContract.DefaultMeaningCode, RecallContract.DefaultVisualMode));

    public Task<bool> VerifyAsync(RecallSignatureVerification verification, CancellationToken cancellationToken = default)
    {
        Verifications.Enqueue(verification);
        return Task.FromResult(_issued.TryGetValue(verification.SignatureId, out var issued)
                               && issued.Signer == verification.SignerUserId && issued.Subject == verification.SubjectId
                               && issued.Action == verification.ActionCode);
    }
}

/// <summary>
/// The REAL host (Program.cs, real middleware incl. the scope resolution, real MediatR pipeline, real Mongo repositories on the suite's own
/// replica set) of <see cref="TraceApiFactory"/>, with the recall signature gate replaced by <see cref="TestRecallSignature"/>.
/// <see cref="ProductionSignatureRecallApiFactory"/> keeps the production fail-closed gate. The recall records of the tenants it created are
/// removed on dispose (DB-010).
/// </summary>
public class RecallApiFactory : TraceApiFactory
{
    private readonly ConcurrentBag<Guid> _recallTenants = [];

    public TestRecallSignature Signature { get; } = new();

    protected virtual bool UseTestSignature => true;

    public void TrackRecallTenant(Guid tenant) => _recallTenants.Add(tenant);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            if (UseTestSignature)
            {
                services.RemoveAll<IRecallSignature>();
                services.AddSingleton<IRecallSignature>(Signature);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_recallTenants.IsEmpty)
        {
            var tenants = new BsonArray(_recallTenants.Select(t => t.ToString()));
            foreach (var name in RecallCollections.All)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>The production signature gate (fail-closed until R4) — WP-0177-R1 item 2, sabotage (g).</summary>
public sealed class ProductionSignatureRecallApiFactory : RecallApiFactory
{
    protected override bool UseTestSignature => false;
}

/// <summary>
/// Every RECALL-BUNDLE response of an R1 operation is checked against <c>recall.openapi.yaml</c> (status allowed, body schema — closed objects,
/// formats enforced — ETag where the contract declares it, correlation id). Other calls of the same client pass through.
/// </summary>
public sealed class RecallContractValidatingHandler : DelegatingHandler
{
    private sealed record Operation(string Method, Regex Path, HashSet<int> Allowed, string Schema, bool ETag);

    private static Regex Route(string template) => new("^" + Regex.Escape(template).Replace("\\{id}", "[^/]+") + "/?$", RegexOptions.Compiled);

    private static Operation Op(string method, string template, int[] allowed, string schema, bool etag = false) => new(method, Route(template), [.. allowed], schema, etag);

    // 400 / 401 / 403 / 503 are the scope refusals every /api/recall endpoint may give (InventoryScopeResolutionMiddleware).
    private static readonly Operation[] Operations =
    [
        Op("GET", "/api/recall/settings", [200, 400, 401, 403, 503], "RecallSettings", etag: true),
        Op("PUT", "/api/recall/settings", [200, 400, 401, 403, 409, 503], "RecallSettings", etag: true),
        Op("GET", "/api/recall/settings/history", [200, 400, 401, 403, 503], "SettingsHistoryPage"),
        Op("GET", "/api/recall/cases", [200, 400, 401, 403, 503], "CasePage"),
        Op("POST", "/api/recall/cases", [200, 201, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("GET", "/api/recall/cases/{id}", [200, 400, 401, 403, 404, 503], "CaseDetail", etag: true),
        Op("GET", "/api/recall/cases/{id}/scope", [200, 400, 401, 403, 404, 503], "ScopeView", etag: true),
        Op("GET", "/api/recall/cases/{id}/timeline", [200, 400, 401, 403, 404, 503], "TimelinePage"),
        Op("POST", "/api/recall/cases/{id}/scope/items", [200, 400, 401, 403, 404, 409, 503], "ScopeView", etag: true),
        Op("POST", "/api/recall/cases/{id}/scope/marks", [200, 400, 401, 403, 404, 409, 503], "ScopeView", etag: true),
        Op("POST", "/api/recall/cases/{id}/scope/submit", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/scope/return", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/pending-action", [200, 400, 401, 403, 404, 409, 503], "PendingActionResponse", etag: true),
        Op("POST", "/api/recall/cases/{id}/scope/approve", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/cancel", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/segregations", [200, 201, 400, 401, 403, 404, 409, 422, 503], "Segregation"),
        Op("POST", "/api/recall/cases/{id}/notifications", [200, 201, 400, 401, 403, 404, 409, 503], "Notification"),
        Op("POST", "/api/recall/cases/{id}/consumption-declarations", [200, 201, 400, 401, 403, 404, 409, 422, 503], "ConsumptionDeclaration"),
        Op("POST", "/api/recall/cases/{id}/effectiveness/start", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/effectiveness/complete", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("POST", "/api/recall/cases/{id}/close", [200, 400, 401, 403, 404, 409, 503], "CaseDetail", etag: true),
        Op("GET", "/api/recall/lots/{id}/status", [200, 400, 401, 403, 404, 503], "LotRecallStatus")
    ];

    private static readonly Regex WeakETag = new("^W/\"\\d+\"$", RegexOptions.Compiled);

    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var operation = Operations.FirstOrDefault(o => o.Method == request.Method.Method && o.Path.IsMatch(path));
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!operation.Allowed.Contains(status))
        {
            throw new ContractViolationException($"{request.Method} {path} answered {status}, which the RECALL contract does not allow. Body: {body}");
        }

        var schema = status is 200 or 201 ? operation.Schema : "Error";
        var errors = ContractSchemas.ValidateRecall(schema, body);
        if (errors.Count > 0)
        {
            throw new ContractViolationException($"{request.Method} {path} {status} body does not match recall '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{request.Method} {path} {status} carries no UUID X-Correlation-Id header.");
        }

        if (operation.ETag && status is 200 or 201 && (response.Headers.ETag is null || !WeakETag.IsMatch(response.Headers.ETag.ToString())))
        {
            throw new ContractViolationException($"{request.Method} {path} {status} carries no ETag W/\"<version>\" header.");
        }

        Validated.AddOrUpdate($"{request.Method} {operation.Path} {status} → recall:{schema}", 1, (_, n) => n + 1);
        response.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return response;
    }
}

/// <summary>
/// MOD-0177 world: the MOD-0174 <see cref="LotLedgerScenario"/> (fresh tenant + company, real location tree, a lot-controlled item) plus the
/// mockup roles (L:9-15): two QA users (CT-R1 needs a second one — the submitter cannot approve, the completer cannot close), the warehouse
/// operator, the commercial user, the quality-system admin and management (read-only). Every call goes through the real API and every R1
/// response is checked against RECALL-BUNDLE.
/// </summary>
public sealed class RecallScenario
{
    public const string Reason = "Tedarikci sapma bildirimi DEV-SUP-2026-031";

    public static readonly string[] QaKeys =
    [
        "recall.cases.read", "recall.cases.create", "recall.cases.update", "recall.cases.approve", "recall.cases.cancel", "recall.cases.close",
        "recall.effectiveness.complete", "recall.notifications.create", "recall.consumptions.create", "recall.settings.read", "recall.lots.status.read"
    ];

    public static readonly string[] DepoKeys = ["recall.cases.read", "recall.segregations.confirm", "recall.lots.status.read"];

    public static readonly string[] TicariKeys = ["recall.cases.read", "recall.notifications.create", "recall.consumptions.create"];

    public static readonly string[] AdminKeys = ["recall.settings.read", "recall.settings.manage", "recall.cases.read"];

    public static readonly string[] ManagementKeys = ["recall.cases.read"];

    public RecallScenario(RecallApiFactory factory)
    {
        Factory = factory;
        Lots = new LotLedgerScenario(factory);
        factory.TrackRecallTenant(Tenant);
        Qa1Token = Mint(Qa1, QaKeys);
        Qa2Token = Mint(Qa2, QaKeys);
        DepoToken = Mint(Depo, DepoKeys);
        TicariToken = Mint(Ticari, TicariKeys);
        AdminToken = Mint(Admin, AdminKeys);
        ManagementToken = Mint(Management, ManagementKeys);
        Client = factory.CreateDefaultClient(new RecallContractValidatingHandler());
    }

    public RecallApiFactory Factory { get; }
    public LotLedgerScenario Lots { get; }
    public InventoryScenario Ledger => Lots.Ledger;
    public Guid Tenant => Ledger.Tenant;
    public Guid Qa1 { get; } = Guid.NewGuid();
    public Guid Qa2 { get; } = Guid.NewGuid();
    public Guid Depo { get; } = Guid.NewGuid();
    public Guid Ticari { get; } = Guid.NewGuid();
    public Guid Admin { get; } = Guid.NewGuid();
    public Guid Management { get; } = Guid.NewGuid();
    public string Qa1Token { get; }
    public string Qa2Token { get; }
    public string DepoToken { get; }
    public string TicariToken { get; }
    public string AdminToken { get; }
    public string ManagementToken { get; }
    public HttpClient Client { get; }

    public static string Key(string prefix = "rc") => prefix + "-" + Guid.NewGuid().ToString("N");

    public string Mint(Guid user, IEnumerable<string> keys, Guid? legalEntity = null, Guid? tenant = null)
        => TestTokens.Mint(tenant ?? Tenant, legalEntity ?? Ledger.LegalEntity, user, keys);

    public async Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> SendAsync(
        HttpMethod method, string path, string token, object? body = null, string? key = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return ((int)response.StatusCode, json, response);
    }

    public async Task<JsonElement> ExpectAsync(HttpMethod method, string path, string token, object? body, int expected, string? key = null, string? ifMatch = null)
    {
        var (status, json, _) = await SendAsync(method, path, token, body, key, ifMatch);
        Assert.True(status == expected, $"{method} {path}: expected {expected}, got {status}: {json}");
        return json;
    }

    /// <summary>POST a command body (its idempotencyKey is the header) on a case.</summary>
    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> CommandAsync(string caseId, string verb, string token, Dictionary<string, object?> body,
        string? ifMatch)
        => SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/{verb}", token, body, (string?)body["idempotencyKey"], ifMatch);

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    public static string ETag(JsonElement body) => $"W/\"{body.GetProperty("version").GetInt32()}\"";

    public static string Id(JsonElement c) => c.GetProperty("caseId").GetString()!;

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A registered, released lot (TRACE API) of the scenario's item.</summary>
    public async Task<string> LotAsync()
    {
        var lot = await Lots.CreateLotAsync();
        await Lots.ReleaseAsync(lot);
        return lot;
    }

    public static string LotKey(string lotId) => $"Lot:{lotId}";

    public string StockKey(string lotId, string status = "QUARANTINE") => $"Stock:{lotId}:{Ledger.Warehouse}:-:{status}";

    public static string ShipmentKey(string lotId, string document) => $"Shipment:{lotId}:{document}";

    // ── case ────────────────────────────────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> Open(string rootLotId, int @class = 2, string? key = null, string reason = Reason)
        => new()
        {
            ["idempotencyKey"] = key ?? Key("open"),
            ["rootLotId"] = rootLotId,
            ["direction"] = "Both",
            ["class"] = @class,
            ["level"] = "Pharmacy",
            ["reason"] = reason,
            ["reasonReference"] = "DEV-SUP-2026-031"
        };

    public async Task<JsonElement> OpenAsync(string rootLotId, string? token = null, int expected = 201, int @class = 2)
    {
        var body = Open(rootLotId, @class);
        return await ExpectAsync(HttpMethod.Post, "/api/recall/cases", token ?? Qa1Token, body, expected, (string)body["idempotencyKey"]!);
    }

    /// <summary>POST /cases with the body's own idempotency key as the header.</summary>
    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PostOpenAsync(string token, Dictionary<string, object?> body)
        => SendAsync(HttpMethod.Post, "/api/recall/cases", token, body, (string)body["idempotencyKey"]!);

    public Task<JsonElement> GetAsync(string caseId, string? token = null, int expected = 200)
        => ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}", token ?? Qa1Token, null, expected);

    public Dictionary<string, object?> StockItem(string lotId, decimal quantity = 450m, string status = "QUARANTINE")
        => new() { ["kind"] = "Stock", ["lotId"] = lotId, ["warehouseId"] = Ledger.Warehouse, ["stockStatus"] = status, ["quantity"] = quantity, ["baseUomId"] = "EA" };

    public static Dictionary<string, object?> ShipmentItem(string lotId, string document, string customer, decimal quantity)
        => new() { ["kind"] = "Shipment", ["lotId"] = lotId, ["sourceDocumentId"] = document, ["customerRef"] = customer, ["quantity"] = quantity, ["baseUomId"] = "EA" };

    public static Dictionary<string, object?> LotItem(string lotId, string? parentKey = null)
    {
        var item = new Dictionary<string, object?> { ["kind"] = "Lot", ["lotId"] = lotId };
        if (parentKey is not null) item["parentKey"] = parentKey;
        return item;
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> AddItemsAsync(string caseId, string ifMatch, params Dictionary<string, object?>[] items)
        => CommandAsync(caseId, "scope/items", Qa1Token, new Dictionary<string, object?> { ["idempotencyKey"] = Key("add"), ["items"] = items }, ifMatch);

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> MarkAsync(string caseId, string ifMatch, string nodeKey, bool included, string? reason = null)
    {
        var mark = new Dictionary<string, object?> { ["nodeKey"] = nodeKey, ["included"] = included };
        if (reason is not null) mark["exclusionReason"] = reason;
        return CommandAsync(caseId, "scope/marks", Qa1Token, new Dictionary<string, object?> { ["idempotencyKey"] = Key("mark"), ["marks"] = new[] { mark } }, ifMatch);
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> SubmitAsync(string caseId, string ifMatch, string? token = null)
        => CommandAsync(caseId, "scope/submit", token ?? Qa1Token, new Dictionary<string, object?> { ["idempotencyKey"] = Key("sub") }, ifMatch);

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PrepareAsync(string caseId, string action, string ifMatch, string token,
        string? note = null, string? summary = null, string? acceptance = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = Key("prep"), ["action"] = action };
        if (note is not null) body["note"] = note;
        if (summary is not null) body["summary"] = summary;
        if (acceptance is not null) body["openQuantityAcceptance"] = acceptance;
        return CommandAsync(caseId, "pending-action", token, body, ifMatch);
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> ApproveAsync(string caseId, string ifMatch, string token, string? signatureId, string? note = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = Key("appr") };
        if (note is not null) body["note"] = note;
        if (signatureId is not null) body["signatureId"] = signatureId;
        return CommandAsync(caseId, "scope/approve", token, body, ifMatch);
    }

    /// <summary>Prepare + sign (test double) + approve as <paramref name="approver"/>; returns the approved case.</summary>
    public async Task<JsonElement> ApproveSignedAsync(string caseId, JsonElement current, Guid approver, string approverToken)
    {
        var (sp, prepared, _) = await PrepareAsync(caseId, "ApproveScope", ETag(current), approverToken);
        Assert.True(sp == 200, prepared.ToString());
        var signature = Factory.Signature.Issue(approver, caseId, prepared.GetProperty("actionCode").GetString()!);
        var (sa, approved, _) = await ApproveAsync(caseId, ETag(prepared), approverToken, signature);
        Assert.True(sa == 200, approved.ToString());
        return approved;
    }

    /// <summary>A case with a root lot, a stock item and two shipments (two customers), approved by Qa2 — Active.</summary>
    public async Task<(string CaseId, string Lot, JsonElement Case)> ActiveCaseAsync(decimal shippedA = 300m, decimal shippedB = 200m)
    {
        var lot = await LotAsync();
        var c = await OpenAsync(lot);
        var id = Id(c);
        var (s1, scope, _) = await AddItemsAsync(id, ETag(c), StockItem(lot), ShipmentItem(lot, "SHP-2026-0820", "CUST-100/ADDR-2", shippedA),
            ShipmentItem(lot, "SHP-2026-0790", "CUST-200/ADDR-1", shippedB));
        Assert.True(s1 == 200, scope.ToString());
        var (s2, submitted, _) = await SubmitAsync(id, ETag(scope));
        Assert.True(s2 == 200, submitted.ToString());
        var approved = await ApproveSignedAsync(id, submitted, Qa2, Qa2Token);
        return (id, lot, approved);
    }

    public List<BsonDocument> Persisted(string collection) => Ledger.Persisted(collection);

    public List<BsonDocument> Audit(string action)
        => Persisted("inventory_audit").Where(d => d.Contains("Action") && d["Action"].AsString == action).ToList();
}
