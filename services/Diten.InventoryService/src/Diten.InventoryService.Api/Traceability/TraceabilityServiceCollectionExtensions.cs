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
/// Traceability feature are already picked up by the Application assembly scan. The ledger is NOT wired to the lot master
/// here (in-process <c>ILotMasterClient</c> = slice P2).
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
