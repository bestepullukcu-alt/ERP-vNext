using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// WP-0174-P4 world: a <see cref="LotLedgerScenario"/> (lot-controlled item + Lsku, every ledger + trace key) whose ledger
/// already holds TYPED lot texts (posted with <c>Trace:EnforceRegisteredLots</c> off, as in production before P4), plus the
/// typed-lot migration API and the persisted-state oracles the pack §16 P4 items are measured with: the
/// <c>inventory_transactions</c> hash (append-only), balance totals per company / SKU / stock status (re-key keeps them)
/// and the restart rebuild of the SKU positions.
/// </summary>
public sealed class MigrationScenario
{
    public const string Reason = "Elle lot aktarimi - W-UI-02 kapanisi";

    public MigrationScenario(TraceApiFactory factory, bool serialControlled = false)
    {
        Lots = new LotLedgerScenario(factory, lotControlled: true, serialControlled: serialControlled);
    }

    public LotLedgerScenario Lots { get; }

    public InventoryScenario Ledger => Lots.Ledger;

    public TraceApiFactory Factory => Lots.Factory;

    // ── ledger with typed lots ───────────────────────────────────────────────────────────────────────────

    public async Task<string> ReceiveTypedAsync(string typedLot, string quantity, string? location = null, string? toStatus = null, string[]? serials = null)
        => await Lots.PostOkAsync(Lots.Receipt(quantity, typedLot, location, "OPENING_BALANCE", toStatus, serials));

    public async Task<string> IssueTypedAsync(string typedLot, string quantity, string? location = null, string[]? serials = null)
        => await Lots.PostOkAsync(Lots.Issue(quantity, typedLot, location, serials: serials));

    /// <summary>A second item of the same tenant that is NOT lot-controlled (CT K4-4), with its own Lsku.</summary>
    public (Guid Item, Guid Sku) AddPlainItem()
    {
        var item = Guid.NewGuid();
        var sku = Guid.NewGuid();
        Factory.Products.AddItem(item, lotControlled: false);
        Factory.Products.AddSku(sku, SkuLevel.Lsku, item);
        Factory.Skus.Add(sku, SkuLevel.Lsku, item, "08699512340999");
        return (item, sku);
    }

    public static Dictionary<string, object?> ForItem(Dictionary<string, object?> body, (Guid Item, Guid Sku) plain)
    {
        body["itemId"] = plain.Item.ToString();
        body["skuId"] = plain.Sku.ToString();
        return body;
    }

