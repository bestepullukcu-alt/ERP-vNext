using Diten.Platform.Infrastructure.Persistence.Schema;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class RabbitMqEventingIntegrationTests
{
    [SkippableFact]
    public async Task ExternalRabbitMq_GoldenFlow_PublishesFromOutbox_AndConsumerReceivesOnce()
    {
        Skip.IfNot(
            IsEnabled(),
            "External/local RabbitMQ integration test is disabled. Set Eventing__RabbitMq__IntegrationTestsEnabled=true and configure Eventing__RabbitMq__Host/Port/VirtualHost/Username/Password.");

        var rabbitMq = RabbitMqTestSettings.FromEnvironment();
        var mongo = MongoTestSettings.FromEnvironment();

        var mongoClient = new MongoClient(mongo.ConnectionString);
        /*
         * ⚠ A FIXED NAME, AND ONLY THE PROFILES THIS TEST USES. This used to be
         * `mongo.DatabaseName + "_" + Guid.NewGuid()` followed by MongoDbIndexConfigurations.EnsureIndexesAsync,
         * which built all 82 collections and 218 indexes to reach the two this test reads. A fixed name cannot
         * accumulate one database per run, and it is dropped in the finally below either way.
         */
        var databaseName = mongo.DatabaseName + "_eventing_golden_flow";
        var database = mongoClient.GetDatabase(databaseName);

        try
        {
            await mongoClient.DropDatabaseAsync(databaseName);
            await PlatformSchemaManifest.ApplyAsync(
                database,
                new[] { SchemaProfile.Eventing, SchemaProfile.Core });

            var tenantContext = new TenantContext();
            tenantContext.SetPlatformContext(Guid.NewGuid());

            var dbContext = new PlatformDbContext(mongoClient, database);
            var outboxRepository = new OutboxEventRepository(dbContext, tenantContext);
            var consumedRepository = new ConsumedEventRepository(dbContext, tenantContext);
            var consumedStore = new ConsumedEventStore(consumedRepository, NullLogger<ConsumedEventStore>.Instance);
            var sideEffects = new TestConsumerSideEffects();
            var consumedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var queueName = "tenant-activated-v1-test-" + Guid.NewGuid().ToString("N");

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

                cfg.ReceiveEndpoint(queueName, endpoint =>
                {
                    endpoint.Consumer(() => new TestTenantActivatedV1Consumer(consumedStore, sideEffects, consumedSignal));
                });
            });

            try
            {
                await bus.StartAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "External/local RabbitMQ integration test is enabled but the broker connection failed. Check Eventing__RabbitMq__Host, Eventing__RabbitMq__Port, Eventing__RabbitMq__VirtualHost, Eventing__RabbitMq__Username, Eventing__RabbitMq__Password, and Eventing__RabbitMq__UseTls.",
                    ex);
            }

            try
            {
                var eventBus = new EventBus(
                    outboxRepository,
                    new EventPayloadContractValidator(),
                    Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests" }),
                    NullLogger<EventBus>.Instance);
                var processor = new OutboxPublisherProcessor(
                    outboxRepository,
                    new MassTransitRabbitMqEventPublisher(bus),
                    Options.Create(new RabbitMqEventingOptions
                    {
                        RetryCount = 5,
                        InitialRetryDelaySeconds = 10,
                        MaxRetryDelaySeconds = 300,
                        BatchSize = 25
                    }),
                    NullLogger<OutboxPublisherProcessor>.Instance);

                var tenantId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                var causationId = Guid.NewGuid();

                var envelope = await eventBus.PublishAsync(
                    new TenantActivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()),
                    new EventPublishOptions
                    {
                        TenantId = tenantId,
                        CorrelationId = correlationId,
                        CausationId = causationId,
                        Producer = "Diten.Platform.Tests"
                    });

                var storedBeforePublish = await outboxRepository.GetByEventIdAsync(envelope.EventId);
                Assert.NotNull(storedBeforePublish);
                Assert.Equal(OutboxEventStatus.Pending, storedBeforePublish!.Status);

                var published = await processor.PublishPendingAsync();
                Assert.Equal(1, published);

                await consumedSignal.Task.WaitAsync(TimeSpan.FromSeconds(20));

                Assert.Equal(1, sideEffects.Count);
                Assert.Equal(correlationId, sideEffects.CorrelationId);
                Assert.Equal(causationId, sideEffects.CausationId);

                var consumed = await consumedRepository.GetAsync(envelope.EventId, nameof(TestTenantActivatedV1Consumer));
                Assert.NotNull(consumed);
                Assert.Equal(ConsumedEventStatus.Consumed, consumed!.Status);

                var duplicateEnvelope = sideEffects.LastEnvelope
                    ?? throw new InvalidOperationException("The test consumer did not capture an envelope.");
                var duplicateResult = await consumedStore.ExecuteOnceAsync(
                    duplicateEnvelope,
                    nameof(TestTenantActivatedV1Consumer),
                    _ =>
                    {
                        sideEffects.Increment();
                        return Task.CompletedTask;
                    });

                Assert.Equal(ConsumedEventExecutionResult.Duplicate, duplicateResult);
                Assert.Equal(1, sideEffects.Count);

                var duplicateConsumed = await consumedRepository.GetAsync(envelope.EventId, nameof(TestTenantActivatedV1Consumer));
                Assert.NotNull(duplicateConsumed);
                Assert.Equal(ConsumedEventStatus.SkippedDuplicate, duplicateConsumed!.Status);

                var storedAfterPublish = await outboxRepository.GetByEventIdAsync(envelope.EventId);
                Assert.NotNull(storedAfterPublish);
                Assert.Equal(OutboxEventStatus.Published, storedAfterPublish!.Status);
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

    [SkippableFact]
    public async Task ExternalRabbitMq_ConsumerFailure_RetriesThenMovesMessageToErrorQueue()
    {
        Skip.IfNot(
            IsEnabled(),
            "External/local RabbitMQ integration test is disabled. Set Eventing__RabbitMq__IntegrationTestsEnabled=true and configure Eventing__RabbitMq__Host/Port/VirtualHost/Username/Password.");

        var rabbitMq = RabbitMqTestSettings.FromEnvironment();
        var mongo = MongoTestSettings.FromEnvironment();

        var mongoClient = new MongoClient(mongo.ConnectionString);
        /*
         * ⚠ A FIXED NAME, AND ONLY THE PROFILES THIS TEST USES. This used to be
         * `mongo.DatabaseName + "_" + Guid.NewGuid()` followed by MongoDbIndexConfigurations.EnsureIndexesAsync,
         * which built all 82 collections and 218 indexes to reach the two this test reads. A fixed name cannot
         * accumulate one database per run, and it is dropped in the finally below either way.
         */
        var databaseName = mongo.DatabaseName + "_eventing_failure_path";
        var database = mongoClient.GetDatabase(databaseName);

        try
        {
            await mongoClient.DropDatabaseAsync(databaseName);
            await PlatformSchemaManifest.ApplyAsync(
                database,
                new[] { SchemaProfile.Eventing, SchemaProfile.Core });

            var tenantContext = new TenantContext();
            tenantContext.SetPlatformContext(Guid.NewGuid());

            var dbContext = new PlatformDbContext(mongoClient, database);
            var outboxRepository = new OutboxEventRepository(dbContext, tenantContext);
            var queueName = "tenant-activated-v1-failure-test-" + Guid.NewGuid().ToString("N");
            var attempts = new TestConsumerAttempts();
            var errorQueueSignal = new TaskCompletionSource<EventTransportMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            const int retryCount = 5;

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

                cfg.ReceiveEndpoint(queueName, endpoint =>
                {
                    endpoint.UseMessageRetry(r => r.Immediate(retryCount));
                    endpoint.Consumer(() => new AlwaysFailingTenantActivatedV1Consumer(attempts));
                });

                cfg.ReceiveEndpoint(queueName + "_error", endpoint =>
                {
                    endpoint.ConfigureConsumeTopology = false;
                    endpoint.Consumer(() => new ErrorQueueEventTransportMessageConsumer(errorQueueSignal));
                });
            });

            try
            {
                await bus.StartAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "External/local RabbitMQ integration test is enabled but the broker connection failed. Check Eventing__RabbitMq__Host, Eventing__RabbitMq__Port, Eventing__RabbitMq__VirtualHost, Eventing__RabbitMq__Username, Eventing__RabbitMq__Password, and Eventing__RabbitMq__UseTls.",
                    ex);
            }

            try
            {
                var eventBus = new EventBus(
                    outboxRepository,
                    new EventPayloadContractValidator(),
                    Options.Create(new EventBusOptions { Producer = "Diten.Platform.Tests" }),
                    NullLogger<EventBus>.Instance);
                var processor = new OutboxPublisherProcessor(
                    outboxRepository,
                    new MassTransitRabbitMqEventPublisher(bus),
                    Options.Create(new RabbitMqEventingOptions
                    {
                        RetryCount = 5,
                        InitialRetryDelaySeconds = 10,
                        MaxRetryDelaySeconds = 300,
                        BatchSize = 25,
                        PublishingStaleAfterSeconds = 300
                    }),
                    NullLogger<OutboxPublisherProcessor>.Instance);

                var tenantId = Guid.NewGuid();
                var envelope = await eventBus.PublishAsync(
                    new TenantActivatedV1(tenantId, DateTimeOffset.UtcNow, Guid.NewGuid()),
                    new EventPublishOptions
                    {
                        TenantId = tenantId,
                        CorrelationId = Guid.NewGuid(),
                        Producer = "Diten.Platform.Tests"
                    });

                var published = await processor.PublishPendingAsync();
                Assert.Equal(1, published);

                var errorMessage = await errorQueueSignal.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(envelope.EventId, errorMessage.EventId);
                Assert.Equal(TenantActivatedV1.Name, errorMessage.EventName);
                Assert.Equal(retryCount + 1, attempts.Count);

                var storedAfterPublish = await outboxRepository.GetByEventIdAsync(envelope.EventId);
                Assert.NotNull(storedAfterPublish);
                Assert.Equal(OutboxEventStatus.Published, storedAfterPublish!.Status);
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

    private static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("Eventing__RabbitMq__IntegrationTestsEnabled"),
            "true",
            StringComparison.OrdinalIgnoreCase);
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

    private static string Get(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private sealed class TestTenantActivatedV1Consumer : IConsumer<EventTransportMessage>
    {
        private readonly ConsumedEventStore _consumedEventStore;
        private readonly TestConsumerSideEffects _sideEffects;
        private readonly TaskCompletionSource _signal;

        public TestTenantActivatedV1Consumer(
            ConsumedEventStore consumedEventStore,
            TestConsumerSideEffects sideEffects,
            TaskCompletionSource signal)
        {
            _consumedEventStore = consumedEventStore;
            _sideEffects = sideEffects;
            _signal = signal;
        }

        public Task Consume(ConsumeContext<EventTransportMessage> context)
        {
            var message = context.Message;
            var payload = JsonSerializer.Deserialize<TenantActivatedV1>(message.PayloadJson)
                ?? throw new InvalidOperationException("Unable to deserialize tenant.activated.v1 payload.");

            var envelope = new EventEnvelope<TenantActivatedV1>(
                new EventMetadata(
                    message.EventId,
                    message.EventName,
                    message.EventVersion,
                    message.CorrelationId,
                    message.CausationId,
                    message.TenantId,
                    message.Producer,
                    message.OccurredAtUtc),
                payload);

            return _consumedEventStore.ExecuteOnceAsync(
                envelope,
                nameof(TestTenantActivatedV1Consumer),
                _ =>
                {
                    _sideEffects.Capture(envelope);
                    _signal.TrySetResult();
                    return Task.CompletedTask;
                },
                context.CancellationToken);
        }
    }

    private sealed class TestConsumerSideEffects
    {
        public int Count { get; private set; }

        public Guid? CorrelationId { get; private set; }

        public Guid? CausationId { get; private set; }

        public EventEnvelope<TenantActivatedV1>? LastEnvelope { get; private set; }

        public void Capture(EventEnvelope<TenantActivatedV1> envelope)
        {
            Count++;
            CorrelationId = envelope.CorrelationId;
            CausationId = envelope.CausationId;
            LastEnvelope = envelope;
        }

        public void Increment()
        {
            Count++;
        }
    }

    private sealed class AlwaysFailingTenantActivatedV1Consumer : IConsumer<EventTransportMessage>
    {
        private readonly TestConsumerAttempts _attempts;

        public AlwaysFailingTenantActivatedV1Consumer(TestConsumerAttempts attempts)
        {
            _attempts = attempts;
        }

        public Task Consume(ConsumeContext<EventTransportMessage> context)
        {
            if (string.Equals(context.Message.EventName, TenantActivatedV1.Name, StringComparison.Ordinal))
            {
                _attempts.Increment();
            }

            throw new InvalidOperationException("Intentional RabbitMQ retry/error queue validation failure.");
        }
    }

    private sealed class ErrorQueueEventTransportMessageConsumer : IConsumer<EventTransportMessage>
    {
        private readonly TaskCompletionSource<EventTransportMessage> _signal;

        public ErrorQueueEventTransportMessageConsumer(TaskCompletionSource<EventTransportMessage> signal)
        {
            _signal = signal;
        }

        public Task Consume(ConsumeContext<EventTransportMessage> context)
        {
            _signal.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class TestConsumerAttempts
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment()
        {
            Interlocked.Increment(ref _count);
        }
    }
}
