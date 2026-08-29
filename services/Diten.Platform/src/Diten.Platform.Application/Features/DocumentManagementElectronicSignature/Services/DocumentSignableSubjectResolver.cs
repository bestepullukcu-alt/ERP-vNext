using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — resolves a signable subject and projects it to a CANONICAL METADATA FINGERPRINT
/// (GMG-QMS-SOP-0001 §11.2).
///
/// THIS IS THE HEART OF THE FEATURE. A signature that merely says "user X approved record Y" degrades silently the
/// moment Y changes. By hashing a canonical, key-sorted projection of Y's governance metadata at signing time, a
/// later change to any of those fields makes the hash stop matching, and the signature drops to RequiresResign
/// instead of continuing to look like current approval.
///
/// WHAT GOES INTO THE PROJECTION: status, dates, identifiers, decisions and evidence REFERENCES — the fields whose
/// change would alter what the signer was attesting to. WHAT NEVER GOES IN: document bytes, file contents or
/// attachments. FU23 does not read content and cannot hash it.
///
/// FAIL-CLOSED BY CONSTRUCTION: a subject type with no resolver here, or a subject that does not exist in the
/// caller's tenant, returns null. The sign path treats null as a hard block. It never falls back to hashing the
/// caller's own input, because that would let a caller manufacture a fingerprint for an object they cannot see —
/// and would make cross-tenant signing indistinguishable from legitimate signing.
/// </summary>
public sealed class DocumentSignableSubjectResolver
{
    private readonly IDocumentApprovalEvidenceRepository _approvalEvidence;
    private readonly IDocumentReleaseGateEvidenceRepository _releaseGateEvidence;
    private readonly IDocumentTrainingAssignmentRepository _trainingAssignments;
    private readonly IDocumentGDocPCorrectionRecordRepository _corrections;
    private readonly IDocumentQualityEventRepository _qualityEvents;
    private readonly IDocumentDeviationRepository _deviations;
    private readonly IDocumentCAPAActionRepository _capaActions;
    private readonly IDocumentRepositoryAssessmentRepository _repositoryAssessments;
    private readonly IDocumentLegalHoldRepository _legalHolds;
    private readonly IDocumentDispositionRequestRepository _dispositionRequests;
    private readonly IDocumentTemporaryControlledIssueRepository _temporaryIssues;
    private readonly IDocumentCopyWithdrawalPlanRepository _withdrawalPlans;
    private readonly IExternalDocumentImpactAssessmentRepository _externalImpacts;
    private readonly IDocumentMasterRegisterRepository _registerEntries;

    public DocumentSignableSubjectResolver(
        IDocumentApprovalEvidenceRepository approvalEvidence,
        IDocumentReleaseGateEvidenceRepository releaseGateEvidence,
        IDocumentTrainingAssignmentRepository trainingAssignments,
        IDocumentGDocPCorrectionRecordRepository corrections,
        IDocumentQualityEventRepository qualityEvents,
        IDocumentDeviationRepository deviations,
        IDocumentCAPAActionRepository capaActions,
        IDocumentRepositoryAssessmentRepository repositoryAssessments,
        IDocumentLegalHoldRepository legalHolds,
        IDocumentDispositionRequestRepository dispositionRequests,
        IDocumentTemporaryControlledIssueRepository temporaryIssues,
        IDocumentCopyWithdrawalPlanRepository withdrawalPlans,
        IExternalDocumentImpactAssessmentRepository externalImpacts,
        IDocumentMasterRegisterRepository registerEntries)
    {
        _approvalEvidence = approvalEvidence;
        _releaseGateEvidence = releaseGateEvidence;
        _trainingAssignments = trainingAssignments;
        _corrections = corrections;
        _qualityEvents = qualityEvents;
        _deviations = deviations;
        _capaActions = capaActions;
        _repositoryAssessments = repositoryAssessments;
        _legalHolds = legalHolds;
        _dispositionRequests = dispositionRequests;
        _temporaryIssues = temporaryIssues;
        _withdrawalPlans = withdrawalPlans;
        _externalImpacts = externalImpacts;
        _registerEntries = registerEntries;
    }

