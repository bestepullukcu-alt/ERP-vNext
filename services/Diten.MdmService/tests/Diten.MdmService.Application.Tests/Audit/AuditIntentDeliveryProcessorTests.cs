using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuditIntentDeliveryProcessorTests
{
    [Fact]
    public async Task Accepted_delivery_uses_exact_envelope_and_compacts_receipt()
    {
        var repository = new Repository();
        var identity = new IdentityProvider();
        var client = new Client((envelope, _) => TrustedSourceAuditIntentDeliveryResult.Accepted(
            new TrustedSourceAuditIntentAcceptanceReceipt(
                "ack-1",
                AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                envelope.ContractVersion,
                DateTimeOffset.UtcNow,
                false)));

        var result = await Processor(repository, identity, client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal((1, 1, 1, 0), (result.Discovered, result.Claimed, result.Accepted, result.ClaimConflicts));
        Assert.True(repository.Acknowledged);
        Assert.False(identity.ForceRefreshCalls.Single());
        Assert.Equal(AuditIntentContract.SourceService, client.Envelopes.Single().SourceService);
        Assert.Equal(AuditIntentDeliveryProcessor.RequiredContractVersion, client.Envelopes.Single().ContractVersion);
    }

    [Fact]
    public async Task Authentication_rejection_forces_one_refresh_then_accepts()
    {
        var repository = new Repository();
        var identity = new IdentityProvider();
        var calls = 0;
        var client = new Client((envelope, _) => ++calls == 1
            ? TrustedSourceAuditIntentDeliveryResult.AuthenticationRejected("AUTH")
            : TrustedSourceAuditIntentDeliveryResult.Accepted(new TrustedSourceAuditIntentAcceptanceReceipt(
                "ack-2",
                AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                envelope.ContractVersion,
                DateTimeOffset.UtcNow,
                false)));

        var result = await Processor(repository, identity, client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal([false, true], identity.ForceRefreshCalls);
        Assert.Equal(2, client.Envelopes.Count);
        Assert.Equal(1, result.Accepted);
    }

    [Fact]
    public async Task Second_authentication_rejection_is_terminal_after_exactly_one_reacquisition()
    {
        var repository = new Repository();
        var identity = new IdentityProvider();
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.AuthenticationRejected("FORBIDDEN"));

        var result = await Processor(repository, identity, client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal([false, true], identity.ForceRefreshCalls);
        Assert.Equal(2, client.Envelopes.Count);
        Assert.Equal(1, result.DeadLettered);
    }

    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public async Task Failure_is_retry_scheduled_or_dead_lettered(bool retryable, int retries, int deadLetters)
    {
        var repository = new Repository();
        var client = new Client((_, _) => retryable
            ? TrustedSourceAuditIntentDeliveryResult.Retryable("TEMP")
            : TrustedSourceAuditIntentDeliveryResult.Terminal("BAD"));

        var result = await Processor(repository, new IdentityProvider(), client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(retries, result.RetryScheduled);
        Assert.Equal(deadLetters, result.DeadLettered);
    }

    [Fact]
    public async Task Cross_tenant_discovery_fails_before_claim()
    {
        var repository = new Repository { ReturnedTenantId = Guid.NewGuid() };
        var processor = Processor(repository, new IdentityProvider(), new Client((_, _) => throw new InvalidOperationException()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3));

        Assert.Equal("AUDIT_INTENT_TENANT_PARTITION_VIOLATION", error.Message);
        Assert.False(repository.ClaimAttempted);
    }

    [Fact]
    public async Task Malformed_accepted_receipt_is_dead_lettered_without_worker_exception()
    {
        var repository = new Repository();
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.Accepted(
            new TrustedSourceAuditIntentAcceptanceReceipt(
                "bad acknowledgement ", "wrong-key", "wrong-version", DateTimeOffset.UtcNow, false)));

        var result = await Processor(repository, new IdentityProvider(), client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(1, result.DeadLettered);
        Assert.False(repository.Acknowledged);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Malformed_claimed_payload_is_dead_lettered_and_does_not_escape_worker_cycle(bool throwDuringRead)
    {
        var repository = new Repository
        {
            ThrowDuringPayloadRead = throwDuringRead,
            ReturnWrongContract = !throwDuringRead
        };

        var result = await Processor(repository, new IdentityProvider(), new Client((_, _) =>
            throw new InvalidOperationException("must not dispatch"))).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(1, result.DeadLettered);
        Assert.Equal("AUDIT_SOURCE_INTENT_CLAIMED_PAYLOAD_INVALID", repository.LastDeadLetterReason);
    }

    [Fact]
    public async Task Response_loss_retry_then_exact_replay_produces_one_atomic_acknowledgement()
    {
        var repository = new Repository();
        var calls = 0;
        var client = new Client((envelope, _) => ++calls == 1
            ? TrustedSourceAuditIntentDeliveryResult.Retryable("RESPONSE_LOST")
            : TrustedSourceAuditIntentDeliveryResult.Accepted(new TrustedSourceAuditIntentAcceptanceReceipt(
                "immutable-ack",
                AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                envelope.ContractVersion,
                DateTimeOffset.UtcNow,
                true)));
        var processor = Processor(repository, new IdentityProvider(), client);

        var lost = await processor.ProcessTenantAsync(repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);
        var replay = await processor.ProcessTenantAsync(repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(1, lost.RetryScheduled);
        Assert.Equal(1, replay.Accepted);
        Assert.Equal(1, repository.AcknowledgementCount);
    }

    [Fact]
    public async Task Selected_delivery_never_discovers_and_compacted_replay_never_claims_or_calls_transport()
    {
        var repository = new Repository();
        var identity = new IdentityProvider();
        var client = new Client((envelope, _) => new(TrustedSourceAuditIntentDeliveryOutcome.Accepted,
            new("receipt", AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                envelope.ContractVersion, DateTimeOffset.UtcNow, false), ""));
        var locator = new AuditIntentLocator(repository.TenantId, AuditAggregateType.GlobalProduct, Guid.NewGuid(), Guid.NewGuid());
        var request = new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), repository.TenantId, [new(locator, 0, new string('a', 64))]);
        var processor = Processor(repository, identity, client);
        var first = await processor.ProcessSelectedAsync(request, "selected", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), 3);
        Assert.Single(first.Receipts);
        Assert.Equal(1, first.Batch.Accepted);
        var replay = await processor.ProcessSelectedAsync(request, "selected", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), 3);
        Assert.Single(replay.Receipts);
        Assert.Equal(0, replay.Batch.Claimed);
        Assert.Equal(0, repository.DiscoveryCount);
        Assert.Equal(1, repository.AcknowledgementCount);
        Assert.Single(identity.ForceRefreshCalls);
        Assert.Single(client.Envelopes);
    }

    private static AuditIntentDeliveryProcessor Processor(
        IAuditIntentDeliveryRepository repository,
        ITrustedSourceAuditServiceIdentityProvider identity,
        ITrustedSourceAuditIntentClient client) => new(repository, identity, client);

    private sealed class IdentityProvider : ITrustedSourceAuditServiceIdentityProvider
    {
        public List<bool> ForceRefreshCalls { get; } = [];
        public Task<TrustedSourceAuditServiceIdentity> GetAsync(Guid tenantId, string audience, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Assert.Equal(AuditIntentDeliveryProcessor.RequiredAudience, audience);
            ForceRefreshCalls.Add(forceRefresh);
            return Task.FromResult(new TrustedSourceAuditServiceIdentity("token", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    private sealed class Client(Func<TrustedSourceAuditIntentEnvelope, TrustedSourceAuditServiceIdentity, TrustedSourceAuditIntentDeliveryResult> response)
        : ITrustedSourceAuditIntentClient
    {
        public List<TrustedSourceAuditIntentEnvelope> Envelopes { get; } = [];
        public Task<TrustedSourceAuditIntentDeliveryResult> AcceptAsync(TrustedSourceAuditIntentEnvelope envelope, TrustedSourceAuditServiceIdentity identity, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return Task.FromResult(response(envelope, identity));
        }
    }

    private sealed class Repository : IAuditIntentDeliveryRepository
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ReturnedTenantId { get; set; }
        public bool ClaimAttempted { get; private set; }
        public bool Acknowledged { get; private set; }
        public int AcknowledgementCount { get; private set; }
        public bool ThrowDuringPayloadRead { get; init; }
        public bool ReturnWrongContract { get; init; }
        public string? LastDeadLetterReason { get; private set; }
        public int DiscoveryCount { get; private set; }
        private bool _selected;
        private LocalAuditIntentReceipt? _selectedReceipt;
        public Task PrepareSelectedAsync(SelectedAuditIntentDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            _selected = true;
            Assert.Equal(TenantId, request.TenantId);
            return Task.CompletedTask;
        }
        public Task<LocalAuditIntentReceipt?> ReadSelectedReceiptAsync(AuditIntentLocator locator, CancellationToken cancellationToken = default)
            => Task.FromResult(_selectedReceipt);
        private Guid EffectiveTenant => ReturnedTenantId == Guid.Empty ? TenantId : ReturnedTenantId;
        private AuditIntentLocator Locator => new(EffectiveTenant, AuditAggregateType.GlobalProduct, Guid.NewGuid(), Guid.NewGuid());

        public Task<IReadOnlyList<AuditIntentWorkItem>> DiscoverEligibleAsync(int limit, CancellationToken cancellationToken = default)
        {
            DiscoveryCount++;
            return Task.FromResult<IReadOnlyList<AuditIntentWorkItem>>([new(Locator, AuditIntentDeliveryState.Pending, 0, 0, DateTimeOffset.UtcNow, null, null, false)]);
        }
        public Task<AuditIntentClaim?> TryClaimAsync(AuditIntentLocator locator, long expectedClaimGeneration, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            ClaimAttempted = true;
            return Task.FromResult<AuditIntentClaim?>(new(locator, "claim", leaseOwner, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(leaseDuration), 1));
        }
        public Task<AuditIntentClaimedPayload?> ReadClaimedPayloadAsync(AuditIntentClaim claim, CancellationToken cancellationToken = default)
        {
            if (ThrowDuringPayloadRead) throw new InvalidOperationException("malformed persisted payload");
            return Task.FromResult<AuditIntentClaimedPayload?>(new(AuditIntentContract.SourceService,
                ReturnWrongContract ? "wrong-contract" : AuditIntentDeliveryProcessor.RequiredContractVersion,
                claim.Locator.IntentId, claim.Locator.TenantId, claim.Locator.AggregateType, claim.Locator.AggregateId, 0, 1,
                ProductAuditOperation.GlobalProductDraftCreated, "actor", Guid.NewGuid(), "cause", "command", 1,
                DateTimeOffset.UtcNow, new string('a', 64), null, "idempotency"));
        }
        public Task<bool> MarkRetryableFailureAsync(AuditIntentClaim claim, TimeSpan retryDelay, string reason, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> MarkDeadLetterAsync(AuditIntentClaim claim, string reason, CancellationToken cancellationToken = default)
        {
            LastDeadLetterReason = reason;
            return Task.FromResult(true);
        }
        public Task<bool> MarkDeliveredAsync(AuditIntentClaim claim, AuditIntentAcknowledgement acknowledgement, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> AcknowledgeAndCompactAsync(AuditIntentClaim claim, AuditIntentAcknowledgement acknowledgement, string compactReceiptReference, CancellationToken cancellationToken = default)
        {
            Acknowledged = true;
            AcknowledgementCount++;
            if (_selected) _selectedReceipt = new LocalAuditIntentReceipt { IntentId = claim.Locator.IntentId, TenantId = claim.Locator.TenantId };
            return Task.FromResult(true);
        }
        public Task<bool> CompactDeliveredAsync(AuditIntentClaim claim, string compactReceiptReference, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
