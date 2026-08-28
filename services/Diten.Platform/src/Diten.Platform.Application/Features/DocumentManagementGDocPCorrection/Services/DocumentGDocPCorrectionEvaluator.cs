using System.Globalization;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;

/// <summary>
/// MOD-0029-FU21 — classifies the risk of a proposed correction and resolves what it must carry
/// (GMG-QMS-SOP-0001 §21). This is where the SOP's GDocP rules actually live.
///
/// RISK RULES (a correction is high risk if ANY applies):
/// • <see cref="GDocPCorrectionType.Reconstruction"/> — recreating a lost value. Also requires evidence, a
///   deviation reference and review: an undocumented reconstruction is precisely what the SOP forbids.
/// • <see cref="GDocPCorrectionType.DataIntegrityCorrection"/> — same treatment.
/// • <see cref="GDocPCorrectionType.EvidenceReferenceCorrection"/> — changing which evidence a regulated decision
///   rests on. Requires evidence for the correction itself.
/// • <see cref="GDocPCorrectionType.StatusCorrection"/> — rewriting a lifecycle/approval/release status.
/// • BACKDATING — a regulated timestamp moved to an EARLIER value.
/// • An unknown previous value — the trail cannot show what was replaced.
/// • Any matching policy marking the field backdating/status/evidence sensitive.
///
/// POLICY RESOLUTION is deliberately monotonic: every "requires" flag is OR-ed and every "allow" flag is AND-ed
/// across all matching active policies, so adding a policy can only tighten control. With no policy at all a safe
/// default applies rather than an permissive one.
///
/// This evaluator is a pure function over its inputs and the policy set — it mutates nothing and persists nothing.
/// </summary>
public sealed class DocumentGDocPCorrectionEvaluator
{
    private readonly IDocumentGDocPCorrectionPolicyRepository _policies;

    public DocumentGDocPCorrectionEvaluator(IDocumentGDocPCorrectionPolicyRepository policies) => _policies = policies;

    /// <summary>
    /// Field names whose value is a regulated timestamp. Moving one of these earlier is backdating, and correcting
    /// one at all requires review under the safe default. Matched case-insensitively on suffix/equality.
    /// </summary>
    private static readonly string[] RegulatedTimestampFields =
    [
        "CreatedAt", "PerformedAt", "ApprovedAt", "CompletedAt", "EffectiveDate", "IssuedAt", "ReconciledAt",
        "VerificationDate", "AllocatedAt", "ReviewedAt", "WithdrawnAt", "ExecutedAt", "StartedAt", "RestoredAt",
        "OccurredAt", "SourceEffectiveDate", "LocalEffectiveDate", "EffectiveFrom", "LastTransitionAt"
    ];

    /// <summary>
    /// Server-owned timestamps that no correction may target: they are the system's own attestation of when
    /// something happened, and are <c>init</c>-only on the persistence base entity anyway.
    /// </summary>
    private static readonly string[] ImmutableServerFields = ["CorrectedAt", "WrittenAtUtc", "CreatedAt.Server", "Id", "TenantId"];

