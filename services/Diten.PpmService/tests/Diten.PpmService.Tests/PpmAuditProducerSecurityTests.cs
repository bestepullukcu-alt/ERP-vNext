using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Events;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Infrastructure.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class PpmAuditProducerSecurityTests
{
    private const string FixtureSecret =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    [Fact]
    public async Task Fixture_signature_matches_consumer_and_persists_exact_metadata()
    {
        var repository = new AuditRepository();
        var provider = new PpmAuditTrustedTransportMetadataProvider(
            repository,
            Options.Create(EnabledOptions()));
        var candidate = FixtureCandidate();
        var @event = new PpmAuditIntentSubmittedV1(candidate);
        var canonical = ((ICanonicalIntegrationEvent)@event).CanonicalPayloadUtf8;
        var metadata = FixtureMetadata();

        var trusted = await provider.CreateAsync(metadata, canonical);

        Assert.Equal(
            "ea31a1822130953463f4705493aa1e4de6a09752801dd52d810dfbc461e9d40e",
            trusted.Headers[TrustedTransportMetadata.SignatureHeader]);
        Assert.Equal(
            PpmAuditTrustedTransportMetadataProvider.SignatureScheme,
            trusted.Headers[TrustedTransportMetadata.SignatureSchemeHeader]);
        Assert.Equal("ppm-fixture-current", trusted.Headers[TrustedTransportMetadata.KeyIdHeader]);
        Assert.Equal(candidate.Id, repository.MetadataIntentId);
    }

    [Fact]
    public void Signing_input_covers_tenant_correlation_producer_causation_and_payload()
    {
        var payload = Encoding.UTF8.GetBytes("{\"x\":1}");
        var metadata = FixtureMetadata() with { CausationId = Guid.NewGuid() };
        var baseline = PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
            metadata,
            payload);

        Assert.NotEqual(
            SHA256.HashData(baseline),
            SHA256.HashData(PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
                metadata with { TenantId = Guid.NewGuid() },
                payload)));
        Assert.NotEqual(
            SHA256.HashData(baseline),
            SHA256.HashData(PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
                metadata with { CorrelationId = Guid.NewGuid() },
                payload)));
        Assert.NotEqual(
            SHA256.HashData(baseline),
            SHA256.HashData(PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
                metadata with { Producer = "Other" },
                payload)));
        Assert.NotEqual(
            SHA256.HashData(baseline),
            SHA256.HashData(PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
                metadata with { CausationId = null },
                payload)));
    }

    [Fact]
    public void Secret_and_activation_validation_is_fail_closed()
    {
        var validator = new PpmAuditProducerOptionsValidator();
        var validRuntimeSecret = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
        Assert.False(validator.Validate(null, new()).Failed);
        Assert.True(validator.Validate(null, new() { WorkerEnabled = true }).Failed);
        Assert.True(validator.Validate(null, new() { Enabled = true }).Failed);
        Assert.True(validator.Validate(null, WithSecret(EnabledOptions(), "not-base64")).Failed);
        Assert.True(validator.Validate(
            null,
            WithSecret(EnabledOptions(), Convert.ToBase64String(new byte[31]))).Failed);
        Assert.True(validator.Validate(
            null,
            WithSecret(EnabledOptions(), FixtureSecret)).Failed);
        Assert.True(validator.Validate(
            null,
            WithSecret(EnabledOptions(), Convert.ToBase64String(new byte[32]))).Failed);
        Assert.True(validator.Validate(
            null,
            WithSecret(EnabledOptions(), Convert.ToBase64String(
                Enumerable.Repeat((byte)0x5a, 32).ToArray()))).Failed);
        foreach (var placeholder in new[]
                 {
                     "changeme", "change-me", "placeholder", "default", "secret", "test"
                 })
        {
            Assert.True(validator.Validate(
                null,
                WithSecret(EnabledOptions(), Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(placeholder)))).Failed);
        }
        Assert.True(validator.Validate(
            null,
            WithKeyId(EnabledOptions(), "bad\r\nkey")).Failed);
        var validResult = validator.Validate(
            null,
            WithSecret(EnabledOptions(), validRuntimeSecret));
        Assert.False(validResult.Failed);
        Assert.DoesNotContain(
            FixtureSecret,
            validator.Validate(null, WithSecret(EnabledOptions(), FixtureSecret))
                .FailureMessage ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_quarantines_missing_correlation_without_publish()
    {
        var candidate = FixtureCandidate() with { CorrelationId = Guid.Empty };
        var repository = new AuditRepository(candidate);
        var bus = new RecordingEventBus();
        var dispatcher = new PpmAuditIntentDispatcher(
            repository,
            bus,
            Options.Create(EnabledOptions(worker: true)),
            NullLogger<PpmAuditIntentDispatcher>.Instance);

        Assert.Equal(0, await dispatcher.DispatchPendingAsync(default));
        Assert.Equal("ppm.audit-intent.correlation-missing", repository.QuarantineReason);
        Assert.Equal(0, bus.PublishCount);
        Assert.False(repository.MarkerWritten);
    }

    [Fact]
    public async Task Dispatcher_uses_intent_id_and_correlation_then_marks_after_publish()
    {
        var candidate = FixtureCandidate();
        var repository = new AuditRepository(candidate);
        var bus = new RecordingEventBus();
        repository.BeforeMarker = () => bus.PublishCount > 0;
        var dispatcher = new PpmAuditIntentDispatcher(
            repository,
            bus,
            Options.Create(EnabledOptions(worker: true)),
            NullLogger<PpmAuditIntentDispatcher>.Instance);

        Assert.Equal(1, await dispatcher.DispatchPendingAsync(default));
        Assert.Equal(candidate.Id, bus.Options!.EventId);
        Assert.Equal(candidate.CorrelationId, bus.Options.CorrelationId);
        Assert.Equal(candidate.TenantId, bus.Options.TenantId);
        Assert.True(repository.MarkerWritten);
    }

    [Fact]
    public async Task Dispatcher_quarantines_invalid_signing_metadata_and_continues_with_valid_intent()
    {
        var invalid = FixtureCandidate() with
        {
            DispatchMetadata = new AuditIntentDispatchMetadata(
                PpmAuditTrustedTransportMetadataProvider.SignatureScheme,
                "invalid key",
                new string('a', 64))
        };
        var valid = FixtureCandidate() with
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        };
        var repository = new AuditRepository(invalid, valid);
        var bus = new RecordingEventBus();
        var dispatcher = new PpmAuditIntentDispatcher(
            repository,
            bus,
            Options.Create(EnabledOptions(worker: true)),
            NullLogger<PpmAuditIntentDispatcher>.Instance);

        Assert.Equal(1, await dispatcher.DispatchPendingAsync(default));
        Assert.Equal(
            "ppm.audit-intent.signing-metadata-invalid",
            repository.QuarantineReasons[invalid.Id]);
        Assert.DoesNotContain(valid.Id, repository.QuarantineReasons.Keys);
        Assert.Equal([valid.Id], bus.PublishedEventIds);
        Assert.Equal([valid.Id], repository.MarkedIntentIds);
        Assert.Equal(1, bus.PublishCount);
    }

    [Fact]
    public void Application_services_do_not_depend_on_transport_or_platform_runtime()
    {
        var serviceTypes = typeof(Diten.PpmService.Application.Features.Portfolios.PortfolioService)
            .Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains(".Features.", StringComparison.Ordinal) == true);

        Assert.DoesNotContain(serviceTypes.SelectMany(type =>
                type.GetConstructors().SelectMany(constructor => constructor.GetParameters())),
            parameter => parameter.ParameterType == typeof(IEventTransportPublisher)
                         || parameter.ParameterType.FullName?.Contains(
                             "MassTransit",
                             StringComparison.Ordinal) == true
                         || parameter.ParameterType.FullName?.StartsWith(
                             "Diten.Platform.",
                             StringComparison.Ordinal) == true);
    }

    private static PpmAuditProducerOptions EnabledOptions(bool worker = false) =>
        new()
        {
            Enabled = true,
            WorkerEnabled = worker,
            KeyId = "ppm-fixture-current",
            SecretBase64 = FixtureSecret,
            RabbitMqHost = worker ? "localhost" : null,
            RabbitMqUsername = worker ? "ppm" : null,
            RabbitMqPassword = worker ? "environment-only" : null
        };

    private static PpmAuditProducerOptions WithSecret(
        PpmAuditProducerOptions options,
        string secret)
    {
        options.SecretBase64 = secret;
        return options;
    }

    private static PpmAuditProducerOptions WithKeyId(
        PpmAuditProducerOptions options,
        string keyId)
    {
        options.KeyId = keyId;
        return options;
    }

    private static AuditIntentDispatchCandidate FixtureCandidate() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Project",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "created",
            DateTime.Parse(
                "2026-07-30T10:20:30.0000000Z",
                null,
                System.Globalization.DateTimeStyles.RoundtripKind),
            null);

    private static EventMetadata FixtureMetadata() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "ppm.audit-intent.submitted.v1",
            1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Diten.PpmService",
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"));

    private sealed class AuditRepository(params AuditIntentDispatchCandidate[] candidates)
        : IAuditIntentRepository
    {
        public Guid MetadataIntentId { get; private set; }
        public string? QuarantineReason { get; private set; }
        public bool MarkerWritten { get; private set; }
        public Dictionary<Guid, string> QuarantineReasons { get; } = [];
        public List<Guid> MarkedIntentIds { get; } = [];
        public Func<bool>? BeforeMarker { get; set; }
        public List<string> CallOrder { get; } = [];
        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<AuditIntentDispatchCandidate>> GetDispatchCandidatesAsync(
            int batchSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditIntentDispatchCandidate>>(candidates);
        public Task<AuditIntentDispatchMetadata> EnsureDispatchMetadataAsync(
            Guid intentId,
            AuditIntentDispatchMetadata proposed,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            MetadataIntentId = intentId;
            return Task.FromResult(proposed);
        }
        public Task<bool> MarkOutboxEnqueuedAsync(
            Guid intentId,
            DateTime enqueuedAtUtc,
            CancellationToken cancellationToken)
        {
            if (BeforeMarker is not null && !BeforeMarker())
            {
                throw new InvalidOperationException(
                    "The outbox enqueue must complete before the intent marker.");
            }
            CallOrder.Add("marker");
            MarkerWritten = true;
            MarkedIntentIds.Add(intentId);
            return Task.FromResult(true);
        }
        public Task<bool> MarkDispatchQuarantinedAsync(
            Guid intentId,
            string failureCode,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken)
        {
            QuarantineReason = failureCode;
            QuarantineReasons[intentId] = failureCode;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public int PublishCount { get; private set; }
        public EventPublishOptions? Options { get; private set; }
        public List<string> CallOrder { get; } = [];
        public List<Guid> PublishedEventIds { get; } = [];
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            throw new NotSupportedException();
        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event,
            EventPublishOptions options,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            PublishCount++;
            Options = options;
            PublishedEventIds.Add(options.EventId!.Value);
            CallOrder.Add("publish");
            return Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(
                    options.EventId!.Value,
                    @event.EventName,
                    @event.EventVersion,
                    options.CorrelationId!.Value,
                    options.CausationId,
                    options.TenantId,
                    options.Producer!,
                    options.OccurredAtUtc!.Value),
                @event));
        }
    }
}
