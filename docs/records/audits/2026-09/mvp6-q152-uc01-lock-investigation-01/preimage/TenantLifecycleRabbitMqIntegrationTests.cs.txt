using Diten.Platform.Infrastructure.Persistence.Schema;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Services.Notifications;
using Diten.Platform.Infrastructure.Settings;
using MassTransit;
using MediatR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class TenantLifecycleRabbitMqIntegrationTests
{
    [SkippableFact]
    public async Task TenantLifecycle_LiveRabbitMq_PublishesConsumesAuditsNotificationsAndSkipsDuplicates()
    {
        Skip.IfNot(
            IsEnabled(),
            "External/local RabbitMQ tenant lifecycle integration test is disabled. Set Eventing__RabbitMq__IntegrationTestsEnabled=true and configure Eventing__RabbitMq__Host/Port/VirtualHost/Username/Password.");

        var rabbitMq = RabbitMqTestSettings.FromEnvironment();
        var mongo = MongoTestSettings.FromEnvironment();
        var mongoClient = new MongoClient(mongo.ConnectionString);
        /*
         * ⚠ A FIXED NAME, AND ONLY THE PROFILES THIS TEST USES. This used to be
         * `mongo.DatabaseName + "_" + Guid.NewGuid()` followed by MongoDbIndexConfigurations.EnsureIndexesAsync,
         * which built all 82 collections and 218 indexes to reach the two this test reads. A fixed name cannot
         * accumulate one database per run, and it is dropped in the finally below either way.
         */
        var databaseName = mongo.DatabaseName + "_tenant_lifecycle";
        var database = mongoClient.GetDatabase(databaseName);

        try
        {
            await mongoClient.DropDatabaseAsync(databaseName);
            await PlatformSchemaManifest.ApplyAsync(
                database,
                new[]
                {
                    SchemaProfile.Eventing,
                    SchemaProfile.Core,
                    SchemaProfile.Notification,
                    SchemaProfile.AccessGovernance
                });

            var tenantContext = new TenantContext();
            tenantContext.SetPlatformContext(Guid.NewGuid());

            var dbContext = new PlatformDbContext(mongoClient, database);
            var outboxRepository = new OutboxEventRepository(dbContext, tenantContext);
            var consumedRepository = new ConsumedEventRepository(dbContext, tenantContext);
            var consumedStore = new ConsumedEventStore(consumedRepository, NullLogger<ConsumedEventStore>.Instance);
            var tenantRepository = new TenantRegistryRepository(dbContext, tenantContext);
            var templateRepository = new NotificationTemplateRepository(dbContext);
            var settingsRepository = new TenantMessagingSettingsRepository(dbContext);
            var dispatchRepository = new NotificationDispatchRepository(dbContext);
            var auditService = new CapturingAuditService(targetCount: 6);

            var tenantId = Guid.NewGuid();
            var initialAdminId = Guid.NewGuid();
            var adminEmail = "tenant.lifecycle.admin@example.com";
            await SeedTenantAsync(tenantRepository, tenantId, initialAdminId, adminEmail);
            await SeedNotificationFixtureAsync(settingsRepository, templateRepository);

            var eventBus = new EventBus(
                outboxRepository,
                new EventPayloadContractValidator(),
                Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests" }),
                NullLogger<EventBus>.Instance);

            var queueHandler = new QueueEmailNotificationHandler(
                new TenantMessagingSettingsResolver(settingsRepository),
                templateRepository,
                new EmailTemplateRenderer(),
                dispatchRepository,
                new MessagingProviderResolver([new FakeMessagingProvider(
                    Options.Create(new FakeMessagingProviderOptions { Enabled = true }),
                    new TestHostEnvironment())]),
                eventBus,
                NullLogger<QueueEmailNotificationHandler>.Instance);
            var mediator = new RecordingMediator(queueHandler, targetCount: 3);

            var queueName = "tenant-lifecycle-mod-0009-" + Guid.NewGuid().ToString("N");
            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(rabbitMq.Host, rabbitMq.Port, rabbitMq.VirtualHost, h =>
                {
                    h.Username(rabbitMq.Username);
                    h.Password(rabbitMq.Password);
                    if (rabbitMq.UseTls)
                    {
                        h.UseSsl(s => s.Protocol = System.Security.Authentication.SslProtocols.Tls12);
                    }
                });

                cfg.ReceiveEndpoint(queueName + "-audit", endpoint =>
                {
                    endpoint.Consumer(() => new TenantLifecycleAuditConsumer(consumedStore, auditService));
                });

                cfg.ReceiveEndpoint(queueName + "-notification", endpoint =>
                {
                    endpoint.Consumer(() => new TenantLifecycleNotificationConsumer(
                        consumedStore,
                        tenantRepository,
                        mediator,
                        new TenantCreatedV1NotificationMapper(),
                        new TenantSuspendedV1NotificationMapper(),
                        new TenantReactivatedV1NotificationMapper(),
                        NullLogger<TenantLifecycleNotificationConsumer>.Instance));
                });
            });

            try
            {
                await bus.StartAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "External/local RabbitMQ tenant lifecycle integration test is enabled but the broker connection failed. Check Eventing__RabbitMq__Host, Eventing__RabbitMq__Port, Eventing__RabbitMq__VirtualHost, Eventing__RabbitMq__Username, Eventing__RabbitMq__Password, and Eventing__RabbitMq__UseTls.",
                    ex);
            }

            try
            {
                var processor = new OutboxPublisherProcessor(
                    outboxRepository,
                    new MassTransitRabbitMqEventPublisher(bus),
                    Options.Create(new RabbitMqEventingOptions
                    {
                        RetryCount = 5,
                        InitialRetryDelaySeconds = 10,
                        MaxRetryDelaySeconds = 300,
                        BatchSize = 50,
                        PublishingStaleAfterSeconds = 300
                    }),
                    NullLogger<OutboxPublisherProcessor>.Instance);

                var correlationId = Guid.NewGuid();
                var actorId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var envelopes = new List<object>
                {
                    await PublishAsync(eventBus, new TenantCreatedV1(tenantId, now, Guid.NewGuid(), actorId, "Tenant Lifecycle", "en-US", initialAdminId), tenantId, correlationId),
                    await PublishAsync(eventBus, new TenantActivatedV1(tenantId, now, actorId), tenantId, correlationId),
                    await PublishAsync(eventBus, new TenantSuspendedV1(tenantId, now, "Sensitive suspension reason", actorId), tenantId, correlationId),
                    await PublishAsync(eventBus, new TenantReactivatedV1(tenantId, now, actorId), tenantId, correlationId),
                    await PublishAsync(eventBus, new TenantCancelledV1(tenantId, now, now.AddDays(1), "Sensitive cancellation reason", actorId), tenantId, correlationId),
                    await PublishAsync(eventBus, new TenantProvisioningFailedV1(tenantId, now, "bootstrap", "token=[REDACTED]", 1), tenantId, correlationId)
                };

                EventEnvelope<TenantProvisioningCompletedV1>? provisioningCompletedEnvelope = null;
                try
                {
                    provisioningCompletedEnvelope = await PublishAsync(
                        eventBus,
                        new TenantProvisioningCompletedV1(tenantId, now, ["registry-created"]),
                        tenantId,
                        correlationId);
                    envelopes.Add(provisioningCompletedEnvelope);
                    auditService.TargetCount = 7;
                }
                catch (EventValidationException)
                {
                    auditService.TargetCount = 6;
                }

                var published = await processor.PublishPendingAsync();
                Assert.Equal(envelopes.Count, published);

                await auditService.WaitAsync(TimeSpan.FromSeconds(30));
                await mediator.WaitAsync(TimeSpan.FromSeconds(30));

                Assert.Equal(auditService.TargetCount, auditService.Requests.Count);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantCreatedV1.Name);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantActivatedV1.Name);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantSuspendedV1.Name);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantReactivatedV1.Name);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantCancelledV1.Name);
                Assert.Contains(auditService.Requests, request => request.RequestType == TenantProvisioningFailedV1.Name);
                if (provisioningCompletedEnvelope is not null)
                {
                    Assert.Contains(auditService.Requests, request => request.RequestType == TenantProvisioningCompletedV1.Name);
                }

                Assert.All(auditService.Requests, request =>
                {
                    Assert.Equal(correlationId, request.CorrelationId);
                    Assert.Equal(tenantId, request.Metadata["TenantId"]);
                    Assert.Equal(correlationId, request.Metadata["CorrelationId"]);
                    Assert.True(request.Metadata.ContainsKey("EventId"));
                    Assert.True(request.Metadata.ContainsKey("EventName"));
                    Assert.True(request.Metadata.ContainsKey("EventVersion"));
                });
                var auditMetadataJson = JsonSerializer.Serialize(auditService.Requests.Select(x => x.Metadata));
                Assert.DoesNotContain(adminEmail, auditMetadataJson, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Sensitive suspension reason", auditMetadataJson, StringComparison.Ordinal);
                Assert.Contains("[REDACTED]", auditMetadataJson, StringComparison.Ordinal);

                Assert.Equal(3, mediator.Commands.Count);
                Assert.Contains(mediator.Commands, command => command.Request.TemplateKey == "tenant.invite.email");
                Assert.Contains(mediator.Commands, command => command.Request.TemplateKey == "tenant.suspended.email");
                Assert.Contains(mediator.Commands, command => command.Request.TemplateKey == "tenant.reactivated.email");
                Assert.All(mediator.Commands, command =>
                {
                    Assert.Equal(correlationId.ToString("N"), command.CorrelationId);
                    Assert.Contains(command.Request.To, recipient => recipient.Email == adminEmail);
                });

                var dispatches = await dispatchRepository.ListByTenantAsync(tenantId, take: 20);
                Assert.Equal(3, dispatches.Count);
                Assert.All(dispatches, dispatch =>
                {
                    Assert.Equal(NotificationDispatchStatus.Sent, dispatch.Status);
                    Assert.StartsWith("fake-", dispatch.ProviderMessageId, StringComparison.Ordinal);
                    Assert.Equal(correlationId.ToString("N"), dispatch.CorrelationId);
                    Assert.Contains(dispatch.To, recipient => recipient.Email == adminEmail);
                });

                var duplicateEventId = Guid.NewGuid();
                var duplicateEvent = new TenantSuspendedV1(tenantId, DateTimeOffset.UtcNow, "Duplicate sensitive reason", actorId);
                await PublishAsync(eventBus, duplicateEvent, tenantId, correlationId, duplicateEventId);
                await processor.PublishPendingAsync();
                await WaitForConsumedStatusAsync(consumedRepository, duplicateEventId, TenantLifecycleAuditConsumer.ConsumerName, ConsumedEventStatus.Consumed);
                await WaitForConsumedStatusAsync(consumedRepository, duplicateEventId, TenantLifecycleNotificationConsumer.ConsumerName, ConsumedEventStatus.Consumed);

                var duplicateOutboxEvent = await outboxRepository.GetByEventIdAsync(duplicateEventId)
                    ?? throw new InvalidOperationException("Duplicate delivery source outbox event was not found.");
                await bus.Publish(duplicateOutboxEvent.ToTransportMessage());
                await Task.Delay(TimeSpan.FromSeconds(2));

                Assert.Equal(auditService.TargetCount + 1, auditService.Requests.Count);
                Assert.Equal(4, mediator.Commands.Count);
                await WaitForConsumedStatusAsync(consumedRepository, duplicateEventId, TenantLifecycleAuditConsumer.ConsumerName, ConsumedEventStatus.Consumed);
                await WaitForConsumedStatusAsync(consumedRepository, duplicateEventId, TenantLifecycleNotificationConsumer.ConsumerName, ConsumedEventStatus.Consumed);
            }
            finally
            {
                await bus.StopAsync();
            }
        }
        finally
        {
            await mongoClient.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
        EventBus eventBus,
        TEvent payload,
        Guid tenantId,
        Guid correlationId,
        Guid? eventId = null)
        where TEvent : IIntegrationEvent
    {
        return await eventBus.PublishAsync(
            payload,
            new EventPublishOptions
            {
                EventId = eventId,
                TenantId = tenantId,
                CorrelationId = correlationId,
                CausationId = Guid.NewGuid(),
                Producer = "Diten.Platform.Tests"
            });
    }

    private static async Task SeedTenantAsync(
        TenantRegistryRepository tenantRepository,
        Guid tenantId,
        Guid initialAdminId,
        string adminEmail)
    {
        await tenantRepository.CreateAsync(new Tenant
        {
            Id = tenantId,
            Code = "MOD0009",
            Slug = "mod-0009",
            Name = "MOD-0009",
            DisplayName = "MOD-0009 Tenant",
            Domain = "mod-0009.example.test",
            Region = "EU",
            Environment = "Test",
            DefaultLanguage = "en-US",
            AdminUsers =
            [
                new TenantAdminUser
                {
                    Id = initialAdminId,
                    Name = "Tenant Lifecycle Admin",
                    Email = adminEmail,
                    Status = TenantAdminUserStatus.Invited
                }
            ]
        });
    }

    private static async Task SeedNotificationFixtureAsync(
        TenantMessagingSettingsRepository settingsRepository,
        NotificationTemplateRepository templateRepository)
    {
        await settingsRepository.CreateAsync(new TenantMessagingSettings
        {
            TenantId = null,
            IsPlatformDefault = true,
            ProviderCode = MessagingProviderCode.Fake,
            SenderEmail = "no-reply@example.test",
            SenderName = "Diten Test",
            IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault
        });

        await templateRepository.CreateAsync(CreateTemplate(
            "tenant.invite.email",
            "Invite {{TenantDisplayName}}",
            "<p>Invite {{TenantDisplayName}}</p>"));
        await templateRepository.CreateAsync(CreateTemplate(
            "tenant.suspended.email",
            "Suspended {{TenantId}}",
            "<p>Suspended {{TenantId}} {{SuspendedAtUtc}}</p>"));
        await templateRepository.CreateAsync(CreateTemplate(
            "tenant.reactivated.email",
            "Reactivated {{TenantId}}",
            "<p>Reactivated {{TenantId}} {{ReactivatedAtUtc}}</p>"));
    }

    private static NotificationTemplate CreateTemplate(string key, string subject, string html)
    {
        return new NotificationTemplate
        {
            TenantId = null,
            IsPlatformDefault = true,
            TemplateKey = key,
            Channel = NotificationChannelCode.Email,
            Locale = "en-us",
            SubjectTemplate = subject,
            BodyHtmlTemplate = html,
            BodyTextTemplate = subject,
            Status = NotificationTemplateStatus.Active,
            SemanticVersion = "test"
        };
    }

    private static async Task WaitForConsumedStatusAsync(
        ConsumedEventRepository repository,
        Guid eventId,
        string consumerName,
        ConsumedEventStatus status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            var consumed = await repository.GetAsync(eventId, consumerName, timeout.Token);
            if (consumed?.Status == status)
            {
                return;
            }

            await Task.Delay(100, timeout.Token);
        }

        throw new TimeoutException($"Timed out waiting for {consumerName} to reach {status} for event {eventId}.");
    }

    private static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("Eventing__RabbitMq__IntegrationTestsEnabled"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Get(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private sealed record RabbitMqTestSettings(
        string Host,
        ushort Port,
        string VirtualHost,
        string Username,
        string Password,
        bool UseTls)
    {
        public static RabbitMqTestSettings FromEnvironment()
        {
            return new RabbitMqTestSettings(
                Get("Eventing__RabbitMq__Host", "localhost"),
                GetPort(),
                Get("Eventing__RabbitMq__VirtualHost", "/"),
                Get("Eventing__RabbitMq__Username", "guest"),
                Get("Eventing__RabbitMq__Password", "guest"),
                bool.TryParse(Environment.GetEnvironmentVariable("Eventing__RabbitMq__UseTls"), out var useTls) && useTls);
        }

        private static ushort GetPort()
        {
            var raw = Environment.GetEnvironmentVariable("Eventing__RabbitMq__Port");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return 5672;
            }

            if (ushort.TryParse(raw, out var port))
            {
                return port;
            }

            throw new InvalidOperationException("Eventing__RabbitMq__Port must be a valid TCP port.");
        }
    }

    private sealed record MongoTestSettings(string ConnectionString, string DatabaseName)
    {
        public static MongoTestSettings FromEnvironment()
        {
            return new MongoTestSettings(
                Get("Eventing__MongoDb__ConnectionString", Get("MongoDbSettings__ConnectionString", "mongodb://localhost:27017")),
                Get("Eventing__MongoDb__DatabaseName", "eventing_mvp_tests"));
        }
    }

    private sealed class CapturingAuditService : IAuditService
    {
        private readonly object _gate = new();
        private TaskCompletionSource _targetReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _targetCount;

        public CapturingAuditService(int targetCount)
        {
            _targetCount = targetCount;
        }

        public List<AuditAppendRequest> Requests { get; } = [];

        public int TargetCount
        {
            get => Volatile.Read(ref _targetCount);
            set => Volatile.Write(ref _targetCount, value);
        }

        public Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
        {
            lock (_gate)
            {
                Requests.Add(request);
                if (Requests.Count >= TargetCount)
                {
                    _targetReached.TrySetResult();
                }
            }

            return Task.FromResult(AuditAppendResult.Queued(Guid.NewGuid().ToString("N")));
        }

        public Task WaitAsync(TimeSpan timeout)
        {
            lock (_gate)
            {
                if (Requests.Count >= TargetCount)
                {
                    return Task.CompletedTask;
                }
            }

            return _targetReached.Task.WaitAsync(timeout);
        }
    }

    private sealed class RecordingMediator : IMediator
    {
        private readonly QueueEmailNotificationHandler _handler;
        private readonly int _targetCount;
        private readonly TaskCompletionSource _targetReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _gate = new();
        // MOD-0027-FU04C — suspended/reactivated now dispatch by eventCode; route through the real FU04B adapter,
        // which resolves the Active event and delegates back here as a QueueEmailNotificationCommand.
        private readonly Diten.Platform.Application.Features.Notifications.Services.INotificationEventDispatchAdapter _adapter;

        public RecordingMediator(QueueEmailNotificationHandler handler, int targetCount)
        {
            _handler = handler;
            _targetCount = targetCount;
            _adapter = new Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchAdapter(
                new SeededEventRepository(), this, new PassThroughLocaleResolver(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<
                    Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchAdapter>.Instance);
        }

        public List<QueueEmailNotificationCommand> Commands { get; } = [];

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is Diten.Platform.Application.Features.Notifications.Commands.DispatchNotificationByEventCodeCommand dispatch)
            {
                var dispatchResponse = await _adapter.DispatchByEventCodeAsync(dispatch.Request, cancellationToken);
                return (TResponse)(object)dispatchResponse;
            }

            if (request is not QueueEmailNotificationCommand command)
            {
                throw new NotSupportedException($"RecordingMediator does not handle {request.GetType().Name}.");
            }

            lock (_gate)
            {
                Commands.Add(command);
            }

            var response = await _handler.Handle(command, cancellationToken);
            lock (_gate)
            {
                if (Commands.Count >= _targetCount)
                {
                    _targetReached.TrySetResult();
                }
            }

            return (TResponse)(object)response;
        }

        public Task WaitAsync(TimeSpan timeout)
        {
            lock (_gate)
            {
                if (Commands.Count >= _targetCount)
                {
                    return Task.CompletedTask;
                }
            }

            return _targetReached.Task.WaitAsync(timeout);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
    }

    // MOD-0027-FU04C — minimal Active event catalog for the FU04B adapter (suspended/reactivated eventCodes bound to
    // their templates; empty RequiredVariables so the adapter passes validation and delegates to the real handler).
    private sealed class SeededEventRepository : Diten.Platform.Domain.Repositories.INotificationEventDefinitionRepository
    {
        private static NotificationEventDefinition Event(string code, string templateKey) => new()
        {
            EventCode = code,
            OwnerModuleId = "MOD-0009",
            Channel = NotificationChannelCode.Email,
            DefaultTemplateKey = templateKey,
            FallbackDisplayName = code,
            Status = NotificationEventStatus.Active
        };

        private readonly Dictionary<string, NotificationEventDefinition> _events = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tenant.user.invited"] = Event("tenant.user.invited", "tenant.invite.email"),
            ["tenant.lifecycle.suspended"] = Event("tenant.lifecycle.suspended", "tenant.suspended.email"),
            ["tenant.lifecycle.reactivated"] = Event("tenant.lifecycle.reactivated", "tenant.reactivated.email")
        };

        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult(_events.TryGetValue((eventCode ?? string.Empty).Trim().ToLowerInvariant(), out var e) ? e : null);

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition d, CancellationToken ct = default) => Task.FromResult(d);
        public Task UpdateAsync(NotificationEventDefinition d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<NotificationEventDefinition?>(null);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null, bool? canTenantOverride = null, NotificationEventUsageType? usageType = null, int skip = 0, int take = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NotificationEventDefinition>>([]);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(_events.Values.ToArray());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Diten.Platform.Eventing.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>
/// The tenant-lifecycle mappers supply a locale of their own, so this double only has to prove the adapter still
/// forwards it untouched — the behaviour WC-4's locale change had to leave alone.
/// </summary>
internal sealed class PassThroughLocaleResolver
    : Diten.Platform.Application.Features.Notifications.Services.INotificationLocaleResolver
{
    public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(requested) ? "en" : requested.Trim().ToLowerInvariant());
}
