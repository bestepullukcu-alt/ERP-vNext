using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Infrastructure.WavePlanning;
using Diten.InventoryService.Persistence.WavePlanning;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.WavePlanning;

/// <summary>
/// MOD-0180 DL-1 wiring — one call from <c>Program.cs</c> (the <c>AddWarehouse</c> / <c>AddCycleCount</c> pattern; pack §5 "kompozisyon kökü").
/// MediatR handlers and validators are picked up by the Application assembly scan. DL-1: the wave store, the read-only view of MOD-0178's
/// outbounds and of LOCATION, the own clock, the form / number checks (the number from MOD-0178's temporary counter, type WAV — Q-4 = A; its
/// registration is MOD-0178's <c>AddWarehouse</c>), the responsible-user port over the existing AUTH adapter, the fail-closed index gate and the
/// maintenance service (it sends no command in DL-1).
/// </summary>
public static class WavePlanningServiceCollectionExtensions
{
    public static IServiceCollection AddWavePlanning(this IServiceCollection services)
    {
        services.TryAddSingleton(new WaveTime(TimeProvider.System));
        services.AddScoped<WaveSupport>();
        services.AddScoped<IWaveRepository, WaveRepository>();
        services.AddScoped<IWaveInventoryReader, WaveInventoryReader>();
        services.AddScoped<IWaveResponsibleDirectory, WaveResponsibleDirectory>();
        services.AddScoped<WaveChecks>();

        // Index gate (fail-closed): the unique indexes carry the number, the request keys and "an order in one active wave" (D-8).
        services.AddHostedService<WaveIndexInitializerHostedService>();

        // Maintenance (pack §8.8): registered once, resolvable by the tests, started by the host.
        services.AddSingleton<WaveMaintenanceHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<WaveMaintenanceHostedService>());
        return services;
    }
}
