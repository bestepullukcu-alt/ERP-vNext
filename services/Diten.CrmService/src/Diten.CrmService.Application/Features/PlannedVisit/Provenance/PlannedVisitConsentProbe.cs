using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>
/// MOD-0164 read-only consent wrapper. Calls <see cref="IConsentPreferenceEvaluator"/> <b>in-process via DI</b> and
/// snapshots the verdict into <see cref="PlannedVisitConsentProvenance"/>. The channel is ALWAYS <c>visit</c> and the
/// purpose is the deterministic map of the plan's VisitPurpose (§4.7). Nothing but the verdict + matched ids + evaluator
/// version + time is copied (D5).
/// <para>The evaluator never throws into us (it returns a controlled <c>unknown</c> with
/// <c>consent_evaluation_error</c>), so a broken evaluator degrades to unknown rather than a 500 (AC-CONSENT-5). When
/// the subject id cannot be resolved the filter is reported as NOT applied — <see cref="PlannedVisitConsentProvenance.FilterApplied"/>
/// is false — and no eligibility inference may be drawn (D6/AC-CONSENT-4).</para>
/// </summary>
public sealed class PlannedVisitConsentProbe
{
    private readonly IConsentPreferenceEvaluator _evaluator;

    public PlannedVisitConsentProbe(IConsentPreferenceEvaluator evaluator) => _evaluator = evaluator;

    public async Task<PlannedVisitConsentProvenance> EvaluateAsync(PlannedVisitEntity plan, CancellationToken cancellationToken)
    {
        var subjectType = PlannedVisitValidation.ToConsentSubjectType(plan.TargetType);
        var purpose = PlannedVisitValidation.ToConsentPurpose(plan.VisitPurpose);
        var subjectId = PlannedVisitValidation.ConsentSubjectId(plan);
        var now = DateTimeOffset.UtcNow;

        if (subjectId == Guid.Empty)
        {
            // The filter could not be applied: no eligibility inference may be drawn from this row (D6).
            return new PlannedVisitConsentProvenance
            {
                FilterApplied = false,
                EligibilityStatus = ConsentEligibilityStatus.Unknown,
                Decision = ConsentDecision.ConsentUnknown,
                Channel = ConsentChannel.Visit,
                Purpose = purpose,
                ReasonCodes = new List<string> { ConsentReasonCodes.ConsentUnknown },
                SelectionReason = "Consent filter not applied: subject id could not be resolved.",
                EvaluatorVersion = ConsentEvaluationResult.CurrentEvaluatorVersion,
                EvaluatedAt = now
            };
        }

        var request = new ConsentEvaluationRequest(
            SubjectType: subjectType,
            SubjectId: subjectId,
            Channel: ConsentChannel.Visit,
            Purpose: purpose);

        var result = await _evaluator.EvaluateAsync(request, cancellationToken);

        return new PlannedVisitConsentProvenance
        {
            FilterApplied = true,
            EligibilityStatus = result.EligibilityStatus,
            Decision = result.Decision,
            Channel = result.Channel,
            Purpose = result.Purpose,
            MatchedConsentId = result.MatchedConsentId,
            MatchedPreferenceIds = result.MatchedPreferenceIds.ToList(),
            ReasonCodes = result.ReasonCodes.ToList(),
            SelectionReason = result.SelectionReason,
            EvaluatorVersion = result.EvaluatorVersion,
            EvaluatedAt = result.EvaluatedAt
        };
    }
}
