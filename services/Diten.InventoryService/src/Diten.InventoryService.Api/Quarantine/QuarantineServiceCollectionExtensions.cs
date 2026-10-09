using Diten.InventoryService.Api.Controllers.Quarantine;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Handlers;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Infrastructure.Eventing;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Workflow;
using Diten.InventoryService.Persistence.Quarantine;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Api.Quarantine;

/// <summary>
/// MOD-0175 slice Q1 wiring — one call from <c>Program.cs</c> (MOD-0174 <c>AddTraceability</c> pattern), so the shared layer
/// registrations stay untouched. MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Approval port.</b> Production default since Q2: <see cref="WorkflowApprovalPort"/> — MOD-0023 over the Gateway with the
/// caller's token (<see cref="GatewayWorkflowClient"/>, <c>Gateway:BaseUrl</c>); outcomes come back by read-time reconciliation
/// (<see cref="QuarantineWorkflowReconciler"/>). It is the ONLY approval port (Q5, O-Q2-5: the Q1 test approval port and its outcome
/// endpoint are gone from production code; the test suite brings its own).</para>
/// </summary>
public static class QuarantineServiceCollectionExtensions
{
    public static IServiceCollection AddQuarantine(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<QuarantineSupport>();
        services.AddScoped<QuarantineFacts>();
        services.AddScoped<QuarantineMovementPoster>();
        services.AddScoped<QuarantineIntakeService>();
        services.AddScoped<QuarantineApplyEngine>();
        services.TryAddSingleton(TimeProvider.System); // the applier's in-flight grace clock (tests pass their own)
        services.AddScoped<QuarantineOutcomeApplier>();
        services.AddScoped<QuarantineStockActions>();
        services.AddScoped<QuarantineDecisionChecks>();
        services.AddScoped<QuarantineReads>();
        services.AddScoped<QuarantineWorkflowReconciler>();
        // Q3 — MOD-0022: the qd-v1 subject, the decider-signature check on submit, the signable actions registered at start.
        services.AddScoped<QuarantineSignableSubjectBuilder>();
        services.AddScoped<QuarantineDeciderSignature>();
        services.AddHostedService<ESignActionRegistrationHostedService>();
        services.AddScoped<IQuarantineEventInbox, QuarantineEventInboxRepository>();
        services.AddQuarantineEventing(configuration); // completion-event consumer; MassTransit only when Eventing:Transport = RabbitMQ

        services.AddScoped<IQuarantineRepository, QuarantineRepository>();
        services.AddScoped<IQuarantineDecisionRepository, QuarantineDecisionRepository>();
        services.AddScoped<IQuarantineLedgerReader, QuarantineLedgerReader>();
        services.AddScoped<IQuarantineIntakeFeed, QuarantineIntakeFeed>();

        // MOD-0173 seam: the post-commit intake observer (pack §5 Q1) + the sweep that covers a missed call.
        services.AddScoped<IPostedMovementObserver, QuarantineIntakeObserver>();
        services.Configure<QuarantineSweeperOptions>(configuration.GetSection(QuarantineSweeperOptions.SectionName));
        services.AddSingleton<QuarantineIntakeSweeper>();
        services.AddHostedService<QuarantineIntakeSweeperHostedService>();

        // Q4 (KQ-H, CT-Q16): the retest hold — sweeper (start + periodic, TimeProvider) and the one-lot refresh of a retest release.
        services.AddScoped<RetestHoldSweeper>();
        services.AddScoped<ILotRetestHold>(sp => sp.GetRequiredService<RetestHoldSweeper>());
        services.AddHostedService<RetestHoldSweeperHostedService>();

        // Q4 (KQ-I): the 0175 lot gate replaces MOD-0174's AlwaysAllow (scoped: it reads the company's settings), the 202 approval
        // request and the approved-change source of ApplyApprovedLotStatusChange.
        services.RemoveAll<ILotStatusChangeGate>();
        services.AddScoped<ILotStatusChangeGate, QuarantineLotStatusChangeGate>();
        services.AddScoped<QuarantineLotReleaseApprovals>();
        services.AddScoped<ILotStatusChangeApprovals>(sp => sp.GetRequiredService<QuarantineLotReleaseApprovals>());
        services.AddScoped<IApprovedLotChanges>(sp => sp.GetRequiredService<QuarantineLotReleaseApprovals>());

        // Q2: MOD-0023 over the Gateway, the caller's bearer forwarded; no service token exists on this path (pack §8).
        var gateway = configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new GatewayOptions();
        var gatewayUri = Uri.TryCreate(gateway.BaseUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Configuration error: '{GatewayOptions.SectionName}:BaseUrl' must be an absolute URL.");
        services.AddHttpClient<IQuarantineWorkflowClient, GatewayWorkflowClient>(http =>
            {
                http.BaseAddress = gatewayUri;
                http.Timeout = TimeSpan.FromSeconds(gateway.TimeoutSeconds > 0 ? gateway.TimeoutSeconds : 10);
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();
        services.AddHttpClient<IQuarantineESignClient, GatewayESignClient>(http =>
            {
                http.BaseAddress = gatewayUri;
                http.Timeout = TimeSpan.FromSeconds(gateway.TimeoutSeconds > 0 ? gateway.TimeoutSeconds : 10);
            })
            .AddHttpMessageHandler<ForwardCallerHeadersHandler>();

        // The approval port: MOD-0023 Workflow, nothing else (DEC-INV-08, KQ-F; Q5 O-Q2-5).
        services.AddScoped<IQuarantineApprovalPort, WorkflowApprovalPort>();
        return services;
    }
}
