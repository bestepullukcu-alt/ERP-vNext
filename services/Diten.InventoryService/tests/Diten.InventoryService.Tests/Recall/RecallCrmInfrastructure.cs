using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>MOD-0177-FU01 — what the CRM double answers for an account (pack §8.6 classification is the production reader's).</summary>
public enum CrmBehaviour
{
    Resolved,
    BadRequest,
    Unauthorized,
    Forbidden,
    ServerError,
    ServiceUnavailable,
    Hang,
    Unreachable,
    Raw
}

/// <summary>An account of the CRM double: its tenant (the only tenant it answers), name and code.</summary>
public sealed record CrmAccount(Guid Tenant, string Name, string Code, string Status = "active", bool IsBillable = true);

/// <summary>
/// MOD-0177-FU01 (pack §16) — the CRM-ACCOUNT-LOOKUP U-1 double, built from the contract text (draft sha256 ec5a1062…): it resolves the
/// tenant from the bearer token ONLY (tenant_id claim; no token → 401), answers the same 404 for an unknown account, a deleted one or one of
/// another tenant, counts every call (per account, in flight) and records the headers it saw. Behaviour and latency per account.
/// </summary>
public sealed class CrmDirectoryDouble : HttpMessageHandler
{
    private int _inFlight;
    private int _maxInFlight;
    private int _calls;

    public ConcurrentDictionary<Guid, CrmAccount> Accounts { get; } = new();

    public ConcurrentDictionary<Guid, CrmBehaviour> Behaviour { get; } = new();

    public ConcurrentDictionary<Guid, TimeSpan> Delay { get; } = new();

    public ConcurrentDictionary<Guid, string> RawBody { get; } = new();

    public TimeSpan DefaultDelay { get; set; } = TimeSpan.Zero;

    /// <summary>When set, every account without its own behaviour answers so (e.g. a CRM that answers nothing at all).</summary>
    public CrmBehaviour? Everyone { get; set; }

    public ConcurrentQueue<(Guid Account, string? Authorization, string? Correlation, string? Tenant, string? LegalEntity)> Seen { get; } = new();

    public int Calls => Volatile.Read(ref _calls);

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    public int CallsFor(Guid account) => Seen.Count(s => s.Account == account);

    public void Reset()
    {
        Accounts.Clear();
        Behaviour.Clear();
        Delay.Clear();
        RawBody.Clear();
        Seen.Clear();
        Everyone = null;
        DefaultDelay = TimeSpan.Zero;
        Interlocked.Exchange(ref _calls, 0);
        Interlocked.Exchange(ref _maxInFlight, 0);
    }

    public void ResetCounts()
    {
        Seen.Clear();
        Interlocked.Exchange(ref _calls, 0);
        Interlocked.Exchange(ref _maxInFlight, 0);
    }

    public static string Body(Guid account, string code, string name, string status = "active", bool billable = true, string version = "crm-account-lookup/1.0")
        => JsonSerializer.Serialize(new
        {
            data = new { accountId = account.ToString("D"), code, name, status, isBillable = billable, contractVersion = version },
            errors = (string[]?)null, statusCode = 200, isSuccessful = true
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while ((seen = Volatile.Read(ref _maxInFlight)) < now && Interlocked.CompareExchange(ref _maxInFlight, now, seen) != seen)
        {
        }

        try
        {
            var path = request.RequestUri!.AbsolutePath;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(["api", "crm", "accounts"], segments.Take(3));
            Assert.Equal("lookup-validation", segments[^1]);
            var account = Guid.Parse(segments[3]);
            Seen.Enqueue((account, request.Headers.Authorization?.ToString(), Header(request, "X-Correlation-Id"), Header(request, "X-Tenant-Id"),
                Header(request, "X-Legal-Entity-Id")));
            var behaviour = Behaviour.TryGetValue(account, out var b) ? b : Everyone ?? CrmBehaviour.Resolved;
            var delay = Delay.TryGetValue(account, out var d) ? d : DefaultDelay;
            if (behaviour == CrmBehaviour.Hang) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (behaviour == CrmBehaviour.Unreachable) throw new HttpRequestException("CRM double: connection refused");
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            switch (behaviour)
            {
                case CrmBehaviour.BadRequest: return Status(HttpStatusCode.BadRequest);
                case CrmBehaviour.Unauthorized: return Status(HttpStatusCode.Unauthorized);
                case CrmBehaviour.Forbidden: return Status(HttpStatusCode.Forbidden);
                case CrmBehaviour.ServerError: return Status(HttpStatusCode.InternalServerError);
                case CrmBehaviour.ServiceUnavailable: return Status(HttpStatusCode.ServiceUnavailable);
                case CrmBehaviour.Raw: return Json(HttpStatusCode.OK, RawBody[account]);
            }

            var tenant = TenantOf(request.Headers.Authorization?.Parameter);
            if (tenant is null) return Status(HttpStatusCode.Unauthorized);
            if (!Accounts.TryGetValue(account, out var a) || a.Tenant != tenant)
            {
                return Json(HttpStatusCode.NotFound, """{"data":null,"errors":["Account not found."],"statusCode":404,"isSuccessful":false}""");
            }

            return Json(HttpStatusCode.OK, Body(account, a.Code, a.Name, a.Status, a.IsBillable));
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private static string? Header(HttpRequestMessage request, string name) => request.Headers.TryGetValues(name, out var v) ? v.Single() : null;

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Json(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>The tenant of a bearer token — the tenant_id claim of its payload (the double never trusts a header).</summary>
    private static Guid? TenantOf(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length < 2) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        return json.RootElement.TryGetProperty("tenant_id", out var t) && Guid.TryParse(t.GetString(), out var id) ? id : null;
    }
}

/// <summary>
/// MOD-0177-FU01 — a traced recall world whose shipments carry SHIPMENT-BUNDLE 3.5.0 <c>customerRef</c> values: the MOD-0177 trace world (raw
/// material → finished-good lot <see cref="Fg"/>, one first sale) with stock for many more sales, each sale an MVP-6 shipment whose
/// customerRef / shipToReference the shipment double answers. The CRM double answers the scenario's tenant for the accounts registered here.
/// </summary>
public sealed class CrmWorld
{
    public static readonly Guid A = Guid.Parse("3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c");
    public const string NameA = "Example Hastanesi";
    public const string CodeA = "ACC-2026-000042";

    private CrmWorld(RecallTraceWorld w) => W = w;

    public RecallTraceWorld W { get; }
    public RecallScenario S => W.S;
    public RecallApiFactory F => S.Factory;
    public string Fg => W.Fg;

    /// <summary>The first sale of the trace world (a ShipTo customer unless it is given a customerRef).</summary>
    public string FirstShipment => W.Shipment;

    public string Qa => S.Mint(S.Qa1, [.. RecallR3.QaKeys, "recall.trace.export", "recall.evidence.export", "recall.returns.link"]);

    public static async Task<CrmWorld> CreateAsync(RecallApiFactory factory)
    {
        var w = new CrmWorld(await RecallTraceWorld.CreateAsync(factory));
        await w.W.PostAsync(w.W.Lots.Receipt("5000", w.Fg, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", w.W.Order);
        w.Account(A, NameA, CodeA);
        return w;
    }

    public void Account(Guid id, string name, string code, string status = "active", bool billable = true)
        => F.Crm.Accounts[id] = new CrmAccount(S.Tenant, name, code, status, billable);

    /// <summary>A sale of <see cref="Fg"/> on a new MVP-6 shipment whose read answers <paramref name="customerRefJson"/> (raw JSON; null = no member).</summary>
    public async Task<string> SellRawAsync(string? customerRefJson, string? shipTo = null, string quantity = "1")
    {
        var shipment = await W.SellAsync(Fg, quantity);
        if (customerRefJson is not null) F.Shipments.CustomerRefJson[shipment] = customerRefJson;
        if (shipTo is not null) F.Shipments.ShipToOverride[shipment] = shipTo;
        return shipment;
    }

    public Task<string> SellAsync(Guid account, string? shipTo = null, string quantity = "1", bool upper = false)
        => SellRawAsync("\"" + (upper ? account.ToString("D").ToUpperInvariant() : account.ToString("D")) + "\"", shipTo, quantity);

    /// <summary>The first sale gets a customerRef too (so every customer of the case is a CRM one).</summary>
    public void FirstSaleIs(Guid account) => F.Shipments.CustomerRefJson[FirstShipment] = "\"" + account.ToString("D") + "\"";

    public Task<(string Id, JsonElement Case)> ActiveAsync() => RecallR3.ApproveAsync(S, Fg);

    /// <summary>Open (Forward) on Fg as Qa1 — a Draft case traced from the live trace.</summary>
    public async Task<JsonElement> DraftAsync()
    {
        var body = RecallScenario.Open(Fg);
        body["direction"] = "Forward";
        var (status, opened, _) = await S.PostOpenAsync(S.Mint(S.Qa1, RecallR3.QaKeys), body);
        Assert.True(status == 201, opened.ToString());
        return opened;
    }

    public Task<JsonElement> GetAsync(string path, int expected = 200, string? token = null) => S.ExpectAsync(HttpMethod.Get, path, token ?? Qa, null, expected);

    public Task<JsonElement> CustomersAsync(string caseId, int? page = null, string? token = null)
        => GetAsync($"/api/recall/cases/{caseId}/customers" + (page is null ? "" : $"?customerPage={page}"), 200, token);

    public List<BsonDocument> ScopeDocs(string caseId) => S.Persisted("recall_scope_items").Where(d => d["CaseId"].ToString() == caseId || Same(d["CaseId"], caseId)).ToList();

    private static bool Same(BsonValue v, string id) => v.IsBsonBinaryData && v.AsBsonBinaryData.ToGuid() == Guid.Parse(id);

    public static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    public static JsonElement Customer(JsonElement row) => row.GetProperty("customer");

    /// <summary>The customer row of a key (getCaseCustomers).</summary>
    public static JsonElement Row(JsonElement rows, string key) => Assert.Single(rows.EnumerateArray(), r => Str(r, "customerRef") == key);

    public Dictionary<string, object?> Notify(string customer, string? key = null) => new()
    {
        ["idempotencyKey"] = key ?? RecallScenario.Key("nt"), ["customerRef"] = customer, ["notifiedOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["channel"] = "Phone"
    };

    public Dictionary<string, object?> Consume(string customer, decimal quantity = 1m, string? key = null) => new()
    {
        ["idempotencyKey"] = key ?? RecallScenario.Key("cd"), ["customerRef"] = customer, ["skuId"] = S.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
        ["quantity"] = quantity, ["declaredOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["evidence"] = "Musteri yazisi 2026/90"
    };

    /// <summary>A CUSTOMER_RETURN of Fg into QUARANTINE (after the approval); returns its transaction id.</summary>
    public async Task<string> ReturnAsync(string quantity = "1")
    {
        return await W.Lots.PostOkAsync(W.Lots.Receipt(quantity, Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
    }

    public Dictionary<string, object?> Link(string transactionId, string customer, string? key = null) => new()
    {
        ["idempotencyKey"] = key ?? RecallScenario.Key("ln"), ["transactionId"] = transactionId, ["customerRef"] = customer
    };

    public int Count(string collection) => S.Persisted(collection).Count;

    // ── bulk worlds (V200 / V400): the shipment rows of a traced case, cloned in the store ──────────────────────

    /// <summary>
    /// Clones the case's first shipment item (and the sale behind it) <paramref name="count"/> times as CRM items — each a new MVP-6 document of a
    /// new account (registered in the CRM double), sale number <c>GIS-0001</c>… — so a read sees that many CRM customers without as many postings.
    /// </summary>
    public IReadOnlyList<Guid> SeedCustomers(string caseId, int count, Func<int, Guid>? account = null, int startNumber = 1)
    {
        var items = F.Database.GetCollection<BsonDocument>("recall_scope_items");
        var template = ScopeDocs(caseId).First(d => d["Kind"] == "Shipment");
        var sale = S.Persisted("inventory_transactions").First(d => d.Contains("SourceDocumentId") && d["SourceDocumentId"] == template["SourceDocumentId"]);
        var accounts = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var id = account?.Invoke(i) ?? Guid.NewGuid();
            accounts.Add(id);
            Account(id, $"Musteri {i + startNumber:0000}", $"ACC-V-{i + startNumber:0000}");
            var document = Guid.NewGuid().ToString("D");
            var item = template.DeepClone().AsBsonDocument;
            item["_id"] = template["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            item["NodeKey"] = $"Shipment:{Fg}:{document}";
            item["SourceDocumentId"] = document;
            item["CustomerRef"] = id.ToString("D");
            item["CustomerRefSource"] = "Crm";
            item["NoCrmWarning"] = false;
            items.InsertOne(item);
            W.CloneTransaction(sale["SourceDocumentId"].AsString, d =>
            {
                d["SourceDocumentId"] = document;
                d["TransactionNumber"] = $"GIS-{i + startNumber:0000}";
            });
            F.Shipments.CustomerRefJson[document] = "\"" + id.ToString("D") + "\"";
        }

        return accounts;
    }
}
