using System.Collections.Concurrent;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// The REAL service host of <see cref="InventoryApiFactory"/> plus the MOD-0174 SKU directory stub (GTIN source). Every
/// tenant a trace test registers is removed from the six <c>trace_*</c> collections of the shared test database when the
/// factory is disposed (DB-010).
/// </summary>
public sealed class TraceApiFactory : InventoryApiFactory
{
    private static readonly string[] TraceOwnedCollections =
        ["trace_lots", "trace_lot_history", "trace_serials", "trace_genealogy_links", "trace_movement_lot_links", "trace_lot_migrations"];

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
