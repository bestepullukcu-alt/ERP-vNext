using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.Platform.Application.Contracts.Eventing;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Eventing;

/// <summary>
/// MOD-0175 Q2 (pack §8 "Outcome event") — applies MOD-0023's <c>platform.workflow.instance.completed.v1</c> for
/// <c>objectType = quarantine.decision</c>. The CRM <c>ClaimWorkflowOutcomeConsumer</c> pattern: broker-independent
/// <see cref="ConsumeAsync"/>, inbox idempotency (<c>quarantine_event_inbox</c>, one row per EventId).
/// <para><b>Trust.</b> Platform publishes UNSIGNED today, so an event is applied only when it BINDS to state 0175 wrote
/// itself: the tenant owns the decision, and the event's instance id equals the instance id 0175 stored when MOD-0023
/// accepted the start (checked by <see cref="QuarantineOutcomeApplier"/>) of a decision still waiting for approval.
/// An approval whose approver cannot be read is NOT applied here (the four-eyes check needs the real person) — read-time
/// reconciliation stays the guaranteed path.</para>
/// <para>The 0175 scope (tenant + the decision's company) is set on a fresh DI scope BEFORE the applier is resolved: the
/// quarantine repositories capture the scope when they are constructed.</para>
/// </summary>
public sealed class QuarantineWorkflowOutcomeConsumer : IConsumer<EventTransportMessage>
{
    public const string CompletedEventName = "platform.workflow.instance.completed.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<QuarantineWorkflowOutcomeConsumer> _logger;

    public QuarantineWorkflowOutcomeConsumer(IServiceScopeFactory scopes, ILogger<QuarantineWorkflowOutcomeConsumer> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public enum Outcome
    {
        Ignored,
        Rejected,
        Duplicate,
        Processed
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context) => ConsumeAsync(context.Message, context.CancellationToken);

    public async Task<Outcome> ConsumeAsync(EventTransportMessage message, CancellationToken ct = default)
    {
        if (!string.Equals(message.EventName, CompletedEventName, StringComparison.Ordinal))
        {
            return Outcome.Ignored; // not ours
        }

        var payload = Deserialize(message);
        if (payload is null || !string.Equals(payload.ObjectType, QuarantineWorkflowTemplate.ObjectType, StringComparison.Ordinal))
        {
            return Outcome.Ignored; // another module's workflow
        }

        var tenantId = payload.TenantId != Guid.Empty ? payload.TenantId : message.TenantId ?? Guid.Empty;
        if (tenantId == Guid.Empty || (message.TenantId is { } envelopeTenant && envelopeTenant != tenantId)
            || !Guid.TryParse(payload.ObjectId, out var decisionId) || payload.WorkflowInstanceId == Guid.Empty
            || payload.Outcome is null || QuarantineWorkflowReconciler.Map(payload.Outcome) is not { } mapped)
        {
            _logger.LogWarning("quarantine.workflow.event_rejected EventId={EventId} reason=shape_or_tenant", message.EventId);
            return Outcome.Rejected;
        }

        var actorParsed = Guid.TryParse(payload.CompletedBy, out var actor);
        if (mapped == ApprovalOutcome.Approved && !actorParsed)
        {
            _logger.LogWarning("quarantine.workflow.event_rejected EventId={EventId} reason=approver_unknown", message.EventId);
            return Outcome.Rejected;
        }

        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;
        var inbox = services.GetRequiredService<IQuarantineEventInbox>();
        if (await inbox.DecisionCompanyAsync(tenantId, decisionId, ct) is not { } company)
        {
            _logger.LogWarning("quarantine.workflow.event_rejected EventId={EventId} reason=unknown_decision", message.EventId);
            return Outcome.Rejected;
        }

        services.GetRequiredService<ITenantContext>().SetScope(tenantId, company, actorParsed ? actor : Guid.Empty);
        services.GetRequiredService<ICorrelationContext>().Set(message.CorrelationId != Guid.Empty ? message.CorrelationId : Guid.NewGuid());

        if (!await inbox.TryInsertAsync(message.EventId, message.EventName, tenantId, ct))
        {
            _logger.LogInformation("quarantine.workflow.event_duplicate EventId={EventId}", message.EventId);
            return Outcome.Duplicate;
        }

        var result = await services.GetRequiredService<QuarantineOutcomeApplier>()
            .ApplyOutcomeAsync(decisionId, mapped, actorParsed ? actor : Guid.Empty, payload.WorkflowInstanceId.ToString("D"), ct);
        _logger.LogInformation(
            "quarantine.workflow.event_processed EventId={EventId} Decision={Decision} WorkflowInstanceId={InstanceId} Outcome={Outcome} Result={Result}",
            message.EventId, decisionId, payload.WorkflowInstanceId, payload.Outcome, result.IsSuccessful ? result.Data.ToString() : result.ErrorCode);
        return Outcome.Processed;
    }

    private CompletedPayload? Deserialize(EventTransportMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<CompletedPayload>(message.PayloadJson, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "quarantine.workflow.event_payload_invalid EventId={EventId}", message.EventId);
            return null;
        }
    }

    // Minimal projection of WorkflowInstanceCompletedV1 (the CRM consumer's projection).
    private sealed record CompletedPayload(
        Guid TenantId,
        Guid WorkflowInstanceId,
        string? ObjectType,
        string? ObjectId,
        string? Outcome,
        DateTimeOffset? CompletedAt,
        string? CompletedBy,
        string? ReasonCode);
}

/// <summary>Q2 transport switch (CrmService <c>AddClaimWorkflowEventing</c> pattern): InMemory (default) ⇒ no MassTransit at all.</summary>
public static class QuarantineEventingServiceCollectionExtensions
{
    public static IServiceCollection AddQuarantineEventing(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(QuarantineEventingOptions.SectionName);
        services.Configure<QuarantineEventingOptions>(section);
        services.AddTransient<QuarantineWorkflowOutcomeConsumer>();
        var options = section.Get<QuarantineEventingOptions>() ?? new QuarantineEventingOptions();
        if (!options.UseRabbitMq)
        {
            return services;
        }

        services.AddMassTransit(x =>
        {
            x.AddConsumer<QuarantineWorkflowOutcomeConsumer>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                    if (options.UseTls)
                    {
                        h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                    }
                });

                cfg.UseMessageRetry(r => r.Exponential(
                    options.RetryCount,
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                    TimeSpan.FromSeconds(options.InitialRetryDelaySeconds)));
                cfg.ConfigureEndpoints(context);
            });
        });
        return services;
    }
}
