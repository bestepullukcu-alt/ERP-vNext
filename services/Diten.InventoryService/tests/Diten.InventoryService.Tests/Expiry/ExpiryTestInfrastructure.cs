using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Persistence.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>A clock the test moves (SLA due times, pending-resolution expiry). Starts at the real UTC now.</summary>
public sealed class MutableClock : TimeProvider
{
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + _offset;

    public void Advance(TimeSpan by) => _offset += by;
}

/// <summary>
/// The test double of the e-signature gate (F3 brings the MOD-0022 one): required as configured; a signature verifies when its id
/// starts with <c>sig-ok</c> and was issued for the very decision it is verified against (<see cref="Issue"/>). Everything else is a
/// mismatch. Records every verification it was asked.
/// </summary>
public sealed class TestExpirySignature : IExpiryResolutionSignature
{
    private readonly ConcurrentDictionary<string, (Guid Signer, Guid Exception, string Action)> _issued = new(StringComparer.Ordinal);

    public bool Required { get; set; } = true;

    public ConcurrentQueue<ExpirySignatureVerification> Verifications { get; } = new();

    /// <summary>A signature id for <paramref name="signer"/> on <paramref name="exceptionId"/> with the action of the resolution.</summary>
    public string Issue(Guid signer, string exceptionId, string actionCode)
    {
        var id = "sig-ok-" + Guid.NewGuid().ToString("N");
        _issued[id] = (signer, Guid.Parse(exceptionId), actionCode);
        return id;
    }

    public Task<ExpirySignatureRequirement> RequirementAsync(string actionCode, Guid exceptionId, CancellationToken cancellationToken = default)
        => Task.FromResult(new ExpirySignatureRequirement(Required, ExpiryContract.DefaultMeaningCode, ExpiryContract.DefaultVisualMode));

    public Task<bool> VerifyAsync(ExpirySignatureVerification verification, CancellationToken cancellationToken = default)
    {
        Verifications.Enqueue(verification);
        return Task.FromResult(_issued.TryGetValue(verification.SignatureId, out var issued)
                               && issued.Signer == verification.SignerUserId && issued.Exception == verification.ExceptionId
                               && issued.Action == verification.ActionCode);
    }
}

/// <summary>
/// The REAL host (Program.cs, real middleware incl. the scope resolution, real MediatR pipeline, real Mongo repositories on the suite's
/// own replica set) of <see cref="TraceApiFactory"/>, with the e-signature gate replaced by <see cref="TestExpirySignature"/> and a
/// <see cref="MutableClock"/>. <see cref="ProductionSignatureExpiryApiFactory"/> keeps the production fail-closed gate.
/// The expiry records of the tenants it created are removed on dispose (DB-010).
/// </summary>
public class ExpiryApiFactory : TraceApiFactory
{
    private static readonly string[] ExpiryOwned = [.. ExpiryCollections.All];

    private readonly ConcurrentBag<Guid> _expiryTenants = [];

    public TestExpirySignature Signature { get; } = new();

    public MutableClock Clock { get; } = new();

    protected virtual bool UseTestSignature => true;

    public void TrackExpiryTenant(Guid tenant) => _expiryTenants.Add(tenant);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            if (UseTestSignature)
            {
                services.RemoveAll<IExpiryResolutionSignature>();
                services.AddSingleton<IExpiryResolutionSignature>(Signature);
            }
        });
    }

    public Task<ExpiredOnHandSweeper.SweepResult> SweepAsync(Guid tenant, DateTimeOffset now)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ExpiredOnHandSweeper>().SweepAsync(now, CancellationToken.None, tenant);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_expiryTenants.IsEmpty)
        {
            var tenants = new BsonArray(_expiryTenants.Select(t => t.ToString()));
            foreach (var name in ExpiryOwned)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }

            Collection(ExpiryCollections.Counters).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
        }

        base.Dispose(disposing);
    }
}

/// <summary>The production e-signature gate (fail-closed until F3) — WP-0176-F1 §4, K3 sabotage (g).</summary>
public sealed class ProductionSignatureExpiryApiFactory : ExpiryApiFactory
{
    protected override bool UseTestSignature => false;
}

/// <summary>
/// Every FEFO-BUNDLE response of an F1 operation is checked against <c>fefo.openapi.yaml</c> (status allowed, body schema — closed
/// objects, formats enforced — ETag where the contract declares it, correlation id). The INVENTORY / TRACE / QUARANTINE calls of the
/// same client pass through to the suite's own validator.
/// </summary>
public sealed class ExpiryContractValidatingHandler : DelegatingHandler
{
    private sealed record Operation(string Method, Regex Path, HashSet<int> Allowed, Func<int, string> Schema, bool ETag);

    private static Regex Route(string template) => new("^" + Regex.Escape(template).Replace("\\{id}", "[^/]+") + "/?$", RegexOptions.Compiled);

    private static Operation Op(string method, string template, int[] allowed, Func<int, string> schema, bool etag = false)
        => new(method, Route(template), [.. allowed], schema, etag);