    /// <summary>
    /// The subject types that need a <c>RegisterEntryId</c> alongside the subject id. Their repositories expose only
    /// a by-register-entry lookup, and FU23 deliberately does NOT widen those existing FU09/FU10 contracts.
    /// </summary>
    public static bool RequiresRegisterEntryId(SignableSubjectType subjectType) =>
        subjectType is SignableSubjectType.ApprovalEvidence or SignableSubjectType.ReleaseGateEvidence;

    /// <summary>Subject types with no resolver in FU23. Signing them is blocked rather than silently permitted.</summary>
    public static bool IsResolvable(SignableSubjectType subjectType) =>
        subjectType is not (SignableSubjectType.GDocPCorrectionReview or SignableSubjectType.Other);

    /// <summary>
    /// Resolves the subject within the caller's tenant and returns its canonical projection plus fingerprint.
    /// Returns null when the subject does not exist, is not visible to this tenant, or has no resolver.
    /// </summary>
    public async Task<SignableSubjectSnapshot?> ResolveAsync(
        SignableSubjectType subjectType, Guid subjectId, Guid? registerEntryId, CancellationToken ct)
    {
        var fields = await ProjectAsync(subjectType, subjectId, registerEntryId, ct);
        if (fields is null)
        {
            return null;
        }

        var canonical = Canonicalize(subjectType, subjectId, fields);
        return new SignableSubjectSnapshot(
            subjectType,
            subjectId,
            registerEntryId,
            canonical,
            ComputeSha256(canonical),
            SignatureFingerprintAlgorithm.CanonicalJsonSha256,
            Summarize(fields));
    }

    // ── per-subject projections ───────────────────────────────────────────────
    //
    // Each projection lists the governance fields whose change should invalidate a signature. Adding a field here
    // is a behaviour change: it makes previously-valid signatures require re-signing, which is correct when the
    // field genuinely forms part of what was attested, and wrong when it does not.

