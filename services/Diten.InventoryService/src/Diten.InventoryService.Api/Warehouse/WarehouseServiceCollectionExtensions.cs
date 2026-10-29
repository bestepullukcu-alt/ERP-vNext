using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Persistence.Warehouse;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.Warehouse;

/// <summary>
/// MOD-0178 DL-1 wiring — one call from <c>Program.cs</c> (the <c>AddExpiry</c> / <c>AddRecall</c> pattern), so the shared registrations stay
/// untouched. MediatR handlers are picked up by the Application assembly scan. DL-1: the location roles (records + history), their reads over
/// the in-process LOCATION store, the temporary RCS-001 counter behind its port (Q-07 B) and the fail-closed index gate.
/// </summary>
public static class WarehouseServiceCollectionExtensions
{
    public static IServiceCollection AddWarehouse(this IServiceCollection services)
    {
        WarehouseBsonMappings.Register(); // role sets stored as names
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<WarehouseSupport>();
        services.AddScoped<IWarehouseLocationRoleRepository, WarehouseLocationRoleRepository>();
        services.AddScoped<LocationRoleReads>();
        services.AddScoped<LocationRoleChecks>();
        services.AddScoped<IWarehouseRecordCodeAllocator, WarehouseRecordCodeAllocator>();

        // Index gate (fail-closed): the unique indexes carry "one record per node", the request keys and the counter.
        services.AddHostedService<WarehouseIndexInitializerHostedService>();
        return services;
    }
}
