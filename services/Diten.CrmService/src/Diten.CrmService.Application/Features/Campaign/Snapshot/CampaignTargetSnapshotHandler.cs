using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Features.Campaign.Snapshot;

/// <summary>
/// MOD-0165 FU04 — static campaign target snapshot.
/// <para>
/// <b>What it is:</b> a transport that normalizes a caller-supplied target list into <see cref="CampaignTarget"/> rows,
/// asks MOD-0164 whether each person-shaped target may be contacted, and records the verdict as provenance.
/// </para>
/// <para>
/// <b>What it is NOT:</b> a segmentation engine (membership is never computed — a segment-sourced snapshot stores the
/// segment id and takes the items as given), a campaign rule evaluator, a consent engine (the decision is always
/// MOD-0164's, reached through <see cref="IConsentPreferenceEvaluator"/> — this class holds no consent logic and never
/// reads the consent store), or a frequency/visit/route planner.
/// </para>
/// <para><b>Three structural guarantees:</b></para>
/// <list type="number">
/// <item><b>Additive.</b> A snapshot never deletes or archives an earlier target. There is no delete path at all.</item>
/// <item><b>Idempotent per source.</b> Re-running produces reconciles, not duplicates. A row whose target is already
/// owned by a DIFFERENT source is a conflict, and conflicts are detected BEFORE any write, so the batch is
/// all-or-nothing rather than half-applied.</item>
/// <item><b>Never silently unfiltered.</b> With <c>ApplyConsentFilter=true</c> a channel and purpose are mandatory
/// (request or campaign default) — absent, the request is rejected instead of evaluated against a guessed question.
/// With an explicit <c>ApplyConsentFilter=false</c>, every produced row carries <c>consent_filter_not_applied</c>.</item>
/// </list>
/// </summary>
public sealed class CreateCampaignTargetSnapshotHandler
    : IRequestHandler<CreateCampaignTargetSnapshotCommand, Response<CampaignTargetSnapshotResultDto>>
{
    /// <summary>Returned when a consent-filtered snapshot has no channel/purpose to evaluate with.</summary>
    public const string ConsentContextRequiredCode = "campaign_consent_context_required";

    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICampaignRepository _campaigns;
    private readonly ICampaignTargetRepository _targets;
    private readonly IConsentPreferenceEvaluator _consentEvaluator;

    public CreateCampaignTargetSnapshotHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICampaignRepository campaigns,
        ICampaignTargetRepository targets,
        IConsentPreferenceEvaluator consentEvaluator)
    {
        _tenant = tenant;
        _actor = actor;
        _campaigns = campaigns;
        _targets = targets;
        _consentEvaluator = consentEvaluator;
    }

    public async Task<Response<CampaignTargetSnapshotResultDto>> Handle(
        CreateCampaignTargetSnapshotCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail("Tenant context is required.", 400);
        }

        if (CampaignValidation.ValidateSnapshotSourceType(request.SourceType) is { } sourceTypeError)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(sourceTypeError, 400);
        }

        if (CampaignValidation.ValidateSelectionReason(request.SelectionReason) is { } reasonError)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(reasonError, 400);
        }

        if (CampaignValidation.ValidateSnapshotItems(request.TargetItems) is { } itemsError)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(itemsError, 400);
        }

        if (CampaignValidation.ValidateSourceReference(request.SourceReferenceType, request.SourceReferenceId)
            is { } sourceReferenceError)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(sourceReferenceError, 400);
        }

        var (campaign, campaignError, campaignStatusCode) = await CampaignTargetWrite.LoadMutableCampaignAsync(
            _campaigns, tenantId, request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(campaignError!, campaignStatusCode);
        }

        // ---- Consent context resolution (fail-closed: never guess a channel/purpose) ----
        var (channel, purpose, contextError) = ResolveConsentContext(request, campaign);
        if (contextError is not null)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(contextError, 400);
        }

        // ---- Step 1: structural validation of EVERY row before any write (no half-applied snapshot) ----
        if (ValidateRows(request.TargetItems) is { Count: > 0 } rowErrors)
        {
            return Response<CampaignTargetSnapshotResultDto>.Fail(rowErrors, 400);
        }

        // ---- Step 2: source-conflict pre-scan before any write (all-or-nothing) ----
        var existingByTarget = new Dictionary<(string, Guid), CampaignTarget>();
        var conflicts = new List<string>();
        var requestedSource = CampaignTargetSources.Normalize(request.SourceType);

        foreach (var item in request.TargetItems)
        {
            var targetType = CampaignTargetTypes.Normalize(item.TargetType);
            var existing = await _targets.FindActiveByTargetAsync(
                tenantId, request.CampaignId, targetType, item.TargetId, cancellationToken);
            if (existing is null)
            {
                continue;
            }

            existingByTarget[(targetType, item.TargetId)] = existing;
            if (!IsSameSource(existing, requestedSource, request.SourceReferenceId))
            {
                conflicts.Add(
                    $"{targetType}/{item.TargetId}: already owned by campaignTargetId={existing.Id} with source " +
                    $"'{existing.TargetSource}'" +
                    (existing.SourceReferenceId is { } id ? $" (sourceReferenceId={id})" : string.Empty) +
                    $"; the snapshot claims source '{requestedSource}' — " +
                    $"{CampaignReasonCodes.CampaignTargetSourceConflict}.");
            }
        }

        if (conflicts.Count > 0)
        {
            conflicts.Insert(0,
                $"Snapshot rejected: {conflicts.Count} target(s) are owned by a different source. " +
                "Nothing was written — a snapshot is never half-applied. Reconcile the conflicting rows explicitly " +
                "(archive the existing target, or re-run the snapshot with the owning source).");
            return Response<CampaignTargetSnapshotResultDto>.Fail(conflicts, 409);
        }

        // ---- Step 3: evaluate + write ----
        var now = DateTimeOffset.UtcNow;
        var effectiveAt = request.EffectiveAt ?? now;
        var batchId = Guid.NewGuid();
        var rows = new List<CampaignSnapshotRowResultDto>();
        var createdCount = 0;
        var reconciledCount = 0;
        var activeCount = 0;
        var excludedCount = 0;

        foreach (var item in request.TargetItems)
        {
            var targetType = CampaignTargetTypes.Normalize(item.TargetType);

            var evaluation = await EvaluateConsentAsync(
                request.ApplyConsentFilter, targetType, item.TargetId, channel, purpose,
                request.CampaignId, effectiveAt, cancellationToken);

            var (targetStatus, exclusionReason, consentReasonCodes) = MapConsentToStatus(evaluation);

            var reasonCodes = BuildRowReasonCodes(
                request, targetType, item, consentReasonCodes,
                isReconcile: existingByTarget.ContainsKey((targetType, item.TargetId)));

            if (existingByTarget.TryGetValue((targetType, item.TargetId), out var existing))
            {
                // Reconcile in place — no duplicate row, and the earlier row keeps its identity/history.
                existing.TargetDisplayName = CampaignWrite.Trim(item.TargetDisplayName) ?? existing.TargetDisplayName;
                existing.TargetStatus = targetStatus;
                existing.TargetSource = requestedSource;
                existing.SourceReferenceType = ResolveSourceReferenceType(request, item);
                existing.SourceReferenceId = item.SourceReferenceId ?? request.SourceReferenceId;
                existing.SnapshotBatchId = batchId;
                existing.SelectionReason = request.SelectionReason.Trim();
                existing.ReasonCodes = reasonCodes;
                existing.Priority = item.Priority ?? existing.Priority;
                existing.PriorityLevel = CampaignTargetPriorityLevels.Normalize(item.PriorityLevel)
                    ?? existing.PriorityLevel;
                existing.ConsentEvaluation = evaluation;
                existing.EffectiveFrom = effectiveAt;
                existing.EffectiveTo = request.EffectiveTo ?? existing.EffectiveTo;
                existing.ExclusionReason = exclusionReason;
                existing.UpdatedAt = now;
                existing.UpdatedBy = _actor.ActorName;

                await _targets.UpdateAsync(existing, cancellationToken);
                reconciledCount++;
                Tally(targetStatus, ref activeCount, ref excludedCount);
                rows.Add(ToRow(item, targetType, existing.Id, CampaignSnapshotRowOutcome.Reconciled, targetStatus,
                    exclusionReason, reasonCodes, evaluation,
                    $"Existing target reconciled in place (batch {batchId}); no duplicate was created."));
                continue;
            }

            var target = new CampaignTarget
            {
                TenantId = tenantId,
                CampaignId = request.CampaignId,
                TargetType = targetType,
                TargetId = item.TargetId,
                TargetDisplayName = CampaignWrite.Trim(item.TargetDisplayName),
                TargetStatus = targetStatus,
                TargetSource = requestedSource,
                SourceReferenceType = ResolveSourceReferenceType(request, item),
                SourceReferenceId = item.SourceReferenceId ?? request.SourceReferenceId,
                SnapshotBatchId = batchId,
                SelectionReason = request.SelectionReason.Trim(),
                ReasonCodes = reasonCodes,
                Priority = item.Priority,
                PriorityLevel = CampaignTargetPriorityLevels.Normalize(item.PriorityLevel),
                ConsentEvaluation = evaluation,
                EffectiveFrom = effectiveAt,
                EffectiveTo = request.EffectiveTo,
                ExclusionReason = exclusionReason,
                CreatedAt = now,
                CreatedBy = _actor.ActorName
            };

            await _targets.InsertAsync(target, cancellationToken);
            createdCount++;
            Tally(targetStatus, ref activeCount, ref excludedCount);
            rows.Add(ToRow(item, targetType, target.Id, CampaignSnapshotRowOutcome.Created, targetStatus,
                exclusionReason, reasonCodes, evaluation, $"Target created in batch {batchId}."));
        }

        var batchReasonCodes = BuildBatchReasonCodes(request, createdCount, reconciledCount, excludedCount);
        var result = new CampaignTargetSnapshotResultDto(
            batchId,
            request.CampaignId,
            requestedSource,
            ResolveSourceReferenceType(request, null),
            request.SourceReferenceId,
            effectiveAt,
            request.ApplyConsentFilter,
            channel,
            purpose,
            RequestedCount: request.TargetItems.Count,
            CreatedCount: createdCount,
            ReconciledCount: reconciledCount,
            ActiveCount: activeCount,
            ExcludedCount: excludedCount,
            ConflictCount: 0, // a conflict aborts the whole batch before any write (see step 2)
            Rows: rows,
            ReasonCodes: batchReasonCodes,
            SelectionReason: request.SelectionReason.Trim());

        return Response<CampaignTargetSnapshotResultDto>.Success(result, 201);
    }

    /// <summary>
    /// Resolves the consent question. When the filter is on, the channel and purpose come from the request or the
    /// campaign defaults — and if neither supplies them the request is REJECTED. This is the deliberate fail-closed
    /// choice over the alternative (running unfiltered and flagging it): a targeting run that silently skipped consent
    /// is exactly the audit hole the boundary forbids, and the caller can always opt out explicitly with
    /// <c>ApplyConsentFilter=false</c>.
    /// </summary>
    private static (string? Channel, string? Purpose, string? Error) ResolveConsentContext(
        CreateCampaignTargetSnapshotCommand request, CampaignEntity campaign)
    {
        if (!request.ApplyConsentFilter)
        {
            // Explicit opt-out: no question is asked, and every row will carry consent_filter_not_applied.
            return (null, null, null);
        }

        var channel = string.IsNullOrWhiteSpace(request.ConsentChannel)
            ? campaign.DefaultConsentChannel
            : request.ConsentChannel.Trim();
        var purpose = string.IsNullOrWhiteSpace(request.ConsentPurpose)
            ? campaign.DefaultConsentPurpose
            : request.ConsentPurpose.Trim();

        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(purpose))
        {
            return (null, null,
                $"{ConsentContextRequiredCode}: a consent-filtered snapshot requires both ConsentChannel and " +
                "ConsentPurpose (from the request or the campaign's DefaultConsentChannel/DefaultConsentPurpose). " +
                "No channel/purpose is ever assumed. Send them, or send ApplyConsentFilter=false to snapshot without " +
                $"the filter (every row will then carry '{CampaignReasonCodes.ConsentFilterNotApplied}').");
        }

        if (!ConsentChannel.IsValid(channel))
        {
            return (null, null, $"ConsentChannel must be one of: {string.Join(", ", ConsentChannel.All)}.");
        }

        if (!ConsentPurpose.IsValid(purpose))
        {
            return (null, null, $"ConsentPurpose must be one of: {string.Join(", ", ConsentPurpose.All)}.");
        }

        return (ConsentChannel.Normalize(channel), ConsentPurpose.Normalize(purpose), null);
    }

    /// <summary>Validates every row up front. A structurally bad row rejects the WHOLE request, so a typo can never
    /// leave a partially applied snapshot behind.</summary>
    private static List<string> ValidateRows(IReadOnlyList<CampaignSnapshotTargetItem> items)
    {
        var errors = new List<string>();
        var seen = new HashSet<(string, Guid)>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var error = CampaignValidation.ValidateTargetType(item.TargetType)
                ?? CampaignValidation.ValidateTargetId(item.TargetId)
                ?? CampaignValidation.ValidatePriority(item.Priority)
                ?? CampaignValidation.ValidatePriorityLevel(item.PriorityLevel)
                ?? CampaignValidation.ValidateSourceReference(item.SourceReferenceType, item.SourceReferenceId);

            if (error is not null)
            {
                errors.Add($"TargetItems[{i}]: {error}");
                continue;
            }

            var key = (CampaignTargetTypes.Normalize(item.TargetType), item.TargetId);
            if (!seen.Add(key))
            {
                errors.Add(
                    $"TargetItems[{i}]: duplicate row for {key.Item1}/{key.Item2} inside the same snapshot payload.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Asks MOD-0164 through the provider seam. FU04 holds no consent logic: it does not read the consent store, does
    /// not interpret consent status, and never writes to MOD-0164. A group-shaped target (segment / territory-node /
    /// concept-node / audience-profile) is not a consent subject, so it is reported <c>not_applicable</c> rather than
    /// evaluated — resolving its members is the MOD-0167 / MOD-0155 boundary.
    /// </summary>
    private async Task<CampaignTargetConsentEvaluation> EvaluateConsentAsync(
        bool applyConsentFilter,
        string targetType,
        Guid targetId,
        string? channel,
        string? purpose,
        Guid campaignId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        if (!applyConsentFilter)
        {
            return new CampaignTargetConsentEvaluation
            {
                Decision = ConsentDecision.NotApplicable,
                EligibilityStatus = ConsentEligibilityStatus.NotApplicable,
                ReasonCodes = new List<string> { CampaignReasonCodes.ConsentFilterNotApplied },
                EvaluatedAt = effectiveAt,
                EvaluatorVersion = ConsentEvaluationResult.CurrentEvaluatorVersion,
                SelectionReason =
                    "Consent filter was explicitly disabled for this snapshot (ApplyConsentFilter=false); " +
                    "no MOD-0164 evaluation was performed and no eligibility may be inferred from this row.",
                Channel = channel,
                Purpose = purpose,
                FilterApplied = false
            };
        }

        if (!CampaignTargetTypes.SupportsConsentEvaluation(targetType))
        {
            return new CampaignTargetConsentEvaluation
            {
                Decision = ConsentDecision.NotApplicable,
                EligibilityStatus = ConsentEligibilityStatus.NotApplicable,
                ReasonCodes = new List<string> { CampaignReasonCodes.ConsentEvaluationNotApplicable },
                EvaluatedAt = effectiveAt,
                EvaluatorVersion = ConsentEvaluationResult.CurrentEvaluatorVersion,
                SelectionReason =
                    $"Target type '{targetType}' is a group, not a consent subject; MOD-0164 evaluation is not " +
                    "applicable here. Consent must be evaluated per resolved person by the consuming module.",
                Channel = channel,
                Purpose = purpose,
                FilterApplied = true
            };
        }

        var result = await _consentEvaluator.EvaluateAsync(
            new ConsentEvaluationRequest(
                SubjectType: MapSubjectType(targetType),
                SubjectId: targetId,
                Channel: channel!,
                Purpose: purpose!,
                EffectiveAt: effectiveAt,
                ScopeType: ConsentScopeType.Campaign,
                ScopeId: campaignId,
                IncludeDiagnostics: false),
            cancellationToken);

        // Provenance ONLY: decision + matched ids + evaluator version + the question asked. No ConsentStatus, no
        // PreferenceStatus, no record payload is copied out of MOD-0164.
        return new CampaignTargetConsentEvaluation
        {
            Decision = result.Decision,
            EligibilityStatus = result.EligibilityStatus,
            ReasonCodes = result.ReasonCodes.ToList(),
            EvaluatedAt = result.EvaluatedAt,
            MatchedConsentId = result.MatchedConsentId,
            MatchedPreferenceIds = result.MatchedPreferenceIds.ToList(),
            EvaluatorVersion = result.EvaluatorVersion,
            SelectionReason = result.SelectionReason,
            Channel = result.Channel,
            Purpose = result.Purpose,
            FilterApplied = true
        };
    }

    /// <summary>Campaign target type → MOD-0164 consent subject type. Only the three person/relationship-shaped types
    /// reach this method (see <see cref="CampaignTargetTypes.SupportsConsentEvaluation"/>).</summary>
    private static string MapSubjectType(string targetType) => targetType switch
    {
        CampaignTargetTypes.Contact => ConsentSubjectType.Contact,
        CampaignTargetTypes.AccountContactLink => ConsentSubjectType.AccountContactLink,
        CampaignTargetTypes.Account => ConsentSubjectType.Account,
        _ => targetType
    };

    /// <summary>
    /// Maps the MOD-0164 verdict onto a target status. <b>Fail-closed:</b> only <c>allowed</c> produces an active
    /// target. <c>blocked</c> and <c>unknown</c> both produce an <c>excluded</c> target WITH a reason — the row is kept
    /// (not dropped) precisely so that "why was this person left out?" is auditable. An evaluator error is treated as
    /// unknown, never as allowed.
    /// </summary>
    private static (string TargetStatus, string? ExclusionReason, List<string> ReasonCodes) MapConsentToStatus(
        CampaignTargetConsentEvaluation evaluation)
    {
        if (!evaluation.FilterApplied)
        {
            return (CampaignTargetStatuses.Active, null,
                new List<string> { CampaignReasonCodes.ConsentFilterNotApplied });
        }

        if (evaluation.EligibilityStatus == ConsentEligibilityStatus.NotApplicable)
        {
            return (CampaignTargetStatuses.Active, null,
                new List<string> { CampaignReasonCodes.ConsentEvaluationNotApplicable });
        }

        var hadError = evaluation.ReasonCodes.Contains(ConsentReasonCodes.ConsentEvaluationError);

        return evaluation.EligibilityStatus switch
        {
            ConsentEligibilityStatus.Allowed => (
                CampaignTargetStatuses.Active,
                null,
                new List<string>
                {
                    CampaignReasonCodes.ConsentAllowed,
                    CampaignReasonCodes.ConsentProvenanceStored,
                    CampaignReasonCodes.CampaignTargetActive
                }),

            ConsentEligibilityStatus.Blocked => (
                CampaignTargetStatuses.Excluded,
                CampaignReasonCodes.ConsentBlocked,
                new List<string>
                {
                    CampaignReasonCodes.ConsentBlocked,
                    CampaignReasonCodes.ConsentProvenanceStored,
                    CampaignReasonCodes.CampaignTargetExcluded
                }),

            // unknown — and any unexpected value — is NEVER allowed.
            _ => (
                CampaignTargetStatuses.Excluded,
                CampaignReasonCodes.ConsentUnknown,
                hadError
                    ? new List<string>
                    {
                        CampaignReasonCodes.ConsentUnknown,
                        CampaignReasonCodes.ConsentEvaluationError,
                        CampaignReasonCodes.ConsentProvenanceStored,
                        CampaignReasonCodes.CampaignTargetExcluded
                    }
                    : new List<string>
                    {
                        CampaignReasonCodes.ConsentUnknown,
                        CampaignReasonCodes.ConsentProvenanceStored,
                        CampaignReasonCodes.CampaignTargetExcluded
                    })
        };
    }

    /// <summary>Whether an existing target belongs to the same source as this snapshot (idempotency key). When both
    /// sides carry a source reference id, they must match too — a snapshot of segment A must not silently take over a
    /// target produced from segment B.</summary>
    private static bool IsSameSource(CampaignTarget existing, string requestedSource, Guid? requestedSourceReferenceId)
    {
        if (!string.Equals(existing.TargetSource, requestedSource, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return existing.SourceReferenceId is not { } existingReference
               || requestedSourceReferenceId is not { } requestedReference
               || existingReference == requestedReference;
    }

    private static string? ResolveSourceReferenceType(
        CreateCampaignTargetSnapshotCommand request, CampaignSnapshotTargetItem? item)
    {
        var value = CampaignWrite.Trim(item?.SourceReferenceType) ?? CampaignWrite.Trim(request.SourceReferenceType);
        return value?.ToLowerInvariant();
    }

    private static List<string> BuildRowReasonCodes(
        CreateCampaignTargetSnapshotCommand request,
        string targetType,
        CampaignSnapshotTargetItem item,
        List<string> consentReasonCodes,
        bool isReconcile)
    {
        var defaults = new List<string>
        {
            isReconcile
                ? CampaignReasonCodes.CampaignTargetSnapshotReconciled
                : CampaignReasonCodes.CampaignTargetSnapshotCreated
        };

        if (!isReconcile)
        {
            defaults.Add(CampaignReasonCodes.CampaignTargetCreated);
        }

        var source = CampaignTargetSources.Normalize(request.SourceType);
        if (source == CampaignTargetSources.Manual)
        {
            defaults.Add(CampaignReasonCodes.ManualTargetSelected);
        }

        // A segment-sourced snapshot is provenance only: the segment id is recorded, never expanded.
        var sourceReferenceType = ResolveSourceReferenceType(request, item);
        if (source == CampaignTargetSources.Segment || sourceReferenceType == CampaignTargetTypes.Segment)
        {
            defaults.Add(CampaignReasonCodes.SegmentSourceSnapshot);
        }

        if ((item.SourceReferenceId ?? request.SourceReferenceId) is not null)
        {
            defaults.Add(CampaignReasonCodes.TargetSourceProvenanceStored);
        }

        defaults.AddRange(consentReasonCodes);
        return CampaignTargetWrite.NormalizeReasonCodes(request.ReasonCodes, defaults.ToArray());
    }

    private static List<string> BuildBatchReasonCodes(
        CreateCampaignTargetSnapshotCommand request, int createdCount, int reconciledCount, int excludedCount)
    {
        var codes = new List<string>();
        if (createdCount > 0)
        {
            codes.Add(CampaignReasonCodes.CampaignTargetSnapshotCreated);
        }

        if (reconciledCount > 0)
        {
            codes.Add(CampaignReasonCodes.CampaignTargetSnapshotReconciled);
        }

        if (excludedCount > 0)
        {
            codes.Add(CampaignReasonCodes.CampaignTargetExcluded);
        }

        var source = CampaignTargetSources.Normalize(request.SourceType);
        if (source == CampaignTargetSources.Segment
            || string.Equals(request.SourceReferenceType?.Trim(), CampaignTargetTypes.Segment,
                StringComparison.OrdinalIgnoreCase))
        {
            codes.Add(CampaignReasonCodes.SegmentSourceSnapshot);
        }

        if (source == CampaignTargetSources.Manual)
        {
            codes.Add(CampaignReasonCodes.ManualTargetSelected);
        }

        if (request.SourceReferenceId is not null)
        {
            codes.Add(CampaignReasonCodes.TargetSourceProvenanceStored);
        }

        if (!request.ApplyConsentFilter)
        {
            codes.Add(CampaignReasonCodes.ConsentFilterNotApplied);
        }

        return CampaignTargetWrite.NormalizeReasonCodes(request.ReasonCodes, codes.ToArray());
    }

    private static void Tally(string targetStatus, ref int activeCount, ref int excludedCount)
    {
        if (targetStatus == CampaignTargetStatuses.Excluded)
        {
            excludedCount++;
        }
        else if (targetStatus == CampaignTargetStatuses.Active)
        {
            activeCount++;
        }
    }

    private static CampaignSnapshotRowResultDto ToRow(
        CampaignSnapshotTargetItem item,
        string targetType,
        Guid campaignTargetId,
        string outcome,
        string targetStatus,
        string? exclusionReason,
        List<string> reasonCodes,
        CampaignTargetConsentEvaluation? evaluation,
        string message)
        => new(
            targetType,
            item.TargetId,
            campaignTargetId,
            outcome,
            targetStatus,
            exclusionReason,
            reasonCodes,
            CampaignMapper.ToDto(evaluation),
            message);
}
