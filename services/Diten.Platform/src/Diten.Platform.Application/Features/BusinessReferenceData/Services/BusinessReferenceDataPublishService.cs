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

    public BusinessReferenceDataPublishService(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataValidationService validationService,
        IBusinessReferenceDataEvidenceAdapter evidenceAdapter,
        IBusinessReferenceDataGovernanceAuditAdapter auditAdapter,
        IBusinessReferenceDataEventPublisher eventPublisher,
        IBusinessReferenceDataPostPublicationReviewHook reviewHook)
    {
        _repository = repository;
        _validationService = validationService;
        _evidenceAdapter = evidenceAdapter;
        _auditAdapter = auditAdapter;
        _eventPublisher = eventPublisher;
        _reviewHook = reviewHook;
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

        if (version.RequiresApproval && version.BusinessReferenceDataApprovalState != BusinessReferenceDataApprovalState.Approved && !overrideAction)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "approval_required", mode, ct);
            throw new InvalidOperationException("approval_required");
        }

        var evidence = await _evidenceAdapter.CheckEvidenceAsync(version, BusinessReferenceDataEvidenceInput.FromPersisted(version), "publish", correlationId, ct);
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

        var prevToken = string.IsNullOrWhiteSpace(expectedConcurrencyToken)
            ? version.ConcurrencyToken
            : expectedConcurrencyToken.Trim();

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

        var updated = await _repository.UpdateVersionAsync(version, prevToken, ct);
        if (!updated)
        {
            await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, false, "concurrency_conflict", mode, ct);
            throw new InvalidOperationException("concurrency_conflict");
        }

        await _repository.DeprecatePublishedVersionsAsync(version.BusinessReferenceDataSetId, version.BusinessReferenceDataVersionId, version.BusinessReferenceDataVersionId, ct);
        await PromoteSetPointersAsync(version, actorId, correlationId, ct);
        await EmitPublishedEventsAsync(version, actorId, correlationId, idempotencyKey, mode, ct);

        if (overrideAction && !string.IsNullOrWhiteSpace(version.OverrideReason))
        {
            await _reviewHook.TriggerAsync(version, actorId, correlationId, version.OverrideReason, ct);
        }

        await _auditAdapter.EmitPublishAsync(version, actorId, correlationId, true, overrideAction ? "override_publish" : null, mode, ct);
        return BusinessReferenceDataModelMapper.ToVersionDetail(version);
    }

    private async Task PromoteSetPointersAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        CancellationToken ct)
    {
        // Publishing flips the version's own Status to Published, but the parent set's pointers must follow:
        // surface the new published version and clear the active-draft pointer. Without this the set keeps
        // PublishedVersionId = null, so the Set Workspace "Open Published" action never enables. Best-effort —
        // a rare concurrency conflict here must not fail an already-persisted publish.
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct);
        if (set is null)
        {
            return;
        }

        if (set.PublishedVersionId == version.BusinessReferenceDataVersionId
            && set.ActiveDraftVersionId is null
            && set.Status == BusinessReferenceDataSetStatus.Active)
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

            await _eventPublisher.PublishAsync(deprecatedEvent, $"{idempotencyKey}:deprecated", version.BusinessReferenceDataVersionId, ct);
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
