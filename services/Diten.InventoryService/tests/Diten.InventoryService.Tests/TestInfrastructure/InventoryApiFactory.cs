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
/// transaction-capable replica set) with only the EXTERNAL masters swapped for in-process stubs: PRODUCT-MASTER,
/// the MDM legal-entity master and the Platform org-unit directory. LOCATION is not external any more
/// (MOD-0173-FU02): the ledger reads the real <c>inventory_locations</c> through the production in-process directory.
/// Only <see cref="StubLocationMasterApiFactory"/> puts the old LOCATION stub back, for the one scenario the in-process
/// directory cannot produce on demand — "location master unreachable" (AC-22 note in the evidence).
/// Database per DB-010: the fixed <see cref="TestSettings.DatabaseName"/>; each scenario gets its own TenantId, and
/// the tenants a factory created are removed from the shared database when it is disposed.
/// </summary>
public class InventoryApiFactory : WebApplicationFactory<Program>
{
    private static readonly string[] OwnedCollections =
    [
        "inventory_transactions", "inventory_balances", "inventory_audit", "inventory_outbox", "inventory_sequences",
        "inventory_reservations", "inventory_sku_positions", "inventory_locations", "inventory_location_history",
        // MOD-0175: the post-commit intake observer writes quarantine records for any tenant that posts into QUARANTINE /
        // QUALITY_INSPECTION / BLOCKED — removed with the tenant like the ledger documents (DB-010).
        "quarantine_holds", "quarantine_decisions", "quarantine_decision_events", "quarantine_blocked_stock", "quarantine_history",
        "quarantine_settings", "quarantine_block_reasons", "quarantine_sequences", "quarantine_event_inbox",
        // BL-501: the shared central-audit outbox (TenantId stored as a string, like the ledger's).
        "central_audit_outbox"
    ];

    private readonly ConcurrentBag<Guid> _tenants = [];

    static InventoryApiFactory() => TestSettings.Apply();

    public StubProductMaster Products { get; } = new();

    /// <summary>The pre-FU02 LOCATION stub. Registered in the host ONLY by <see cref="StubLocationMasterApiFactory"/>.</summary>
    public StubLocationMaster Locations { get; } = new();

    public StubLegalEntityMaster LegalEntities { get; } = new();

    public StubOrgUnitDirectory OrgUnits { get; } = new();

    /// <summary>MOD-0022 at the HTTP edge of the real e-signature client (Q3) — no test ever reaches a real Gateway.</summary>
    public FakeESignGateway ESign { get; } = new();

    /// <summary>Host environment; "Testing" unless a test needs another one.</summary>
    public string HostEnvironment { get; init; } = "Testing";

    protected virtual bool UseLocationStub => false;

    protected virtual bool UseOrgUnitStub => true;

    public IMongoDatabase Database { get; } =
        new MongoClient(TestSettings.MongoConnectionString).GetDatabase(TestSettings.DatabaseName);

    public void TrackTenant(Guid tenantId) => _tenants.Add(tenantId);

    /// <summary>An HttpClient whose every response is checked against the frozen contracts (AC15 / FU02 AC-15).</summary>
    public HttpClient CreateContractClient() => CreateDefaultClient(new ContractValidatingHandler());

    public IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(HostEnvironment);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IProductMasterClient>(Products);
            services.AddHttpClient(nameof(Diten.InventoryService.Application.Features.Quarantine.Ports.IQuarantineESignClient))
                .ConfigurePrimaryHttpMessageHandler(() => new FakeESignHandler(ESign));
            if (UseLocationStub)
            {
                services.AddSingleton<ILocationMasterClient>(Locations);
            }

            if (UseOrgUnitStub)
            {
                services.AddSingleton<IOrgUnitDirectoryClient>(OrgUnits);
            }

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
            foreach (var name in OwnedCollections)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>The pre-FU02 LOCATION stub in place of the in-process directory — only for "location master unreachable".</summary>
public sealed class StubLocationMasterApiFactory : InventoryApiFactory
{
    protected override bool UseLocationStub => true;
}

/// <summary>The REAL Platform org-unit HTTP client, pointed at a closed port (TestSettings) — measures the 503 path.</summary>
public sealed class RealOrgUnitClientApiFactory : InventoryApiFactory
{
    protected override bool UseOrgUnitStub => false;
}
