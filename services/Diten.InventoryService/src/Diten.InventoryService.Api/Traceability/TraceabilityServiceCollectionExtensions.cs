using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Infrastructure.Traceability;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Persistence.Traceability;

namespace Diten.InventoryService.Api.Traceability;

/// <summary>
/// MOD-0174 slice P1 wiring — one call from <c>Program.cs</c>, so the shared layer registrations (Application /
/// Infrastructure / Persistence <c>DependencyInjection</c>) stay untouched. MediatR handlers and validators of the
/// Traceability feature are already picked up by the Application assembly scan. Slice P2 wires the ledger to the lot
/// master: the in-process <see cref="ILotMasterClient"/> (<see cref="InventoryLotDirectory"/>) and the request-scoped
/// <see cref="LedgerTransactionBinding"/> through which the ledger repository lends it its posting transaction.
/// </summary>
public static class TraceabilityServiceCollectionExtensions
{
    public static IServiceCollection AddTraceability(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TraceWriteSupport>();
        services.AddScoped<ILotRepository, LotRepository>();
        services.AddScoped<ISerialRepository, SerialRepository>();
        services.AddScoped<ILotExpirySweeper, LotExpirySweeper>();
        services.AddSingleton<ILotStatusChangeGate, AlwaysAllowLotStatusChangeGate>();
        // MOD-0176 F2 (KF-D): no lot-date rule by default; AddExpiry replaces the gate.
        services.AddScoped<ILotDateOverrideGate, NoLotDateOverrideGate>();

        // WP-0174-P2 — the ledger link (pack §8), in-process; Trace:EnforceRegisteredLots defaults to false (ASSUMPTION-0174-10).
        services.AddScoped<LedgerTransactionBinding>();
        services.AddScoped<ILotMasterClient, InventoryLotDirectory>();
        // WP-0174-P4 — typed-lot migration: the pre-check → posting-transaction hand-over (K4-2 / K4-4) and the store.
        services.AddScoped<TypedLotPostingGuard>();
        services.AddScoped<IManualLotMigrationRepository, ManualLotMigrationRepository>();
        services.AddScoped<ManualLotGroupReader>();
        services.Configure<LotLedgerOptions>(configuration.GetSection(LotLedgerOptions.SectionName));

        services.Configure<LotExpirySweeperOptions>(configuration.GetSection(LotExpirySweeperOptions.SectionName));
        services.AddHostedService<LotExpirySweeperHostedService>();

        // PRODUCT-MASTER getSku with GTIN (K-B): same base URL, timeout and caller-header forwarding as the ledger client.
        var upstream = configuration.GetSection(UpstreamServicesOptions.SectionName).Get<UpstreamServicesOptions>() ?? new UpstreamServicesOptions();
        var productMaster = Uri.TryCreate(upstream.ProductMasterBaseUrl, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"Configuration error: '{UpstreamServicesOptions.SectionName}:ProductMasterBaseUrl' must be an absolute URL.");
        var timeout = TimeSpan.FromSeconds(upstream.TimeoutSeconds > 0 ? upstream.TimeoutSeconds : 5);
        services.AddHttpClient<ITraceSkuDirectory, TraceSkuDirectoryHttpClient>(http =>
            {
                http.BaseAddress = productMaster;
                http.Timeout = timeout;
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();

        return services;
    }
}
