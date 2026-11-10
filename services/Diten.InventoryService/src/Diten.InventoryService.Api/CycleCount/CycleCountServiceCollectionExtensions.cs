using Diten.InventoryService.Application.Features.CountVariances.Handlers.QueryHandlers;
using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Infrastructure.CycleCounting;
using Diten.InventoryService.Persistence.CycleCounting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.CycleCounting;

/// <summary>
/// MOD-0181 DL-1 wiring — one call from <c>Program.cs</c> (the <c>AddWarehouse</c> pattern; pack §5 "kompozisyon kökü"). MediatR handlers and
/// validators are picked up by the Application assembly scan. DL-1: the count store with its one snapshot read (the release probe — production
/// no-op), the own clock, the form / number checks (the number from MOD-0178's temporary counter, type CNT — Q-7 = B; its registration is
/// MOD-0178's <c>AddWarehouse</c>), the PRODUCT-MASTER serial flags, the assignee port over the existing AUTH adapter and the fail-closed index gate.
/// </summary>
public static class CycleCountServiceCollectionExtensions
{
    public static IServiceCollection AddCycleCount(this IServiceCollection services)
    {
        services.TryAddSingleton(new CycleCountTime(TimeProvider.System));
        services.TryAddScoped<ICycleCountReleaseProbe, NoopCycleCountReleaseProbe>();
        services.TryAddScoped<ICycleCountSubmitProbe, NoopCycleCountSubmitProbe>();
        services.AddScoped<CycleCountSupport>();
        services.AddScoped<ICycleCountRepository, CycleCountRepository>();
        services.AddScoped<ICycleCountAssigneeDirectory, CycleCountAssigneeDirectory>();
        services.AddScoped<CycleCountChecks>();
        services.AddScoped<CycleCountProductFlags>();

        // DL-3: the ONE stock door (pack §8.6, AC-29), the posting engine and the line writer, the Variance Trace reads, the sweeper (it only sends
        // the reconcile command; F-18). IPostingKeyLookup is MOD-0173's additive port, registered once by AddWarehouse (TryAdd there).
        services.TryAddScoped<ICycleCountMovementPoster, CycleCountMovementPoster>();
        services.AddScoped<CycleCountLineWriter>();
        services.AddScoped<CycleCountPostingEngine>();
        services.AddScoped<CountVarianceReads>();
        services.AddScoped<ICycleCountPendingScan, CycleCountPendingScan>();
        services.AddOptions<CycleCountReconcilerOptions>().BindConfiguration(CycleCountReconcilerOptions.Section);
        services.AddSingleton<CycleCountReconciler>();
        services.AddHostedService<PendingCountAdjustmentReconcilerHostedService>();

        // Index gate (fail-closed): the unique indexes carry the number, the request keys and "one open count per bin" (F-6).
        services.AddHostedService<CycleCountIndexInitializerHostedService>();
        return services;
    }
}
