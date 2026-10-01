using System.Text;
using Diten.Platform.API.Models.Audit;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentTwoServiceContractTests
{
    [Theory]
    [InlineData("ProductLegalEntityScopeEnforcementActivated", AuditOperation.Activate)]
    [InlineData("ProductLegalEntityScopeEnforcementSuspended", AuditOperation.Suspend)]
    public async Task ExactMdmWireFixture_ProducesDurableProviderReceipt(
        string sourceOperation,
        AuditOperation platformOperation)
    {
        var parser = new TrustedSourceAuditIntentRequestParser();
        var sourceEnvelope = TrustedSourceAuditIntentTestData.Envelope(
            intentId: Guid.NewGuid(),
            operation: sourceOperation);
        var wire = Encoding.UTF8.GetBytes(TrustedSourceAuditIntentTestData.Json(sourceEnvelope));
        Assert.True(parser.TryParse(wire, out var request));
        var outbox = new ReplayOutbox();
        var provider = new TrustedSourceAuditIntentAcceptanceService(outbox, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));

        var first = await provider.AcceptAsync(request!.ToEnvelope());
        var replay = await provider.AcceptAsync(request.ToEnvelope());

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted, first.Status);
        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Duplicate, replay.Status);
        Assert.Equal(first.Receipt!.CentralAcknowledgement, replay.Receipt!.CentralAcknowledgement);
        Assert.Equal(sourceEnvelope.ContractVersion, first.Receipt.ContractVersion);
        Assert.Equal(platformOperation, outbox.Operation);
        Assert.Equal(1, outbox.DurableRows);
    }

    [Fact]
    public async Task SourceAcknowledgementLoss_ReplaysSameReceipt_AndDriftConflicts()
    {
        var outbox = new ReplayOutbox();
        var provider = new TrustedSourceAuditIntentAcceptanceService(outbox, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));
        var source = TrustedSourceAuditIntentTestData.Envelope(intentId: Guid.NewGuid());

        _ = await provider.AcceptAsync(source); // Provider accepted; source did not persist local acknowledgement.
        var recovered = await provider.AcceptAsync(source);
        var drift = await provider.AcceptAsync(source with { CommandId = "different-command" });

        Assert.True(recovered.Receipt!.Duplicate);
        Assert.Equal("central-ack-1", recovered.Receipt.CentralAcknowledgement);
        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.IdempotencyConflict, drift.Status);
        Assert.Equal(1, outbox.DurableRows);
    }

    private sealed class ReplayOutbox : ITrustedSourceAuditIntentOutbox
    {
        private string? _key;
        private string? _fingerprint;
        private TrustedSourceAuditIntentAcceptanceReceipt? _receipt;
        public int DurableRows { get; private set; }
        public AuditOperation Operation { get; private set; }

        public Task<TrustedSourceAuditIntentAcceptanceResult> AcceptAsync(
            TrustedSourceAuditIntentEnvelope envelope,
            string centralIdempotencyKey,
            string sourceIntentFingerprint,
            string mappedEntityType,
            AuditOperation mappedOperation,
            CancellationToken ct = default)
        {
            Operation = mappedOperation;
            if (_key is null)
            {
                _key = centralIdempotencyKey;
                _fingerprint = sourceIntentFingerprint;
                _receipt = new("central-ack-1", centralIdempotencyKey, envelope.ContractVersion, TrustedSourceAuditIntentTestData.Now, false);
                DurableRows++;
                return Task.FromResult(TrustedSourceAuditIntentAcceptanceResult.Accepted(_receipt));
            }

            return Task.FromResult(string.Equals(_fingerprint, sourceIntentFingerprint, StringComparison.Ordinal)
                ? TrustedSourceAuditIntentAcceptanceResult.Duplicate(_receipt!)
                : TrustedSourceAuditIntentAcceptanceResult.IdempotencyConflict());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