    private static readonly Operation[] Operations =
    [
        // 400 / 401 / 403 / 503 are the scope refusals every /api/expiry endpoint may give (InventoryScopeResolutionMiddleware).
        Op("GET", "/api/expiry/policy", [200, 400, 401, 403, 503], _ => "ExpiryPolicy", etag: true),
        Op("PUT", "/api/expiry/policy", [200, 400, 401, 403, 409, 503], _ => "ExpiryPolicy", etag: true),
        Op("GET", "/api/expiry/policy/history", [200, 400, 401, 403, 503], _ => "PolicyHistoryPage"),
        Op("GET", "/api/expiry/exceptions", [200, 400, 401, 403, 503], _ => "ExceptionPage"),
        Op("POST", "/api/expiry/exceptions", [200, 201, 400, 401, 403, 404, 409, 503], _ => "ExceptionDetail", etag: true),
        Op("GET", "/api/expiry/exceptions/{id}", [200, 400, 401, 403, 404, 503], _ => "ExceptionDetail", etag: true),
        Op("POST", "/api/expiry/exceptions/{id}/take", [200, 400, 401, 403, 404, 409, 503], _ => "ExceptionDetail", etag: true),
        Op("POST", "/api/expiry/exceptions/{id}/comments", [200, 201, 400, 401, 403, 404, 409, 503], _ => "ExceptionEvent"),
        Op("POST", "/api/expiry/exceptions/{id}/pending-resolution", [200, 400, 401, 403, 404, 409, 503], _ => "ResolutionPrepared", etag: true),
        Op("POST", "/api/expiry/exceptions/{id}/resolve", [200, 400, 401, 403, 404, 409, 503], _ => "ExceptionDetail", etag: true),
        Op("POST", "/api/expiry/exceptions/{id}/close", [200, 400, 401, 403, 404, 409, 503], _ => "ExceptionDetail", etag: true)
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
            throw new ContractViolationException($"{request.Method} {path} answered {status}, which the FEFO contract does not allow. Body: {body}");
        }

        var schema = status is 200 or 201 ? operation.Schema(status) : "Error";
        var errors = ContractSchemas.ValidateFefo(schema, body);
        if (errors.Count > 0)
        {
            throw new ContractViolationException($"{request.Method} {path} {status} body does not match fefo '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{request.Method} {path} {status} carries no UUID X-Correlation-Id header.");
        }

        if (operation.ETag && status is 200 or 201 && (response.Headers.ETag is null || !WeakETag.IsMatch(response.Headers.ETag.ToString())))
        {
            throw new ContractViolationException($"{request.Method} {path} {status} carries no ETag W/\"<version>\" header.");
        }

        Validated.AddOrUpdate($"{request.Method} {operation.Path} {status} → fefo:{schema}", 1, (_, n) => n + 1);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        response.Content = content;
        return response;
    }
}

/// <summary>
/// MOD-0176 world: the MOD-0174 <see cref="LotLedgerScenario"/> (fresh tenant + company, real location tree, a lot-controlled item)
/// plus three users of the mockup roles (L:17-21): the warehouse OPERATOR (reads, opens, comments), QA (reviews, resolves, closes)
/// and the quality-system ADMIN (policy). Every call goes through the real API and every response is checked against FEFO-BUNDLE.
/// </summary>
public sealed class ExpiryScenario
{
    public const string Reason = "QA karari - sapma DEV-2026-021";

    public static readonly string[] OperatorKeys = ["expiry.exceptions.read", "expiry.exceptions.create", "expiry.exceptions.comment"];

    public static readonly string[] QaKeys =
        ["expiry.exceptions.read", "expiry.exceptions.comment", "expiry.exceptions.review", "expiry.exceptions.resolve", "expiry.exceptions.close", "expiry.policies.read"];

    public static readonly string[] AdminKeys = ["expiry.exceptions.read", "expiry.policies.read", "expiry.policies.manage"];

    public ExpiryScenario(ExpiryApiFactory factory)
    {
        Factory = factory;
        Lots = new LotLedgerScenario(factory);
        factory.TrackExpiryTenant(Tenant);
        OperatorToken = TestTokens.Mint(Tenant, Ledger.LegalEntity, Operator, OperatorKeys);
        QaToken = TestTokens.Mint(Tenant, Ledger.LegalEntity, Qa, QaKeys);
        AdminToken = TestTokens.Mint(Tenant, Ledger.LegalEntity, Admin, AdminKeys);
        Client = factory.CreateDefaultClient(new ExpiryContractValidatingHandler());
    }

    public ExpiryApiFactory Factory { get; }
    public LotLedgerScenario Lots { get; }
    public InventoryScenario Ledger => Lots.Ledger;
    public Guid Tenant => Ledger.Tenant;
    public Guid Operator { get; } = Guid.NewGuid();
    public Guid Qa { get; } = Guid.NewGuid();
    public Guid Admin { get; } = Guid.NewGuid();
    public string OperatorToken { get; }
    public string QaToken { get; }
    public string AdminToken { get; }
    public HttpClient Client { get; }

