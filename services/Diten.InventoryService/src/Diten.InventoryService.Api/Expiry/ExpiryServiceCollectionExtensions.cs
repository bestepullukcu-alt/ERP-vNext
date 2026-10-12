using Diten.InventoryService.Application.Features.Expiry.Handlers;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Persistence.Expiry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// MOD-0176 slice F1 wiring — one call from <c>Program.cs</c> (the <c>AddQuarantine</c> / <c>AddTraceability</c> pattern), so the
/// shared registrations stay untouched. MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Signature gate.</b> Production default until F3: <see cref="FailClosedExpiryResolutionSignature"/> — every resolution is
/// refused (no signature → 400, a signature → 409), so nothing is resolved unsigned or unverified. F3 replaces it with the MOD-0022
/// implementation. Tests bring their own double.</para>
/// </summary>
public static class ExpiryServiceCollectionExtensions
{
    public static IServiceCollection AddExpiry(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ExpirySupport>();
        services.AddScoped<ExpiryReads>();
        services.AddScoped<IExpiryRepository, ExpiryRepository>();
        services.AddScoped<IExpiryResolutionSignature, FailClosedExpiryResolutionSignature>();

        // Index gate first (fail-closed), then the expired-on-hand / SLA sweeper (start + periodic, pack §8.5).
        services.AddHostedService<ExpiryIndexInitializerHostedService>();
        services.Configure<ExpirySweeperOptions>(configuration.GetSection(ExpirySweeperOptions.SectionName));
        services.AddScoped<ExpiredOnHandSweeper>();
        services.AddHostedService<ExpiredOnHandSweeperHostedService>();
        return services;
    }
}
