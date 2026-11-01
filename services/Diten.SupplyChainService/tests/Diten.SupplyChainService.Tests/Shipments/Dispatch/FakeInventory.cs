using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Diten.SupplyChainService.Tests.Shipments.Dispatch;

/// <summary>
/// A fake INVENTORY 1.5.0 postMovement behind the typed HttpClient. By default it behaves like the parts of INVENTORY that
/// MOD-0183 §23 relies on (measured in InternalMovementPostingService / MovementRequestParser / ReversalRules at this HEAD):
/// a processed key answers 200 idempotentReplay with the same id; the same key with other bytes is 400
/// IDEMPOTENCY_KEY_REUSED; a REVERSAL carrying lotId, serialIds, a from*/to* field, fefoBypassReason or sourceLineId is 400;
/// a REVERSAL of an already reversed issue is 422 ALREADY_REVERSED; no X-Legal-Entity-Id with a claim-less token is 403
/// CROSS_LE_FORBIDDEN. A refused request stores nothing. <see cref="Script"/> overrides an answer per call.
/// </summary>
public sealed class FakeInventory : HttpMessageHandler
{
    public sealed record Call(int Index, string Key, string BodyJson, JsonObject Body, string? LegalEntity, string? Correlation, string? Authorization, string? Tenant)
    {
        public string MovementType => Body["movementType"]!.GetValue<string>();
        public string? Line => Body["sourceLineId"]?.GetValue<string>();
    }

    private readonly object _sync = new();
    private readonly List<Call> _calls = [];
    private readonly Dictionary<string, (string Body, string Tx)> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> _issues = new(StringComparer.Ordinal);       // issue tx → quantity
    private readonly HashSet<string> _reversed = new(StringComparer.Ordinal);                // issue tx reversed
    // MOD-0186 §34: CUSTOMER_RETURN tx → (quantity, toStockStatus); a REVERSAL of one takes the quantity out again.
    private readonly Dictionary<string, (decimal Quantity, string Status)> _returns = new(StringComparer.Ordinal);
    private decimal _net;
    private decimal _quarantine;

    /// <summary>Return a response to override the default for this call, or null to let INVENTORY answer.</summary>
    public Func<Call, Task<HttpResponseMessage?>>? Script { get; set; }

    public IReadOnlyList<Call> Calls { get { lock (_sync) return _calls.ToArray(); } }
    /// <summary>Net AVAILABLE change of every movement INVENTORY stored (issue −q, reversal +q).</summary>
    public decimal Net { get { lock (_sync) return _net; } }
    public int Stored { get { lock (_sync) return _byKey.Count; } }
    public IReadOnlyCollection<string> IssueIds { get { lock (_sync) return _issues.Keys.ToArray(); } }
    /// <summary>Net QUARANTINE change (CUSTOMER_RETURN into QUARANTINE +q, its REVERSAL −q).</summary>
    public decimal Quarantine { get { lock (_sync) return _quarantine; } }

    /// <summary>Someone else reverses our issue in INVENTORY under another key (§23.4 external reversal).</summary>
    public void ReverseExternally(string issueTx)
    {
        lock (_sync) { if (_reversed.Add(issueTx)) _net += _issues[issueTx]; }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var text = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Call call;
        lock (_sync)
        {
            call = new Call(_calls.Count, Header(request, "Idempotency-Key") ?? "", text, JsonNode.Parse(text)!.AsObject(), Header(request, "X-Legal-Entity-Id"),
                Header(request, "X-Correlation-Id"), request.Headers.Authorization?.ToString(), Header(request, "X-Tenant-Id"));
            _calls.Add(call);
        }
        if (Script is not null && await Script(call) is { } scripted) return scripted;
        return Answer(call);
    }

    /// <summary>The default INVENTORY behaviour for a call (scripts may call it, e.g. to post and then fail the answer).</summary>
    public HttpResponseMessage Answer(Call call)
    {
        if (call.LegalEntity is null) return Error(403, "CROSS_LE_FORBIDDEN");
        var body = call.Body;
        if (body["idempotencyKey"]?.GetValue<string>() != call.Key) return Error(400, "INVALID_REQUEST");
        foreach (var required in new[] { "movementType", "itemId", "skuId", "skuLevel", "quantity", "uomId", "sourceModule", "sourceType", "transactionDate", "postingDate" })
            if (body[required] is null) return Error(400, "INVALID_REQUEST");
        var reversal = call.MovementType == "REVERSAL";
        if (reversal && body.Select(p => p.Key).Any(k => k is "lotId" or "serialIds" or "fefoBypassReason" or "sourceLineId" || k.StartsWith("from", StringComparison.Ordinal) || k.StartsWith("to", StringComparison.Ordinal)))
            return Error(400, "INVALID_REQUEST");
        lock (_sync)
        {
            if (_byKey.TryGetValue(call.Key, out var stored))
                return stored.Body == call.BodyJson ? Json(200, new JsonObject { ["transactionId"] = stored.Tx, ["idempotentReplay"] = true, ["contractVersion"] = "v1" })
                    : Error(400, "IDEMPOTENCY_KEY_REUSED");
            var quantity = decimal.Parse(body["quantity"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
            var tx = Guid.NewGuid().ToString();
            if (reversal)
            {
                var original = body["sourceDocumentId"]!.GetValue<string>();
                if (_returns.TryGetValue(original, out var received))
                {
                    if (!_reversed.Add(original)) return Error(422, "ALREADY_REVERSED");
                    if (received.Status == "QUARANTINE") _quarantine -= received.Quantity; else _net -= received.Quantity;
                }
                else
                {
                    if (!_issues.ContainsKey(original)) return Error(422, "UNKNOWN_ITEM");
                    if (!_reversed.Add(original)) return Error(422, "ALREADY_REVERSED");
                    _net += _issues[original];
                }
            }
            else if (call.MovementType == "CUSTOMER_RETURN")
            {
                var status = body["toStockStatus"]?.GetValue<string>() ?? "AVAILABLE";
                _returns[tx] = (quantity, status);
                if (status == "QUARANTINE") _quarantine += quantity; else _net += quantity;
            }
            else
            {
                _issues[tx] = quantity; _net -= quantity;
            }
            _byKey[call.Key] = (call.BodyJson, tx);
            var snapshot = new JsonObject { ["lotId"] = body["lotId"]?.GetValue<string>() };
            return Json(201, new JsonObject { ["transactionId"] = tx, ["idempotentReplay"] = false, ["balanceSnapshot"] = snapshot, ["contractVersion"] = "v1" });
        }
    }

    public static HttpResponseMessage Error(int status, string code, JsonObject? details = null)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = code, ["correlationId"] = Guid.NewGuid().ToString() };
        if (details is not null) error["details"] = details;
        return Json(status, new JsonObject { ["error"] = error, ["contractVersion"] = "v1" });
    }
    public static HttpResponseMessage Json(int status, JsonObject body) =>
        new((HttpStatusCode)status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
}
