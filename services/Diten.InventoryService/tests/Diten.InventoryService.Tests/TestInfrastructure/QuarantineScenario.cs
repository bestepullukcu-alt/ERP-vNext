using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Persistence.Quarantine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// The REAL host of <see cref="TraceApiFactory"/> with the slice Q1 TEST approval port switched on
/// (<c>Quarantine:TestApprovalPort:Enabled</c>, allowed because the suite database is a DB-010 test database). Production
/// keeps the fail-closed port; <see cref="InventoryApiFactory"/> (port off) proves that.
/// </summary>
public sealed class QuarantineApiFactory : TraceApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Quarantine:TestApprovalPort:Enabled"] = "true" }));
    }

    /// <summary>Runs the intake sweep once for one tenant (the hosted sweep is off in tests).</summary>
    public Task<int> SweepAsync(Guid tenant, DateTimeOffset? now = null)
        => Services.GetRequiredService<QuarantineIntakeSweeper>().SweepAsync(now ?? DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None, tenant);
}

/// <summary>
/// MOD-0175 world: the MOD-0174 <see cref="LotLedgerScenario"/> (fresh tenant + company, real location tree, a lot-controlled
/// item, a registered lot) plus a DECIDER token (ledger + trace + every <c>quarantine.*</c> key) and a second user, the
/// APPROVER. Every call goes through the real API and every response is checked against its frozen contract.
/// </summary>
public sealed class QuarantineScenario
{
    public const string Reason = "QA karari - sapma DEV-2026-021";

    public static readonly string[] QuarantinePermissions =
    [
        "quarantine.holds.read", "quarantine.holds.create", "quarantine.holds.review", "quarantine.decisions.create", "quarantine.decisions.withdraw",
        "quarantine.decisions.apply", "quarantine.blocked.dispose", "quarantine.blocked.unblock", "quarantine.blocked.block", "quarantine.settings.manage"
    ];

    public QuarantineScenario(QuarantineApiFactory factory, bool lotControlled = true)
    {
        Factory = factory;
        Lots = new LotLedgerScenario(factory, lotControlled);
        Token = TestTokens.Mint(Tenant, Ledger.LegalEntity, Ledger.User, TestTokens.AllPermissions.Concat(TestTokens.TracePermissions).Concat(QuarantinePermissions));
        ApproverToken = TestTokens.Mint(Tenant, Ledger.LegalEntity, Approver, TestTokens.AllPermissions.Concat(QuarantinePermissions));
        Client = factory.CreateContractClient();
    }

    public QuarantineApiFactory Factory { get; }
    public LotLedgerScenario Lots { get; }
    public InventoryScenario Ledger => Lots.Ledger;
    public Guid Tenant => Ledger.Tenant;
    public Guid Approver { get; } = Guid.NewGuid();
    public string Token { get; }
    public string ApproverToken { get; }
    public HttpClient Client { get; }

    public static decimal D(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDecimal() : decimal.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    public static string Key(string prefix = "q") => prefix + "-" + Guid.NewGuid().ToString("N");

    // ── stock ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<string> ReleasedLotAsync()
    {
        var lot = await Lots.CreateLotAsync(expiryDate: "2028-01-31T00:00:00Z");
        await Lots.ReleaseAsync(lot);
        return lot;
    }

    /// <summary>A goods receipt INTO the status (QUARANTINE / QUALITY_INSPECTION / BLOCKED / AVAILABLE) at bin A; returns the transaction id.</summary>
    public Task<string> ReceiveAsync(string quantity, string? lotId, string toStatus, string movementType = "GOODS_RECEIPT_PO", string? location = null)
        => Lots.PostOkAsync(Lots.Receipt(quantity, lotId, location, movementType, toStatus));

    public Task<decimal> OnHandAsync(string status, string? location = null) => Ledger.OnHandAsync(location ?? Ledger.LocationA, status);

    /// <summary>The ledger transactions 0175 posted for a document (ledger read API, sourceModule MOD-0175).</summary>
    public async Task<List<JsonElement>> LedgerMovementsAsync(string sourceDocumentId)
    {
        using var response = await Ledger.GetAsync($"/api/inventory/transactions?itemId={Ledger.Item}&pageSize=200");
        var json = await Ledger.ReadAsync(response);
        Assert.Equal(200, (int)response.StatusCode);
        return json.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("sourceModule").GetString() == "MOD-0175" && t.TryGetProperty("sourceDocumentId", out var d) && d.GetString() == sourceDocumentId)
            .ToList();
    }

    public async Task<int> AllMovementCountAsync()
    {
        using var response = await Ledger.GetAsync($"/api/inventory/transactions?itemId={Ledger.Item}&pageSize=200");
        return (await Ledger.ReadAsync(response)).GetProperty("items").GetArrayLength();
    }

    // ── quarantine API ───────────────────────────────────────────────────────────────────────────────────

