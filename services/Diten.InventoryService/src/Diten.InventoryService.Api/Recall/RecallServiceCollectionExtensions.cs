using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Application.Features.Recall.Trace;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Infrastructure.Recall;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Workflow;
using Diten.InventoryService.Persistence.Recall;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// MOD-0177 slice R1 wiring — one call from <c>Program.cs</c> (the <c>AddExpiry</c> pattern), so the shared registrations stay untouched.
/// MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Signature gate (R4):</b> <see cref="ESignRecallSignature"/> — MOD-0022 over the Gateway with the caller's token (getSignature + the local
/// subject hash, CT decision F3-A); MOD-0022 unreadable → refused. No approval, completion or closure passes without a verified signature.</para>
/// </summary>
public static class RecallServiceCollectionExtensions
{
    public static IServiceCollection AddRecall(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<RecallSupport>();
        services.AddScoped<RecallReads>();
        services.AddScoped<IRecallRepository, RecallRepository>();
        services.AddScoped<IRecallActorRecords, RecallActorRecords>(); // WP-MVP1-NAMES1: who did an action (events / audit rows), batch reads
        services.AddScoped<RecallActorNames>();
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(RecallPersonNamesBehavior<,>)); // RECALL 1.8.0: every recall answer named in one place
        // R4 — MOD-0022 (CT decision F3-A: getSignature + the local rc-v1 / rs-v1 hash from the provider's own function); FailClosedRecallSignature
        // (R1) stays as the fallback type. Registration of the five recall actions rides the shared hosted service (one call per module).
        services.AddScoped<IRecallSignature, ESignRecallSignature>();
        services.AddScoped<RecallSubjectFacts>();
        services.AddScoped<Application.Features.Recall.Evidence.RecallEvidenceBuilder>(); // R7b Kanıt Paketi (preview + files)
        services.AddSingleton<Application.Features.Recall.Ports.IRecallEvidencePdfRenderer, RecallEvidencePdfRenderer>();
        // WP-MVP1-FIX-LIVE2: person names of the pack — the event actor's name at event time, and the central audit queue's name (read only)
        services.AddScoped<Application.Features.Recall.Ports.IRecallActorName, RecallActorName>();
        services.TryAddScoped<Application.Common.IAuditActorNames, Diten.InventoryService.Persistence.CentralAuditActorNames>(); // FU-CLEAN1: the shared reader
        services.AddScoped<IRecallSubjectDirectory, RecallSubjectDirectory>();

        // R2: the trace engine over in-process reads (pack §8.2) and the MVP-6 shipment read through the Gateway with the caller's
        // token + company (pack §8.6 — no cache; the reader bounds each read to 2 s and 8 in flight)
        services.AddScoped<IRecallTraceSource, RecallTraceSource>();
        services.AddScoped<IRecallDashboardSource, RecallDashboardSource>(); // R6: the panel's company-bound ledger read (pack §8.9)
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
        services.AddHttpClient<IRecallESignClient, GatewayRecallESignClient>(http =>
            {
                http.BaseAddress = gatewayUri;
                http.Timeout = TimeSpan.FromSeconds(gateway.TimeoutSeconds > 0 ? gateway.TimeoutSeconds : 10);
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.AddHostedService<ESignActionRegistrationHostedService>(); // deduplicated with AddQuarantine's / AddExpiry's

        // R3 (pack §8.4 / §8.5 / §8.6, CT-R11 / CT-R12): the recall exit block and the "under recall?" read replace the ledger's defaults (the 0176
        // RemoveAll + AddScoped pattern); the KR-B dispatcher + its 60 s sweeper; the in-process ledger reads of the stock / returns tabs
        services.RemoveAll<IRecallPostingGate>();
        services.RemoveAll<IRecallLotStatusReader>();
        services.AddScoped<RecallPostingGate>();
        services.AddScoped<IRecallPostingGate>(sp => sp.GetRequiredService<RecallPostingGate>());
        services.AddScoped<IRecallLotStatusReader>(sp => sp.GetRequiredService<RecallPostingGate>());
        services.AddScoped<IRecallLedgerReader, RecallLedgerReads>();
        services.AddScoped<Application.Features.Recall.Quarantine.RecallQuarantineDispatcher>();
        // WP-MVP1-FU-CLEAN1 (FU-0177-SWEEP-01): the retry limit of the sweeper (Inventory:Recall:QuarantineSweep:MaxDeferredAttempts, default 10)
        services.Configure<RecallQuarantineSweepOptions>(configuration.GetSection(RecallQuarantineSweepOptions.SectionName));
        services.AddSingleton<RecallQuarantineSweeper>();
        services.AddHostedService<RecallQuarantineSweeperHostedService>();

        // R5 (pack §8.10, KR-G): the drill store (drill records + a Drill snapshot + counter + audit — nothing else), the read-only candidate
        // reads of the Auto pick and its randomness (Random.Shared; the tests inject a seed)
        services.AddScoped<IRecallDrillRepository, RecallDrillRepository>();
        services.AddScoped<Application.Features.Recall.Drills.IRecallDrillCandidates, RecallDrillCandidates>();
        services.TryAddSingleton<Application.Features.Recall.Drills.IRecallDrillRandom, Application.Features.Recall.Drills.SharedRecallDrillRandom>();

        // Index gate (fail-closed): the unique indexes carry idempotency and "one active case per root lot".
        services.AddHostedService<RecallIndexInitializerHostedService>();
        return services;
    }
}
