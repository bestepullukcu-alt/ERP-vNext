using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Warehouse;
using Diten.InventoryService.Infrastructure.Workflow;
using Diten.InventoryService.Persistence.Warehouse;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Api.Warehouse;

/// <summary>
/// MOD-0178 DL-1 wiring — one call from <c>Program.cs</c> (the <c>AddExpiry</c> / <c>AddRecall</c> pattern), so the shared registrations stay
/// untouched. MediatR handlers are picked up by the Application assembly scan. DL-1: the location roles (records + history), their reads over
/// the in-process LOCATION store, the temporary RCS-001 counter behind its port (Q-07 B) and the fail-closed index gate. DL-2: the putaway
/// tasks — their store, the read ports, the one stock door, the MOD-0173 key lookup, the confirmation engine and the Pending reconciler.
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

        // DL-2 — the putaway tasks. The time settings are checked at start (pack §4.2: lease ≥ posting + 30 s, long-pending minutes > 0 and
        // their seconds > the lease) — an inconsistent setting stops the host.
        services.AddOptions<WarehousePutawayOptions>().BindConfiguration(WarehousePutawayOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<WarehousePutawayOptions>, WarehousePutawayOptionsValidator>();
        services.AddScoped<IWarehouseTaskRepository, WarehouseTaskRepository>();
        services.AddScoped<IWarehouseStockReader, WarehouseStockReads>();
        services.AddScoped<IWarehouseLotReader, WarehouseLotReader>();
        services.AddScoped<IWarehouseSerialReader, WarehouseSerialReader>();
        services.AddScoped<IWarehouseMovementPoster, WarehouseMovementPoster>(); // the ONE stock door → InternalMovementPostingService
        // Q-13 A: the assignee check — AUTH lookup-validation through the Gateway with the caller's token + correlation id (user requests only)
        services.AddHttpClient<IWarehouseAssigneeDirectory, AuthAssigneeDirectory>((sp, http) =>
            {
                var gateway = sp.GetRequiredService<IConfiguration>().GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new GatewayOptions();
                http.BaseAddress = Uri.TryCreate(gateway.BaseUrl, UriKind.Absolute, out var uri)
                    ? uri
                    : throw new InvalidOperationException($"Configuration error: '{GatewayOptions.SectionName}:BaseUrl' must be an absolute URL.");
                http.Timeout = TimeSpan.FromSeconds(10); // the per-check 3 s bound is the directory's own
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.TryAddScoped<IPostingKeyLookup, PostingKeyLookup>();           // MOD-0173 additive port (pack §5)
        services.AddScoped<PutawayTaskSupport>();
        services.AddScoped<PutawayTaskReads>();
        services.AddScoped<PutawayChecks>();
        services.AddScoped<PutawayPostingEngine>();
        services.AddScoped<PutawayReconcilePlanner>();
        services.AddScoped<IWarehousePendingScan, WarehousePendingScan>();
        services.AddSingleton<PutawayReconciler>();
        services.AddHostedService<PendingConfirmationReconcilerHostedService>();

        // DL-4 — the S2 outbound with its allocation (pack §8.17): the FEFO read over the MOD-0176 read model, the planner, the FU03 batch and
        // transaction B (the reservation port is MOD-0173-FU03's in-process door, registered by the Application), the outbound store and the
        // maintenance service that completes an interrupted allocation with the same keys. The lease setting is checked at start.
        services.AddOptions<WarehouseOutboundOptions>().BindConfiguration(WarehouseOutboundOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<WarehouseOutboundOptions>, WarehouseOutboundOptionsValidator>();
        services.AddScoped<IWarehouseFefoReader, WarehouseFefoReader>();
        services.AddScoped<IOutboundShipmentRepository, OutboundShipmentRepository>();
        services.AddScoped<OutboundShipmentReads>();
        services.AddScoped<OutboundAllocationPlanner>();
        services.AddScoped<OutboundAllocationService>();
        services.AddScoped<IOutboundMaintenanceScan, OutboundMaintenanceScan>();

        // DL-5 — pick / pack / ship / cancel (pack §8.19–§8.22): a pick task's terminal write moves its outbound in the task's transaction; the ONE
        // reservation release rule (cancel, post-dispatch remainder, maintenance). The MVP-6 shipment read is MOD-0177's IShipmentReader (its DI
        // registration is unchanged — pack §5).
        services.AddScoped<IPickTaskCompletion, PickTaskCompletion>();
        services.AddScoped<OutboundReservationReleaser>();
        services.AddSingleton<OutboundMaintenance>();
        services.AddHostedService<OutboundMaintenanceHostedService>();

        // Index gate (fail-closed): the unique indexes carry "one record per node", the request keys, the counter, the task number and the
        // serial slot.
        services.AddHostedService<WarehouseIndexInitializerHostedService>();
        return services;
    }
}
