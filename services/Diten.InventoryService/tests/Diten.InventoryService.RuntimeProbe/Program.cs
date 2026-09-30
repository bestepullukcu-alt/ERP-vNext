using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

/*
 * WP-0173-S1 §R — runtime E4 probe.
 *
 *   dotnet run -- flows          <baseUrl> <evidenceDir> <stateFile>
 *   dotnet run -- after-restart  <baseUrl> <evidenceDir> <stateFile>
 *
 * Environment: PROBE_JWT_SECRET (the secret the service was started with), PROBE_MONGO, PROBE_DB.
 * The service runs in the Mock environment against Prism mocks of the frozen PRODUCT-MASTER / LOCATION contracts,
 * so the ids below are the contract examples Prism answers with. Each flow runs in its OWN fresh tenant so its
 * balance and availability are not mixed with the other flows (and with earlier probe runs).
 *
 * Every step records: the request (bearer masked), the response status + X-Correlation-Id + body, and the
 * persisted Mongo documents of that tenant. Every expectation is CHECKED; the exit code is non-zero if any fails.
 */

const string Item = "b1f2c3d4-0000-0000-0000-000000000001";
const string Sku = "c3d4e5f6-0000-0000-0000-000000000002";
const string Warehouse = "wh-01";
const string Location = "loc-A-12-3";

var phase = args[0];
var baseUrl = new Uri(args[1]);
var evidenceDir = args[2];
var statePath = args[3];
var secret = Environment.GetEnvironmentVariable("PROBE_JWT_SECRET") ?? throw new InvalidOperationException("PROBE_JWT_SECRET missing");
var mongo = new MongoClient(Environment.GetEnvironmentVariable("PROBE_MONGO") ?? "mongodb://localhost:27017")
    .GetDatabase(Environment.GetEnvironmentVariable("PROBE_DB") ?? "diten_inventory_mock");
Directory.CreateDirectory(evidenceDir);

using var http = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(30) };
var checks = new List<JsonObject>();
var json = new JsonSerializerOptions { WriteIndented = true };