    public static bool IsRegulatedTimestampField(string fieldPath) =>
        RegulatedTimestampFields.Any(f => fieldPath.EndsWith(f, StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(fieldPath, f, StringComparison.OrdinalIgnoreCase));

    public static bool IsImmutableServerField(string fieldPath) =>
        ImmutableServerFields.Any(f => string.Equals(fieldPath, f, StringComparison.OrdinalIgnoreCase));

    public async Task<GDocPCorrectionRequirementModel> EvaluateAsync(
        GDocPSubjectType subjectType,
        string fieldPath,
        string previousValue,
        string newValue,
        GDocPCorrectionType correctionType,
        CancellationToken ct)
    {
        var matching = (await _policies.GetActiveBySubjectTypeAsync(subjectType, ct))
            .Where(p => p.Matches(fieldPath))
            .ToList();

        // ── policy resolution: most restrictive wins ──────────────────────────
        // Safe defaults when nothing matches: reason always required; high-risk types need a deviation.
        var requiresReason = true;
        var requiresEvidence = matching.Any(p => p.RequiresEvidenceReference);
        var requiresReview = matching.Any(p => p.RequiresReview);
        var requiresDeviationForHighRisk = matching.Count == 0 || matching.Any(p => p.RequiresDeviationReferenceForHighRisk);
        var allowAfterApproval = matching.All(p => p.AllowCorrectionAfterApproval);
        var allowAfterEffective = matching.All(p => p.AllowCorrectionAfterEffective);
        var backdatingSensitive = matching.Any(p => p.IsBackdatingSensitive);
        var statusSensitive = matching.Any(p => p.IsStatusSensitive);
        var evidenceSensitive = matching.Any(p => p.IsEvidenceSensitive);

        // ── risk classification ───────────────────────────────────────────────
        var notes = new List<string>();
        var highRisk = false;
        var isBackdating = false;

        var isRegulatedTimestamp = IsRegulatedTimestampField(fieldPath);
        if (isRegulatedTimestamp && TryDetectBackdating(previousValue, newValue))
        {
            isBackdating = true;
            highRisk = true;
            notes.Add($"Regulated timestamp '{fieldPath}' is being moved EARLIER — treated as backdating.");
        }

        switch (correctionType)
        {
            case GDocPCorrectionType.Reconstruction:
                highRisk = true;
                requiresEvidence = true;
                requiresReview = true;
                notes.Add("Reconstruction of a lost value: evidence, a deviation reference and review are mandatory.");
                break;

            case GDocPCorrectionType.DataIntegrityCorrection:
                highRisk = true;
                requiresEvidence = true;
                requiresReview = true;
                notes.Add("Data-integrity correction: evidence, a deviation reference and review are mandatory.");
                break;

            case GDocPCorrectionType.EvidenceReferenceCorrection:
                highRisk = true;
                requiresEvidence = true;
                notes.Add("Changing the evidence a regulated decision rests on is inherently high risk.");
                break;

            case GDocPCorrectionType.StatusCorrection:
                highRisk = true;
                notes.Add("Correcting a lifecycle/approval/release status is inherently high risk.");
                break;
        }

        if (string.Equals(previousValue, DocumentGDocPCorrectionRecord.UnknownPreviousValue, StringComparison.Ordinal))
        {
            highRisk = true;
            notes.Add("The previous value could not be established; the trail cannot show what was replaced.");
        }

        if (backdatingSensitive && isRegulatedTimestamp)
        {
            highRisk = true;
            notes.Add("Policy marks this field backdating-sensitive.");
        }

        if (statusSensitive)
        {
            highRisk = true;
            notes.Add("Policy marks this field status-sensitive.");
        }

        if (evidenceSensitive)
        {
            highRisk = true;
            requiresEvidence = true;
            notes.Add("Policy marks this field evidence-sensitive.");
        }

        // SAFE DEFAULT: with no policy on file, correcting a regulated timestamp still demands second-person review.
        if (matching.Count == 0 && isRegulatedTimestamp)
        {
            requiresReview = true;
            notes.Add("No policy on file; the safe default requires review for a regulated timestamp correction.");
        }

        // A high-risk correction always demands review — a deviation reference alone is not a second person.
        if (highRisk)
        {
            requiresReview = true;
        }

        var requiresDeviation = highRisk && requiresDeviationForHighRisk;

        if (notes.Count == 0)
        {
            notes.Add("Routine correction: no high-risk indicator detected.");
        }

        return new GDocPCorrectionRequirementModel(
            requiresReason, requiresEvidence, requiresReview, requiresDeviation,
            allowAfterApproval, allowAfterEffective, highRisk, isBackdating,
            string.Join(" ", notes), matching.Select(p => p.PolicyKey).ToList());
    }

    /// <summary>
    /// Backdating is only asserted when BOTH values parse as dates and the new one is genuinely earlier. An
    /// unparseable value is never guessed at — it simply does not raise the backdating flag on its own.
    /// </summary>
    private static bool TryDetectBackdating(string previousValue, string newValue) =>
        TryParse(previousValue, out var previous) && TryParse(newValue, out var updated) && updated < previous;

    private static bool TryParse(string value, out DateTimeOffset parsed) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed);
}
