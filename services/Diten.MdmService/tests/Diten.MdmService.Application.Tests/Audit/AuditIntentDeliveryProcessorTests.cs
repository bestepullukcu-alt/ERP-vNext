using Microsoft.Extensions.DependencyInjection;
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

    // BL-552 flow 3 FIX1 item 2: MDM deployed before Platform — an operation BL-552 added (79 / 80 / 81) that Platform
    // does not map YET waits (retryable, counted), and is delivered once Platform maps it. Not dead-lettered.
    [Theory]
    [InlineData(ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementEnded)]
    public async Task An_operation_platform_does_not_map_yet_waits_and_is_delivered_once_it_does(ProductAuditOperation operation)
    {
        var repository = new Repository { PayloadOperation = operation };
        var mapped = false;
        var client = new Client((envelope, _) => mapped
            ? TrustedSourceAuditIntentDeliveryResult.Accepted(new TrustedSourceAuditIntentAcceptanceReceipt("ack",
                AuditIntentContract.BuildCentralIdempotencyKey(envelope.TenantId, envelope.IntentId, envelope.ContractVersion),
                envelope.ContractVersion, DateTimeOffset.UtcNow, false))
            : TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT") with
            {
                CentralReasonCode = AuditIntentDeliveryProcessor.MappingUnsupported
            });
        var processor = Processor(repository, new IdentityProvider(), client);

        var waiting = await processor.ProcessTenantAsync(repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);
        Assert.Equal(1, waiting.RetryScheduled);
        Assert.Equal(1, waiting.AwaitingCentralMapping);
        Assert.Equal(0, waiting.DeadLettered);
        Assert.Equal(AuditIntentDeliveryProcessor.MappingUnsupported, repository.LastRetryReason);
        Assert.Null(repository.LastDeadLetterReason);

        mapped = true;
        var delivered = await processor.ProcessTenantAsync(repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);
        Assert.Equal(1, delivered.Accepted);
    }

    // The wait is bounded (CentralMappingWaitAttempts), and it is only for the operations the deployed Platform does not
    // map yet (CentralMappingRollout) and that reason: an operation never produced, and any other 409, is dead-lettered.
    // SETTLE-CAP FIX1 (CT decision): the deployed base is EMPTY — every produced operation waits in this first release,
    // so the GP neighbours below now wait too; the never-produced pair is what still dead-letters.
    [Theory]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    // Flow 3 FIX3 item 7 (CT G1): past the ORDINARY attempt bound (3 here) it still waits; only the long bound ends it.
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, 4, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, AuditIntentDeliveryProcessor.CentralMappingWaitAttempts - 1,
        "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, AuditIntentDeliveryProcessor.CentralMappingWaitAttempts,
        "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", true)]
    [InlineData(ProductAuditOperation.GlobalProductDraftCreated, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    // Flow 3 FIX2 K1: their neighbours — with an empty deployed base (FIX1) they wait as well.
    [InlineData(ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRequested, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", false)]
    // The never-produced pair (CentralMappingRollout.NeverProduced) has nothing to wait for.
    [InlineData(ProductAuditOperation.GlobalProductCorrectionCancelled, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", true)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementCancelled, 1, "AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", true)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, 1, "AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT", true)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, 1, null, true)]
    public async Task Only_the_new_operations_wait_for_platforms_map_and_only_for_a_while(
        ProductAuditOperation operation, int attempts, string? reason, bool deadLettered)
    {
        var repository = new Repository { PayloadOperation = operation, ClaimAttemptCount = attempts };
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonCode = reason });

        var result = await Processor(repository, new IdentityProvider(), client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(deadLettered ? 1 : 0, result.DeadLettered);
        Assert.Equal(deadLettered ? 0 : 1, result.RetryScheduled);
    }

    // WP-MDM-SETTLE-CAP-01 (BL-566): the LSKU / GSKU operations Platform maps new in this release (82–86) wait like
    // GP's three; their older LSKU neighbours, mapped by an earlier Platform, are dead-lettered as before.
    [Theory]
    [InlineData(ProductAuditOperation.GskuChildCreationAdmissionReleased, false)]
    [InlineData(ProductAuditOperation.LskuDraftCancelled, false)]
    [InlineData(ProductAuditOperation.LskuIdentityDecisionNotApplicable, false)]
    [InlineData(ProductAuditOperation.LskuRetirementManualReconciliationRequired, false)]
    [InlineData(ProductAuditOperation.LskuIdentitySubmitAbandoned, false)]
    // FIX1: the older LSKU operations wait too (empty deployed base); the never-produced pair does not.
    [InlineData(ProductAuditOperation.LskuIdentityApprovalWithdrawn, false)]
    [InlineData(ProductAuditOperation.LskuRetirementRejected, false)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementCancelled, true)]
    public async Task The_lsku_and_gsku_operations_new_in_this_release_wait_for_platforms_map_too(
        ProductAuditOperation operation, bool deadLettered)
    {
        var repository = new Repository { PayloadOperation = operation, ClaimAttemptCount = 1 };
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonCode = AuditIntentDeliveryProcessor.MappingUnsupported });

        var result = await Processor(repository, new IdentityProvider(), client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(deadLettered ? 1 : 0, result.DeadLettered);
        Assert.Equal(deadLettered ? 0 : 1, result.RetryScheduled);
    }

    // Flow 3 FIX2 item 2: what waits is SAID — the worker's cycle reports it (count, operations, tenants) and writes one
    // Warning; the attempt at the bound is dead-lettered and is not counted as waiting.
    [Theory]
    [InlineData(1, true)]
    [InlineData(AuditIntentDeliveryProcessor.CentralMappingWaitAttempts, false)]
    public async Task The_delivery_worker_says_what_waits_for_platforms_map(int attempts, bool waits)
    {
        var repository = new Repository
        {
            PayloadOperation = ProductAuditOperation.GlobalProductCorrectionEnded, ClaimAttemptCount = attempts
        };
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonCode = AuditIntentDeliveryProcessor.MappingUnsupported });
        var log = new WorkerLog();
        using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IAuditIntentTenantPartitionDiscovery>(new OneTenant(repository.TenantId))
            .AddScoped<Diten.MdmService.Application.Common.TenantContext>()
            .AddScoped(_ => Processor(repository, new IdentityProvider(), client))
            .BuildServiceProvider();
        var worker = new Diten.MdmService.Api.Services.Audit.AuditIntentDeliveryWorker(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(services),
            Microsoft.Extensions.Options.Options.Create(new Diten.MdmService.Api.Configuration.AuditIntentDeliveryWorkerOptions
                { LeaseOwner = "worker" }),
            Microsoft.Extensions.Options.Options.Create(new Diten.MdmService.Infrastructure.Audit.AuthTrustedSourceAuditServiceIdentityProviderOptions()),
            Microsoft.Extensions.Options.Options.Create(new Diten.MdmService.Infrastructure.Audit.TrustedSourceAuditIntentClientOptions()),
            log);

        var cycle = await worker.RunCycleAsync(CancellationToken.None);

        if (waits)
        {
            Assert.Equal(1, cycle.AwaitingCentralMapping);
            Assert.Equal(1, cycle.TenantsAwaitingCentralMapping);
            Assert.Equal(["GlobalProductCorrectionEnded"], cycle.AwaitingCentralMappingOperations);
            var line = Assert.Single(log.Warnings);
            Assert.Contains("GlobalProductCorrectionEnded", line);
            Assert.Contains("AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED", line);
        }
        else
        {
            Assert.Equal(0, cycle.AwaitingCentralMapping);
            Assert.Empty(log.Warnings);
            Assert.Equal(AuditIntentDeliveryProcessor.MappingUnsupported, repository.LastDeadLetterReason);
        }
    }

    private sealed class OneTenant(Guid tenantId) : IAuditIntentTenantPartitionDiscovery
    {
        public Task<AuditIntentTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditIntentTenantPartitionPage(afterTenantId is null ? [tenantId] : [], null));
    }

    private sealed class WorkerLog : Microsoft.Extensions.Logging.ILogger<Diten.MdmService.Api.Services.Audit.AuditIntentDeliveryWorker>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }

    // Flow 3 FIX3 item 3: a 409 whose reason did not arrive in time is a timeout (retried, ordinary bound) for an
    // operation that may wait for Platform's map — and stays a definite 409 for one never produced (FIX1: with an empty
    // deployed base, every produced operation may wait).
    [Theory]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionEnded, false)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementEnded, false)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable, false)]
    [InlineData(ProductAuditOperation.GlobalProductDraftCreated, false)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRequested, false)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionCancelled, true)]
    public async Task A_conflict_whose_reason_did_not_arrive_is_retried_only_for_the_operations_that_may_wait(
        ProductAuditOperation operation, bool deadLettered)
    {
        var repository = new Repository { PayloadOperation = operation };
        var client = new Client((_, _) => TrustedSourceAuditIntentDeliveryResult.Terminal("AUDIT_SOURCE_INTENT_CONFLICT")
            with { CentralReasonTimedOut = true });

        var result = await Processor(repository, new IdentityProvider(), client).ProcessTenantAsync(
            repository.TenantId, 10, "worker", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5), 3);

        Assert.Equal(deadLettered ? 1 : 0, result.DeadLettered);
        Assert.Equal(deadLettered ? 0 : 1, result.RetryScheduled);
        Assert.Equal(0, result.AwaitingCentralMapping);
        if (!deadLettered) Assert.Equal("AUDIT_SOURCE_INTENT_TIMEOUT", repository.LastRetryReason);
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
            return Task.FromResult<AuditIntentClaim?>(new(locator, "claim", leaseOwner, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(leaseDuration), ClaimAttemptCount));
        }
        public Task<AuditIntentClaimedPayload?> ReadClaimedPayloadAsync(AuditIntentClaim claim, CancellationToken cancellationToken = default)
        {
            if (ThrowDuringPayloadRead) throw new InvalidOperationException("malformed persisted payload");
            return Task.FromResult<AuditIntentClaimedPayload?>(new(AuditIntentContract.SourceService,
                ReturnWrongContract ? "wrong-contract" : AuditIntentDeliveryProcessor.RequiredContractVersion,
                claim.Locator.IntentId, claim.Locator.TenantId, claim.Locator.AggregateType, claim.Locator.AggregateId, 0, 1,
                PayloadOperation, "actor", Guid.NewGuid(), "cause", "command", 1,
                DateTimeOffset.UtcNow, new string('a', 64), null, "idempotency"));
        }
        public ProductAuditOperation PayloadOperation { get; init; } = ProductAuditOperation.GlobalProductDraftCreated;
        public int ClaimAttemptCount { get; init; } = 1;
        public string? LastRetryReason { get; private set; }
        public Task<bool> MarkRetryableFailureAsync(AuditIntentClaim claim, TimeSpan retryDelay, string reason, CancellationToken cancellationToken = default)
        {
            LastRetryReason = reason;
            return Task.FromResult(true);
        }
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
