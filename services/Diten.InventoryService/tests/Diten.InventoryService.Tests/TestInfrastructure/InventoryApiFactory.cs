using System.Collections.Concurrent;
using Diten.InventoryService.Application.Common.Upstream;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Hosts the REAL service (Program.cs, real middleware, real MediatR pipeline, real Mongo repository on a
/// transaction-capable replica set) with only the two upstream masters swapped for in-process stubs.
/// Database per DB-010: the fixed <see cref="TestSettings.DatabaseName"/>; each scenario gets its own TenantId, and
/// the tenants a factory created are removed from the shared database when it is disposed.
/// </summary>
public class InventoryApiFactory : WebApplicationFactory<Program>
{
    private readonly ConcurrentBag<Guid> _tenants = [];

    static InventoryApiFactory() => TestSettings.Apply();

    public StubProductMaster Products { get; } = new();

    public StubLocationMaster Locations { get; } = new();

    public StubLegalEntityMaster LegalEntities { get; } = new();

    /// <summary>Host environment; "Testing" unless a test needs another one.</summary>
    public string HostEnvironment { get; init; } = "Testing";

    public IMongoDatabase Database { get; } =
        new MongoClient(TestSettings.MongoConnectionString).GetDatabase(TestSettings.DatabaseName);

    public void TrackTenant(Guid tenantId) => _tenants.Add(tenantId);

    /// <summary>An HttpClient whose every response is checked against the frozen contract (AC15).</summary>
    public HttpClient CreateContractClient() => CreateDefaultClient(new ContractValidatingHandler());

    public IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostEnvironment);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IProductMasterClient>(Products);
            services.AddSingleton<ILocationMasterClient>(Locations);
            services.AddSingleton<ILegalEntityMasterClient>(sp =>
            {
                LegalEntities.HttpContextAccessor = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                return LegalEntities;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_tenants.IsEmpty)
        {
            var tenants = new BsonArray(_tenants.Select(t => t.ToString()));
            foreach (var name in new[] { "inventory_transactions", "inventory_balances", "inventory_audit", "inventory_outbox", "inventory_sequences", "inventory_reservations", "inventory_sku_positions" })
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}
