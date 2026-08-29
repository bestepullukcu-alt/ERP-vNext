using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Xunit;

namespace Diten.BuildingBlocks.Eventing.Tests;

public sealed class SignedEventSecurityFoundationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-06T12:00:00Z");
    private static readonly byte[] VerificationMaterial = SHA256.HashData(
        Encoding.UTF8.GetBytes("gate-i-contract-fixture-material-v1"));
    private static readonly SignedEventAuthorizationTuple Authorization = new(
        "Diten.FpaService",
        "fpa.budgeting-audit-intent.submitted.v1",
        1,
        "fpa-budgeting-event-hmac-sha256.v1",
        "diten.fpa.budgeting.audit");

    [Fact]
    public void AuthorizationTuple_IsExactOrdinal()
    {
        Assert.True(Authorization.Matches(new(
            Authorization.Producer, Authorization.EventName, 1, Authorization.Scheme, Authorization.SigningIdentity)));
        Assert.False(Authorization.Matches(new(
            Authorization.Producer.ToLowerInvariant(), Authorization.EventName, 1, Authorization.Scheme,
            Authorization.SigningIdentity)));
        Assert.Throws<EventContractException>(() => new SignedEventAuthorizationTuple(
            Authorization.Producer + " ", Authorization.EventName, 1, Authorization.Scheme, Authorization.SigningIdentity));
    }

    [Theory]
    [InlineData("X-Diten-Event-Signature")]
    [InlineData("x-diten-event-signature")]
    public void RawExtractor_RejectsDuplicateAndCaseDuplicateBeforeMapping(string secondName)
    {
        var raw = Headers("a").Concat([KeyValuePair.Create(secondName, "b")]);
        var exception = Assert.Throws<EventContractException>(() => RawSignatureHeaderExtractor.Extract(raw));
        Assert.Equal("event.header.duplicate", exception.Failure.ReasonCode);
    }

    [Theory]
    [InlineData("x-diten-event-signature-scheme", "scheme")]
    [InlineData("X-Diten-Event-Signature-Scheme", " scheme")]
    [InlineData("X-Diten-Event-Signature-Scheme", "scheme\r\nforged")]
    [InlineData("X-Unknown", "value")]
    public void RawExtractor_FailsClosedForMutationMatrix(string name, string value)
    {
        var raw = Headers("a").Select(header =>
            header.Key == TrustedTransportMetadata.SignatureSchemeHeader
                ? KeyValuePair.Create(name, value)
                : header);
        Assert.Throws<EventContractException>(() => RawSignatureHeaderExtractor.Extract(raw));
    }

    [Fact]
    public void TrustedMetadata_UsesExactOrdinalNames()
    {
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([
            KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader.ToLowerInvariant(), "scheme"),
            KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "key"),
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, new string('a', 64))]));
    }

    [Fact]
    public async Task ValidSignature_AtFreshnessBoundaries_IsAccepted()
    {
        await Verifier().VerifyAsync(SignedMessage(Now - TimeSpan.FromHours(24)), Authorization,
            Authorization.SigningIdentity);
        await Verifier().VerifyAsync(SignedMessage(Now + TimeSpan.FromMinutes(2)), Authorization,
            Authorization.SigningIdentity);
    }

    [Theory]
    [InlineData(-86400, -1)]
    [InlineData(120, 1)]
    public async Task Freshness_ImmediatelyOutsideBoundary_TerminallyRejects(int seconds, int delta)
    {
        var occurred = Now + TimeSpan.FromSeconds(seconds + delta);
        var exception = await Assert.ThrowsAsync<EventSecurityException>(() =>
            Verifier().VerifyAsync(SignedMessage(occurred), Authorization, Authorization.SigningIdentity).AsTask());
        Assert.Equal(EventOutboxTerminalFailureKind.Security, exception.Failure.Kind);
    }

    [Fact]
    public async Task TupleHeaderPayloadAndSignatureMutations_Reject()
    {
        var cases = new[]
        {
            SignedMessage(Now, producer: "diten.fpaservice"),
            SignedMessage(Now, scheme: Authorization.Scheme.ToUpperInvariant()),
            SignedMessage(Now, signatureOverride: new string('A', 64)),
            SignedMessage(Now, signatureOverride: new string('0', 64)),
            SignedMessage(Now, payload: "{\"changed\":true}", signDifferentPayload: true)
        };

        foreach (var message in cases)
        {
            await Assert.ThrowsAnyAsync<EventTransportTerminalExceptionBase>(() =>
                Verifier().VerifyAsync(message, Authorization, Authorization.SigningIdentity).AsTask());
        }
    }

    [Fact]
    public async Task PreviousKey_AcceptsBeforeDeadline_RejectsAtDeadline_AndRevocationOverridesOverlap()
    {
        await Verifier(new EventVerificationKey("key-1", VerificationMaterial, true, Now + TimeSpan.FromTicks(1)))
            .VerifyAsync(SignedMessage(Now), Authorization, Authorization.SigningIdentity);
        await Assert.ThrowsAsync<EventSecurityException>(() =>
            Verifier(new EventVerificationKey("key-1", VerificationMaterial, true, Now))
                .VerifyAsync(SignedMessage(Now), Authorization, Authorization.SigningIdentity).AsTask());
        await Assert.ThrowsAsync<EventSecurityException>(() =>
            Verifier(new EventVerificationKey("key-1", VerificationMaterial, true, Now + TimeSpan.FromHours(1), Now))
                .VerifyAsync(SignedMessage(Now), Authorization, Authorization.SigningIdentity).AsTask());
    }

    [Fact]
    public void KeySet_EnforcesExactlyOneActiveAndAtMostOnePrevious()
    {
        var active = new EventVerificationKey("active", VerificationMaterial);
        var previous = new EventVerificationKey("previous", VerificationMaterial, true, Now + TimeSpan.FromHours(1));
        var set = new EventVerificationKeySet(active, previous);
        Assert.Same(active, set.Resolve("active", Now));
        Assert.Same(previous, set.Resolve("previous", Now));
        Assert.Throws<EventValidationException>(() => new EventVerificationKeySet(
            active, new EventVerificationKey("active", VerificationMaterial, true, Now + TimeSpan.FromHours(1))));
        Assert.Throws<EventSecurityException>(() => set.Resolve("ACTIVE", Now));
    }

    [Fact]
    public async Task DependencyFailure_IsTransientButTerminalFailureIsNotWrapped()
    {
        var dependencyVerifier = new HmacSha256EventSignatureVerifier(
            new ThrowingProvider(new IOException("vault unavailable")), new EventSigningInputBuilder(),
            new FixedTimeProvider(Now));
        await Assert.ThrowsAsync<EventDependencyException>(() => dependencyVerifier
            .VerifyAsync(SignedMessage(Now), Authorization, Authorization.SigningIdentity).AsTask());

        var terminalVerifier = new HmacSha256EventSignatureVerifier(
            new ThrowingProvider(new EventUnsupportedException("unsupported", "event.scheme.unsupported")),
            new EventSigningInputBuilder(), new FixedTimeProvider(Now));
        var terminal = await Assert.ThrowsAsync<EventUnsupportedException>(() => terminalVerifier
            .VerifyAsync(SignedMessage(Now), Authorization, Authorization.SigningIdentity).AsTask());
        Assert.Equal(EventOutboxTerminalFailureKind.Unsupported, terminal.Failure.Kind);
    }

    [Fact]
    public void FailureTypes_KeepAllFourTerminalKindsDistinct()
    {
        Assert.Equal(EventOutboxTerminalFailureKind.Contract,
            new EventContractException("x", "event.contract").Failure.Kind);
        Assert.Equal(EventOutboxTerminalFailureKind.Security,
            new EventSecurityException("x", "event.security").Failure.Kind);
        Assert.Equal(EventOutboxTerminalFailureKind.Validation,
            new EventTerminalValidationException("x", "event.validation").Failure.Kind);
        Assert.Equal(EventOutboxTerminalFailureKind.Unsupported,
            new EventUnsupportedException("x", "event.unsupported").Failure.Kind);
    }

    private static HmacSha256EventSignatureVerifier Verifier(EventVerificationKey? key = null) => new(
        new FixedKeyProvider(key ?? new EventVerificationKey("key-1", VerificationMaterial)),
        new EventSigningInputBuilder(),
        new FixedTimeProvider(Now));

    private static EventTransportMessage SignedMessage(
        DateTimeOffset occurredAtUtc,
        string? producer = null,
        string? scheme = null,
        string? signatureOverride = null,
        string payload = "{\"auditIntentId\":\"1\"}",
        bool signDifferentPayload = false)
    {
        var actualScheme = scheme ?? Authorization.Scheme;
        var unsigned = Message(occurredAtUtc, producer ?? Authorization.Producer, payload,
            RawSignatureHeaderExtractor.Extract(Headers(new string('0', 64), actualScheme)));
        var inputMessage = signDifferentPayload
            ? Message(occurredAtUtc, producer ?? Authorization.Producer, "{\"original\":true}", unsigned.TransportMetadata)
            : unsigned;
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            VerificationMaterial, new EventSigningInputBuilder().Build(inputMessage, actualScheme))).ToLowerInvariant();
        return Message(occurredAtUtc, producer ?? Authorization.Producer, payload,
            RawSignatureHeaderExtractor.Extract(Headers(signatureOverride ?? signature, actualScheme)));
    }

    private static EventTransportMessage Message(
        DateTimeOffset occurredAtUtc,
        string producer,
        string payload,
        TrustedTransportMetadata metadata) => new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Authorization.EventName,
            1,
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            null,
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
            producer,
            occurredAtUtc,
            payload,
            metadata);

    private static IEnumerable<KeyValuePair<string, string>> Headers(string signature, string? scheme = null) =>
    [
        KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, scheme ?? Authorization.Scheme),
        KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "key-1"),
        KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, signature)
    ];

    private sealed class FixedKeyProvider(EventVerificationKey key) : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(
            SignedEventAuthorizationTuple authorization,
            string keyId,
            CancellationToken cancellationToken = default) => ValueTask.FromResult(key);
    }

    private sealed class ThrowingProvider(Exception exception) : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(
            SignedEventAuthorizationTuple authorization,
            string keyId,
            CancellationToken cancellationToken = default) => ValueTask.FromException<EventVerificationKey>(exception);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
