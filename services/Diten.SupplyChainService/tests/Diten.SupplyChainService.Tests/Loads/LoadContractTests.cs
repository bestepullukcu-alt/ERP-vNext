using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Api.Features.Loads;
using Diten.SupplyChainService.Domain.Features.Loads;
using Diten.SupplyChainService.Persistence.Features.Loads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Diten.SupplyChainService.Tests.Common;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Diten.SupplyChainService.Tests.Loads;

internal sealed class LoadProbe : ILoadCommitProbe
{
    public string? FailAt;
    public Task AtAsync(string boundary, CancellationToken ct)
    {
        while (true)
        {
            var expected = Volatile.Read(ref FailAt);
            if (!string.Equals(expected, boundary, StringComparison.Ordinal)) break;
            if (ReferenceEquals(Interlocked.CompareExchange(ref FailAt, null, expected), expected))
                throw new IOException("Injected Load write boundary failure.");
        }
        return Task.CompletedTask;
    }
    public Guid NewId() => Guid.NewGuid();
}

internal sealed class LoadTestFactory(LoadProbe probe, string referenceUrl) : WebApplicationFactory<Program>
{
    public const string DatabaseName = "diten_mod0185_tests";
    public static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Connection => Environment.GetEnvironmentVariable("MOD0185_TEST_MONGO") ?? throw new InvalidOperationException("Set MOD0185_TEST_MONGO to an isolated replica set.");
    private static Dictionary<string, string?> Settings(string referenceUrl) => new()
    {
        ["Mongo:ConnectionString"] = Connection,
        ["Mongo:DatabaseName"] = DatabaseName,
        ["JwtSettings:Secret"] = Secret,
        ["JwtSettings:Issuer"] = "loads-tests",
        ["JwtSettings:Audience"] = "loads-tests",
        ["Loads:ReferenceBaseUrl"] = referenceUrl
    };
    protected override IHost CreateHost(IHostBuilder builder)
    { builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings(referenceUrl))); return base.CreateHost(builder); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings(referenceUrl)));
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<ILoadCommitProbe>(probe))
            .StubLegalEntityValidation());
    }
}

internal sealed class LoadTestContext : IAsyncDisposable
{
    public readonly Guid Tenant = Guid.NewGuid(), LegalEntity = Guid.NewGuid(), Actor = Guid.NewGuid();
    public readonly Guid CarrierId = Guid.NewGuid(), ShipmentId = Guid.NewGuid();
    public readonly LoadProbe Probe = new();
    public object? CarrierResponse { get; set; }
    public Func<Guid, object?> ShipmentResponse { get; set; }
    public int CarrierStatusCode { get; set; } = 200;
    public int ShipmentStatusCode { get; set; } = 200;
    public readonly ConcurrentDictionary<string, int> Calls = new(StringComparer.Ordinal);
    public readonly ConcurrentQueue<LoadMockCall> MockCalls = new();
    private readonly WebApplication _mock;
    public LoadTestFactory App { get; private set; }
    public HttpClient Client { get; private set; }
    public readonly IMongoDatabase Db;
    public string Root = Guid.NewGuid().ToString();

    public LoadTestContext()
    {
        CarrierResponse = DefaultCarrierResponse(CarrierId);
        ShipmentResponse = id => DefaultShipmentResponse(id);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _mock = builder.Build();
        _mock.MapFallback(async http =>
        {
            var path = http.Request.Path + http.Request.QueryString;
            Calls.AddOrUpdate(path, 1, (_, n) => n + 1);
            MockCalls.Enqueue(new(http.Request.Method, path,
                http.Request.Headers["X-Tenant-Id"].ToString(),
                http.Request.Headers["X-Legal-Entity-Id"].ToString(),
                http.Request.Headers["X-Correlation-Id"].ToString(),
                http.Request.Headers.ContainsKey("Authorization")));
            if (http.Request.Path.Value == "/api/shipment-bundle/carriers")
            {
                if (CarrierStatusCode != 200) { http.Response.StatusCode = CarrierStatusCode; return; }
                await http.Response.WriteAsJsonAsync(CarrierResponse);
            }
            else if (http.Request.Path.Value?.StartsWith("/api/shipment-bundle/shipments/", StringComparison.Ordinal) == true &&
                     Guid.TryParse(http.Request.Path.Value[("/api/shipment-bundle/shipments/".Length)..], out var shipmentId))
            {
                if (ShipmentStatusCode != 200) { http.Response.StatusCode = ShipmentStatusCode; return; }
                var value = ShipmentResponse(shipmentId);
                if (value is null) { http.Response.StatusCode = 404; return; }
                await http.Response.WriteAsJsonAsync(value);
            }
            else http.Response.StatusCode = 404;
        });
        _mock.StartAsync().GetAwaiter().GetResult();
        var address = _mock.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        App = new LoadTestFactory(Probe, address);
        Client = App.CreateClient();
        Db = App.Services.GetRequiredService<IMongoDatabase>();
    }

