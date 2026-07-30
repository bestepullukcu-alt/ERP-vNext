using System.Security.Cryptography;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Eventing;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditMassTransitPipelineTests
{
    private const string Secret = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    [Fact]
    public async Task EndpointRetryOwnsPpmFailuresWithoutGlobalMultiplication()
    {
        var repository = new PipelineRepository();
        var observer = new PipelineObserver();
        var genericProbe = new GenericRetryProbe();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IOptions<PpmAuditConsumerOptions>>(Options.Create(new PpmAuditConsumerOptions
            {
                Enabled = true,
                ActiveKeyId = "current",
                ActiveSecret = Secret
            }))
            .AddScoped<PpmAuditSignatureVerifier>()
            .AddSingleton<IPpmAuditAcceptanceRepository>(repository)
            .AddSingleton<IPpmAuditDeadLetterObserver>(observer)
            .AddSingleton<ITenantContext, TenantContext>()
            .AddScoped<PpmAuditConsumerProcessor>()
            .AddSingleton(genericProbe)
            .AddMassTransitTestHarness(registration =>
            {
                registration.AddConsumer<PpmAuditIntentSubmittedV1Consumer>()
                    .ExcludeFromConfigureEndpoints();
                registration.AddConsumer<GenericRetryProbeConsumer>()
                    .ExcludeFromConfigureEndpoints();
                registration.UsingInMemory((context, cfg) =>
                {
                    // Production ordering: PPM endpoint first, global policy afterwards.
                    cfg.ReceiveEndpoint("platform-ppm-audit-pipeline-test", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry =>
                            PpmAuditIntentSubmittedV1ConsumerDefinition.ConfigureRetry(
                                retry,
                                TimeSpan.FromMilliseconds(1),
                                TimeSpan.FromMilliseconds(1),
                                TimeSpan.FromMilliseconds(1),
                                TimeSpan.FromMilliseconds(1)));
                        endpoint.ConfigureConsumer<PpmAuditIntentSubmittedV1Consumer>(context);
                    });

                    cfg.UseMessageRetry(retry =>
                    {
                        retry.Ignore<PpmAuditContractException>();
                        retry.Ignore<PpmAuditTransientException>();
                        retry.Immediate(2);
                    });
                    cfg.ReceiveEndpoint("generic-retry-pipeline-test", endpoint =>
                        endpoint.ConfigureConsumer<GenericRetryProbeConsumer>(context));
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var ppmEndpoint = await harness.Bus.GetSendEndpoint(
                new Uri("queue:platform-ppm-audit-pipeline-test"));
            var message = Message(Payload());
            repository.FailTransiently = true;
            await ppmEndpoint.Send(message, context => SetValidHeaders(context, message));
            await WaitUntilAsync(() => repository.Attempts == 5 && observer.Count == 1);

            Assert.Equal(5, repository.Attempts);
            Assert.Equal(1, observer.Count);
            var exhausted = Assert.IsType<PpmAuditRetriesExhaustedException>(observer.LastException);
            Assert.IsType<InvalidOperationException>(exhausted.InnerException);

            var malformed = Message(Payload()[..^1]) with { EventId = Guid.NewGuid() };
            await ppmEndpoint.Send(malformed, context =>
            {
                context.Headers.Set(PpmAuditSignatureVerifier.SchemeHeader, PpmAuditIntentParser.SignatureScheme);
                context.Headers.Set(PpmAuditSignatureVerifier.KeyIdHeader, "current");
                context.Headers.Set(PpmAuditSignatureVerifier.SignatureHeader, new string('0', 64));
            });
            await WaitUntilAsync(() => observer.Count == 2);

            // Contract failure never reaches the repository and is observed exactly once.
            Assert.Equal(5, repository.Attempts);
            Assert.Equal(2, observer.Count);
            Assert.IsType<PpmAuditContractException>(observer.LastException);

            var genericEndpoint = await harness.Bus.GetSendEndpoint(
                new Uri("queue:generic-retry-pipeline-test"));
            await genericEndpoint.Send(new GenericRetryProbeMessage(Guid.NewGuid()));
            await WaitUntilAsync(() => genericProbe.Attempts == 3);

            // The unrelated consumer still receives initial + two generic retries.
            Assert.Equal(3, genericProbe.Attempts);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static void SetValidHeaders(
        SendContext<EventTransportMessage> context,
        EventTransportMessage message)
    {
        context.Headers.Set(PpmAuditSignatureVerifier.SchemeHeader, PpmAuditIntentParser.SignatureScheme);
        context.Headers.Set(PpmAuditSignatureVerifier.KeyIdHeader, "current");
        context.Headers.Set(PpmAuditSignatureVerifier.SignatureHeader, Sign(message));
    }

    private static string Sign(EventTransportMessage message)
    {
        var intent = PpmAuditIntentParser.Parse(message);
        return Convert.ToHexString(HMACSHA256.HashData(
            Convert.FromBase64String(Secret),
            PpmAuditIntentParser.BuildSigningInput(message, intent.CanonicalPayload))).ToLowerInvariant();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static EventTransportMessage Message(string payload) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PpmAuditIntentParser.EventName,
            1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            payload);

    private static string Payload() =>
        "{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"entityType\":\"Project\",\"mutation\":\"created\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}";

    private sealed class PipelineRepository : IPpmAuditAcceptanceRepository
    {
        private int _attempts;
        public int Attempts => Volatile.Read(ref _attempts);
        public bool FailTransiently { get; set; }

        public Task<PpmAuditAcceptanceResult> AcceptAsync(
            EventTransportMessage message,
            PpmAuditIntent intent,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            return FailTransiently
                ? Task.FromException<PpmAuditAcceptanceResult>(
                    new InvalidOperationException("pipeline transient failure"))
                : Task.FromResult(PpmAuditAcceptanceResult.Accepted);
        }
    }

    private sealed class PipelineObserver : IPpmAuditDeadLetterObserver
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public Exception? LastException { get; private set; }

        public void Record(EventTransportMessage message, Exception exception)
        {
            LastException = exception;
            Interlocked.Increment(ref _count);
        }
    }

    public sealed record GenericRetryProbeMessage(Guid Id);

    private sealed class GenericRetryProbe
    {
        private int _attempts;
        public int Attempts => Volatile.Read(ref _attempts);
        public int Increment() => Interlocked.Increment(ref _attempts);
    }

    private sealed class GenericRetryProbeConsumer : IConsumer<GenericRetryProbeMessage>
    {
        private readonly GenericRetryProbe _probe;

        public GenericRetryProbeConsumer(GenericRetryProbe probe)
        {
            _probe = probe;
        }

        public Task Consume(ConsumeContext<GenericRetryProbeMessage> context)
        {
            if (_probe.Increment() < 3)
            {
                throw new InvalidOperationException("generic transient failure");
            }

            return Task.CompletedTask;
        }
    }
}
