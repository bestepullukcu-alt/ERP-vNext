using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

/// <summary>
/// Non-runtime evidence only. The test reads producer-owned bytes from their immutable checkpoint and does not
/// register a production consumer, broker endpoint, key, hosted worker or replay command.
/// </summary>
public sealed class Mod0355GateIAuditNonRuntimeEvidenceTests
{
    private const string ProducerCheckpoint = "3e69577e787ac7ccca149b4065e83eeb97fd5146";
    private const string ConsumerCheckpoint = "58b8b0e766314a86314a888ec865bb01ed5bb54e";
    private const string Root = "execution/domains/management-governance/module-packs/fixtures/MOD-0355/audit";
    private const string ManifestPath = "execution/domains/platform-shared-services/module-packs/fixtures/MOD-0021/gate-i/gate-i-audit-intake-bilateral-acceptance-v1.json";
    private const string Producer = "Diten.ManagementGovernanceService";
    private const string Module = "MOD-0355";
    private const string EventName = "management-governance.process-modeling-audit-intent.submitted.v1";
    private const string Scheme = "management-governance-event-hmac-sha256.v1";
    private const string SigningIdentity = "diten.management-governance.process-modeling-audit-intent.v1.test-only";
    private const string KeyId = "mod-0355-process-modeling-audit-fixture-test-only-20260826";

    [Fact]
    public async Task ImmutableFixture_TraversesRealVerifierAndTestOwnedConsumerComposition()
    {
        var fixture = LoadFixture();
        Assert.Equal(258, fixture.Payload.Length);
        Assert.Equal("7ee7d2d6b19d832d71dbe89967b0964ce5932b872b8d2528fb2cf14918500f1a",
            Sha256(fixture.Payload));

        var verifier = new HmacSha256EventSignatureVerifier(
            new FixtureKeyProvider(fixture.Secret), new EventSigningInputBuilder(),
            new FixedTimeProvider(fixture.OccurredAtUtc.AddMinutes(1)));
        await verifier.VerifyAsync(fixture.Message,
            new SignedEventAuthorizationTuple(Producer, EventName, 1, Scheme, SigningIdentity),
            SigningIdentity);

        var intent = ParseCanonical(fixture.Payload, SigningIdentity);
        var mapping = Mapping(intent.EntityType, intent.Mutation);
        var acceptance = new TestOwnedAtomicAcceptance();
        Assert.Equal(GateIAuditAcceptanceResult.Accepted,
            await acceptance.AcceptAsync(fixture.Message, intent, mapping, CancellationToken.None));
        Assert.Equal(GateIAuditAcceptanceResult.Duplicate,
            await acceptance.AcceptAsync(fixture.Message, intent, mapping, CancellationToken.None));
        Assert.Equal((1, 1), (acceptance.InboxCount, acceptance.OutboxCount));
        Assert.Equal(Module, mapping.ModuleCode);
        Assert.Equal("ProcessModeling.ProcessArchitecture", mapping.AuditEntityType);
        Assert.Equal(AuditOperation.Create, mapping.Operation);
    }

    [Fact]
    public void BilateralManifest_BindsExactOwnerCheckpointWithoutCopyingFixtureBytes()
    {
        using var manifest = JsonDocument.Parse(GitBytes("show", $"{ConsumerCheckpoint}:{ManifestPath}"));
        var entry = manifest.RootElement.GetProperty("producerCases").EnumerateArray()
            .Single(item => item.GetProperty("moduleCode").GetString() == Module);
        Assert.Equal(ProducerCheckpoint, entry.GetProperty("checkpoint").GetString());
        Assert.Equal(Root, entry.GetProperty("fixtureRoot").GetString());
        Assert.False(entry.GetProperty("productionKeyProvisioned").GetBoolean());
        Assert.False(entry.GetProperty("productionKeyIdProvisioned").GetBoolean());
        Assert.Equal(20, entry.GetProperty("positivePairCount").GetInt32());
        Assert.Equal(34, entry.GetProperty("invalidCrossPairCount").GetInt32());
    }

    [Fact]
    public async Task TestOnlyTuple_IsExactAndProductionBoundaryRejectsActiveAndPreviousSlots()
    {
        var fixture = LoadFixture();
        var verifier = new HmacSha256EventSignatureVerifier(
            new FixtureKeyProvider(fixture.Secret), new EventSigningInputBuilder(),
            new FixedTimeProvider(fixture.OccurredAtUtc.AddMinutes(1)));
        await Assert.ThrowsAsync<EventSecurityException>(() => verifier.VerifyAsync(fixture.Message,
            new SignedEventAuthorizationTuple(Producer, EventName, 1, Scheme, SigningIdentity + ".wrong"),
            SigningIdentity + ".wrong").AsTask());

        var boundary = new ProductionBoundaryProvider();
        var tuple = new SignedEventAuthorizationTuple(Producer, EventName, 1, Scheme, SigningIdentity);
        await Assert.ThrowsAsync<EventSecurityException>(async () =>
            await boundary.GetVerificationKeyAsync(tuple, KeyId));
        await Assert.ThrowsAsync<EventSecurityException>(async () =>
            await boundary.GetVerificationKeyAsync(tuple, KeyId + ".previous"));
    }

