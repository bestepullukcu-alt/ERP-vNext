using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Eventing;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class TenantLifecycleNotificationConsumerTests
{
    [Fact]
    public async Task TenantCreated_QueuesInviteToInitialAdminUser()
    {
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var tenant = CreateTenant(tenantId);
        tenant.AdminUsers.Add(new TenantAdminUser
        {
            Id = adminUserId,
            Name = "Tenant Owner",
            Email = "Owner@Example.COM",
            Status = TenantAdminUserStatus.Invited
        });
        var mediator = new RecordingMediator();
        var consumer = CreateConsumer(new InMemoryTenantRepository(tenant), mediator);
        var message = CreateMessage(
            TenantCreatedV1.Name,
            TenantCreatedV1.Version,
            correlationId,
            tenantId,
            new TenantCreatedV1(tenantId, DateTimeOffset.UtcNow, null, Guid.NewGuid(), tenant.DisplayName, "tr-TR", adminUserId));

        var result = await consumer.ConsumeAsync(message);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        var command = Assert.Single(mediator.Commands);
        Assert.Equal(tenantId, command.TenantId);
        Assert.Equal(correlationId.ToString("N"), command.CorrelationId);
        Assert.Equal("tenant.invite.email", command.Request.TemplateKey);
        Assert.Equal("tr-TR", command.Request.Locale);
        Assert.Single(command.Request.To);
        Assert.Equal("owner@example.com", command.Request.To[0].Email);
    }

    [Fact]
    public async Task TenantCreated_SkipsControlled_WhenInitialAdminRecipientIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var tenant = CreateTenant(tenantId);
        var mediator = new RecordingMediator();
        var consumer = CreateConsumer(new InMemoryTenantRepository(tenant), mediator);
        var message = CreateMessage(
            TenantCreatedV1.Name,
            TenantCreatedV1.Version,
            Guid.NewGuid(),
            tenantId,
            new TenantCreatedV1(tenantId, DateTimeOffset.UtcNow, null, Guid.NewGuid(), tenant.DisplayName, "en-US", Guid.NewGuid()));

        var result = await consumer.ConsumeAsync(message);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        Assert.Empty(mediator.Commands);
    }

    [Fact]
    public async Task TenantSuspended_QueuesToActiveAndInvitedTenantAdmins()
    {
        var tenantId = Guid.NewGuid();
        var tenant = CreateTenant(tenantId);
        tenant.DefaultLanguage = "tr-TR";
        tenant.AdminUsers.Add(new TenantAdminUser { Name = "Active Admin", Email = "active@example.com", Status = TenantAdminUserStatus.Active });
        tenant.AdminUsers.Add(new TenantAdminUser { Name = "Invited Admin", Email = "invited@example.com", Status = TenantAdminUserStatus.Invited });
        tenant.AdminUsers.Add(new TenantAdminUser { Name = "Disabled Admin", Email = "disabled@example.com", Status = TenantAdminUserStatus.Disabled });
        var mediator = new RecordingMediator();
        var consumer = CreateConsumer(new InMemoryTenantRepository(tenant), mediator);
        var message = CreateMessage(
            TenantSuspendedV1.Name,
            TenantSuspendedV1.Version,
            Guid.NewGuid(),
            tenantId,
            new TenantSuspendedV1(tenantId, DateTimeOffset.UtcNow, "policy hold", Guid.NewGuid()));

        await consumer.ConsumeAsync(message);

        var command = Assert.Single(mediator.Commands);
        Assert.Equal("tenant.suspended.email", command.Request.TemplateKey);
        Assert.Equal("tr-TR", command.Request.Locale);
        Assert.Equal(2, command.Request.To.Count);
        Assert.Contains(command.Request.To, recipient => recipient.Email == "active@example.com");
        Assert.Contains(command.Request.To, recipient => recipient.Email == "invited@example.com");
        Assert.DoesNotContain(command.Request.To, recipient => recipient.Email == "disabled@example.com");
        Assert.Equal("policy hold", command.Request.Variables["Reason"]);
    }

    [Fact]
    public async Task TenantReactivated_QueuesToTenantAdmins_AndSkipsDuplicate()
    {
        var tenantId = Guid.NewGuid();
        var tenant = CreateTenant(tenantId);
        tenant.AdminUsers.Add(new TenantAdminUser { Name = "Active Admin", Email = "active@example.com", Status = TenantAdminUserStatus.Active });
        var mediator = new RecordingMediator();
        var consumer = CreateConsumer(new InMemoryTenantRepository(tenant), mediator);
        var message = CreateMessage(
            TenantReactivatedV1.Name,
            TenantReactivatedV1.Version,
            Guid.NewGuid(),
            tenantId,
            new TenantReactivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()));

        var first = await consumer.ConsumeAsync(message);
        var duplicate = await consumer.ConsumeAsync(message);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, first);
        Assert.Equal(ConsumedEventExecutionResult.Duplicate, duplicate);
        Assert.Single(mediator.Commands);
        Assert.Equal("tenant.reactivated.email", mediator.Commands[0].Request.TemplateKey);
    }

    [Fact]
    public async Task QueueFailure_MarksConsumerFailed()
    {
        var tenantId = Guid.NewGuid();
        var tenant = CreateTenant(tenantId);
        tenant.AdminUsers.Add(new TenantAdminUser { Name = "Active Admin", Email = "active@example.com", Status = TenantAdminUserStatus.Active });
        var mediator = new RecordingMediator { Response = Response<NotificationDispatchDto>.Fail("template missing", 404) };
        var repository = new InMemoryConsumedEventRepository();
        var consumer = CreateConsumer(new InMemoryTenantRepository(tenant), mediator, repository);
        var message = CreateMessage(
            TenantSuspendedV1.Name,
            TenantSuspendedV1.Version,
            Guid.NewGuid(),
            tenantId,
            new TenantSuspendedV1(tenantId, DateTimeOffset.UtcNow, "hold", Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync(message)!);

        var consumed = await repository.GetAsync(message.EventId, TenantLifecycleNotificationConsumer.ConsumerName);
        Assert.NotNull(consumed);
        Assert.Equal(ConsumedEventStatus.Failed, consumed!.Status);
    }

    private static TenantLifecycleNotificationConsumer CreateConsumer(
        ITenantRegistryRepository tenantRepository,
        IMediator mediator,
        IConsumedEventRepository? consumedRepository = null)
    {
        return new TenantLifecycleNotificationConsumer(
            new ConsumedEventStore(consumedRepository ?? new InMemoryConsumedEventRepository(), NullLogger<ConsumedEventStore>.Instance),
            tenantRepository,
            mediator,
            new TenantCreatedV1NotificationMapper(),
            new TenantSuspendedV1NotificationMapper(),
            new TenantReactivatedV1NotificationMapper());
    }

    private static Tenant CreateTenant(Guid tenantId)
    {
        return new Tenant
        {
            Id = tenantId,
            Code = "TENANT",
            Slug = "tenant",
            Name = "Tenant",
            DisplayName = "Tenant Display",
            Domain = "tenant.example.com",
            Region = "EU",
            Environment = "Production"
        };
    }

    private static EventTransportMessage CreateMessage(
        string eventName,
        int eventVersion,
        Guid correlationId,
        Guid tenantId,
        object payload)
    {
        return new EventTransportMessage(
            Guid.NewGuid(),
            eventName,
            eventVersion,
            correlationId,
            Guid.NewGuid(),
            tenantId,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(payload));
    }

    private sealed class RecordingMediator : IMediator
    {
        public List<QueueEmailNotificationCommand> Commands { get; } = [];
        public Response<NotificationDispatchDto> Response { get; set; } = Response<NotificationDispatchDto>.Success(202);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is QueueEmailNotificationCommand command)
            {
                Commands.Add(command);
                return Task.FromResult((TResponse)(object)Response);
            }

            throw new NotSupportedException($"RecordingMediator does not handle {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    private sealed class InMemoryTenantRepository : ITenantRegistryRepository
    {
        private readonly Tenant? _tenant;

        public InMemoryTenantRepository(Tenant? tenant)
        {
            _tenant = tenant;
        }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_tenant?.Id == id ? _tenant : null);

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => Task.FromResult(tenant);
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => Task.FromResult<(IReadOnlyList<Tenant>, long)>(([], 0));
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => Task.FromResult(new TenantRegistryStats(0, 0, 0, 0, 0, 0, 0));
    }

    private sealed class InMemoryConsumedEventRepository : IConsumedEventRepository
    {
        private readonly Dictionary<(Guid EventId, string ConsumerName), ConsumedEvent> _items = [];

        public Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
        {
            var key = (consumedEvent.EventId, consumedEvent.ConsumerName);
            if (!_items.TryGetValue(key, out var existing))
            {
                _items[key] = consumedEvent;
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, consumedEvent));
            }

            if (existing.Status == ConsumedEventStatus.Failed)
            {
                existing.MarkRetryStarted();
                return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, existing));
            }

            var status = existing.Status == ConsumedEventStatus.Consumed || existing.Status == ConsumedEventStatus.SkippedDuplicate
                ? ConsumedEventStartStatus.ConsumedDuplicate
                : ConsumedEventStartStatus.InFlightDuplicate;
            return Task.FromResult(new ConsumedEventStartResult(status, existing));
        }

        public Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkConsumed();
            return Task.CompletedTask;
        }

        public Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkSkippedDuplicate();
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default)
        {
            _items[(eventId, consumerName)].MarkFailed(error);
            return Task.CompletedTask;
        }

        public Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue((eventId, consumerName), out var value);
            return Task.FromResult(value);
        }
    }
}
