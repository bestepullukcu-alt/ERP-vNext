using System.Net;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

// MOD-0185 producer uptake (DRAFT, not built). API-level tests against the isolated replica set
// (LoadTestContext). Covers LU-01, LU-03..LU-09, LU-11 and the integration part of LU-12.
internal static class LoadRootSeed
{
    public const string ListPath = "/api/shipment-bundle/loads";
    public const string Nil = "00000000-0000-0000-0000-000000000000";

    public static async Task<Guid> Create(LoadTestContext f, string key, string? correlation = null, Guid? tenant = null, Guid? le = null)
    {
        var created = await f.Send("POST", ListPath, f.CreateBody(Guid.NewGuid()), key, correlation: correlation, tenant: tenant, le: le);
        Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
        return Guid.Parse(created.Body!["loadId"]!.GetValue<string>());
    }

    public static BsonDocument ById(LoadTestContext f, Guid id)
    {
        var filter = f.Scope();
        filter.Add("_id", id.ToString());
        return filter;
    }

    // Test-only fixture writes that imitate legacy or tampered storage. The product never does this.
    public static Task SetRoot(LoadTestContext f, Guid id, BsonValue value) =>
        f.Db.GetCollection<BsonDocument>("loads").UpdateOneAsync(ById(f, id), new BsonDocument("$set", new BsonDocument("CorrelationRoot", value)));

    public static Task UnsetRoot(LoadTestContext f, Guid id) =>
        f.Db.GetCollection<BsonDocument>("loads").UpdateOneAsync(ById(f, id), new BsonDocument("$unset", new BsonDocument("CorrelationRoot", "")));

    public static Task<BsonDocument> Stored(LoadTestContext f, Guid id) =>
        f.Db.GetCollection<BsonDocument>("loads").Find(ById(f, id)).SingleAsync();

    public static Dictionary<Guid, JsonNode?> Items(JsonNode body)
    {
        var result = new Dictionary<Guid, JsonNode?>();
        foreach (var item in body["items"]!.AsArray())
        {
            var obj = Assert.IsType<JsonObject>(item);
            Assert.True(obj.TryGetPropertyValue("lifecycleCorrelationId", out var root), "every item carries lifecycleCorrelationId (value or explicit null)");
            result.Add(Guid.Parse(obj["loadId"]!.GetValue<string>()), root);
        }
        return result;
    }
}