    /// <summary>A registered Draft lot with this exact number on the scenario's Lsku.</summary>
    public async Task<string> CreateLotAsync(string lotNumber)
    {
        var key = "lot-" + Guid.NewGuid().ToString("N");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key,
            ["itemId"] = Ledger.Item.ToString("D"),
            ["lotNumber"] = lotNumber,
            ["skuId"] = Ledger.Sku.ToString("D"),
            ["skuLevel"] = "Lsku"
        };
        var json = await Expect(Send(HttpMethod.Post, "/api/trace/lots", body, r => r.Headers.TryAddWithoutValidation("Idempotency-Key", key)), 201);
        return json.GetProperty("lotId").GetString()!;
    }

    // ── migration API ────────────────────────────────────────────────────────────────────────────────────

    public Task<JsonElement> ListAsync(int expected = 200, string? token = null)
        => Expect(Send(HttpMethod.Get, "/api/trace/migration/manual-lots?pageSize=200", null, token: token), expected);

    public async Task<List<JsonElement>> GroupsAsync()
        => (await ListAsync()).GetProperty("items").EnumerateArray().ToList();

    public static JsonElement Group(IEnumerable<JsonElement> groups, string typedText)
        => Assert.Single(groups, g => g.GetProperty("typedText").GetString() == typedText);

    public Task<JsonElement> StatusAsync(string? token = null) => Expect(Send(HttpMethod.Get, "/api/trace/migration/status", null, token: token), 200);

    public Task<JsonElement> LinksAsync(string query) => Expect(Send(HttpMethod.Get, "/api/trace/migration/links?" + query, null), 200);

    public async Task<JsonElement> CommitAsync(IEnumerable<object> decisions, int expectedGroupCount, int expected, string? reason = Reason, string? key = null)
    {
        key ??= "mig-" + Guid.NewGuid().ToString("N");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key,
            ["reason"] = reason,
            ["expectedGroupCount"] = expectedGroupCount,
            ["decisions"] = decisions.ToList()
        };
        return await Expect(Send(HttpMethod.Post, "/api/trace/migration/manual-lots/commit", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", key)), expected);
    }

    public static object Link(string groupId, string lotId) => new { groupId, kind = "LinkExisting", lotId };

    public static object Typo(string groupId, string targetGroupId) => new { groupId, kind = "Typo", targetGroupId };

    public static object NewLot(string groupId, string lotNumber, string? expiryDate = null)
        => new { groupId, kind = "NewLot", newLot = new Dictionary<string, object?> { ["lotNumber"] = lotNumber, ["expiryDate"] = expiryDate } };

    /// <summary>Every group decided by its own suggestion (ExactLot / SimilarLot → link, SimilarGroup → typo, None → new lot).</summary>
    public static List<object> ApplySuggestions(IEnumerable<JsonElement> groups, Func<string, string>? newNumber = null)
        => groups.Select(g =>
        {
            var id = g.GetProperty("groupId").GetString()!;
            var s = g.GetProperty("suggestion");
            return s.GetProperty("kind").GetString() switch
            {
                "ExactLot" or "SimilarLot" => Link(id, s.GetProperty("lotId").GetString()!),
                "SimilarGroup" => Typo(id, s.GetProperty("groupId").GetString()!),
                _ => NewLot(id, newNumber?.Invoke(g.GetProperty("typedText").GetString()!) ?? g.GetProperty("typedText").GetString()!.Trim().ToUpperInvariant())
            };
        }).ToList<object>();

    public static string ErrorCode(JsonElement json) => json.GetProperty("error").GetProperty("code").GetString()!;

    // ── oracles (read-only) ──────────────────────────────────────────────────────────────────────────────

    /// <summary>SHA-256 of every <c>inventory_transactions</c> document of the tenant (canonical JSON, by id).</summary>
    public string TransactionsHash() => HashOf(Lots.Persisted("inventory_transactions"));

    public static string HashOf(IEnumerable<BsonDocument> documents)
    {
        var text = string.Join("\n", documents.OrderBy(d => d["_id"].ToString(), StringComparer.Ordinal)
            .Select(d => d.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson })));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Σ OnHand of the OPEN balance rows per company / item / SKU / stock status (the re-key keeps every total).</summary>
    public Dictionary<string, decimal> BalanceTotals()
        => Lots.Persisted("inventory_balances").Where(d => !d["IsDeleted"].AsBoolean)
            .GroupBy(d => $"{d["LegalEntityId"]}|{d["ItemId"]}|{d["SkuId"]}|{d["SkuLevel"]}|{d["StockStatus"]}")
            .ToDictionary(g => g.Key, g => g.Sum(d => d["OnHand"].ToDecimal()));

    /// <summary>Open balance rows that still carry a typed lot text of a lot-controlled SKU.</summary>
    public List<BsonDocument> OpenTypedRows(Guid sku)
        => Lots.Persisted("inventory_balances")
            .Where(d => !d["IsDeleted"].AsBoolean && d["SkuId"].AsString == sku.ToString() && d["LotId"].IsString
                        && !Guid.TryParseExact(d["LotId"].AsString, "D", out _))
            .ToList();

    public async Task RebuildSkuPositionsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync();
    }

    // ── transport ────────────────────────────────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, Action<HttpRequestMessage>? configure = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token ?? Lots.Token);
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        configure?.Invoke(request);
        return Lots.Client.SendAsync(request);
    }

    public static async Task<JsonElement> Expect(Task<HttpResponseMessage> call, int status)
    {
        using var response = await call;
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"expected {status}, got {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
