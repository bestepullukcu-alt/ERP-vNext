using Diten.InventoryService.Application.Features.Expiry.Handlers;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Workflow;
using Diten.InventoryService.Persistence.Expiry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// MOD-0176 slice F1 wiring — one call from <c>Program.cs</c> (the <c>AddQuarantine</c> / <c>AddTraceability</c> pattern), so the
/// shared registrations stay untouched. MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Signature gate (F3).</b> MOD-0022 over the Gateway with the caller's token: <see cref="ESignExpiryResolutionSignature"/> and
/// <see cref="ESignLotDateOverrideSignature"/> (getSignature + the local subject hash, CT decision F3-A); MOD-0022 unreadable → refused.</para>
/// </summary>
public static class ExpiryServiceCollectionExtensions
{
    public static IServiceCollection AddExpiry(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ExpirySupport>();
        services.AddScoped<ExpiryReads>();
        services.AddScoped<ExpiryLotView>(); // F4 reads
        services.AddScoped<IExpiryRepository, ExpiryRepository>();
        // F3 — MOD-0022 (CT decision F3-A: getSignature + the local fx-v1 / do-v1 hash); F1 / F2 fail-closed classes stay as fallbacks.
        services.AddScoped<IExpiryResolutionSignature, ESignExpiryResolutionSignature>();
        services.AddScoped<IExpirySubjectDirectory, ExpirySubjectDirectory>();
        // WP-MVP1-FIX-LIVE2 / FU-CLEAN1: evidence person names — the ONE shared reader (central audit queue, read only)
        services.TryAddScoped<Application.Common.IAuditActorNames, Persistence.CentralAuditActorNames>();
        var gateway = configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new GatewayOptions();
        var gatewayUri = Uri.TryCreate(gateway.BaseUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Configuration error: '{GatewayOptions.SectionName}:BaseUrl' must be an absolute URL.");
        services.AddHttpClient<IExpiryESignClient, GatewayExpiryESignClient>(http =>
            {
                http.BaseAddress = gatewayUri;
                http.Timeout = TimeSpan.FromSeconds(gateway.TimeoutSeconds > 0 ? gateway.TimeoutSeconds : 10);
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.AddHostedService<ESignActionRegistrationHostedService>(); // one call per module (quarantine + expiry); deduplicated with AddQuarantine's

        // F2 — the hooks: the ledger's posting policy (CT-F2) and MOD-0174's lot-date gate (KF-D), replaced here; KF-D signature fail-closed until F3.
        services.AddScoped<ExpiryHookWriter>();
        services.AddScoped<FefoCandidateReader>(); // F4: the one FEFO candidate read (posting hook + FEFO order)
        services.AddScoped<IExpiryReadModel, ExpiryReadModel>();
        services.AddScoped<ExpiryEvidenceBuilder>(); // F5 Kanıt Paketi (preview + files)
        services.AddSingleton<Application.Features.Expiry.Ports.IExpiryEvidencePdfRenderer, ExpiryEvidencePdfRenderer>();
        services.RemoveAll<Application.Features.Expiry.Ports.IExpiryPostingPolicy>();
        services.AddScoped<Application.Features.Expiry.Ports.IExpiryPostingPolicy, ExpiryPostingPolicy>();
        services.RemoveAll<Application.Features.Traceability.Ports.ILotDateOverrideGate>();
        services.AddScoped<Application.Features.Traceability.Ports.ILotDateOverrideGate, LotDateOverrideGate>();
        services.AddScoped<ILotDateOverrideSignature, ESignLotDateOverrideSignature>();

        // Index gate first (fail-closed), then the expired-on-hand / SLA sweeper (start + periodic, pack §8.5).
        services.AddHostedService<ExpiryIndexInitializerHostedService>();
        services.Configure<ExpirySweeperOptions>(configuration.GetSection(ExpirySweeperOptions.SectionName));
        services.AddScoped<ExpiredOnHandSweeper>();
        services.AddHostedService<ExpiredOnHandSweeperHostedService>();
        return services;
    }
}