    [Fact]
    public async Task DuplicateConflictRollbackUnknownCommitAndDeliveryEvidence_IsFailClosed()
    {
        var fixture = LoadFixture();
        var intent = ParseCanonical(fixture.Payload, SigningIdentity);
        var mapping = Mapping(intent.EntityType, intent.Mutation);
        var acceptance = new TestOwnedAtomicAcceptance();
        await acceptance.AcceptAsync(fixture.Message, intent, mapping, CancellationToken.None);

        var changedPayload = MutateEntityId(fixture.Payload);
        var changedMessage = new EventTransportMessage(fixture.Message.EventId, fixture.Message.EventName,
            fixture.Message.EventVersion, fixture.Message.CorrelationId, fixture.Message.CausationId,
            fixture.Message.TenantId, fixture.Message.Producer, fixture.Message.OccurredAtUtc, changedPayload,
            fixture.Message.TransportMetadata);
        await Assert.ThrowsAsync<GateIAuditConflictException>(() => acceptance.AcceptAsync(
            changedMessage,
            intent with { PayloadSha256 = Sha256(MutateEntityId(fixture.Payload)) }, mapping, CancellationToken.None));
        Assert.Equal((1, 1), (acceptance.InboxCount, acceptance.OutboxCount));

        var faulted = new TestOwnedAtomicAcceptance { FailAfterInbox = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            faulted.AcceptAsync(fixture.Message, intent, mapping, CancellationToken.None));
        Assert.Equal((0, 0), (faulted.InboxCount, faulted.OutboxCount));

        var unknown = new TestOwnedCommitOnlyCoordinator(unknownBeforeSuccess: 1);
        Assert.Equal(DeliveryDisposition.Accepted, await unknown.ExecuteAsync());
        Assert.Equal(1, unknown.BodyExecutions);
        Assert.Equal(2, unknown.CommitAttempts);

        var delivery = new TestOwnedDeliveryEvidence(maxAttempts: 3);
        Assert.Equal(DeliveryDisposition.DeadLettered, delivery.TransientExhaustion());
        Assert.Equal(3, delivery.Attempts);
        Assert.Equal(1, delivery.AlarmCount);
        Assert.Equal(DeliveryDisposition.Replayed, delivery.AuthorizedReplay(exactPayload: true, authorized: true));
        Assert.Throws<EventSecurityException>(() => delivery.AuthorizedReplay(exactPayload: true, authorized: false));
        Assert.Throws<GateIAuditConflictException>(() => delivery.AuthorizedReplay(exactPayload: false, authorized: true));
    }

    [Fact]
    public void CompleteAllowlistAndCrossPairs_ProjectExactlyOrFailTerminally()
    {
        using var matrix = JsonDocument.Parse(GitBytes("show", $"{ProducerCheckpoint}:{Root}/process-modeling-audit-intent-submitted-v1.allowlist-matrix.json"));
        var positive = matrix.RootElement.GetProperty("positive").EnumerateArray().ToArray();
        var negative = matrix.RootElement.GetProperty("invalidCrossPairs").EnumerateArray().ToArray();
        Assert.Equal(20, positive.Length);
        Assert.Equal(34, negative.Length);
        foreach (var pair in positive)
            Assert.StartsWith("ProcessModeling.", Mapping(pair[0].GetString()!, pair[1].GetString()!).AuditEntityType);
        foreach (var pair in negative)
            Assert.Throws<EventUnsupportedException>(() => Mapping(pair[0].GetString()!, pair[1].GetString()!));
    }

    private static GateIAuditMapping Mapping(string entity, string mutation)
        => new GateIAuditAdapterRegistry().Resolve(Producer, EventName, 1, entity, mutation);

    private static GateIAuditIntent ParseCanonical(byte[] payload, string identity)
    {
        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        return new(Guid.Parse(root.GetProperty("auditIntentId").GetString()!),
            Guid.Parse(root.GetProperty("actorId").GetString()!), root.GetProperty("entityType").GetString()!,
            Guid.Parse(root.GetProperty("entityId").GetString()!), root.GetProperty("mutation").GetString()!,
            DateTimeOffset.Parse(root.GetProperty("occurredAtUtc").GetString()!), payload, Sha256(payload), identity);
    }

