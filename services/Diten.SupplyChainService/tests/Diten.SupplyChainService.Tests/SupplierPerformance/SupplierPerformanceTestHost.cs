using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Diten.BuildingBlocks.CentralAudit;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using Diten.SupplyChainService.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// The real SupplyChain host (Program.cs composition, context middleware, MediatR pipeline with the central-audit behavior,
/// Mongo transactions on the owned replica set). Only three seams are replaced: Procurement (<see cref="FakeProcurement"/>
/// behind the typed client's primary handler), MDM (<see cref="StubLegalEntityScopeValidator"/>, settable) and the commit probe.
/// </summary>
internal sealed class SupplierPerformanceFactory(FakeProcurement procurement, StubLegalEntityScopeValidator legalEntities, SupplierPerformanceProbe probe, string appName)
    : WebApplicationFactory<Program>
{
    public const string DatabaseName = "diten_mod0147_tests";
    public static readonly string Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private Dictionary<string, string?> Settings() => new(AuthServiceToken.JwtSettings(Secret))
    {
        ["Mongo:ConnectionString"] = new MongoUrlBuilder(SupplierPerformanceMongo.Connection) { ApplicationName = appName }.ToMongoUrl().ToString(),
        ["Mongo:DatabaseName"] = DatabaseName,
        [SupplierEligibilityReader.BaseUrlKey] = "http://procurement-through-gateway.test/"
    };
    protected override IHost CreateHost(IHostBuilder builder)
    { builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings())); return base.CreateHost(builder); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings()));
        builder.ConfigureServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<ISupplierPerformanceCommitProbe>(probe));
            s.Replace(ServiceDescriptor.Singleton<ILegalEntityScopeValidator>(legalEntities));
            s.AddHttpClient<ISupplierEligibilityReader, SupplierEligibilityReader>().ConfigurePrimaryHttpMessageHandler(() => procurement);
        });
    }
}

internal sealed class SupplierPerformanceHost : IAsyncDisposable
{
    public const string Base = "/api/supplier-performance";
    public const string Evaluate = SupplierPerformancePermissions.Evaluate, Read = SupplierPerformancePermissions.Read;
    public readonly Guid Tenant = Guid.NewGuid(), LegalEntity = Guid.NewGuid(), Actor = Guid.NewGuid();
    public readonly FakeProcurement Procurement = new();
    public readonly StubLegalEntityScopeValidator LegalEntities = StubLegalEntityScopeValidator.Valid;
    public readonly SupplierPerformanceProbe Probe = new();
    public readonly SupplierPerformanceFactory App;
    public readonly HttpClient Client;
    public readonly IMongoDatabase Db;
    public string Correlation = Guid.NewGuid().ToString();

    public SupplierPerformanceHost()
    {
        App = new SupplierPerformanceFactory(Procurement, LegalEntities, Probe, "mod0147-" + Guid.NewGuid());
        Client = App.CreateClient();
        Db = App.Services.GetRequiredService<IMongoDatabase>();
    }

    public static JsonObject Metric(string code, string value, string weight = "25") => new() { ["metricCode"] = code, ["measuredValue"] = value, ["weight"] = weight };

    public static JsonObject Body(string supplierId = FakeProcurement.Code, string start = "2026-07-01", string end = "2026-07-31", JsonArray? metrics = null) => new()
    {
        ["supplierId"] = supplierId, ["periodStart"] = start, ["periodEnd"] = end,
        ["metrics"] = metrics ?? new JsonArray(Metric("OTD", "90"), Metric("QTY_ACC", "80"), Metric("QUAL_ACC", "100"), Metric("INV_MATCH", "70"))
    };

    public string Token(Guid? tenant = null, Guid? actor = null, params string[] permissions) =>
        AuthServiceToken.Mint(SupplierPerformanceFactory.Secret, tenant ?? Tenant, actor ?? Actor, permissions);

    public sealed record Answer(HttpStatusCode Status, JsonNode? Body, HttpResponseMessage Response)
    {
        public string? Code => Body?["error"]?["code"]?.GetValue<string>();
        public string? Header(string name) => Response.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;
    }

    public async Task<Answer> Send(HttpMethod method, string path, JsonNode? body = null, string? key = "key-1", string? correlation = null,
        Guid? tenant = null, Guid? le = null, Guid? headerTenant = null, string[]? permissions = null, string? token = null, string? rawBody = null,
        string contentType = "application/json", Action<HttpRequestMessage>? edit = null)
    {
        using var request = new HttpRequestMessage(method, path);
        var t = tenant ?? Tenant;
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (token ?? Token(t, null, permissions ?? [Evaluate, Read])));
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", (headerTenant ?? t).ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", (le ?? LegalEntity).ToString());
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation ?? Correlation);
        if (method == HttpMethod.Post && key is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (rawBody is not null || body is not null) request.Content = new StringContent(rawBody ?? body!.ToJsonString(), Encoding.UTF8, contentType);
        edit?.Invoke(request);
        var response = await Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return new(response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), response);
    }

    public Task<Answer> Create(JsonNode? body = null, string? key = "key-1", Guid? tenant = null, Guid? le = null, string[]? permissions = null, string? correlation = null) =>
        Send(HttpMethod.Post, Base + "/evaluations", body ?? Body(), key, correlation, tenant, le, permissions: permissions);

    public Task<Answer> Get(string path, Guid? tenant = null, Guid? le = null, string[]? permissions = null) =>
        Send(HttpMethod.Get, Base + path, null, null, null, tenant, le, permissions: permissions);

    private BsonDocument Scope(Guid? tenant, Guid? le) => new() { { "TenantId", (tenant ?? Tenant).ToString() }, { "LegalEntityId", (le ?? LegalEntity).ToString() } };
    public Task<long> Evaluations(Guid? tenant = null, Guid? le = null) => Db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Evaluations).CountDocumentsAsync(Scope(tenant, le));
    public Task<long> Receipts(Guid? tenant = null, Guid? le = null) => Db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Receipts).CountDocumentsAsync(Scope(tenant, le));
    public Task<List<BsonDocument>> AuditRecords(Guid? tenant = null) =>
        Db.GetCollection<BsonDocument>(CentralAuditOutboxRecord.CollectionName).Find(new BsonDocument { { "TenantId", (tenant ?? Tenant).ToString() }, { "EntityType", "SupplierEvaluation" } }).ToListAsync();
    public async Task<BsonDocument[]> Snapshot()
    {
        var all = new List<BsonDocument>();
        foreach (var name in new[] { SupplierPerformanceSchema.Evaluations, SupplierPerformanceSchema.Receipts })
            all.AddRange(await Db.GetCollection<BsonDocument>(name).Find(Scope(null, null)).Sort(new BsonDocument("_id", 1)).ToListAsync());
        // Central-audit rows are counted separately: the dispatcher updates their delivery fields in the background.
        return all.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}