    public void RestartApi()
    {
        Client.Dispose(); App.Dispose();
        App = new LoadTestFactory(Probe, _mock.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        Client = App.CreateClient();
    }

    public static object DefaultCarrierResponse(Guid? carrierId = null, string status = "Active", string[]? modes = null) => new
    {
        items = new[] { new { carrierId = carrierId ?? Guid.Empty, carrierCode = "C-1", displayName = "Carrier", status, supportedModes = modes ?? ["Road"] } },
        total = 1,
        contractVersion = "v1"
    };

    public static object DefaultShipmentResponse(Guid shipmentId, string status = "Draft", Guid? carrierId = null, Guid? loadId = null) => new
    {
        shipmentId,
        shipmentNumber = "S-1",
        sourceDocumentId = "src-1",
        status,
        carrierId,
        loadId,
        plannedShipAt = "2030-01-01T00:00:00Z",
        sourceModule = "MOD-0183",
        sourceType = "Shipment",
        warehouseReferenceId = "wh-1",
        shipToReference = "dest-1",
        lines = Array.Empty<object>(),
        contractVersion = "v1"
    };

    public JsonObject CreateBody(Guid? shipmentId = null, Guid? carrierId = null)
    {
        var id = shipmentId ?? ShipmentId;
        return new JsonObject
        {
            ["carrierId"] = (carrierId ?? CarrierId).ToString(),
            ["shipmentIds"] = new JsonArray(id.ToString()),
            ["mode"] = "Road",
            ["plannedDepartAt"] = "2030-01-01T01:00:00+01:00",
            ["stops"] = new JsonArray(
                new JsonObject { ["sequence"] = 1, ["locationReferenceId"] = "", ["action"] = "Pickup" },
                new JsonObject { ["sequence"] = 2, ["locationReferenceId"] = "destination", ["action"] = "Delivery" })
        };
    }

    public JsonObject TransitionBody(string status, string? note = null, bool includeNote = false)
    {
        var body = new JsonObject { ["targetStatus"] = status, ["occurredAt"] = "2030-01-01T01:00:00+01:00" };
        if (includeNote) body["note"] = note;
        return body;
    }

    public static string Token(Guid tenant, Guid le, Guid actor, params string[] permissions)
    {
        var claims = new List<Claim> { new("tenant_id", tenant.ToString()), new("legal_entity_id", le.ToString()), new("sub", actor.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", "supplychain.loads." + p)));
        var token = new JwtSecurityToken("loads-tests", "loads-tests", claims, expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(LoadTestFactory.Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpRequestMessage Request(string method, string path, Guid? tenant = null, Guid? le = null, Guid? actor = null,
        string[]? permissions = null, string? correlation = null, string? key = null, JsonObject? body = null, string? contentType = "application/json")
    {
        var t = tenant ?? Tenant; var entity = le ?? LegalEntity; var user = actor ?? Actor;
        var required = permissions ?? ["read", "create", "transition"];
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token(t, entity, user, required));
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", t.ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", entity.ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation ?? Root);
        if (key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, contentType ?? "application/json");
        }
        return request;
    }

    public async Task<(HttpResponseMessage Response, JsonNode? Body)> Send(string method, string path, JsonObject? body = null,
        string key = "key", string? correlation = null, Guid? tenant = null, Guid? le = null, string[]? permissions = null,
        string? contentType = "application/json")
    {
        using var request = Request(method, path, tenant, le, permissions: permissions, correlation: correlation, key: method == "GET" ? null : key, body: body, contentType: contentType);
        var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text));
    }

    public BsonDocument Scope(Guid? tenant = null, Guid? le = null) => new() { { "TenantId", (tenant ?? Tenant).ToString() }, { "LegalEntityId", (le ?? LegalEntity).ToString() } };
    public async Task<long> Count(string collection, Guid? tenant = null, Guid? le = null) => await Db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(Scope(tenant, le));
    public async Task<BsonDocument[]> Snapshot(Guid? tenant = null, Guid? le = null)
    {
        var all = new List<BsonDocument>();
        foreach (var name in new[] { "loads", "load_assignments", "loads_receipts", "loads_audit", "loads_outbox" })
            all.AddRange(await Db.GetCollection<BsonDocument>(name).Find(Scope(tenant, le)).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).ToListAsync());
        return all.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        App.Dispose();
        await _mock.StopAsync();
        await _mock.DisposeAsync();
    }
}

