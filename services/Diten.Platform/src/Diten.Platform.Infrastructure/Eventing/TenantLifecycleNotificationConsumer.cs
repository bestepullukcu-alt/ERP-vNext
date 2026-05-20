using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MassTransit;
using MediatR;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class TenantLifecycleNotificationConsumer : IConsumer<EventTransportMessage>
{
    public const string ConsumerName = nameof(TenantLifecycleNotificationConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsumedEventStore _consumedEventStore;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly IMediator _mediator;
    private readonly TenantCreatedV1NotificationMapper _createdMapper;
    private readonly TenantSuspendedV1NotificationMapper _suspendedMapper;
    private readonly TenantReactivatedV1NotificationMapper _reactivatedMapper;

    public TenantLifecycleNotificationConsumer(
        ConsumedEventStore consumedEventStore,
        ITenantRegistryRepository tenantRepository,
        IMediator mediator,
        TenantCreatedV1NotificationMapper createdMapper,
        TenantSuspendedV1NotificationMapper suspendedMapper,
        TenantReactivatedV1NotificationMapper reactivatedMapper)
    {
        _consumedEventStore = consumedEventStore;
        _tenantRepository = tenantRepository;
        _mediator = mediator;
        _createdMapper = createdMapper;
        _suspendedMapper = suspendedMapper;
        _reactivatedMapper = reactivatedMapper;
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
                async (tenant, envelope, ct) =>
                {
                    var recipients = ResolveInitialAdminRecipient(tenant, envelope.Payload.InitialAdminUserId);
                    var request = _createdMapper.Map(envelope, recipients);
                    await QueueIfMappedAsync(envelope, request, ct);
                },
                cancellationToken),
            TenantSuspendedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantSuspendedV1>(message),
                async (tenant, envelope, ct) =>
                {
                    var recipients = ResolveTenantAdminRecipients(tenant);
                    var request = _suspendedMapper.Map(envelope, recipients, tenant.DefaultLanguage);
                    await QueueIfMappedAsync(envelope, request, ct);
                },
                cancellationToken),
            TenantReactivatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantReactivatedV1>(message),
                async (tenant, envelope, ct) =>
                {
                    var recipients = ResolveTenantAdminRecipients(tenant);
                    var request = _reactivatedMapper.Map(envelope, recipients, tenant.DefaultLanguage);
                    await QueueIfMappedAsync(envelope, request, ct);
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

    private async Task QueueIfMappedAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        QueueEmailNotificationRequest? request,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        if (request is null)
        {
            return;
        }

        var tenantId = ResolveTenantId(envelope);
        var response = await _mediator.Send(
            new QueueEmailNotificationCommand(tenantId, request, envelope.CorrelationId.ToString("N")),
            cancellationToken);

        if (!response.IsSuccessful)
        {
            throw new InvalidOperationException(
                $"Tenant lifecycle notification queue failed. EventName={envelope.EventName} StatusCode={response.StatusCode}");
        }
    }

    private static IReadOnlyList<EmailRecipientDto> ResolveInitialAdminRecipient(Tenant tenant, Guid? initialAdminUserId)
    {
        if (!initialAdminUserId.HasValue)
        {
            return [];
        }

        var admin = tenant.AdminUsers.FirstOrDefault(user =>
            user.Id == initialAdminUserId.Value
            && user.Status != TenantAdminUserStatus.Disabled
            && !string.IsNullOrWhiteSpace(user.Email));

        return admin is null ? [] : [ToRecipient(admin)];
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
