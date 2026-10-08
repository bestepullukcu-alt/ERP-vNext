using Diten.InventoryService.Api.Controllers.Quarantine;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Handlers;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Infrastructure.Eventing;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Infrastructure.Workflow;
using Diten.InventoryService.Persistence;
using Diten.InventoryService.Persistence.Quarantine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Api.Quarantine;

/// <summary>
/// MOD-0175 slice Q1 wiring — one call from <c>Program.cs</c> (MOD-0174 <c>AddTraceability</c> pattern), so the shared layer
/// registrations stay untouched. MediatR handlers are picked up by the Application assembly scan.
/// <para><b>Approval port.</b> Production default since Q2: <see cref="WorkflowApprovalPort"/> — MOD-0023 over the Gateway with the
/// caller's token (<see cref="GatewayWorkflowClient"/>, <c>Gateway:BaseUrl</c>); outcomes come back by read-time reconciliation
/// (<see cref="QuarantineWorkflowReconciler"/>). <see cref="InternalTestApprovalPort"/> + its outcome endpoint exist only when
/// <c>Quarantine:TestApprovalPort:Enabled = true</c> AND <c>Mongo:DatabaseName</c> is a DB-010 test database; enabled on any
/// other database the service refuses to start.</para>
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
        services.AddScoped<QuarantineOutcomeApplier>();
        services.AddScoped<QuarantineStockActions>();
        services.AddScoped<QuarantineDecisionChecks>();
        services.AddScoped<QuarantineReads>();
        services.AddScoped<QuarantineWorkflowReconciler>();
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

        // Approval port, chosen at runtime from options (test hosts override configuration after registration).
        services.Configure<QuarantineTestApprovalOptions>(configuration.GetSection(QuarantineTestApprovalOptions.SectionName));
        services.AddScoped<IQuarantineApprovalPort>(sp =>
            sp.GetRequiredService<IOptions<QuarantineTestApprovalOptions>>().Value.Enabled
                ? new InternalTestApprovalPort()
                : ActivatorUtilities.CreateInstance<WorkflowApprovalPort>(sp));
        services.AddHostedService<QuarantineTestApprovalGate>();
        services.AddSingleton<IConfigureOptions<MvcOptions>, QuarantineTestApprovalMvcSetup>();
        return services;
    }
}

/// <summary>Startup gate: the test approval port on a database that is not a DB-010 test database stops the host.</summary>
public sealed class QuarantineTestApprovalGate(IOptions<QuarantineTestApprovalOptions> options, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var database = configuration["Mongo:DatabaseName"] ?? string.Empty;
        if (options.Value.Enabled && !InventoryTestDatabases.IsTestDatabase(database))
        {
            throw new InvalidOperationException(
                $"Configuration error: '{QuarantineTestApprovalOptions.SectionName}:Enabled' is allowed on a DB-010 test database only (database '{database}').");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Adds <see cref="QuarantineTestApprovalConvention"/> with the runtime option.</summary>
public sealed class QuarantineTestApprovalMvcSetup(IOptions<QuarantineTestApprovalOptions> options) : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions mvc) => mvc.Conventions.Add(new QuarantineTestApprovalConvention(options.Value.Enabled));
}

/// <summary>Removes <see cref="QuarantineTestApprovalController"/> from the application model unless the test port is enabled.</summary>
public sealed class QuarantineTestApprovalConvention(bool enabled) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        if (enabled)
        {
            return;
        }

        foreach (var controller in application.Controllers.Where(c => c.ControllerType.AsType() == typeof(QuarantineTestApprovalController)).ToList())
        {
            application.Controllers.Remove(controller);
        }
    }
}
