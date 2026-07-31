using System.Security.Cryptography;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditConsumerProcessorTests
{
    private const string Secret = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ValidSignedMessageIsAcceptedAndTenantScopeIsRestored()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var tenantContext = new TenantContext();
        var outerTenant = Guid.NewGuid();
        tenantContext.SetTenant(outerTenant);
        var message = Message(Payload());

        await Processor(repository, observer, tenantContext).ProcessAsync(
            message, PpmAuditIntentParser.SignatureScheme, "current", Sign(message),
            retryAttempt: 0, CancellationToken.None);

        Assert.Equal(1, repository.Attempts);
        Assert.Equal(TenantId, repository.TenantObserved);
        Assert.Equal(outerTenant, tenantContext.TenantId);
        Assert.Equal(0, observer.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task MissingOrWrongSignatureFailsWithoutRepositoryAttempt(string? signature)
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = Message(Payload());

        await Assert.ThrowsAsync<PpmAuditSecurityException>(() =>
            Processor(repository, observer).ProcessAsync(
                message, PpmAuditIntentParser.SignatureScheme, "current",
                signature ?? null, 0, CancellationToken.None));

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public async Task UppercaseSignatureFailsWithoutRepositoryAttempt()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = Message(Payload());

        await Assert.ThrowsAsync<PpmAuditSecurityException>(() =>
            Processor(repository, observer).ProcessAsync(
                message, PpmAuditIntentParser.SignatureScheme, "current",
                Sign(message).ToUpperInvariant(), 0, CancellationToken.None));

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public async Task MalformedJsonIsOneAttemptContractFailure()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = Message(Payload()[..^1]);

        await Assert.ThrowsAsync<PpmAuditContractException>(() =>
            Processor(repository, observer).ProcessAsync(
                message, PpmAuditIntentParser.SignatureScheme, "current", "0".PadLeft(64, '0'),
                0, CancellationToken.None));

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public async Task ChangedPayloadWithOldSignatureIsNotRetried()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var original = Message(Payload());
        var changed = Message(Payload().Replace("\"created\"", "\"updated\""));

        await Assert.ThrowsAsync<PpmAuditSecurityException>(() =>
            Processor(repository, observer).ProcessAsync(
                changed, PpmAuditIntentParser.SignatureScheme, "current", Sign(original),
                0, CancellationToken.None));

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public async Task TransientFailureUsesFiveTotalAttemptsAndDeadLettersOnlyAtTerminalAttempt()
    {
        var repository = new StubRepository { Failure = new InvalidOperationException("transient") };
        var observer = new StubObserver();
        var message = Message(Payload());
        var processor = Processor(repository, observer);

        for (var attempt = 0; attempt < PpmAuditRetryPolicy.RetryCount; attempt++)
        {
            var transient = await Assert.ThrowsAsync<PpmAuditTransientException>(() =>
                processor.ProcessAsync(
                    message, PpmAuditIntentParser.SignatureScheme, "current", Sign(message),
                    attempt, CancellationToken.None));
            Assert.IsType<InvalidOperationException>(transient.InnerException);
        }

        await Assert.ThrowsAsync<PpmAuditRetriesExhaustedException>(() =>
            processor.ProcessAsync(
                message, PpmAuditIntentParser.SignatureScheme, "current", Sign(message),
                PpmAuditRetryPolicy.RetryCount, CancellationToken.None));

        Assert.Equal(5, repository.Attempts);
        Assert.Equal(1, observer.Count);
        var exhausted = Assert.IsType<PpmAuditRetriesExhaustedException>(observer.LastException);
        Assert.IsType<InvalidOperationException>(exhausted.InnerException);
    }

    [Fact]
    public async Task UnknownEventNameIsSafelyIgnored()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = new EventTransportMessage(
            EventId, "ppm.unknown.v1", 1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"), null, TenantId,
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            Payload());

        await Processor(repository, observer).ProcessAsync(
            message, null, null, null, 0, CancellationToken.None);

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(0, observer.Count);
    }

    [Fact]
    public async Task FuturePpmV2EventIdentityIsSafelyIgnoredByV1Consumer()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = new EventTransportMessage(
            EventId, "ppm.audit-intent.submitted.v2", 2,
            Guid.Parse("55555555-5555-5555-5555-555555555555"), null, TenantId,
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            Payload());

        await Processor(repository, observer).ProcessAsync(
            message, null, null, null, 0, CancellationToken.None);

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(0, observer.Count);
    }

    [Fact]
    public async Task ExactV1IdentityWithWrongProducerIsContractFailure()
    {
        var repository = new StubRepository();
        var observer = new StubObserver();
        var message = new EventTransportMessage(
            EventId, PpmAuditIntentParser.EventName, 1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"), null, TenantId,
            "Other.Producer",
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            Payload());

        await Assert.ThrowsAsync<PpmAuditContractException>(() =>
            Processor(repository, observer).ProcessAsync(
                message, PpmAuditIntentParser.SignatureScheme, "current",
                new string('0', 64), 0, CancellationToken.None));

        Assert.Equal(0, repository.Attempts);
        Assert.Equal(1, observer.Count);
    }

    [Fact]
    public void ExactV1NameWithVersion2IsRejectedBySharedTransportContract()
    {
        Assert.Throws<Diten.BuildingBlocks.Eventing.EventValidationException>(() =>
            new EventTransportMessage(
                EventId, PpmAuditIntentParser.EventName, 2,
                Guid.Parse("55555555-5555-5555-5555-555555555555"), null, TenantId,
                PpmAuditIntentParser.Producer,
                DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
                Payload()));
    }

    private static PpmAuditConsumerProcessor Processor(
        StubRepository repository,
        StubObserver observer,
        TenantContext? tenantContext = null) =>
        new(
            new PpmAuditSignatureVerifier(Options.Create(new PpmAuditConsumerOptions
            {
                Enabled = true,
                ActiveKeyId = "current",
                ActiveSecret = Secret
            })),
            repository,
            tenantContext ?? new TenantContext(),
            observer,
            NullLogger<PpmAuditConsumerProcessor>.Instance);

    private static string Sign(EventTransportMessage message)
    {
        var intent = PpmAuditIntentParser.Parse(message);
        return Convert.ToHexString(HMACSHA256.HashData(
            Convert.FromBase64String(Secret),
            PpmAuditIntentParser.BuildSigningInput(message, intent.CanonicalPayload))).ToLowerInvariant();
    }

    private static EventTransportMessage Message(string payload) =>
        new(
            EventId,
            PpmAuditIntentParser.EventName,
            1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            TenantId,
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            payload);

    private static string Payload() =>
        "{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"entityType\":\"Project\",\"mutation\":\"created\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}";

    private sealed class StubRepository : IPpmAuditAcceptanceRepository
    {
        public int Attempts { get; private set; }
        public Guid? TenantObserved { get; private set; }
        public Exception? Failure { get; init; }

        public Task<PpmAuditAcceptanceResult> AcceptAsync(
            EventTransportMessage message,
            PpmAuditIntent intent,
            CancellationToken cancellationToken)
        {
            Attempts++;
            TenantObserved = message.TenantId;
            return Failure is null
                ? Task.FromResult(PpmAuditAcceptanceResult.Accepted)
                : Task.FromException<PpmAuditAcceptanceResult>(Failure);
        }
    }

    private sealed class StubObserver : IPpmAuditDeadLetterObserver
    {
        public int Count { get; private set; }
        public Exception? LastException { get; private set; }

        public void Record(EventTransportMessage message, Exception exception)
        {
            Count++;
            LastException = exception;
        }
    }
}
