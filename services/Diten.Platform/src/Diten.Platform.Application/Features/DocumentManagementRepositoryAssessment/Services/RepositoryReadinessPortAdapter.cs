using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Services;

/// <summary>
/// MOD-0029-FU16 — adapter implementing the FU10 <see cref="IRepositoryReadinessPort"/>. Computes Gate 2 from the
/// repository assessment linked to the register entry:
/// <list type="bullet">
/// <item>Linked APPROVED, non-expired assessment that can support the release gate and has no open critical findings → PASS.</item>
/// <item>Linked assessment that is rejected/expired/unapproved/under-review, or has open critical findings → BLOCK.</item>
/// <item>No linked assessment: a Critical / release-gate-required document → BLOCK (an approved repository assessment is
/// mandatory, SOP §11.1); otherwise → fall back to the FU10 manual Gate 2 evidence (backward compatible).</item>
/// </list>
/// </summary>
public sealed class RepositoryReadinessPortAdapter : IRepositoryReadinessPort
{
    private readonly IDocumentRepositoryAssessmentRepository _assessments;
    private readonly IDocumentRepositoryAssessmentFindingRepository _findings;
    private readonly DocumentRepositoryAssessmentEvaluator _evaluator;

    public RepositoryReadinessPortAdapter(
        IDocumentRepositoryAssessmentRepository assessments,
        IDocumentRepositoryAssessmentFindingRepository findings,
        DocumentRepositoryAssessmentEvaluator evaluator)
    {
        _assessments = assessments;
        _findings = findings;
        _evaluator = evaluator;
    }

    public async Task<RepositoryGateDecision> EvaluateGate2Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        if (!Guid.TryParse(entry.ApprovedRepositoryId, out var assessmentId))
        {
            var mustHaveAssessment = entry.Criticality == DocumentCriticality.Critical || entry.RequiresReleaseGateEvaluation;
            return mustHaveAssessment
                ? new RepositoryGateDecision(RepositoryGateOutcome.Block, null,
                    "REPOSITORY_ASSESSMENT_MISSING: an approved repository/DMS assessment is required before a Critical document can be released (SOP §11.1).")
                : new RepositoryGateDecision(RepositoryGateOutcome.FallBackToManual, null, null);
        }

        var assessment = await _assessments.GetByIdAsync(assessmentId, ct);
        if (assessment is null)
        {
            return new RepositoryGateDecision(RepositoryGateOutcome.Block, null, "The linked repository assessment was not found.");
        }

        if (assessment.AssessmentStatus != RepositoryAssessmentStatus.Approved)
        {
            return new RepositoryGateDecision(RepositoryGateOutcome.Block, null,
                $"The repository assessment is {assessment.AssessmentStatus}, not Approved.");
        }

        var now = DateTimeOffset.UtcNow;
        if (assessment.ValidUntil is { } validUntil && now > validUntil)
        {
            return new RepositoryGateDecision(RepositoryGateOutcome.Block, null, "The repository assessment has expired.");
        }

        var result = _evaluator.Evaluate(assessment, now);
        if (!result.CanSupportReleaseGate)
        {
            return new RepositoryGateDecision(RepositoryGateOutcome.Block, null, result.BoundaryStatement);
        }

        var findings = await _findings.GetByAssessmentAsync(assessment.Id, ct);
        if (findings.Any(fnd => fnd.Status == RepositoryFindingStatus.Open && fnd.Severity == RepositoryFindingSeverity.Critical))
        {
            return new RepositoryGateDecision(RepositoryGateOutcome.Block, null, "The repository assessment has open critical findings.");
        }

        return new RepositoryGateDecision(RepositoryGateOutcome.Pass,
            $"Repository assessment: {assessment.RepositoryName} ({assessment.RepositoryType})", null);
    }
}