public sealed class LoadRootQueryTests
{
    [Fact]
    public async Task CreatedLoadListsItsOwnCreateRootAndNeverTheGetTrace()
    {
        await using var f = new LoadTestContext();
        var id = await LoadRootSeed.Create(f, "root-create");
        var trace = Guid.NewGuid().ToString();
        var before = await f.Snapshot();

        var list = await f.Send("GET", LoadRootSeed.ListPath, correlation: trace);

        Assert.Equal(HttpStatusCode.OK, list.Response.StatusCode);
        var value = LoadRootSeed.Items(list.Body!)[id];
        Assert.NotNull(value);
        Assert.Equal(Guid.Parse(f.Root), Guid.Parse(value!.GetValue<string>()));
        Assert.NotEqual(Guid.Parse(trace), Guid.Parse(value.GetValue<string>()));
        Assert.Equal(Guid.Parse(f.Root), Guid.Parse((await LoadRootSeed.Stored(f, id))["CorrelationRoot"].AsString));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task LegacyStoredRootStatesListTogetherWithoutFailingOrBackfilling()
    {
        await using var f = new LoadTestContext();
        var legacy = Guid.NewGuid();
        var upper = Guid.NewGuid();
        var ids = new Dictionary<string, Guid>();
        foreach (var name in new[] { "fresh", "valid", "uppercase", "nil", "missing", "null", "malformed", "int32", "binary-uuid" })
            ids[name] = await LoadRootSeed.Create(f, "legacy-" + name);
        await LoadRootSeed.SetRoot(f, ids["valid"], legacy.ToString());
        await LoadRootSeed.SetRoot(f, ids["uppercase"], upper.ToString().ToUpperInvariant());
        await LoadRootSeed.SetRoot(f, ids["nil"], LoadRootSeed.Nil);
        await LoadRootSeed.UnsetRoot(f, ids["missing"]);
        await LoadRootSeed.SetRoot(f, ids["null"], BsonNull.Value);
        await LoadRootSeed.SetRoot(f, ids["malformed"], "not-a-uuid");
        await LoadRootSeed.SetRoot(f, ids["int32"], 7);
        await LoadRootSeed.SetRoot(f, ids["binary-uuid"], new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard));
        var before = await f.Snapshot();

        var list = await f.Send("GET", LoadRootSeed.ListPath);

        Assert.Equal(HttpStatusCode.OK, list.Response.StatusCode);
        Assert.Equal(ids.Count, list.Body!["total"]!.GetValue<int>());
        var items = LoadRootSeed.Items(list.Body!);
        Assert.Equal(ids.Values.Order(), items.Keys.Order());
        Assert.Equal(Guid.Parse(f.Root).ToString(), items[ids["fresh"]]!.GetValue<string>());
        Assert.Equal(legacy.ToString(), items[ids["valid"]]!.GetValue<string>());
        Assert.Equal(upper, Guid.Parse(items[ids["uppercase"]]!.GetValue<string>()));
        Assert.Equal(LoadRootSeed.Nil, items[ids["nil"]]!.GetValue<string>());
        foreach (var name in new[] { "missing", "null", "malformed", "int32", "binary-uuid" })
            Assert.Null(items[ids[name]]);

        // No write on read: nothing created, replaced, normalized or backfilled (LU-05..LU-07, LU-11).
        Assert.Equal(before, await f.Snapshot());
        Assert.False((await LoadRootSeed.Stored(f, ids["missing"])).Contains("CorrelationRoot"));
        Assert.Equal(upper.ToString().ToUpperInvariant(), (await LoadRootSeed.Stored(f, ids["uppercase"]))["CorrelationRoot"].AsString);
    }

    [Fact]
    public async Task RootIsListedOnlyInsideTrustedScopeAndOnlyWithReadPermission()
    {
        await using var f = new LoadTestContext();
        var own = await LoadRootSeed.Create(f, "scope-own");
        var otherLegalEntity = Guid.NewGuid();
        var otherRoot = Guid.NewGuid().ToString();
        var foreign = await LoadRootSeed.Create(f, "scope-foreign", correlation: otherRoot, le: otherLegalEntity);

        var list = await f.Send("GET", LoadRootSeed.ListPath);

        var items = LoadRootSeed.Items(list.Body!);
        Assert.Equal(new[] { own }, items.Keys.ToArray());
        Assert.False(items.ContainsKey(foreign));
        Assert.DoesNotContain(otherRoot, list.Body!.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        var denied = await f.Send("GET", LoadRootSeed.ListPath, correlation: Guid.NewGuid().ToString(), permissions: ["create", "transition"]);
        Assert.Equal(HttpStatusCode.Forbidden, denied.Response.StatusCode);
        var deniedText = denied.Body?.ToJsonString() ?? "";
        Assert.DoesNotContain("lifecycleCorrelationId", deniedText, StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, deniedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListedRootsSurviveApiRestart()
    {
        await using var f = new LoadTestContext();
        var id = await LoadRootSeed.Create(f, "restart");
        var nilId = await LoadRootSeed.Create(f, "restart-nil");
        await LoadRootSeed.SetRoot(f, nilId, LoadRootSeed.Nil);
        var missingId = await LoadRootSeed.Create(f, "restart-missing");
        await LoadRootSeed.UnsetRoot(f, missingId);
        var first = LoadRootSeed.Items((await f.Send("GET", LoadRootSeed.ListPath)).Body!);

        f.RestartApi();
        var second = LoadRootSeed.Items((await f.Send("GET", LoadRootSeed.ListPath)).Body!);

        Assert.Equal(first[id]!.GetValue<string>(), second[id]!.GetValue<string>());
        Assert.Equal(LoadRootSeed.Nil, second[nilId]!.GetValue<string>());
        Assert.Null(second[missingId]);
    }
}