    public static string Key(string prefix = "fx") => prefix + "-" + Guid.NewGuid().ToString("N");

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

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    public static string ETag(JsonElement body) => $"W/\"{body.GetProperty("version").GetInt32()}\"";

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A registered lot (TRACE API) with an expiry date, released.</summary>
    public async Task<string> LotAsync(string expiry = "2027-01-31T00:00:00Z")
    {
        var lot = await Lots.CreateLotAsync(expiryDate: expiry);
        await Lots.ReleaseAsync(lot);
        return lot;
    }

    // ── policy ──────────────────────────────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> Policy(
        int approaching = 90, int critical = 30, string fefo = "RequireReason", string below = "AutoQuarantine", string date = "ReasonAndSignature",
        int fefoSla = 24, int belowSla = 72, int overrideSla = 48, int onHandSla = 120, int threshold = 14, string? reason = Reason, string? key = null)
        => new()
        {
            ["idempotencyKey"] = key ?? Key("pol"),
            ["approachingDays"] = approaching,
            ["criticalDays"] = critical,
            ["fefoOutboundMode"] = fefo,
            ["belowMinRemainingMode"] = below,
            ["dateOverrideMode"] = date,
            ["slaHours"] = new Dictionary<string, int>
            {
                ["FEFO_BYPASS"] = fefoSla, ["BELOW_MIN_REMAINING"] = belowSla, ["EXPIRY_DATE_OVERRIDE"] = overrideSla, ["EXPIRED_ON_HAND"] = onHandSla
            },
            ["expiredOnHandThresholdDays"] = threshold,
            ["reason"] = reason
        };

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PutPolicyAsync(Dictionary<string, object?> body, string ifMatch, string? token = null)
        => SendAsync(HttpMethod.Put, "/api/expiry/policy", token ?? AdminToken, body, (string)body["idempotencyKey"]!, ifMatch);

    // ── exceptions ──────────────────────────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> Open(string lotId, string? warehouseId, string type = "FEFO_BYPASS", string? key = null, decimal? quantity = null,
        string description = "Elle acilan istisna - test sapma kaydi")
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? Key("open"),
            ["type"] = type,
            ["lotId"] = lotId,
            ["description"] = description
        };
        if (warehouseId is not null) body["warehouseId"] = warehouseId;
        if (quantity is not null) body["quantity"] = quantity;
        return body;
    }

    public async Task<JsonElement> OpenAsync(string lotId, string type = "FEFO_BYPASS", string? token = null, int expected = 201)
    {
        var body = Open(lotId, Ledger.Warehouse, type);
        return await ExpectAsync(HttpMethod.Post, "/api/expiry/exceptions", token ?? OperatorToken, body, expected, (string)body["idempotencyKey"]!);
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> CommandAsync(string id, string verb, string token, Dictionary<string, object?> body, string? ifMatch)
        => SendAsync(HttpMethod.Post, $"/api/expiry/exceptions/{id}/{verb}", token, body, (string?)body["idempotencyKey"], ifMatch);

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> TakeAsync(string id, string ifMatch, string? token = null, string? key = null)
        => CommandAsync(id, "take", token ?? QaToken, new Dictionary<string, object?> { ["idempotencyKey"] = key ?? Key("take") }, ifMatch);

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> PrepareAsync(string id, string resolution, string ifMatch, string? token = null,
        string reason = Reason, string? key = null)
        => CommandAsync(id, "pending-resolution", token ?? QaToken,
            new Dictionary<string, object?> { ["idempotencyKey"] = key ?? Key("prep"), ["resolution"] = resolution, ["reason"] = reason }, ifMatch);

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> ResolveAsync(string id, string resolution, string ifMatch, string? signatureId,
        string? token = null, string reason = Reason, string? key = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? Key("res"), ["resolution"] = resolution, ["reason"] = reason };
        if (signatureId is not null) body["signatureId"] = signatureId;
        return CommandAsync(id, "resolve", token ?? QaToken, body, ifMatch);
    }

    public Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> CloseAsync(string id, string ifMatch, string? token = null)
        => CommandAsync(id, "close", token ?? QaToken, new Dictionary<string, object?> { ["idempotencyKey"] = Key("close") }, ifMatch);

    public Task<JsonElement> GetAsync(string id, string? token = null, int expected = 200)
        => ExpectAsync(HttpMethod.Get, $"/api/expiry/exceptions/{id}", token ?? QaToken, null, expected);

    /// <summary>Prepare + sign (test double) + resolve as QA; returns the resolved detail.</summary>
    public async Task<JsonElement> ResolveSignedAsync(string id, string resolution, string ifMatch)
    {
        var (s1, prepared, _) = await PrepareAsync(id, resolution, ifMatch);
        Assert.True(s1 == 200, prepared.ToString());
        var signature = Factory.Signature.Issue(Qa, id, prepared.GetProperty("actionCode").GetString()!);
        var (s2, resolved, _) = await ResolveAsync(id, resolution, ETag(prepared), signature);
        Assert.True(s2 == 200, resolved.ToString());
        return resolved;
    }

    public List<BsonDocument> Persisted(string collection) => Ledger.Persisted(collection);

    public List<BsonDocument> Audit(string action)
        => Persisted("inventory_audit").Where(d => d.Contains("Action") && d["Action"].AsString == action).ToList();
}