    private static Fixture LoadFixture()
    {
        var payload = GitBytes("show", $"{ProducerCheckpoint}:{Root}/process-modeling-audit-intent-submitted-v1.canonical.json");
        using var vector = JsonDocument.Parse(GitBytes("show", $"{ProducerCheckpoint}:{Root}/process-modeling-audit-intent-submitted-v1.signing-vector.test-only.json"));
        var v = vector.RootElement;
        var headers = new TrustedTransportMetadata([
            KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, Scheme),
            KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, KeyId),
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, v.GetProperty("expectedHmacSha256").GetString()!)
        ]);
        var occurred = DateTimeOffset.Parse(v.GetProperty("occurredAtUtc").GetString()!);
        return new(Convert.FromBase64String(v.GetProperty("keyBase64").GetString()!), payload, occurred,
            new EventTransportMessage(Guid.Parse(v.GetProperty("eventId").GetString()!), EventName, 1,
                Guid.Parse(v.GetProperty("correlationId").GetString()!), null,
                Guid.Parse(v.GetProperty("tenantId").GetString()!), Producer, occurred, payload, headers));
    }

    private static byte[] MutateEntityId(byte[] payload) => Encoding.UTF8.GetBytes(
        Encoding.UTF8.GetString(payload).Replace("35000355-0000-4000-8000-000000000003",
            "99999999-9999-4999-8999-999999999999", StringComparison.Ordinal));

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] GitBytes(params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Unable to start git.");
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output.ToArray();
    }

    private sealed class FixtureKeyProvider(byte[] secret) : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(SignedEventAuthorizationTuple authorization,
            string keyId, CancellationToken cancellationToken = default)
        {
            if (authorization != new SignedEventAuthorizationTuple(Producer, EventName, 1, Scheme, SigningIdentity)
                || keyId != KeyId)
                throw new EventSecurityException("Test-only tuple mismatch.", "mod-0355.fixture.untrusted");
            return ValueTask.FromResult(new EventVerificationKey(keyId, secret));
        }
    }

    private sealed class ProductionBoundaryProvider : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(SignedEventAuthorizationTuple authorization,
            string keyId, CancellationToken cancellationToken = default) =>
            throw new EventSecurityException("Test-only MOD-0355 material is forbidden in production.",
                "mod-0355.fixture.production-rejected");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestOwnedAtomicAcceptance : IGateIAuditAcceptanceRepository
    {
        private readonly Dictionary<Guid, (string Payload, string Metadata)> _rows = [];
        public bool FailAfterInbox { get; init; }
        public int InboxCount { get; private set; }
        public int OutboxCount { get; private set; }

        public Task<GateIAuditAcceptanceResult> AcceptAsync(EventTransportMessage message, GateIAuditIntent intent,
            GateIAuditMapping mapping, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = Sha256(Encoding.UTF8.GetBytes($"{message.Producer}\n{message.EventName}\n{mapping.ModuleCode}\n{intent.VerifiedSigningIdentity}"));
            if (_rows.TryGetValue(message.EventId, out var existing))
            {
                if (existing != (intent.PayloadSha256, metadata)) throw new GateIAuditConflictException();
                return Task.FromResult(GateIAuditAcceptanceResult.Duplicate);
            }
            if (FailAfterInbox) throw new InvalidOperationException("Injected outbox failure; transaction rolled back.");
            _rows.Add(message.EventId, (intent.PayloadSha256, metadata));
            InboxCount++;
            OutboxCount++;
            return Task.FromResult(GateIAuditAcceptanceResult.Accepted);
        }
    }

    private sealed class TestOwnedCommitOnlyCoordinator(int unknownBeforeSuccess)
    {
        public int BodyExecutions { get; private set; }
        public int CommitAttempts { get; private set; }
        public Task<DeliveryDisposition> ExecuteAsync()
        {
            BodyExecutions++;
            for (var attempt = 0; ; attempt++)
            {
                CommitAttempts++;
                if (attempt >= unknownBeforeSuccess) return Task.FromResult(DeliveryDisposition.Accepted);
            }
        }
    }

    private sealed class TestOwnedDeliveryEvidence(int maxAttempts)
    {
        public int Attempts { get; private set; }
        public int AlarmCount { get; private set; }
        public DeliveryDisposition TransientExhaustion()
        {
            Attempts = maxAttempts;
            AlarmCount = 1;
            return DeliveryDisposition.DeadLettered;
        }
        public DeliveryDisposition AuthorizedReplay(bool exactPayload, bool authorized)
        {
            if (!authorized) throw new EventSecurityException("Replay is unauthorized.", "gate-i.replay.unauthorized");
            if (!exactPayload) throw new GateIAuditConflictException();
            return DeliveryDisposition.Replayed;
        }
    }

    private enum DeliveryDisposition { Accepted, DeadLettered, Replayed }
    private sealed record Fixture(byte[] Secret, byte[] Payload, DateTimeOffset OccurredAtUtc,
        EventTransportMessage Message);
}
