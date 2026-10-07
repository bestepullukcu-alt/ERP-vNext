using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class TenantLifecycleNotificationConsumer : IConsumer<EventTransportMessage>
{
    public const string ConsumerName = nameof(TenantLifecycleNotificationConsumer);

    // MOD-0027-FU04C — suspended/reactivated dispatch by canonical eventCode (FU04A PlatformSeed events) via the
    // FU04B adapter. BL-454 slice 2 stage D — the created branch INVITES the initial administrator (AuthService account +
    // the one-time set-password link, IAdminUserInvitationService), the same path as the tenant screen's "Invite".
    private const string SuspendedEventCode = "tenant.lifecycle.suspended";
    private const string ReactivatedEventCode = "tenant.lifecycle.reactivated";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsumedEventStore _consumedEventStore;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly IMediator _mediator;
    private readonly IAdminUserInvitationService _invitations;
    private readonly TenantSuspendedV1NotificationMapper _suspendedMapper;
    private readonly TenantReactivatedV1NotificationMapper _reactivatedMapper;
    private readonly ILogger<TenantLifecycleNotificationConsumer> _logger;

    public TenantLifecycleNotificationConsumer(
        ConsumedEventStore consumedEventStore,
        ITenantRegistryRepository tenantRepository,
        IMediator mediator,
        IAdminUserInvitationService invitations,
        TenantSuspendedV1NotificationMapper suspendedMapper,
        TenantReactivatedV1NotificationMapper reactivatedMapper,
        ILogger<TenantLifecycleNotificationConsumer> logger)
    {
        _consumedEventStore = consumedEventStore;
        _tenantRepository = tenantRepository;
        _mediator = mediator;
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _suspendedMapper = suspendedMapper;
        _reactivatedMapper = reactivatedMapper;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context)
    {
        return ConsumeAsync(context.Message, context.CancellationToken);
    }

    public Task<ConsumedEventExecutionResult?> ConsumeAsync(
        EventTransportMessage message,
        CancellationToken cancellationToken = default)
    {
        return message.EventName switch
        {
            TenantCreatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantCreatedV1>(message),
                (tenant, envelope, ct) => InviteInitialAdministratorAsync(tenant, envelope.Payload.InitialAdminUserId, ct),
                cancellationToken),
            TenantSuspendedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantSuspendedV1>(message),
                async (tenant, envelope, ct) =>
                {
                    var recipients = ResolveTenantAdminRecipients(tenant);
                    var request = _suspendedMapper.Map(envelope, recipients, tenant.DefaultLanguage);
                    await DispatchByEventCodeIfMappedAsync(envelope, SuspendedEventCode, tenant, request, ct);
                },
                cancellationToken),
            TenantReactivatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantReactivatedV1>(message),
                async (tenant, envelope, ct) =>
                {
                    var recipients = ResolveTenantAdminRecipients(tenant);
                    var request = _reactivatedMapper.Map(envelope, recipients, tenant.DefaultLanguage);
                    await DispatchByEventCodeIfMappedAsync(envelope, ReactivatedEventCode, tenant, request, ct);
                },
                cancellationToken),
            _ => Task.FromResult<ConsumedEventExecutionResult?>(null)
        };
    }

    private async Task<ConsumedEventExecutionResult?> ConsumeTenantEventAsync<TEvent>(
        EventTransportMessage message,
        TEvent payload,
        Func<Tenant, EventEnvelope<TEvent>, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var envelope = new EventEnvelope<TEvent>(CreateMetadata(message), payload);
        var result = await _consumedEventStore.ExecuteOnceAsync(
            envelope,
            ConsumerName,
            async ct =>
            {
                var tenant = await _tenantRepository.GetByIdAsync(ResolveTenantId(envelope), ct);
                if (tenant is null)
                {
                    return;
                }

                await handler(tenant, envelope, ct);
            },
            cancellationToken);

        return result;
    }

    // MOD-0027-FU04C — dispatch a lifecycle notification by canonical eventCode (FU04B adapter). Decision A: the
    // consumer supplies TenantDisplayName (tenant.DisplayName -> Name -> Code -> Id) since the V1 payloads carry only
    // TenantId and the mapper signature is not widened. Decision B: a controlled catalog/validation failure (the
    // adapter sets Response.ReasonCode) is non-retryable -> log + swallow; a provider/transient failure (no
    // ReasonCode) preserves the existing throw so the transport retries. Business state is never rolled back.
    private async Task DispatchByEventCodeIfMappedAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        string eventCode,
        Tenant tenant,
        QueueEmailNotificationRequest? mapped,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        if (mapped is null)
        {
            return;
        }

        var tenantId = ResolveTenantId(envelope);
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in mapped.Variables)
        {
            variables[pair.Key] = pair.Value;
        }
        variables["TenantDisplayName"] = ResolveTenantDisplayName(tenant);

        var dispatchRequest = new NotificationEventDispatchRequest(
            tenantId,
            eventCode,
            mapped.To,
            variables,
            mapped.Locale,
            mapped.Cc,
            mapped.Bcc,
            envelope.CorrelationId.ToString("N"),
            mapped.CausationId);

        var response = await _mediator.Send(new DispatchNotificationByEventCodeCommand(dispatchRequest), cancellationToken);
        if (response.IsSuccessful)
        {
            return;
        }

        // Controlled catalog/validation 4xx (EVENT_NOT_FOUND / EVENT_NOT_ACTIVE / REQUIRED_VARIABLE_MISSING /
        // TEMPLATE_KEY_MISSING_OR_INVALID / INVALID_EVENT_CODE / RECIPIENT_MISSING): retrying will not fix config.
        if (!string.IsNullOrEmpty(response.ReasonCode))
        {
            _logger.LogWarning(
                "Tenant lifecycle notification skipped (non-retryable). EventName={EventName} EventCode={EventCode} ReasonCode={ReasonCode} StatusCode={StatusCode} Errors={Errors}",
                envelope.EventName,
                eventCode,
                response.ReasonCode,
                response.StatusCode,
                string.Join("; ", response.Errors));
            return;
        }

        // Provider/transient failure (no ReasonCode): preserve throw so the transport retries.
        throw new InvalidOperationException(
            $"Tenant lifecycle notification dispatch failed (retryable). EventName={envelope.EventName} EventCode={eventCode} StatusCode={response.StatusCode}");
    }

    private static string ResolveTenantDisplayName(Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.DisplayName))
        {
            return tenant.DisplayName!.Trim();
        }
        if (!string.IsNullOrWhiteSpace(tenant.Name))
        {
            return tenant.Name.Trim();
        }
        if (!string.IsNullOrWhiteSpace(tenant.Code))
        {
            return tenant.Code.Trim();
        }
        return tenant.Id.ToString();
    }

    /// <summary>
    /// BL-454 slice 2 stage D — the tenant's first administrator. Before this the created branch sent tenant.invite.email
    /// with the tenant's name and id only, to an administrator nobody had created in AuthService: an invitation with no
    /// way in. Now it is the invitation itself — the account in AuthService and the one-time set-password link — the same
    /// service the tenant screen's "Invite" uses, as the EVENT trigger.
    /// <para>FIX1 (1) — the event only ever CREATES: it acts only on an administrator still Invited, of a tenant that is
    /// neither suspended nor deactivated, and AuthService leaves an account that already exists exactly as it is (no reset,
    /// no reactivation, no link). A redelivered, re-published or retried event therefore changes nothing.</para>
    /// <para>FIX1 (3) — the outcome is written to the tenant record (the administrator's invitation time and the
    /// "admin-invitation" provisioning step), so the tenant screen shows what happened. A failure to reach AuthService
    /// throws, so the transport retries (once per event: the consumed-event store).</para>
    /// </summary>
    private async Task InviteInitialAdministratorAsync(Tenant tenant, Guid? initialAdminUserId, CancellationToken ct)
    {
        var admin = initialAdminUserId is { } id
            ? tenant.AdminUsers.FirstOrDefault(user => user.Id == id && !string.IsNullOrWhiteSpace(user.Email))
            : null;
        var skip = admin is null ? "INITIAL_ADMIN_MISSING"
            : admin.Status != TenantAdminUserStatus.Invited ? "INITIAL_ADMIN_NOT_INVITED"
            : tenant.Status is TenantStatus.Suspended or TenantStatus.Deactivated ? "TENANT_NOT_ACTIVE"
            : null;
        if (skip is not null)
        {
            _logger.LogInformation(
                "tenant.admin_invitation.skipped TenantId={TenantId} AdminUserId={AdminUserId} ReasonCode={ReasonCode}",
                tenant.Id, admin?.Id, skip);
            return;
        }

        // Users quota (measured, FIX1 K4): the operator's "Invite" consumes a seat only for an administrator NOT yet counted
        // (TenantAdminUserSupport.CountsTowardsUsersQuota: Invited or Active). This path acts on Invited only — already
        // counted — so the same rule asks for nothing more here.
        var result = await _invitations.InviteAsync(tenant, admin!, AdminInvitationTrigger.TenantCreatedEvent, ct);
        _logger.LogInformation(
            "tenant.admin_invitation.done TenantId={TenantId} AdminUserId={AdminUserId} UserProvisioned={UserProvisioned} EmailSent={EmailSent} ReasonCode={ReasonCode}",
            tenant.Id, admin!.Id, result.UserProvisioned, result.InvitationEmailSent, result.EmailRefusalCode);
        await RecordInvitationOutcomeAsync(tenant.Id, admin.Id, result, ct);
    }

    /// <summary>The "admin-invitation" provisioning step's key (RegisterTenantCommandHandler writes it Pending).</summary>
    internal const string AdminInvitationStepKey = TenantProvisioningStep.AdminInvitationKey;

    // FIX1 (3) / FIX2 — the outcome on the tenant record, as a targeted write (K7: a suspension made meanwhile stays).
    // FIX2 (1) — on the event path "an account already exists" is NEVER a success: an invitation half-made by an earlier
    // attempt and an account somebody else made look the same from here, so the step says the state is unknown and how to
    // repair it (the operator's "Invite": a new link, the BL-529 reset, audited).
    private Task RecordInvitationOutcomeAsync(Guid tenantId, Guid adminId, AdminUserInvitationResult result, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var (status, detail, eventType) = result switch
        {
            { InvitationEmailSent: true } =>
                ("Completed", "Invitation sent: a one-time set-password link.", "tenant.admin_user.invited"),
            { EmailRefusalCode: AdminInvitationRefusals.AccountExists } =>
                ("Failed", $"Invitation not sent ({AdminInvitationRefusals.AccountExists}): an account with this address already exists and its state is unknown. Use \"Invite\" to send a new link.", "tenant.admin_user.invitation_failed"),
            { EmailRefusalCode: { } code } =>
                ("Failed", $"Invitation not sent ({code}). Fix the cause, then use \"Invite\".", "tenant.admin_user.invitation_failed"),
            _ =>
                ("Failed", "The account was created but the invitation e-mail did not leave. Use \"Invite\" to send a new link.", "tenant.admin_user.invitation_failed")
        };

        return _tenantRepository.RecordAdminInvitationAsync(
            tenantId, adminId, AdminInvitationStepKey, status, detail, now, stampInvitedAt: result.InvitationEmailSent,
            result.InvitationDispatchId,
            new TenantActivityEvent { EventType = eventType, Message = detail, At = now, Actor = ConsumerName }, ct);
    }

    private static IReadOnlyList<EmailRecipientDto> ResolveTenantAdminRecipients(Tenant tenant)
    {
        return tenant.AdminUsers
            .Where(user => user.Status is TenantAdminUserStatus.Active or TenantAdminUserStatus.Invited)
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .GroupBy(user => user.Email.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            .Select(group => ToRecipient(group.First()))
            .ToArray();
    }

    private static EmailRecipientDto ToRecipient(TenantAdminUser user)
    {
        return new EmailRecipientDto(user.Email.Trim().ToLowerInvariant(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name.Trim());
    }

    private static EventMetadata CreateMetadata(EventTransportMessage message)
    {
        return new EventMetadata(
            message.EventId,
            message.EventName,
            message.EventVersion,
            message.CorrelationId,
            message.CausationId,
            message.TenantId,
            message.Producer,
            message.OccurredAtUtc);
    }

    private static TEvent Deserialize<TEvent>(EventTransportMessage message)
        where TEvent : IIntegrationEvent
    {
        return JsonSerializer.Deserialize<TEvent>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize {message.EventName} payload.");
    }

    private static Guid ResolveTenantId<TEvent>(EventEnvelope<TEvent> envelope)
        where TEvent : IIntegrationEvent
    {
        if (envelope.TenantId.HasValue && envelope.TenantId.Value != Guid.Empty)
        {
            return envelope.TenantId.Value;
        }

        var property = typeof(TEvent).GetProperty("TenantId");
        if (property?.GetValue(envelope.Payload) is Guid tenantId && tenantId != Guid.Empty)
        {
            return tenantId;
        }

        throw new InvalidOperationException($"{typeof(TEvent).Name} payload does not expose a valid TenantId.");
    }
}
