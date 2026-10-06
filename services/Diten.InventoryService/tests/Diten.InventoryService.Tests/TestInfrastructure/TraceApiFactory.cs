using System.Collections.Concurrent;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>The same host with <c>Trace:EnforceRegisteredLots = true</c> (the P3 switch, ASSUMPTION-0174-10).</summary>
public sealed class EnforcedLotsTraceApiFactory : TraceApiFactory
{
    protected override bool EnforceRegisteredLots => true;
}

/// <summary>
/// The REAL service host of <see cref="InventoryApiFactory"/> plus the MOD-0174 SKU directory stub (GTIN source). Every
/// tenant a trace test registers is removed from the six <c>trace_*</c> collections of the shared test database when the
/// factory is disposed (DB-010) — and, from WP-0174-P2, from the ledger's per-lot projection <c>inventory_lot_positions</c>.
/// </summary>
public class TraceApiFactory : InventoryApiFactory
{
    private static readonly string[] TraceOwnedCollections =
        ["trace_lots", "trace_lot_history", "trace_serials", "trace_genealogy_links", "trace_movement_lot_links", "trace_lot_migrations",
         "inventory_lot_positions"];

    /// <summary><c>Trace:EnforceRegisteredLots</c> of the host (WP-0174-P2); off, as in production until P3 is CT-accepted.</summary>
    protected virtual bool EnforceRegisteredLots => false;

    private readonly ConcurrentBag<Guid> _traceTenants = [];

    public StubTraceSkuDirectory Skus { get; } = new();

    public void TrackTraceTenant(Guid tenantId)
    {
        _traceTenants.Add(tenantId);
        TrackTenant(tenantId);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ITraceSkuDirectory>(Skus));
        if (EnforceRegisteredLots)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Trace:EnforceRegisteredLots"] = "true" }));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_traceTenants.IsEmpty)
        {
            var tenants = new BsonArray(_traceTenants.Select(t => t.ToString()));
            foreach (var name in TraceOwnedCollections)
            {
                Collection(name).DeleteMany(new BsonDocument("TenantId", new BsonDocument("$in", tenants)));
            }
        }

        base.Dispose(disposing);
    }
}
