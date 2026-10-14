using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Infrastructure.Recall;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Workflow;
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

        // R2: the trace engine over in-process reads (pack §8.2) and the MVP-6 shipment read through the Gateway with the caller's
        // token + company (pack §8.6 — no cache; the reader bounds each read to 2 s and 8 in flight)
        services.AddScoped<IRecallTraceSource, RecallTraceSource>();
        services.AddScoped<RecallTraceEngine>();
        services.AddScoped<RecallTraceService>();
        var gateway = configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new GatewayOptions();
        var gatewayUri = Uri.TryCreate(gateway.BaseUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Configuration error: '{GatewayOptions.SectionName}:BaseUrl' must be an absolute URL.");
        services.AddHttpClient<IShipmentReader, ShipmentBundleHttpReader>(http =>
            {
                http.BaseAddress = gatewayUri;
                http.Timeout = TimeSpan.FromSeconds(10); // the per-read 2 s bound is the reader's own
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();

        // R3 (pack §8.4 / §8.5 / §8.6, CT-R11 / CT-R12): the recall exit block and the "under recall?" read replace the ledger's defaults (the 0176
        // RemoveAll + AddScoped pattern); the KR-B dispatcher + its 60 s sweeper; the in-process ledger reads of the stock / returns tabs
        services.RemoveAll<IRecallPostingGate>();
        services.RemoveAll<IRecallLotStatusReader>();
        services.AddScoped<RecallPostingGate>();
        services.AddScoped<IRecallPostingGate>(sp => sp.GetRequiredService<RecallPostingGate>());
        services.AddScoped<IRecallLotStatusReader>(sp => sp.GetRequiredService<RecallPostingGate>());
        services.AddScoped<IRecallLedgerReader, RecallLedgerReads>();
        services.AddScoped<Application.Features.Recall.Quarantine.RecallQuarantineDispatcher>();
        services.AddSingleton<RecallQuarantineSweeper>();
        services.AddHostedService<RecallQuarantineSweeperHostedService>();

        // Index gate (fail-closed): the unique indexes carry idempotency and "one active case per root lot".
        services.AddHostedService<RecallIndexInitializerHostedService>();
        return services;
    }
}
