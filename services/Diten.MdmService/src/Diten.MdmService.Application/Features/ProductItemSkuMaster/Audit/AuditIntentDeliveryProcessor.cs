using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;

public sealed class AuditIntentDeliveryProcessor
{
    public const string RequiredAudience = "TRUSTED_AUDIT_SOURCE_INGEST";
    public const string RequiredContractVersion = "mod-0290.audit-intent.v1";
    private readonly IAuditIntentDeliveryRepository _repository;
    private readonly ITrustedSourceAuditServiceIdentityProvider _identityProvider;
    private readonly ITrustedSourceAuditIntentClient _client;

    public AuditIntentDeliveryProcessor(
        IAuditIntentDeliveryRepository repository,
        ITrustedSourceAuditServiceIdentityProvider identityProvider,
        ITrustedSourceAuditIntentClient client)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _client = client;
    }

    public async Task<AuditIntentDeliveryBatchResult> ProcessTenantAsync(
        Guid tenantId,
        int limit,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        int maximumAttempts,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(tenantId, limit, leaseOwner, leaseDuration, retryDelay, maximumAttempts);
        var work = await _repository.DiscoverEligibleAsync(limit, cancellationToken);
        if (work.Any(item => item.Locator.TenantId != tenantId))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TENANT_PARTITION_VIOLATION");
        }

        var result = new AuditIntentDeliveryBatchResult();
        foreach (var item in work)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claim = await _repository.TryClaimAsync(
                item.Locator,
                item.ClaimGeneration,
                leaseOwner,
                leaseDuration,
                cancellationToken);
            if (claim is null)
            {
                result.ClaimConflicts++;
                continue;
            }

            result.Claimed++;
            await DeliverClaimAsync(claim, retryDelay, maximumAttempts, result, cancellationToken);
        }

        result.Discovered = work.Count;
        return result;
    }

    public async Task<SelectedAuditIntentDeliveryResult> ProcessSelectedAsync(
        SelectedAuditIntentDeliveryRequest request, string leaseOwner, TimeSpan leaseDuration,
        TimeSpan retryDelay, int maximumAttempts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateArguments(request.TenantId, request.Items.Count, leaseOwner, leaseDuration, retryDelay, maximumAttempts);
        await _repository.PrepareSelectedAsync(request, cancellationToken);
        var batch = new AuditIntentDeliveryBatchResult { Discovered = request.Items.Count };
        var receipts = new List<LocalAuditIntentReceipt>();
        foreach (var item in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = await _repository.ReadSelectedReceiptAsync(item.Locator, cancellationToken);
            if (receipt is not null)
            {
                receipts.Add(receipt);
                continue;
            }
            var claim = await _repository.TryClaimAsync(item.Locator, item.ExpectedClaimGeneration,
                leaseOwner, leaseDuration, cancellationToken);
            if (claim is null)
            {
                batch.ClaimConflicts++;
                continue;
            }
            batch.Claimed++;
            await DeliverClaimAsync(claim, retryDelay, maximumAttempts, batch, cancellationToken);
            receipt = await _repository.ReadSelectedReceiptAsync(item.Locator, cancellationToken);
            if (receipt is not null) receipts.Add(receipt);
        }
        return new SelectedAuditIntentDeliveryResult(batch, receipts.AsReadOnly());
    }

    private async Task DeliverClaimAsync(
        AuditIntentClaim claim,
        TimeSpan retryDelay,
        int maximumAttempts,
        AuditIntentDeliveryBatchResult batch,
        CancellationToken cancellationToken)
    {
        AuditIntentClaimedPayload? payload;
        TrustedSourceAuditIntentEnvelope envelope;
        try
        {
            payload = await _repository.ReadClaimedPayloadAsync(claim, cancellationToken);
            if (payload is null)
            {
                batch.ClaimConflicts++;
                return;
            }

            envelope = ToEnvelope(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException)
        {
            if (await _repository.MarkDeadLetterAsync(
                    claim,
                    "AUDIT_SOURCE_INTENT_CLAIMED_PAYLOAD_INVALID",
                    cancellationToken))
            {
                batch.DeadLettered++;
            }
            else
            {
                batch.ClaimConflicts++;
            }
            return;
        }
        TrustedSourceAuditIntentDeliveryResult delivery;
        try
        {
            var identity = await _identityProvider.GetAsync(
                payload.TenantId,
                RequiredAudience,
                false,
                cancellationToken);
            delivery = await _client.AcceptAsync(envelope, identity, cancellationToken);
            if (delivery.Outcome == TrustedSourceAuditIntentDeliveryOutcome.AuthenticationRejected)
            {
                identity = await _identityProvider.GetAsync(
                    payload.TenantId,
                    RequiredAudience,
                    true,
                    cancellationToken);
                delivery = await _client.AcceptAsync(envelope, identity, cancellationToken);
            }
        }
        catch (TrustedSourceAuditServiceIdentityException exception)
        {
            await TransitionFailureAsync(
                claim,
                exception.ErrorCode,
                exception.IsRetryable,
                retryDelay,
                maximumAttempts,
                batch,
                cancellationToken);
            return;
        }

        switch (delivery.Outcome)
        {
            case TrustedSourceAuditIntentDeliveryOutcome.Accepted:
                await CompleteAsync(claim, delivery.Receipt, batch, cancellationToken);
                return;
            case TrustedSourceAuditIntentDeliveryOutcome.Retryable:
                await TransitionFailureAsync(
                    claim, delivery.ErrorCode, true, retryDelay, maximumAttempts, batch, cancellationToken);
                return;
            case TrustedSourceAuditIntentDeliveryOutcome.AuthenticationRejected:
            case TrustedSourceAuditIntentDeliveryOutcome.Terminal:
                await TransitionFailureAsync(
                    claim, delivery.ErrorCode, false, retryDelay, maximumAttempts, batch, cancellationToken);
                return;
            default:
                throw new InvalidOperationException("AUDIT_SOURCE_INTENT_DELIVERY_OUTCOME_INVALID");
        }
    }

    private async Task CompleteAsync(
        AuditIntentClaim claim,
        TrustedSourceAuditIntentAcceptanceReceipt? receipt,
        AuditIntentDeliveryBatchResult batch,
        CancellationToken cancellationToken)
    {
        var expectedKey = AuditIntentContract.BuildCentralIdempotencyKey(
            claim.Locator.TenantId,
            claim.Locator.IntentId,
            RequiredContractVersion);
        if (receipt is null
            || !string.Equals(receipt.ContractVersion, RequiredContractVersion, StringComparison.Ordinal)
            || !string.Equals(receipt.CentralIdempotencyKey, expectedKey, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
            || receipt.CentralAcknowledgement.Length > 512
            || !string.Equals(
                receipt.CentralAcknowledgement,
                receipt.CentralAcknowledgement.Trim(),
                StringComparison.Ordinal)
            || receipt.CentralAcknowledgement.Any(char.IsControl)
            || receipt.AcceptedAt.Offset != TimeSpan.Zero)
        {
            if (await _repository.MarkDeadLetterAsync(
                    claim,
                    "AUDIT_SOURCE_INTENT_RECEIPT_INVALID",
                    cancellationToken))
            {
                batch.DeadLettered++;
            }
            else
            {
                batch.ClaimConflicts++;
            }
            return;
        }

        var acknowledgement = new AuditIntentAcknowledgement(
            receipt.CentralAcknowledgement,
            receipt.CentralIdempotencyKey,
            receipt.ContractVersion,
            receipt.AcceptedAt);
        if (!await _repository.AcknowledgeAndCompactAsync(
                claim,
                acknowledgement,
                receipt.CentralAcknowledgement,
                cancellationToken))
        {
            batch.ClaimConflicts++;
            return;
        }

        batch.Accepted++;
    }

    private async Task TransitionFailureAsync(
        AuditIntentClaim claim,
        string errorCode,
        bool retryable,
        TimeSpan retryDelay,
        int maximumAttempts,
        AuditIntentDeliveryBatchResult batch,
        CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(errorCode)
            ? "AUDIT_SOURCE_INTENT_DELIVERY_FAILED"
            : errorCode.Trim();
        var retry = retryable && claim.AttemptCount < maximumAttempts;
        var changed = retry
            ? await _repository.MarkRetryableFailureAsync(claim, retryDelay, reason, cancellationToken)
            : await _repository.MarkDeadLetterAsync(claim, reason, cancellationToken);
        if (!changed)
        {
            batch.ClaimConflicts++;
        }
        else if (retry)
        {
            batch.RetryScheduled++;
        }
        else
        {
            batch.DeadLettered++;
        }
    }

    private static TrustedSourceAuditIntentEnvelope ToEnvelope(AuditIntentClaimedPayload payload)
    {
        if (!string.Equals(payload.SourceService, AuditIntentContract.SourceService, StringComparison.Ordinal)
            || !string.Equals(payload.ContractVersion, RequiredContractVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("AUDIT_SOURCE_INTENT_CONTRACT_INVALID");
        }

        return new TrustedSourceAuditIntentEnvelope(
            payload.SourceService,
            payload.ContractVersion,
            payload.IntentId,
            payload.TenantId,
            payload.AggregateType.ToString(),
            payload.AggregateId,
            payload.PreVersion,
            payload.PostVersion,
            payload.Operation.ToString(),
            payload.ActorId,
            payload.CorrelationId,
            payload.CausationId,
            payload.CommandId,
            payload.Sequence,
            payload.TimestampUtc,
            payload.EvidenceHash,
            payload.SnapshotReference,
            payload.IdempotencyKey);
    }

    private static void ValidateArguments(
        Guid tenantId,
        int limit,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        int maximumAttempts)
    {
        if (tenantId == Guid.Empty || limit is < 1 or > 100 || string.IsNullOrWhiteSpace(leaseOwner)
            || leaseOwner.Length > 128 || leaseDuration < TimeSpan.FromSeconds(10)
            || leaseDuration > TimeSpan.FromMinutes(15) || retryDelay < TimeSpan.FromSeconds(1)
            || retryDelay > TimeSpan.FromHours(1)
            || maximumAttempts is < 1 or > 20)
        {
            throw new ArgumentException("AUDIT_INTENT_DELIVERY_BATCH_CONFIGURATION_INVALID");
        }
    }
}
public sealed class AuditIntentDeliveryBatchResult
{
    public int Discovered { get; internal set; }
    public int Claimed { get; internal set; }
    public int Accepted { get; internal set; }
    public int RetryScheduled { get; internal set; }
    public int DeadLettered { get; internal set; }
    public int ClaimConflicts { get; internal set; }
}

public sealed record SelectedAuditIntentDeliveryResult(
    AuditIntentDeliveryBatchResult Batch, IReadOnlyList<LocalAuditIntentReceipt> Receipts);
