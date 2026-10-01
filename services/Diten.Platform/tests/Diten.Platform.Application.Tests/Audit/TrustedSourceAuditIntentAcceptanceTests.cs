using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentAcceptanceTests
{
    [Fact]
    public async Task Accept_ValidEnvelope_UsesCanonicalKeyFingerprintAndMappedOperation()
    {
        var outbox = new CapturingOutbox();
        var service = new TrustedSourceAuditIntentAcceptanceService(outbox, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));
        var envelope = TrustedSourceAuditIntentTestData.Envelope();

        var result = await service.AcceptAsync(envelope);

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted, result.Status);
        Assert.Equal(TrustedSourceAuditIntentCanonicalizer.BuildCentralIdempotencyKey(envelope), outbox.CentralKey);
        Assert.Equal(TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(envelope), outbox.Fingerprint);
        Assert.Equal("ProductLegalEntityScopeRolloutState", outbox.EntityType);
        Assert.Equal(AuditOperation.Activate, outbox.Operation);
    }

    [Theory]
    [InlineData("Diten.Mdm", "mod-0290.audit-intent.v1", TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.ContractUnsupported)]
    [InlineData("Diten.MDM", "MOD-0290.AUDIT-INTENT.V1", TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.ContractUnsupported)]
    [InlineData("Diten.MDM", "mod-0290.audit-intent.v1", TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.MappingUnsupported, "Unknown")]
    public async Task Accept_FailsClosedForContractAndMappingDrift(
        string source,
        string contract,
        TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus status,
        string? operation = null)
    {
        var outbox = new CapturingOutbox();
        var service = new TrustedSourceAuditIntentAcceptanceService(outbox, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));
        var envelope = TrustedSourceAuditIntentTestData.Envelope() with
        {
            SourceService = source,
            ContractVersion = contract,
            Operation = operation ?? "ProductLegalEntityScopeEnforcementActivated"
        };

        var result = await service.AcceptAsync(envelope);

        Assert.Equal(status, result.Status);
        Assert.Equal(0, outbox.Calls);
    }

    public static IEnumerable<object[]> InvalidEnvelopes()
    {
        var valid = TrustedSourceAuditIntentTestData.Envelope();
        yield return [valid with { IntentId = Guid.Empty }];
        yield return [valid with { PostVersion = valid.PreVersion + 2 }];
        yield return [valid with { Sequence = -1 }];
        yield return [valid with { TimestampUtc = valid.TimestampUtc.ToOffset(TimeSpan.FromHours(3)) }];
        yield return [valid with { TimestampUtc = TrustedSourceAuditIntentTestData.Now.AddMinutes(6) }];
        yield return [valid with { ActorId = " actor " }];
        yield return [valid with { EvidenceHash = new string('a', 64) }];
        yield return [valid with { SnapshotReference = string.Empty }];
        yield return [valid with { SnapshotReference = new string('x', 257) }];
        yield return [valid with { SnapshotReference = "<script>alert(1)</script>" }];
    }

    [Theory]
    [MemberData(nameof(InvalidEnvelopes))]
    public async Task Accept_InvalidEnvelope_DoesNotReachOutbox(TrustedSourceAuditIntentEnvelope envelope)
    {
        var outbox = new CapturingOutbox();
        var service = new TrustedSourceAuditIntentAcceptanceService(outbox, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));

        var result = await service.AcceptAsync(envelope);

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Invalid, result.Status);
        Assert.Equal(0, outbox.Calls);
    }

    private sealed class CapturingOutbox : ITrustedSourceAuditIntentOutbox
    {
        public int Calls { get; private set; }
        public string? CentralKey { get; private set; }
        public string? Fingerprint { get; private set; }
        public string? EntityType { get; private set; }
        public AuditOperation Operation { get; private set; }

        public Task<TrustedSourceAuditIntentAcceptanceResult> AcceptAsync(
            TrustedSourceAuditIntentEnvelope envelope,
            string centralIdempotencyKey,
            string sourceIntentFingerprint,
            string mappedEntityType,
            AuditOperation mappedOperation,
            CancellationToken ct = default)
        {
            Calls++;
            CentralKey = centralIdempotencyKey;
            Fingerprint = sourceIntentFingerprint;
            EntityType = mappedEntityType;
            Operation = mappedOperation;
            return Task.FromResult(TrustedSourceAuditIntentAcceptanceResult.Accepted(new(
                "ack",
                centralIdempotencyKey,
                envelope.ContractVersion,
                TrustedSourceAuditIntentTestData.Now,
                false)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
