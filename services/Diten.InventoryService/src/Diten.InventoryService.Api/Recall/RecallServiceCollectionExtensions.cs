using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Persistence.Recall;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// MOD-0177 slice R1 wiring — one call from <c>Program.cs</c> (the <c>AddExpiry</c> pattern), so the shared registrations stay untouched.
/// MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Signature gate:</b> <see cref="FailClosedRecallSignature"/> until R4 brings the MOD-0022 implementation — no approval, completion or
/// closure passes in production without a verified signature (0176 F1 precedent).</para>
/// </summary>
public static class RecallServiceCollectionExtensions
{
    public static IServiceCollection AddRecall(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RecallSupport>();
        services.AddScoped<RecallReads>();
        services.AddScoped<IRecallRepository, RecallRepository>();
        services.AddScoped<IRecallSignature, FailClosedRecallSignature>();

        // Index gate (fail-closed): the unique indexes carry idempotency and "one active case per root lot".
        services.AddHostedService<RecallIndexInitializerHostedService>();
        return services;
    }
}