    public async Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> SendAsync(
        HttpMethod method, string path, object? body = null, string? key = null, string? ifMatch = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? Token);
        var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return ((int)response.StatusCode, json, response);
    }

    public async Task<JsonElement> ExpectAsync(HttpMethod method, string path, object? body, int expected, string? key = null, string? ifMatch = null, string? token = null)
    {
        var (status, json, _) = await SendAsync(method, path, body, key, ifMatch, token);
        Assert.True(status == expected, $"{method} {path}: expected {expected}, got {status}: {json}");
        return json;
    }

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    /// <summary>The ONE hold whose source movement is <paramref name="transactionId"/> (asserted single), read through the API.</summary>
    public async Task<JsonElement> HoldBySourceAsync(string transactionId)
    {
        var doc = Assert.Single(Holds(), h => h.Contains("SourceMovementId") && h["SourceMovementId"].IsString && h["SourceMovementId"].AsString == transactionId);
        return await GetHoldAsync(doc["_id"].AsString);
    }

    public Task<JsonElement> GetHoldAsync(string holdId) => ExpectAsync(HttpMethod.Get, $"/api/quarantine/holds/{holdId}", null, 200);

    /// <summary>An intake receipt into QUARANTINE and its hold (the observer's).</summary>
    public async Task<(string TransactionId, JsonElement Hold)> HoldAsync(string quantity, string? lotId, string status = "QUARANTINE")
    {
        var tx = await ReceiveAsync(quantity, lotId, status);
        return (tx, await HoldBySourceAsync(tx));
    }

    public Task<JsonElement> DecideAsync(string holdId, object[] lines, string? blockReason = null, bool coa = true, int expected = 201, string? key = null)
        => ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", new Dictionary<string, object?>
        {
            ["kind"] = "HoldDecision", ["targetId"] = holdId, ["lines"] = lines, ["blockReasonCode"] = blockReason, ["reason"] = Reason,
            ["coaChecked"] = coa, ["idempotencyKey"] = key ??= Key("dec")
        }.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value), expected, key);

    public static object Line(int no, string decision, decimal quantity) => new { lineNo = no, decision, quantity };

    public Task<JsonElement> DisposeAsync(string blockedId, object disposition, int expected = 201)
    {
        var key = Key("disp");
        return ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions",
            new { kind = "Disposition", targetId = blockedId, reason = Reason, disposition, idempotencyKey = key }, expected, key);
    }

    public Task<JsonElement> UnblockAsync(string blockedId, int expected = 201)
    {
        var key = Key("unb");
        return ExpectAsync(HttpMethod.Post, "/api/quarantine/decisions", new { kind = "Unblock", targetId = blockedId, reason = Reason, idempotencyKey = key }, expected, key);
    }

    public Task<JsonElement> SubmitAsync(string decisionId, int expected = 202)
    {
        var key = Key("sub");
        return ExpectAsync(HttpMethod.Post, $"/api/quarantine/decisions/{decisionId}/submit", new { idempotencyKey = key }, expected, key);
    }

    /// <summary>The Q1 test approval port: the APPROVER (another user) reports the outcome.</summary>
    public Task<JsonElement> OutcomeAsync(string decisionId, string outcome = "Approved", Guid? approver = null, int expected = 200)
        => ExpectAsync(HttpMethod.Post, $"/api/quarantine/test-approval/decisions/{decisionId}",
            new { outcome, approverUserId = (approver ?? Approver).ToString("D") }, expected, token: ApproverToken);

    /// <summary>Draft → submit → approve (by the approver); returns the applied decision.</summary>
    public async Task<JsonElement> DecideAndApproveAsync(Task<JsonElement> draft)
    {
        var decision = await draft;
        var id = decision.GetProperty("decisionId").GetString()!;
        await SubmitAsync(id);
        return await OutcomeAsync(id);
    }

    public async Task<JsonElement> BlockedOfDecisionAsync(string decisionId, int lineNo)
    {
        var doc = Assert.Single(Blocked(), b => b.Contains("OriginDecisionId") && b["OriginDecisionId"].AsString == decisionId && b["OriginLineNo"].ToInt32() == lineNo);
        return await ExpectAsync(HttpMethod.Get, $"/api/quarantine/blocked/{doc["_id"].AsString}", null, 200);
    }

    public async Task<string> SettingsETagAsync()
    {
        var (status, _, raw) = await SendAsync(HttpMethod.Get, "/api/quarantine/settings");
        Assert.Equal(200, status);
        return raw.Headers.ETag!.ToString();
    }

    public async Task<JsonElement> SaveSettingsAsync(object body, int expected = 200)
        => await ExpectAsync(HttpMethod.Put, "/api/quarantine/settings", body, expected, ifMatch: await SettingsETagAsync());

    // ── persisted state (read-only) ──────────────────────────────────────────────────────────────────────

    public List<BsonDocument> Holds() => Ledger.Persisted("quarantine_holds");
    public List<BsonDocument> Blocked() => Ledger.Persisted("quarantine_blocked_stock");
    public List<BsonDocument> Decisions() => Ledger.Persisted("quarantine_decisions");
    public List<BsonDocument> Events() => Ledger.Persisted("quarantine_decision_events");
}