if (phase == "flows")
{
    var run = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    var tenants = new JsonObject();

    // ── AC2 opening golden flow ──────────────────────────────────────────────────────────────────────────
    {
        var t = NewTenant("ac2");
        tenants["ac2"] = t.ToJson();
        var steps = new JsonArray();
        var body = Movement("OPENING_BALANCE", "100", $"probe-{run}-ac2-open", to: true, lot: "lot-777");
        var r = await Post(t, body, steps, "opening OPENING_BALANCE +100");
        Check("AC2 opening → 201", r.Status == 201);
        Check("AC2 snapshot onHand 100.000", r.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "100.000");
        Check("AC2 snapshot available 100.000", r.Body?["balanceSnapshot"]?["available"]?.GetValue<string>() == "100.000");
        var persisted = Persisted(t);
        Check("AC2 exactly 1 transaction persisted", persisted["inventory_transactions"]!.AsArray().Count == 1);
        Check("AC2 exactly 1 balance row", persisted["inventory_balances"]!.AsArray().Count == 1);
        var bal = await Get(t, $"/api/inventory/balance?itemId={Item}", steps, "GET /balance");
        var av = await Get(t, $"/api/inventory/availability?itemId={Item}", steps, "GET /availability");
        Check("AC2 GET /balance onHand 100.000", bal.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "100.000");
        Check("AC2 GET /availability available 100.000", av.Body?["available"]?.GetValue<string>() == "100.000");
        Write("r-ac2-opening.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = persisted });
    }

    // ── AC3 issue flow · AC4 idempotency · AC5 negative stock (one tenant, continuing ledger) ─────────────
    {
        var t = NewTenant("ac3");
        tenants["ac3"] = t.ToJson();
        var steps = new JsonArray();
        var open = await Post(t, Movement("OPENING_BALANCE", "100", $"probe-{run}-ac3-open", to: true, lot: "lot-777"), steps, "opening +100");
        var firstId = open.Body?["transactionId"]?.GetValue<string>();
        var before = RawTransaction(firstId!);

        var issueBody = Movement("GOODS_ISSUE_SALES", "20", $"probe-{run}-ac3-issue", from: true, lot: "lot-777");
        var issue = await Post(t, issueBody, steps, "AC3 GOODS_ISSUE_SALES 20");
        var after = RawTransaction(firstId!);
        Check("AC3 issue → 201", issue.Status == 201);
        Check("AC3 snapshot onHand 80.000", issue.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "80.000");
        Check("AC3 first transaction byte-identical (SHA-256 of BSON)", Sha(before) == Sha(after));
        var ac3 = new JsonObject
        {
            ["firstTransactionBefore"] = new JsonObject { ["sha256"] = Sha(before), ["document"] = Doc(before) },
            ["firstTransactionAfter"] = new JsonObject { ["sha256"] = Sha(after), ["document"] = Doc(after) }
        };

        var counts0 = Counts(t);
        var replay = await Post(t, issueBody, steps, "AC4 same key, same payload (replay)");
        var counts1 = Counts(t);
        Check("AC4 replay → 200", replay.Status == 200);
        Check("AC4 replay idempotentReplay:true", replay.Body?["idempotentReplay"]?.GetValue<bool>() == true);
        Check("AC4 replay returns the original transactionId", replay.Body?["transactionId"]?.GetValue<string>() == issue.Body?["transactionId"]?.GetValue<string>());
        Check("AC4 replay wrote nothing (tx/balance version/audit/outbox unchanged)", counts0.ToJsonString() == counts1.ToJsonString());

        var reused = JsonNode.Parse(issueBody.ToJsonString())!.AsObject();
        reused["quantity"] = "21";
        var reuse = await Post(t, reused, steps, "AC4 same key, different payload");
        var counts2 = Counts(t);
        Check("AC4 key reuse → 400 IDEMPOTENCY_KEY_REUSED", reuse.Status == 400 && reuse.Body?["error"]?["code"]?.GetValue<string>() == "IDEMPOTENCY_KEY_REUSED");
        Check("AC4 key reuse wrote nothing", counts1.ToJsonString() == counts2.ToJsonString());

        var negative = await Post(t, Movement("GOODS_ISSUE_SALES", "1000", $"probe-{run}-ac5-over", from: true, lot: "lot-777"), steps, "AC5 issue 1000 > balance 80");
        var counts3 = Counts(t);
        Check("AC5 → 422 INSUFFICIENT_STOCK", negative.Status == 422 && negative.Body?["error"]?["code"]?.GetValue<string>() == "INSUFFICIENT_STOCK");
        Check("AC5 details.available 80.000 / requested 1000.000",
            negative.Body?["error"]?["details"]?["available"]?.GetValue<string>() == "80.000"
            && negative.Body?["error"]?["details"]?["requested"]?.GetValue<string>() == "1000.000");
        Check("AC5 wrote nothing", counts2.ToJsonString() == counts3.ToJsonString());

        Write("r-ac3-ac4-ac5-issue-idempotency-negative.json", new JsonObject
        {
            ["tenant"] = t.ToJson(),
            ["steps"] = steps,
            ["ac3ByteIdentity"] = ac3,
            ["countsAfterIssue"] = counts0,
            ["countsAfterReplay"] = counts1,
            ["countsAfterKeyReuse"] = counts2,
            ["countsAfterNegative"] = counts3,
            ["persisted"] = Persisted(t)
        });
    }

    // ── AC7 StockStatus (+ AC9 base UoM on the Prism BOX→EA 10/1 conversion) ─────────────────────────────
    {
        var t = NewTenant("ac7");
        tenants["ac7"] = t.ToJson();
        var steps = new JsonArray();
        var qi = await Post(t, Movement("GOODS_RECEIPT_PO", "100", $"probe-{run}-ac7-qi", to: true, lot: "lot-777", toStatus: "QUALITY_INSPECTION"), steps, "AC7 receipt 100 into QUALITY_INSPECTION");
        Check("AC7 → 201", qi.Status == 201);
        Check("AC7 snapshot OnHand 100 / Available 0 / QI 100",
            qi.Body?["balanceSnapshot"]?["onHand"]?.GetValue<string>() == "100.000"
            && qi.Body?["balanceSnapshot"]?["available"]?.GetValue<string>() == "0.000"
            && qi.Body?["balanceSnapshot"]?["qualityInspection"]?.GetValue<string>() == "100.000");
        var box = await Post(t, Movement("GOODS_RECEIPT_PO", "5", $"probe-{run}-ac9-box", to: true, lot: "lot-777", uom: "BOX"), steps, "AC9 receipt 5 BOX (BOX→EA 10/1)");
        Check("AC9 5 BOX → baseQuantity 50.000 EA", box.Body?["balanceSnapshot"]?["baseQuantity"]?.GetValue<string>() == "50.000"
                                                    && box.Body?["balanceSnapshot"]?["baseUomId"]?.GetValue<string>() == "EA");
        var av = await Get(t, $"/api/inventory/availability?itemId={Item}", steps, "AC7 GET /availability");
        Check("AC7 byStatus QUALITY_INSPECTION 100.000", av.Body?["byStatus"]?["QUALITY_INSPECTION"]?.GetValue<string>() == "100.000");
        Check("AC7 byStatus AVAILABLE 50.000", av.Body?["byStatus"]?["AVAILABLE"]?.GetValue<string>() == "50.000");
        Check("AC7 byStatus reports QUARANTINE/BLOCKED/IN_TRANSIT", av.Body?["byStatus"]?["QUARANTINE"] is not null
                                                                   && av.Body?["byStatus"]?["BLOCKED"] is not null
                                                                   && av.Body?["byStatus"]?["IN_TRANSIT"] is not null);
        Check("AC7 available excludes QI (50.000)", av.Body?["available"]?.GetValue<string>() == "50.000");
        Write("r-ac7-ac9-stock-status-base-uom.json", new JsonObject { ["tenant"] = t.ToJson(), ["steps"] = steps, ["persisted"] = Persisted(t) });
    }

    // ── AC10 isolation (tenant B vs the AC2 tenant) ───────────────────────────────────────────────────────
    {
        var a = Tenant.FromJson(tenants["ac2"]!.AsObject());
        var b = NewTenant("ac10-b");
        tenants["ac10b"] = b.ToJson();
        var steps = new JsonArray();
        var av = await Get(b, $"/api/inventory/availability?itemId={Item}", steps, "tenant B GET /availability for tenant A's item");
        var bal = await Get(b, $"/api/inventory/balance?itemId={Item}", steps, "tenant B GET /balance");
        var txs = await Get(b, "/api/inventory/transactions", steps, "tenant B GET /transactions");
        Check("AC10 tenant B availability → 404", av.Status == 404);
        Check("AC10 tenant B balance rows empty", bal.Status == 200 && bal.Body?["rows"]?.AsArray().Count == 0);
        Check("AC10 tenant B transactions empty", txs.Status == 200 && txs.Body?["items"]?.AsArray().Count == 0);
        var crossLe = await Get(a, $"/api/inventory/balance?itemId={Item}", steps, "tenant A with a foreign X-Legal-Entity-Id",
            ("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        Check("AC10 foreign X-Legal-Entity-Id → 403 CROSS_LE_FORBIDDEN", crossLe.Status == 403 && crossLe.Body?["error"]?["code"]?.GetValue<string>() == "CROSS_LE_FORBIDDEN");
        var postCrossLe = await Post(a, Movement("OPENING_BALANCE", "1", $"probe-{run}-ac10-post", to: true, lot: "lot-777"), steps, "tenant A POST with a foreign X-Legal-Entity-Id",
            ("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        Check("AC10 POST foreign X-Legal-Entity-Id → 403 CROSS_LE_FORBIDDEN", postCrossLe.Status == 403);
        var query = await Get(a, $"/api/inventory/balance?tenantId={b.TenantId}", steps, "tenantId in the query");
        Check("AC10 tenantId in query → 400", query.Status == 400);
        Check("AC10 tenant A still has exactly its 1 transaction", Counts(a)["inventory_transactions"]!.GetValue<int>() == 1);
        Write("r-ac10-isolation.json", new JsonObject { ["tenantA"] = a.ToJson(), ["tenantB"] = b.ToJson(), ["steps"] = steps, ["tenantBPersisted"] = Persisted(b) });
    }

    File.WriteAllText(statePath, tenants.ToJsonString(json));
}
else if (phase == "after-restart")
{
    var tenants = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
    var steps = new JsonArray();
    var a2 = Tenant.FromJson(tenants["ac2"]!.AsObject());
    var a3 = Tenant.FromJson(tenants["ac3"]!.AsObject());
    var bal = await Get(a2, $"/api/inventory/balance?itemId={Item}", steps, "AC2 after restart GET /balance");
    var av = await Get(a2, $"/api/inventory/availability?itemId={Item}", steps, "AC2 after restart GET /availability");
    var av3 = await Get(a3, $"/api/inventory/availability?itemId={Item}", steps, "AC3 after restart GET /availability");
    var tx3 = await Get(a3, "/api/inventory/transactions", steps, "AC3 after restart GET /transactions");
    Check("AC2 after restart /balance onHand 100.000", bal.Body?["rows"]?[0]?["onHand"]?.GetValue<string>() == "100.000");
    Check("AC2 after restart /availability onHand 100.000 / available 100.000",
        av.Body?["onHand"]?.GetValue<string>() == "100.000" && av.Body?["available"]?.GetValue<string>() == "100.000");
    Check("AC3 after restart availability 80.000", av3.Body?["available"]?.GetValue<string>() == "80.000");
    Check("AC3 after restart 2 immutable transactions", tx3.Body?["total"]?.GetValue<int>() == 2);
    Write("r-restart-read.json", new JsonObject { ["steps"] = steps, ["ac2Persisted"] = Persisted(a2) });
}
else
{
    throw new ArgumentException($"unknown phase '{phase}'");
}

var failed = checks.Count(c => c["passed"]!.GetValue<bool>() == false);
Write($"r-checks-{phase}.json", new JsonObject { ["phase"] = phase, ["passed"] = checks.Count - failed, ["failed"] = failed, ["checks"] = new JsonArray(checks.ToArray()) });
Console.WriteLine($"{phase}: {checks.Count - failed}/{checks.Count} checks passed");
foreach (var c in checks.Where(c => !c["passed"]!.GetValue<bool>())) Console.WriteLine("  FAILED: " + c["check"]);
return failed == 0 ? 0 : 1;

// ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────

Tenant NewTenant(string label) => new(label, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

JsonObject Movement(string type, string quantity, string key, bool to = false, bool from = false, string? lot = null, string uom = "EA", string? toStatus = null)
{
    var body = new JsonObject
    {
        ["idempotencyKey"] = key,
        ["movementType"] = type,
        ["itemId"] = Item,
        ["skuId"] = Sku,
        ["skuLevel"] = "Lsku",
        ["quantity"] = quantity,
        ["uomId"] = uom,
        ["sourceModule"] = "MOD-PROBE",
        ["sourceType"] = "RUNTIME_PROBE",
        ["sourceSystem"] = "WP-0173-S1-PROBE",
        ["transactionDate"] = "2026-09-30T09:00:00Z",
        ["postingDate"] = "2026-09-30T10:00:00Z"
    };
    if (lot is not null) body["lotId"] = lot;
    if (to) { body["toWarehouseId"] = Warehouse; body["toLocationId"] = Location; }
    if (from) { body["fromWarehouseId"] = Warehouse; body["fromLocationId"] = Location; }
    if (toStatus is not null) body["toStockStatus"] = toStatus;
    return body;
}

async Task<Exchange> Post(Tenant t, JsonObject body, JsonArray steps, string label, params (string Name, string Value)[] headers)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/movements") { Content = JsonContent.Create(body) };
    request.Headers.Add("Idempotency-Key", body["idempotencyKey"]!.GetValue<string>());
    return await Send(t, request, steps, label, body, headers);
}

async Task<Exchange> Get(Tenant t, string url, JsonArray steps, string label, params (string Name, string Value)[] headers)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    return await Send(t, request, steps, label, null, headers);
}

async Task<Exchange> Send(Tenant t, HttpRequestMessage request, JsonArray steps, string label, JsonObject? body, (string Name, string Value)[] headers)
{
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(t));
    foreach (var (name, value) in headers) request.Headers.Add(name, value);
    using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    var parsed = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    var correlation = response.Headers.TryGetValues("X-Correlation-Id", out var c) ? c.Single() : null;
    steps.Add(new JsonObject
    {
        ["step"] = label,
        ["request"] = new JsonObject
        {
            ["method"] = request.Method.Method,
            ["url"] = new Uri(baseUrl, request.RequestUri!).ToString(),
            ["headers"] = new JsonObject(request.Headers
                .Select(h => KeyValuePair.Create<string, JsonNode?>(h.Key, h.Key == "Authorization" ? "Bearer ***(masked)" : string.Join(",", h.Value)))),
            ["body"] = body?.DeepClone()
        },
        ["response"] = new JsonObject
        {
            ["status"] = (int)response.StatusCode,
            ["X-Correlation-Id"] = correlation,
            ["body"] = parsed?.DeepClone()
        }
    });
    return new Exchange((int)response.StatusCode, parsed, correlation);
}

string Token(Tenant t)
{
    var claims = new List<Claim>
    {
        new("sub", t.UserId.ToString()),
        new("tenant_id", t.TenantId.ToString()),
        new("legal_entity_id", t.LegalEntityId.ToString()),
        new("permission", "supplychain.inventory.read"),
        new("permission", "supplychain.inventory.movements.post")
    };
    var token = new JwtSecurityToken("diten-auth-service", "diten-erp", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(15),
        new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
}

BsonDocument RawTransaction(string id)
    => mongo.GetCollection<BsonDocument>("inventory_transactions").Find(new BsonDocument("_id", id)).Single();

string Sha(BsonDocument doc) => Convert.ToHexString(SHA256.HashData(doc.ToBson()));

JsonNode Doc(BsonDocument doc) => JsonNode.Parse(doc.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson }))!;

JsonObject Persisted(Tenant t)
{
    var result = new JsonObject();
    foreach (var name in new[] { "inventory_transactions", "inventory_balances", "inventory_audit", "inventory_outbox" })
    {
        var docs = mongo.GetCollection<BsonDocument>(name).Find(new BsonDocument("TenantId", t.TenantId.ToString())).ToList();
        result[name] = new JsonArray(docs.Select(d => (JsonNode?)Doc(d)).ToArray());
    }

    return result;
}

JsonObject Counts(Tenant t)
{
    var filter = new BsonDocument("TenantId", t.TenantId.ToString());
    var balances = mongo.GetCollection<BsonDocument>("inventory_balances").Find(filter).ToList();
    return new JsonObject
    {
        ["inventory_transactions"] = (int)mongo.GetCollection<BsonDocument>("inventory_transactions").CountDocuments(filter),
        ["inventory_audit"] = (int)mongo.GetCollection<BsonDocument>("inventory_audit").CountDocuments(filter),
        ["inventory_outbox"] = (int)mongo.GetCollection<BsonDocument>("inventory_outbox").CountDocuments(filter),
        ["balanceVersions"] = string.Join(",", balances.Select(b => b["Version"].ToString()).OrderBy(x => x)),
        ["balanceOnHand"] = string.Join(",", balances.Select(b => b["OnHand"].ToString()).OrderBy(x => x))
    };
}

void Check(string name, bool passed) => checks.Add(new JsonObject { ["check"] = name, ["passed"] = passed });

void Write(string file, JsonObject content) => File.WriteAllText(Path.Combine(evidenceDir, file), content.ToJsonString(json));

internal sealed record Exchange(int Status, JsonNode? Body, string? CorrelationId);

internal sealed record Tenant(string Label, Guid TenantId, Guid LegalEntityId, Guid UserId)
{
    public JsonObject ToJson() => new()
    {
        ["label"] = Label,
        ["tenantId"] = TenantId.ToString(),
        ["legalEntityId"] = LegalEntityId.ToString(),
        ["userId"] = UserId.ToString()
    };

    public static Tenant FromJson(JsonObject o) => new(
        o["label"]!.GetValue<string>(),
        Guid.Parse(o["tenantId"]!.GetValue<string>()),
        Guid.Parse(o["legalEntityId"]!.GetValue<string>()),
        Guid.Parse(o["userId"]!.GetValue<string>()));
}