internal sealed record LoadMockCall(string Method, string Path, string Tenant, string LegalEntity, string Correlation, bool HasAuthorization);

public sealed class LoadContractTests
{
    [Fact]
    public async Task EveryRequiredCreateFieldAndTransitionFieldIsEnforcedBeforeWrites()
    {
        await using var f = new LoadTestContext();
        foreach (var property in new[] { "carrierId", "shipmentIds", "mode", "plannedDepartAt", "stops" })
        {
            var body = f.CreateBody(); body.Remove(property);
            var missing = await f.Send("POST", "/api/shipment-bundle/loads", body, "missing-" + property);
            Assert.Equal(HttpStatusCode.BadRequest, missing.Response.StatusCode);
            Assert.Equal("INVALID_REQUEST", missing.Body!["error"]!["code"]!.GetValue<string>());
        }
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "schema-valid-create");
        var id = create.Body!["loadId"]!.GetValue<string>();
        foreach (var property in new[] { "targetStatus", "occurredAt" })
        {
            var body = f.TransitionBody("Planned"); body.Remove(property);
            var missing = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", body, "missing-transition-" + property);
            Assert.Equal(HttpStatusCode.BadRequest, missing.Response.StatusCode);
        }
        var nullDate = f.TransitionBody("Planned"); nullDate["occurredAt"] = null;
        var invalidNull = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", nullDate, "null-transition-date");
        Assert.Equal(HttpStatusCode.BadRequest, invalidNull.Response.StatusCode);
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task CreateSchemaRejectsNullWrongTypeEnumsAndArrayBoundsWithoutInventingDefaults()
    {
        await using var f = new LoadTestContext();
        var invalidBodies = new List<JsonObject>();
        foreach (var mutate in new Action<JsonObject>[]
        {
            b => b["carrierId"] = null,
            b => b["carrierId"] = 7,
            b => b["shipmentIds"] = null,
            b => b["shipmentIds"] = new JsonArray(),
            b => b["shipmentIds"] = new JsonArray("not-a-uuid"),
            b => b["mode"] = null,
            b => b["mode"] = "Truck",
            b => b["plannedDepartAt"] = "not-a-date",
            b => b["stops"] = null,
            b => b["stops"] = new JsonArray(new JsonObject { ["sequence"] = 1, ["locationReferenceId"] = "x", ["action"] = "Pickup" }),
            b => b["stops"] = new JsonArray(new JsonObject { ["sequence"] = 1, ["locationReferenceId"] = "x" }, new JsonObject { ["sequence"] = 2, ["locationReferenceId"] = "y", ["action"] = "Delivery" }),
            b => b["stops"]![0]!["sequence"] = 0,
            b => b["stops"]![0]!["action"] = "Load",
            b => b["unexpected"] = true
        })
        {
            var body = f.CreateBody(); mutate(body); invalidBodies.Add(body);
        }
        var index = 0;
        foreach (var body in invalidBodies)
        {
            var result = await f.Send("POST", "/api/shipment-bundle/loads", body, "invalid-schema-" + index++);
            Assert.Equal(HttpStatusCode.BadRequest, result.Response.StatusCode);
            Assert.Equal("INVALID_REQUEST", result.Body!["error"]!["code"]!.GetValue<string>());
        }
        Assert.Empty(f.Calls);
        Assert.Equal(0, await f.Count("loads"));
        Assert.Equal(0, await f.Count("loads_receipts"));
        Assert.Equal(0, await f.Count("loads_audit"));
        Assert.Equal(0, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task DuplicateShipmentAndInvalidStopSequencesAreBusiness422ButReturnStopAndEmptyLocationStayAllowed()
    {
        await using var f = new LoadTestContext();
        var duplicate = f.CreateBody(); duplicate["shipmentIds"] = new JsonArray(f.ShipmentId.ToString(), f.ShipmentId.ToString());
        var duplicateResult = await f.Send("POST", "/api/shipment-bundle/loads", duplicate, "duplicate-shipment");
        Assert.Equal((HttpStatusCode)422, duplicateResult.Response.StatusCode);
        Assert.Equal("DUPLICATE_SHIPMENT", duplicateResult.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads"));
        Assert.Equal(0, await f.Count("loads_receipts"));

        var gap = f.CreateBody(); gap["stops"]![1]!["sequence"] = 3;
        var gapResult = await f.Send("POST", "/api/shipment-bundle/loads", gap, "stop-sequence-gap");
        Assert.Equal((HttpStatusCode)422, gapResult.Response.StatusCode);
        Assert.Equal("INVALID_LOAD_STOPS", gapResult.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads"));
        Assert.Equal(0, await f.Count("loads_receipts"));

        var returned = f.CreateBody();
        returned["stops"] = new JsonArray(
            new JsonObject { ["sequence"] = 1, ["locationReferenceId"] = "", ["action"] = "Pickup" },
            new JsonObject { ["sequence"] = 2, ["locationReferenceId"] = "return", ["action"] = "Return" },
            new JsonObject { ["sequence"] = 3, ["locationReferenceId"] = "destination", ["action"] = "Delivery" });
        var acceptedReturnStop = await f.Send("POST", "/api/shipment-bundle/loads", returned, "return-stop-allowed");
        Assert.Equal(HttpStatusCode.Created, acceptedReturnStop.Response.StatusCode);
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("load_assignments"));
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task FrozenThreeOperationWireAndSuccessfulCreateList()
    {
        await using var f = new LoadTestContext();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody());
        Assert.Equal(201, (int)create.Response.StatusCode);
        Assert.Equal(Guid.Parse(f.Root), Guid.Parse(create.Response.Headers.GetValues("X-Correlation-Id").Single()));
        var body = Assert.IsType<JsonObject>(create.Body);
        Assert.Equal(new[] { "contractVersion", "idempotentReplay", "loadId", "loadNumber", "status" }, body.Select(x => x.Key).Order().ToArray());
        Assert.Equal("Draft", body["status"]!.GetValue<string>());
        Assert.Equal("v1", body["contractVersion"]!.GetValue<string>());
        var list = await f.Send("GET", "/api/shipment-bundle/loads?status=Draft&carrierId=" + f.CarrierId);
        Assert.Equal(200, (int)list.Response.StatusCode);
        var listBody = Assert.IsType<JsonObject>(list.Body);
        Assert.Equal(new[] { "contractVersion", "items", "total" }, listBody.Select(x => x.Key).Order().ToArray());
        Assert.Equal(1, listBody["total"]!.GetValue<int>());
        Assert.Equal(f.CarrierId.ToString(), listBody["items"]![0]!["carrierId"]!.GetValue<string>());
        Assert.Equal(1, await f.Count("loads_outbox"));
        Assert.Equal("Pending", (await f.Db.GetCollection<BsonDocument>("loads_outbox").Find(f.Scope()).SingleAsync())["Status"].AsString);
    }

    [Fact]
    public async Task SchemaUnknownFieldsAndNoExtraRoutesAreRejected()
    {
        await using var f = new LoadTestContext();
        var body = f.CreateBody(); body["tenantId"] = Guid.NewGuid().ToString();
        var invalid = await f.Send("POST", "/api/shipment-bundle/loads", body);
        Assert.Equal(400, (int)invalid.Response.StatusCode);
        Assert.Equal("INVALID_REQUEST", invalid.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads_receipts"));
        var detail = await f.Send("GET", "/api/shipment-bundle/loads/" + Guid.NewGuid());
        Assert.Equal(404, (int)detail.Response.StatusCode);
        var prefix = await f.Send("GET", "/api/shipment-bundle/loadsXYZ");
        Assert.Equal(404, (int)prefix.Response.StatusCode);
        Assert.Equal(0, f.Calls.Keys.Count(k => k.StartsWith("/api/shipment-bundle/carriers", StringComparison.Ordinal)));
    }
}