    private async Task<Dictionary<string, string?>?> ProjectAsync(
        SignableSubjectType subjectType, Guid subjectId, Guid? registerEntryId, CancellationToken ct)
    {
        switch (subjectType)
        {
            case SignableSubjectType.ApprovalEvidence:
            {
                if (registerEntryId is not { } entryId)
                {
                    return null;
                }

                var e = (await _approvalEvidence.GetByRegisterEntryAsync(entryId, ct))
                    .FirstOrDefault(x => x.Id == subjectId);
                return e is null ? null : new Dictionary<string, string?>
                {
                    ["requirementId"] = e.RequirementId.ToString(),
                    ["registerEntryId"] = e.RegisterEntryId.ToString(),
                    ["action"] = e.Action.ToString(),
                    ["performedByUserId"] = e.PerformedByUserId.ToString(),
                    ["performedByRole"] = e.PerformedByRole.ToString(),
                    ["performedAt"] = Iso(e.PerformedAt),
                    ["evidenceReference"] = e.EvidenceReference,
                    ["segregationResult"] = e.SegregationResult.ToString(),
                    ["isSegregationChecked"] = Bool(e.IsSegregationChecked)
                };
            }

            case SignableSubjectType.ReleaseGateEvidence:
            {
                if (registerEntryId is not { } entryId)
                {
                    return null;
                }

                var e = (await _releaseGateEvidence.GetByRegisterEntryAsync(entryId, ct))
                    .FirstOrDefault(x => x.Id == subjectId);
                return e is null ? null : new Dictionary<string, string?>
                {
                    ["registerEntryId"] = e.RegisterEntryId.ToString(),
                    ["gateKey"] = e.GateKey.ToString(),
                    ["evidenceReference"] = e.EvidenceReference,
                    ["verifiedByUserId"] = e.VerifiedByUserId.ToString(),
                    ["verifiedByRole"] = e.VerifiedByRole,
                    ["verificationDate"] = Iso(e.VerificationDate)
                };
            }

            case SignableSubjectType.TrainingAssignment:
            {
                var a = await _trainingAssignments.GetByIdAsync(subjectId, ct);
                return a is null ? null : new Dictionary<string, string?>
                {
                    ["registerEntryId"] = a.RegisterEntryId.ToString(),
                    ["requirementId"] = a.RequirementId.ToString(),
                    ["assignedToUserId"] = a.AssignedToUserId?.ToString(),
                    ["assignedToRole"] = a.AssignedToRole?.ToString(),
                    ["trainingType"] = a.TrainingType.ToString(),
                    ["status"] = a.Status.ToString(),
                    ["completionEvidenceReference"] = a.CompletionEvidenceReference,
                    ["completedAt"] = Iso(a.CompletedAt),
                    ["dueDate"] = Iso(a.DueDate)
                };
            }

            // The same aggregate, projected around the EFFECTIVENESS decision rather than completion: an
            // effectiveness confirmation must not be invalidated by an unrelated completion-side edit.
            case SignableSubjectType.TrainingEffectiveness:
            {
                var a = await _trainingAssignments.GetByIdAsync(subjectId, ct);
                return a is null ? null : new Dictionary<string, string?>
                {
                    ["registerEntryId"] = a.RegisterEntryId.ToString(),
                    ["requirementId"] = a.RequirementId.ToString(),
                    ["assignedToUserId"] = a.AssignedToUserId?.ToString(),
                    ["effectivenessCheckStatus"] = a.EffectivenessCheckStatus.ToString(),
                    ["effectivenessEvidenceReference"] = a.EffectivenessEvidenceReference,
                    ["status"] = a.Status.ToString()
                };
            }

            case SignableSubjectType.GDocPCorrectionRecord:
            {
                var c = await _corrections.GetByIdAsync(subjectId, ct);
                return c is null ? null : new Dictionary<string, string?>
                {
                    ["correctionNumber"] = c.CorrectionNumber,
                    ["subjectType"] = c.SubjectType.ToString(),
                    ["subjectId"] = c.SubjectId.ToString(),
                    ["fieldPath"] = c.FieldPath,
                    ["previousValueSnapshot"] = c.PreviousValueSnapshot,
                    ["newValueSnapshot"] = c.NewValueSnapshot,
                    ["correctionType"] = c.CorrectionType.ToString(),
                    ["correctionReason"] = c.CorrectionReason,
                    ["correctedAt"] = Iso(c.CorrectedAt),
                    ["correctedByUserId"] = c.CorrectedByUserId?.ToString(),
                    ["isBackdatingCorrection"] = Bool(c.IsBackdatingCorrection),
                    ["isHighRiskCorrection"] = Bool(c.IsHighRiskCorrection),
                    ["reviewStatus"] = c.ReviewStatus.ToString()
                };
            }

            case SignableSubjectType.QualityEvent:
            {
                var q = await _qualityEvents.GetByIdAsync(subjectId, ct);
                return q is null ? null : new Dictionary<string, string?>
                {
                    ["qualityEventNumber"] = q.QualityEventNumber,
                    ["eventType"] = q.EventType.ToString(),
                    ["eventSeverity"] = q.EventSeverity.ToString(),
                    ["eventStatus"] = q.EventStatus.ToString(),
                    ["requiresDeviation"] = Bool(q.RequiresDeviation),
                    ["requiresCapa"] = Bool(q.RequiresCAPA),
                    ["closureEvidenceReference"] = q.ClosureEvidenceReference,
                    ["closedAt"] = Iso(q.ClosedAt)
                };
            }

            case SignableSubjectType.Deviation:
            {
                var d = await _deviations.GetByIdAsync(subjectId, ct);
                return d is null ? null : new Dictionary<string, string?>
                {
                    ["deviationNumber"] = d.DeviationNumber,
                    ["qualityEventId"] = d.QualityEventId.ToString(),
                    ["deviationCategory"] = d.DeviationCategory.ToString(),
                    ["deviationSeverity"] = d.DeviationSeverity.ToString(),
                    ["deviationStatus"] = d.DeviationStatus.ToString(),
                    ["rootCauseCategory"] = d.RootCauseCategory.ToString(),
                    ["rootCauseSummary"] = d.RootCauseSummary,
                    ["impactAssessment"] = d.PatientProductRegulatoryImpact.ToString(),
                    ["closureEvidenceReference"] = d.ClosureEvidenceReference,
                    ["closedAt"] = Iso(d.ClosedAt)
                };
            }

            case SignableSubjectType.CAPAAction:
            {
                var a = await _capaActions.GetByIdAsync(subjectId, ct);
                return a is null ? null : new Dictionary<string, string?>
                {
                    ["capaNumber"] = a.CAPANumber,
                    ["qualityEventId"] = a.QualityEventId?.ToString(),
                    ["deviationId"] = a.DeviationId?.ToString(),
                    ["actionType"] = a.ActionType.ToString(),
                    ["actionStatus"] = a.ActionStatus.ToString(),
                    ["completionEvidenceReference"] = a.CompletionEvidenceReference,
                    ["completedAt"] = Iso(a.CompletedAt),
                    ["effectivenessResult"] = a.EffectivenessResult.ToString(),
                    ["effectivenessEvidenceReference"] = a.EffectivenessEvidenceReference,
                    ["closedAt"] = Iso(a.ClosedAt)
                };
            }

            case SignableSubjectType.RepositoryAssessment:
            {
                var a = await _repositoryAssessments.GetByIdAsync(subjectId, ct);
                return a is null ? null : new Dictionary<string, string?>
                {
                    ["repositoryKey"] = a.RepositoryKey,
                    ["repositoryName"] = a.RepositoryName,
                    ["repositoryType"] = a.RepositoryType.ToString(),
                    ["assessmentStatus"] = a.AssessmentStatus.ToString(),
                    ["approvedAt"] = Iso(a.ApprovedAt),
                    ["validationEvidenceReference"] = a.ValidationEvidenceReference,
                    ["assessmentEvidenceReference"] = a.AssessmentEvidenceReference,
                    ["validFrom"] = Iso(a.ValidFrom),
                    ["validUntil"] = Iso(a.ValidUntil)
                };
            }

            case SignableSubjectType.LegalHold:
            {
                var h = await _legalHolds.GetByIdAsync(subjectId, ct);
                return h is null ? null : new Dictionary<string, string?>
                {
                    ["holdKey"] = h.HoldKey,
                    ["holdTitle"] = h.HoldTitle,
                    ["holdStatus"] = h.HoldStatus.ToString(),
                    ["holdReason"] = h.HoldReason.ToString(),
                    ["scopeType"] = h.ScopeType.ToString(),
                    ["effectiveFrom"] = Iso(h.EffectiveFrom),
                    ["effectiveUntil"] = Iso(h.EffectiveUntil),
                    ["releasedAt"] = Iso(h.ReleasedAt),
                    ["legalApprovalEvidenceReference"] = h.LegalApprovalEvidenceReference
                };
            }

            case SignableSubjectType.DispositionRequest:
            {
                var r = await _dispositionRequests.GetByIdAsync(subjectId, ct);
                return r is null ? null : new Dictionary<string, string?>
                {
                    ["requestNumber"] = r.RequestNumber,
                    ["subjectType"] = r.SubjectType.ToString(),
                    ["subjectId"] = r.SubjectId.ToString(),
                    ["requestStatus"] = r.RequestStatus.ToString(),
                    ["eligibilityResult"] = r.EligibilityResult.ToString(),
                    ["approvalEvidenceReference"] = r.ApprovalEvidenceReference,
                    ["approvedAt"] = Iso(r.ApprovedAt),
                    ["executedAt"] = Iso(r.ExecutedAt)
                };
            }

            case SignableSubjectType.TemporaryControlledIssue:
            {
                var i = await _temporaryIssues.GetByIdAsync(subjectId, ct);
                return i is null ? null : new Dictionary<string, string?>
                {
                    ["issueNumber"] = i.IssueNumber,
                    ["downtimeEventId"] = i.DowntimeEventId.ToString(),
                    ["registerEntryId"] = i.RegisterEntryId.ToString(),
                    ["issueStatus"] = i.IssueStatus.ToString(),
                    ["approvalMechanism"] = i.ApprovalMechanism?.ToString(),
                    ["approvalEvidenceReference"] = i.ApprovalEvidenceReference,
                    ["approvedAt"] = Iso(i.ApprovedAt),
                    ["reconciledAt"] = Iso(i.ReconciledAt),
                    ["issuedCopyCount"] = i.IssuedCopyCount.ToString(CultureInfo.InvariantCulture)
                };
            }

            case SignableSubjectType.ControlledCopyWithdrawal:
            {
                var p = await _withdrawalPlans.GetByIdAsync(subjectId, ct);
                return p is null ? null : new Dictionary<string, string?>
                {
                    ["registerEntryId"] = p.RegisterEntryId.ToString(),
                    ["triggerType"] = p.TriggerType.ToString(),
                    ["planStatus"] = p.PlanStatus.ToString(),
                    ["requiredCopyCount"] = p.RequiredCopyCount.ToString(CultureInfo.InvariantCulture),
                    ["withdrawnCopyCount"] = p.WithdrawnCopyCount.ToString(CultureInfo.InvariantCulture),
                    ["missingCopyCount"] = p.MissingCopyCount.ToString(CultureInfo.InvariantCulture),
                    ["planEvidenceReference"] = p.PlanEvidenceReference,
                    ["completedAt"] = Iso(p.CompletedAt)
                };
            }

            case SignableSubjectType.ExternalImpactAssessment:
            {
                var a = await _externalImpacts.GetByIdAsync(subjectId, ct);
                return a is null ? null : new Dictionary<string, string?>
                {
                    ["externalDocumentRegisterEntryId"] = a.ExternalDocumentRegisterEntryId.ToString(),
                    ["assessmentStatus"] = a.AssessmentStatus.ToString(),
                    ["recommendedAction"] = a.RecommendedAction.ToString(),
                    ["impactSummary"] = a.ImpactSummary,
                    ["assessmentEvidenceReference"] = a.AssessmentEvidenceReference,
                    ["completedAt"] = Iso(a.CompletedAt)
                };
            }

            case SignableSubjectType.DocumentMasterRegisterEntry:
            {
                var e = await _registerEntries.GetByIdAsync(subjectId, ct);
                return e is null ? null : new Dictionary<string, string?>
                {
                    ["permanentUid"] = e.PermanentUid,
                    ["documentCode"] = e.DocumentCode,
                    ["documentTitle"] = e.DocumentTitle,
                    ["documentType"] = e.DocumentType.ToString(),
                    ["criticality"] = e.Criticality.ToString(),
                    ["lifecycleStatus"] = e.LifecycleStatus.ToString(),
                    ["registerStatus"] = e.RegisterStatus.ToString(),
                    ["currentVersionLabel"] = e.CurrentVersionLabel,
                    ["effectiveDate"] = Iso(e.EffectiveDate),
                    ["nextReviewDueDate"] = Iso(e.NextReviewDueDate)
                };
            }

            // GDocPCorrectionReview and Other have no resolver in FU23 — see IsResolvable. Reported as a gap
            // rather than approximated: a fingerprint over a subject we cannot read would be meaningless.
            default:
                return null;
        }
    }

    // ── canonicalisation ──────────────────────────────────────────────────────

    /// <summary>
    /// Key-sorted, culture-invariant JSON. Sorting is what makes the hash reproducible: two projections with the
    /// same content must produce byte-identical input regardless of dictionary iteration order.
    /// </summary>
    private static string Canonicalize(
        SignableSubjectType subjectType, Guid subjectId, Dictionary<string, string?> fields)
    {
        var builder = new StringBuilder();
        builder.Append("{\"subjectType\":\"").Append(subjectType).Append("\",\"subjectId\":\"")
            .Append(subjectId.ToString("D")).Append("\",\"fields\":{");

        var first = true;
        foreach (var pair in fields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append('"').Append(Escape(pair.Key)).Append("\":");
            builder.Append(pair.Value is null ? "null" : $"\"{Escape(pair.Value)}\"");
        }

        return builder.Append("}}").ToString();
    }

    private static string ComputeSha256(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

    /// <summary>A short, human-legible digest of the projection — what a reviewer sees next to the hash.</summary>
    private static string Summarize(Dictionary<string, string?> fields) =>
        string.Join("; ", fields
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string? Iso(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}
