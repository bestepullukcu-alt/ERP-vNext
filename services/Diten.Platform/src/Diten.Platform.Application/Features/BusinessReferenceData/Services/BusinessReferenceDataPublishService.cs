using System.Text.Json;
using Diten.Platform.Application.Contracts.Events;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class BusinessReferenceDataPublishService : IBusinessReferenceDataPublishService
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IBusinessReferenceDataValidationService _validationService;
    private readonly IBusinessReferenceDataEvidenceAdapter _evidenceAdapter;
    private readonly IBusinessReferenceDataGovernanceAuditAdapter _auditAdapter;
    private readonly IBusinessReferenceDataEventPublisher _eventPublisher;
    private readonly IBusinessReferenceDataPostPublicationReviewHook _reviewHook;
    private readonly IBusinessReferenceDataPublicationEligibility _publicationEligibility;
    private readonly IBusinessReferenceDataPublishCheckpointObserver _checkpointObserver;
    private readonly IBusinessReferenceDataVerifiedGskuOperationalEligibility? _operationalEligibility;
    private readonly IBusinessReferenceDataVerifiedMarketOperationalEligibility? _marketOperationalEligibility;

    public BusinessReferenceDataPublishService(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataValidationService validationService,
        IBusinessReferenceDataEvidenceAdapter evidenceAdapter,
        IBusinessReferenceDataGovernanceAuditAdapter auditAdapter,
        IBusinessReferenceDataEventPublisher eventPublisher,
        IBusinessReferenceDataPostPublicationReviewHook reviewHook,
        IBusinessReferenceDataPublicationEligibility publicationEligibility,
        IBusinessReferenceDataPublishCheckpointObserver checkpointObserver,
        IBusinessReferenceDataVerifiedGskuOperationalEligibility? operationalEligibility = null,
        IBusinessReferenceDataVerifiedMarketOperationalEligibility? marketOperationalEligibility = null)
    {
        _repository = repository;
        _validationService = validationService;
        _evidenceAdapter = evidenceAdapter;
        _auditAdapter = auditAdapter;
        _eventPublisher = eventPublisher;
        _reviewHook = reviewHook;
        _publicationEligibility = publicationEligibility;
        _checkpointObserver = checkpointObserver;
        _operationalEligibility = operationalEligibility;
        _marketOperationalEligibility = marketOperationalEligibility;
    }

    public async Task<BusinessReferenceDataVersionDetailModel> PublishAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default)
    {
        var version = await _repository.GetVersionByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");
        if (version.Status == BusinessReferenceDataVersionStatus.Published)
        {
            if (string.Equals(version.LastPublishIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                return BusinessReferenceDataModelMapper.ToVersionDetail(version);
            }

            throw new InvalidOperationException("already_published_different_idempotency");
        }

        var mode = NormalizeMode(publishMode);
        var effectivePublishAt = ResolvePublishAt(mode, publishAt);
        var validation = await _validationService.ValidateDraftVersionAsync(versionId, correlationId, ct);
        if (validation.BlockingErrorCount > 0 && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "validation_blockers", mode, ct);
            throw new InvalidOperationException("validation_blockers");
        }

        if (version.RequiresApproval
            && version.BusinessReferenceDataApprovalState != BusinessReferenceDataApprovalState.Approved
            && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "approval_required", mode, ct);
            throw new InvalidOperationException("approval_required");
        }

        var evidence = await _evidenceAdapter.CheckEvidenceAsync(
            version,
            BusinessReferenceDataEvidenceInput.FromPersisted(version),
            "publish",
            correlationId,
            ct);
        if (!evidence.HasRequiredEvidence && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "evidence_required", mode, ct);
            await _auditAdapter.EmitEvidenceUnsatisfiedAsync(version, actorId, correlationId, evidence, "publish", ct);
            throw new InvalidOperationException(evidence.ReasonCode ?? "evidence_required");
        }

        if (overrideAction)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new InvalidOperationException("override_reason_required");
            }

            version.IsOverrideAction = true;
            version.OverrideReason = overrideReason.Trim();
            await _auditAdapter.EmitOverrideAsync(version, actorId, correlationId, version.OverrideReason, ct);
        }

        var previousToken = string.IsNullOrWhiteSpace(expectedConcurrencyToken)
            ? version.ConcurrencyToken
            : expectedConcurrencyToken.Trim();
        ApplyPublicationState(version, actorId, correlationId, idempotencyKey, mode, effectivePublishAt, evidence);
        if (!await _repository.UpdateVersionAsync(version, previousToken, ct))
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "concurrency_conflict", mode, ct);
            throw new InvalidOperationException("concurrency_conflict");
        }

        await _repository.DeprecatePublishedVersionsAsync(
            version.BusinessReferenceDataSetId,
            version.BusinessReferenceDataVersionId,
            version.BusinessReferenceDataVersionId,
            ct);
        await PromoteSetPointersLegacyAsync(version, actorId, correlationId, ct);
        await EmitPublishedEventsAsync(version, actorId, correlationId, idempotencyKey, mode, ct);
        if (overrideAction && !string.IsNullOrWhiteSpace(version.OverrideReason))
        {
            await _reviewHook.TriggerAsync(version, actorId, correlationId, version.OverrideReason, ct);
        }

        await _auditAdapter.EmitPublishAsync(
            version,
            actorId,
            correlationId,
            true,
            overrideAction ? "override_publish" : null,
            mode,
            ct);
        return BusinessReferenceDataModelMapper.ToVersionDetail(version);
    }

    public async Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedMarketAsync(
        Guid versionId, string actorId, string correlationId, string idempotencyKey, string publishMode,
        DateTimeOffset? publishAt, string? expectedConcurrencyToken, bool overrideAction, string? overrideReason,
        IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization, VerifiedMarketOperationalFacts facts,
        CancellationToken ct = default)
    {
        if (_marketOperationalEligibility is null || !_marketOperationalEligibility.IsAuthorized(authorization, facts)
            || !idempotencyKey.StartsWith($"{facts.IdempotencyNamespace}:", StringComparison.Ordinal)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal))
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        var version = await _repository.GetVersionByIdAsync(versionId, ct) ?? throw new KeyNotFoundException("reference_data_version_not_found");
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct) ?? throw new KeyNotFoundException("reference_data_set_not_found");
        var operation = await _repository.GetPublishOperationByIdempotencyKeyAsync(idempotencyKey, ct);
        if (!string.Equals(set.SetCode, VerifiedMarketCatalogContract.SetCode, StringComparison.Ordinal) || operation is null
            || !string.Equals(operation.CatalogVersion, facts.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(operation.CatalogFingerprint, facts.CatalogFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        return await PublishVerifiedCoreAsync(versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt, expectedConcurrencyToken, overrideAction, overrideReason, ct);
    }

    public async Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default)
    {
        var eligibility = _publicationEligibility.Evaluate();
        if (!eligibility.IsEligible)
        {
            throw new InvalidOperationException(eligibility.ReasonCode);
        }

        return await PublishVerifiedCoreAsync(
            versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
            expectedConcurrencyToken, overrideAction, overrideReason, ct);
    }

    public async Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedMarketAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default)
    {
        var eligibility = _publicationEligibility.Evaluate();
        if (!eligibility.IsEligible)
        {
            throw new InvalidOperationException(eligibility.ReasonCode);
        }

        var version = await _repository.GetVersionByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");
        var operation = await _repository.GetPublishOperationByIdempotencyKeyAsync(idempotencyKey, ct);
        if (!string.Equals(set.SetCode, VerifiedMarketCatalogContract.SetCode, StringComparison.Ordinal)
            || operation is null
            || operation.BusinessReferenceDataSetId != set.BusinessReferenceDataSetId
            || operation.BusinessReferenceDataVersionId != versionId
            || string.IsNullOrWhiteSpace(operation.CatalogVersion)
            || string.IsNullOrWhiteSpace(operation.CatalogFingerprint))
        {
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        return await PublishVerifiedCoreAsync(
            versionId,
            actorId,
            correlationId,
            idempotencyKey,
            publishMode,
            publishAt,
            expectedConcurrencyToken,
            overrideAction,
            overrideReason,
            ct);
    }

    public async Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        IBusinessReferenceDataVerifiedGskuOperationalAuthorization authorization,
        VerifiedGskuOperationalFacts facts,
        CancellationToken ct = default)
    {
        if (_operationalEligibility is null
            || !_operationalEligibility.IsAuthorized(authorization, facts)
            || !string.Equals(actorId.Trim(), facts.ActorId, StringComparison.Ordinal)
            || !idempotencyKey.StartsWith("businessreferencedata-catalog-v", StringComparison.Ordinal)
            || !facts.RequiredSetCodes.SequenceEqual(["pack-applicability", "uom"], StringComparer.Ordinal))
        {
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        var target = await _repository.GetVersionByIdAsync(versionId, ct);
        var operation = await _repository.GetPublishOperationByIdempotencyKeyAsync(idempotencyKey, ct);
        if (target is null
            || target.TenantId != facts.ReferenceTenantId
            || operation is null
            || operation.BusinessReferenceDataVersionId != versionId
            || !string.Equals(operation.CatalogVersion, facts.CatalogVersion, StringComparison.Ordinal)
            || !string.Equals(operation.CatalogFingerprint, facts.CatalogFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("REFERENCE_GOVERNANCE_NOT_PRODUCTION_SAFE");
        }

        return await PublishVerifiedCoreAsync(
            versionId, actorId, correlationId, idempotencyKey, publishMode, publishAt,
            expectedConcurrencyToken, overrideAction, overrideReason, ct);
    }

    private async Task<BusinessReferenceDataVersionDetailModel> PublishVerifiedCoreAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string publishMode,
        DateTimeOffset? publishAt,
        string? expectedConcurrencyToken,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct)
    {

        var normalizedActorId = NormalizeRequired(actorId, nameof(actorId));
        var normalizedCorrelationId = NormalizeRequired(correlationId, nameof(correlationId));
        var normalizedIdempotencyKey = NormalizeRequired(idempotencyKey, nameof(idempotencyKey));
        var mode = NormalizeMode(publishMode);
        var version = await _repository.GetVersionByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");
        var existingOperation = await _repository.GetPublishOperationByIdempotencyKeyAsync(normalizedIdempotencyKey, ct);

        var proposedOperation = new BusinessReferenceDataPublishOperation
        {
            TenantId = version.TenantId,
            BusinessReferenceDataSetId = version.BusinessReferenceDataSetId,
            BusinessReferenceDataVersionId = version.BusinessReferenceDataVersionId,
            IdempotencyKey = normalizedIdempotencyKey,
            ExpectedPublishedVersionId = existingOperation is null
                ? set.PublishedVersionId
                : existingOperation.ExpectedPublishedVersionId,
            ExpectedSetVersion = existingOperation?.ExpectedSetVersion ?? set.RowVersion,
            ExpectedTargetVersionToken = NormalizeExpectedToken(
                expectedConcurrencyToken,
                existingOperation?.ExpectedTargetVersionToken ?? version.ConcurrencyToken),
            CatalogVersion = existingOperation?.CatalogVersion,
            CatalogFingerprint = existingOperation?.CatalogFingerprint,
            CreatedBy = normalizedActorId
        };
        var claim = await _repository.CreateOrGetPublishOperationAsync(proposedOperation, ct);
        if (claim.Outcome == BusinessReferenceDataPublishOperationCreateOutcome.Conflict)
        {
            throw new InvalidOperationException("REFERENCE_PUBLISH_CONFLICT");
        }

        var operation = claim.Operation;
        if (operation.OperationState == BusinessReferenceDataPublishOperationState.COMPLETED)
        {
            if (!await _repository.IsPublishOperationVerifiedAsync(operation.PublishOperationId, ct))
            {
                throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
            }

            var completedTarget = await _repository.GetVersionByIdAsync(operation.BusinessReferenceDataVersionId, ct)
                ?? throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
            return BusinessReferenceDataModelMapper.ToVersionDetail(completedTarget);
        }

        if (operation.OperationState == BusinessReferenceDataPublishOperationState.FAILED_TERMINAL)
        {
            throw new InvalidOperationException(operation.LastErrorCode ?? "REFERENCE_PUBLISH_OPERATION_STALE");
        }

        try
        {
            if (operation.OperationState == BusinessReferenceDataPublishOperationState.PENDING)
            {
                operation = await TransitionAsync(
                    operation,
                    BusinessReferenceDataPublishOperationState.RUNNING,
                    BusinessReferenceDataPublishCheckpoint.INITIALIZED,
                    normalizedActorId,
                    notifyCheckpoint: true,
                    ct);
            }
            else if (operation.OperationState == BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED)
            {
                operation = await TransitionAsync(
                    operation,
                    BusinessReferenceDataPublishOperationState.RUNNING,
                    operation.PublishCheckpoint,
                    normalizedActorId,
                    notifyCheckpoint: false,
                    ct);
            }

            operation = await ExecuteFromCheckpointAsync(
                operation,
                normalizedActorId,
                normalizedCorrelationId,
                mode,
                publishAt,
                overrideAction,
                overrideReason,
                ct);

            var published = await _repository.GetVersionByIdAsync(operation.BusinessReferenceDataVersionId, ct)
                ?? throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
            return BusinessReferenceDataModelMapper.ToVersionDetail(published);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            await TryMarkRecoveryRequiredAsync(
                operation.PublishOperationId,
                normalizedActorId,
                StableRecoveryError(exception),
                ct);
            throw;
        }
    }

    private async Task<BusinessReferenceDataPublishOperation> ExecuteFromCheckpointAsync(
        BusinessReferenceDataPublishOperation operation,
        string actorId,
        string correlationId,
        string mode,
        DateTimeOffset? publishAt,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct)
    {
        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.INITIALIZED)
        {
            var target = await _repository.GetVersionByIdAsync(operation.BusinessReferenceDataVersionId, ct)
                ?? throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
            if (!TargetWriteMatches(operation, target))
            {
                if (!string.Equals(target.ConcurrencyToken, operation.ExpectedTargetVersionToken, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
                }

                await PrepareTargetForPublicationAsync(
                    target,
                    actorId,
                    correlationId,
                    operation.IdempotencyKey,
                    mode,
                    publishAt,
                    overrideAction,
                    overrideReason,
                    ct);
                if (!await _repository.UpdateVersionAsync(target, operation.ExpectedTargetVersionToken, ct))
                {
                    throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
                }
            }

            operation = await AdvanceCheckpointAsync(
                operation,
                BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN,
                actorId,
                ct);
        }

        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN)
        {
            await _repository.DeprecatePublishedVersionsAsync(
                operation.BusinessReferenceDataSetId,
                operation.BusinessReferenceDataVersionId,
                operation.BusinessReferenceDataVersionId,
                ct);
            operation = await AdvanceCheckpointAsync(
                operation,
                BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED,
                actorId,
                ct);
        }

        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.PRIOR_VERSIONS_DEPRECATED)
        {
            var versions = await _repository.GetVersionsBySetIdAsync(operation.BusinessReferenceDataSetId, ct);
            var target = versions.SingleOrDefault(x => x.BusinessReferenceDataVersionId == operation.BusinessReferenceDataVersionId);
            if (target is null
                || !TargetWriteMatches(operation, target)
                || versions.Any(x => x.BusinessReferenceDataVersionId != target.BusinessReferenceDataVersionId
                                     && x.Status == BusinessReferenceDataVersionStatus.Published))
            {
                throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
            }

            operation = await AdvanceCheckpointAsync(
                operation,
                BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED,
                actorId,
                ct);
        }

        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.REQUIRED_WRITES_VERIFIED)
        {
            var set = await _repository.GetSetByIdAsync(operation.BusinessReferenceDataSetId, ct)
                ?? throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
            var pointerAlreadyPromoted = set.PublishedVersionId == operation.BusinessReferenceDataVersionId
                                         && set.RowVersion == operation.ExpectedSetVersion + 1
                                         && set.ActiveDraftVersionId is null
                                         && set.Status == BusinessReferenceDataSetStatus.Active;
            if (!pointerAlreadyPromoted)
            {
                if (set.PublishedVersionId != operation.ExpectedPublishedVersionId
                    || set.RowVersion != operation.ExpectedSetVersion)
                {
                    throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
                }

                set.PublishedVersionId = operation.BusinessReferenceDataVersionId;
                set.ActiveDraftVersionId = null;
                set.Status = BusinessReferenceDataSetStatus.Active;
                set.UpdatedBy = actorId;
                set.LastCorrelationId = correlationId;
                if (!await _repository.UpdateSetAsync(set, operation.ExpectedSetVersion, ct))
                {
                    throw new InvalidOperationException("REFERENCE_PUBLISH_OPERATION_STALE");
                }
            }

            operation = await AdvanceCheckpointAsync(
                operation,
                BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED,
                actorId,
                ct);
        }

        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.POINTER_PROMOTED)
        {
            operation = await AdvanceCheckpointAsync(
                operation,
                BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
                actorId,
                ct);
        }

        if (operation.PublishCheckpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED)
        {
            var target = await _repository.GetVersionByIdAsync(operation.BusinessReferenceDataVersionId, ct)
                ?? throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
            await EmitPublishedEventsAsync(target, actorId, correlationId, operation.IdempotencyKey, mode, ct);
            if (overrideAction && !string.IsNullOrWhiteSpace(target.OverrideReason))
            {
                await _reviewHook.TriggerAsync(target, actorId, correlationId, target.OverrideReason, ct);
            }

            await _auditAdapter.EmitPublishAsync(
                target,
                actorId,
                correlationId,
                true,
                overrideAction ? "override_publish" : null,
                mode,
                ct);
            operation = await TransitionAsync(
                operation,
                BusinessReferenceDataPublishOperationState.COMPLETED,
                BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,
                actorId,
                notifyCheckpoint: false,
                ct);
        }

        if (!await _repository.IsPublishOperationVerifiedAsync(operation.PublishOperationId, ct))
        {
            throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
        }

        return operation;
    }

    private async Task PrepareTargetForPublicationAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string mode,
        DateTimeOffset? publishAt,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct)
    {
        if (version.Status == BusinessReferenceDataVersionStatus.Published)
        {
            if (string.Equals(version.LastPublishIdempotencyKey, idempotencyKey, StringComparison.Ordinal)
                && version.IsImmutable)
            {
                return;
            }

            throw new InvalidOperationException("REFERENCE_PUBLISH_CONFLICT");
        }

        var validation = await _validationService.ValidateDraftVersionAsync(version.BusinessReferenceDataVersionId, correlationId, ct);
        var hasNonOverridableCatalogBlocker = validation.PublishBlockers.Any(x =>
            string.Equals(x, "RDV-011", StringComparison.Ordinal)
            || string.Equals(x, "RDV-012", StringComparison.Ordinal));
        if (validation.BlockingErrorCount > 0 && (hasNonOverridableCatalogBlocker || !overrideAction))
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "validation_blockers", mode, ct);
            throw new InvalidOperationException("validation_blockers");
        }

        if (version.RequiresApproval
            && version.BusinessReferenceDataApprovalState != BusinessReferenceDataApprovalState.Approved
            && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "approval_required", mode, ct);
            throw new InvalidOperationException("approval_required");
        }

        var evidence = await _evidenceAdapter.CheckEvidenceAsync(
            version,
            BusinessReferenceDataEvidenceInput.FromPersisted(version),
            "publish",
            correlationId,
            ct);
        if (!evidence.HasRequiredEvidence && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "evidence_required", mode, ct);
            await _auditAdapter.EmitEvidenceUnsatisfiedAsync(version, actorId, correlationId, evidence, "publish", ct);
            throw new InvalidOperationException(evidence.ReasonCode ?? "evidence_required");
        }

        if (overrideAction)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new InvalidOperationException("override_reason_required");
            }

            version.IsOverrideAction = true;
            version.OverrideReason = overrideReason.Trim();
            await _auditAdapter.EmitOverrideAsync(version, actorId, correlationId, version.OverrideReason, ct);
        }

        var effectivePublishAt = ResolvePublishAt(mode, publishAt);
        ApplyPublicationState(version, actorId, correlationId, idempotencyKey, mode, effectivePublishAt, evidence);
    }

    private static void ApplyPublicationState(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string mode,
        DateTimeOffset effectivePublishAt,
        BusinessReferenceDataEvidenceCheckResult evidence)
    {
        version.Status = BusinessReferenceDataVersionStatus.Published;
        version.BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Approved;
        version.BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.Approved;
        version.IsEditable = false;
        version.IsImmutable = true;
        version.PublishedAt = effectivePublishAt;
        version.PublishedBy = actorId;
        version.LastPublishIdempotencyKey = idempotencyKey;
        version.LastPublishMode = mode;
        version.LastCorrelationId = correlationId;
        BusinessReferenceDataGovernanceService.ApplyEvidenceDecision(version, evidence);
        version.ApprovedAt ??= effectivePublishAt;
        version.PublishedSnapshotJson = BuildSnapshot(version);
    }

    private async Task PromoteSetPointersLegacyAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct);
        if (set is null
            || (set.PublishedVersionId == version.BusinessReferenceDataVersionId
                && set.ActiveDraftVersionId is null
                && set.Status == BusinessReferenceDataSetStatus.Active))
        {
            return;
        }

        set.PublishedVersionId = version.BusinessReferenceDataVersionId;
        set.ActiveDraftVersionId = null;
        set.Status = BusinessReferenceDataSetStatus.Active;
        set.UpdatedBy = actorId;
        set.LastCorrelationId = correlationId;
        await _repository.UpdateSetAsync(set, set.RowVersion, ct);
    }

    private async Task<BusinessReferenceDataPublishOperation> AdvanceCheckpointAsync(
        BusinessReferenceDataPublishOperation operation,
        BusinessReferenceDataPublishCheckpoint checkpoint,
        string actorId,
        CancellationToken ct)
    {
        return await TransitionAsync(
            operation,
            BusinessReferenceDataPublishOperationState.RUNNING,
            checkpoint,
            actorId,
            notifyCheckpoint: true,
            ct);
    }

    private async Task<BusinessReferenceDataPublishOperation> TransitionAsync(
        BusinessReferenceDataPublishOperation operation,
        BusinessReferenceDataPublishOperationState state,
        BusinessReferenceDataPublishCheckpoint checkpoint,
        string actorId,
        bool notifyCheckpoint,
        CancellationToken ct)
    {
        var transitioned = await _repository.TransitionPublishOperationAsync(
            operation.PublishOperationId,
            operation.Version,
            state,
            checkpoint,
            actorId,
            ct: ct);
        var reloaded = await _repository.GetPublishOperationByIdAsync(operation.PublishOperationId, ct)
            ?? throw new InvalidOperationException("REFERENCE_PUBLISH_RECOVERY_REQUIRED");
        if (!transitioned)
        {
            throw new InvalidOperationException(
                reloaded.LastErrorCode
                ?? (checkpoint == BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED
                    ? "REFERENCE_PUBLISH_RECOVERY_REQUIRED"
                    : "REFERENCE_PUBLISH_CONFLICT"));
        }

        if (notifyCheckpoint)
        {
            await _checkpointObserver.OnCheckpointPersistedAsync(reloaded, ct);
        }

        return reloaded;
    }

    private async Task TryMarkRecoveryRequiredAsync(
        Guid publishOperationId,
        string actorId,
        string errorCode,
        CancellationToken ct)
    {
        var current = await _repository.GetPublishOperationByIdAsync(publishOperationId, ct);
        if (current is null || current.OperationState != BusinessReferenceDataPublishOperationState.RUNNING)
        {
            return;
        }

        await _repository.TransitionPublishOperationAsync(
            current.PublishOperationId,
            current.Version,
            BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED,
            current.PublishCheckpoint,
            actorId,
            errorCode,
            ct);
    }

    private static bool TargetWriteMatches(
        BusinessReferenceDataPublishOperation operation,
        BusinessReferenceDataVersion target)
    {
        return target.Status == BusinessReferenceDataVersionStatus.Published
               && target.IsImmutable
               && !string.Equals(target.ConcurrencyToken, operation.ExpectedTargetVersionToken, StringComparison.Ordinal)
               && string.Equals(target.LastPublishIdempotencyKey, operation.IdempotencyKey, StringComparison.Ordinal);
    }

    private async Task EmitPublishedEventsAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        string idempotencyKey,
        string mode,
        CancellationToken ct)
    {
        var corr = Guid.TryParse(correlationId, out var correlationIdGuid) && correlationIdGuid != Guid.Empty
            ? correlationIdGuid
            : Guid.NewGuid();
        var eventActor = new EventActor(actorId, "user");

        var publishedEvent = new EventEnvelope(
            EventId: Guid.NewGuid(),
            EventName: "reference_data.version_published.v1",
            EventVersion: 1,
            TenantId: version.TenantId,
            CorrelationId: corr,
            CausationId: corr,
            OccurredAt: version.PublishedAt ?? DateTimeOffset.UtcNow,
            Producer: "BusinessReferenceData",
            Actor: eventActor,
            Payload: new
            {
                version_id = version.BusinessReferenceDataVersionId,
                set_id = version.BusinessReferenceDataSetId,
                version_number = version.VersionNumber,
                published_at = version.PublishedAt,
                published_by = version.PublishedBy,
                publish_mode = mode,
                immutable = version.IsImmutable
            });

        await _eventPublisher.PublishAsync(publishedEvent, idempotencyKey, version.BusinessReferenceDataVersionId, ct);

        if (version.DeprecatedValuesEffectiveCount > 0)
        {
            var deprecatedEvent = new EventEnvelope(
                EventId: Guid.NewGuid(),
                EventName: "reference_data.value_deprecated.v1",
                EventVersion: 1,
                TenantId: version.TenantId,
                CorrelationId: corr,
                CausationId: publishedEvent.EventId,
                OccurredAt: version.PublishedAt ?? DateTimeOffset.UtcNow,
                Producer: "BusinessReferenceData",
                Actor: eventActor,
                Payload: new
                {
                    version_id = version.BusinessReferenceDataVersionId,
                    deprecated_value_count = version.DeprecatedValuesEffectiveCount,
                    effective_at = version.PublishedAt
                });

            await _eventPublisher.PublishAsync(
                deprecatedEvent,
                $"{idempotencyKey}:deprecated",
                version.BusinessReferenceDataVersionId,
                ct);
        }
    }

    private static string NormalizeMode(string? publishMode)
    {
        var normalized = (publishMode ?? "Immediate").Trim();
        if (normalized.Equals("Immediate", StringComparison.OrdinalIgnoreCase))
        {
            return "Immediate";
        }

        if (normalized.Equals("Future-Dated", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("FutureDated", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Future_Dated", StringComparison.OrdinalIgnoreCase))
        {
            return "Future-Dated";
        }

        throw new InvalidOperationException("invalid_publish_mode");
    }

    private static DateTimeOffset ResolvePublishAt(string mode, DateTimeOffset? publishAt)
    {
        if (mode == "Immediate")
        {
            return DateTimeOffset.UtcNow;
        }

        if (!publishAt.HasValue)
        {
            throw new InvalidOperationException("publish_at_required_for_future_dated");
        }

        if (publishAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("publish_at_must_be_future");
        }

        return publishAt.Value;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeExpectedToken(string? requestedToken, string fallbackToken)
    {
        return string.IsNullOrWhiteSpace(requestedToken)
            ? NormalizeRequired(fallbackToken, nameof(fallbackToken))
            : requestedToken.Trim();
    }

    private static string StableRecoveryError(Exception exception)
    {
        return exception is InvalidOperationException
               && exception.Message.StartsWith("REFERENCE_", StringComparison.Ordinal)
            ? exception.Message
            : "REFERENCE_PUBLISH_RECOVERY_REQUIRED";
    }

    private static string BuildSnapshot(BusinessReferenceDataVersion version)
    {
        return JsonSerializer.Serialize(new
        {
            version_id = version.BusinessReferenceDataVersionId,
            set_id = version.BusinessReferenceDataSetId,
            version_number = version.VersionNumber,
            status = version.Status.ToString(),
            governance_state = version.BusinessReferenceDataGovernanceState.ToString(),
            approval_state = version.BusinessReferenceDataApprovalState.ToString(),
            published_at = version.PublishedAt,
            published_by = version.PublishedBy,
            requires_approval = version.RequiresApproval,
            requires_evidence = version.RequiresEvidence,
            evidence_ref = version.LastEvidenceRef
        });
    }
}

public sealed class NoOpBusinessReferenceDataPublishCheckpointObserver : IBusinessReferenceDataPublishCheckpointObserver
{
    public Task OnCheckpointPersistedAsync(
        BusinessReferenceDataPublishOperation operation,
        CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
