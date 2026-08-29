using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;

/// <summary>
/// MOD-0029-FU23 — decides what a signature may and may not CLAIM, given the repository it lives in
/// (GMG-QMS-SOP-0001 §11, §11.2).
///
/// WHY THIS IS A SEPARATE SERVICE: the single most likely way this feature could cause regulatory harm is by
/// letting a signature recorded in an interim Google Drive folder be presented later as though it came from a
/// validated DMS. Concentrating that judgement in one evaluator — which ALWAYS produces a statement, and always a
/// conservative one — means no sign path can quietly skip it.
///
/// THE FLOOR, WHICH NO POLICY CAN LOWER:
/// • An UNAPPROVED repository BLOCKS a regulated signature outright. A repository nobody has assessed cannot host
///   evidence anyone should rely on, and warning-and-continuing would produce exactly the record we don't want.
/// • A VALIDATED DMS still gets no provider-validation claim, because FU23 calls no provider and validates no
///   certificate. "The repository was assessed as validated" and "this signature was validated" are different
///   assertions, and only the first can honestly be made here.
/// • An APPROVED INTERIM repository is explicitly stated NOT to be a validated DMS.
/// • NO assessment at all yields "boundary unknown" — never silence.
/// </summary>
public sealed class DocumentSignatureBoundaryEvaluator
{
    private readonly IDocumentRepositoryAssessmentRepository _assessments;

    public DocumentSignatureBoundaryEvaluator(IDocumentRepositoryAssessmentRepository assessments)
    {
        _assessments = assessments;
    }

    /// <summary>
    /// The outcome of the boundary check. <paramref name="Blocked"/> is fail-closed: when it is set, no signature
    /// record is written at all.
    /// </summary>
    public sealed record BoundaryDecision(
        bool Blocked,
        string? BlockReasonCode,
        string? BlockMessage,
        RepositoryType? RepositoryTypeAtSigning,
        Guid? RepositoryAssessmentId,
        string BoundaryStatement);

    public async Task<BoundaryDecision> EvaluateAsync(
        Guid? repositoryAssessmentId, DocumentSignaturePolicy? policy, CancellationToken ct)
    {
        DocumentRepositoryAssessment? assessment = null;
        if (repositoryAssessmentId is { } id)
        {
            assessment = await _assessments.GetByIdAsync(id, ct);
        }

        // A referenced assessment that cannot be read is treated as no assessment — never as an approved one.
        if (assessment is null)
        {
            return NoAssessment(policy);
        }

        var repositoryType = assessment.RepositoryType;
        var approved = assessment.AssessmentStatus == RepositoryAssessmentStatus.Approved;

        // SOP §11: an unapproved repository cannot host a regulated signature. Blocked, not warned.
        if (repositoryType == RepositoryType.UnapprovedRepository)
        {
            return new BoundaryDecision(true,
                ElectronicSignatureReasonCodes.RepositoryNotApproved,
                "The linked repository is categorised as unapproved. A regulated signature cannot be recorded " +
                "against an unapproved repository; complete a repository assessment (MOD-0029-FU16) first.",
                repositoryType, assessment.Id, Statement(repositoryType, approved, policy));
        }

        // The policy may narrow which categories are acceptable; it can never widen the floor above.
        if (policy is { AllowedRepositoryTypes.Count: > 0 } && !policy.AllowedRepositoryTypes.Contains(repositoryType))
        {
            return new BoundaryDecision(true,
                ElectronicSignatureReasonCodes.RepositoryTypeNotAllowed,
                $"The signature policy '{policy.PolicyKey}' does not permit signatures hosted in a " +
                $"{repositoryType} repository.",
                repositoryType, assessment.Id, Statement(repositoryType, approved, policy));
        }

        if (repositoryType == RepositoryType.ApprovedInterimRepository
            && policy is { AllowInterimRepositorySignature: false })
        {
            return new BoundaryDecision(true,
                ElectronicSignatureReasonCodes.InterimRepositoryNotAllowed,
                $"The signature policy '{policy.PolicyKey}' does not permit signatures in an approved interim " +
                "repository.",
                repositoryType, assessment.Id, Statement(repositoryType, approved, policy));
        }

        return new BoundaryDecision(false, null, null, repositoryType, assessment.Id,
            Statement(repositoryType, approved, policy));
    }

    private static BoundaryDecision NoAssessment(DocumentSignaturePolicy? policy)
    {
        var statement = Append(
            "Repository boundary UNKNOWN: no repository assessment is linked to this signature. No validated DMS " +
            "claim and no regulated electronic signature claim is permitted for this record. " +
            ElectronicSignatureWire.BoundaryStatement, policy);

        // Only a policy that explicitly demands an assessment blocks; otherwise the signature is recorded with the
        // limitation stated on its face. Blocking every unassessed signature would push users to record nothing,
        // which loses the attribution evidence entirely.
        return policy is { RequiresRepositoryAssessment: true }
            ? new BoundaryDecision(true,
                ElectronicSignatureReasonCodes.RepositoryAssessmentRequired,
                $"The signature policy '{policy.PolicyKey}' requires an approved repository assessment " +
                "(MOD-0029-FU16) before a signature can be recorded.",
                null, null, statement)
            : new BoundaryDecision(false, null, null, null, null, statement);
    }

    /// <summary>
    /// Builds the statement persisted onto the signature record. Every branch is deliberately conservative — the
    /// statement describes what was ASSESSED, never what was VALIDATED by FU23, because FU23 validates nothing.
    /// </summary>
    public static string Statement(RepositoryType repositoryType, bool approved, DocumentSignaturePolicy? policy)
    {
        var core = repositoryType switch
        {
            RepositoryType.ValidatedDms =>
                "The repository is ASSESSED as a validated DMS (MOD-0029-FU16). MOD-0029-FU23 nevertheless performs " +
                "NO provider validation and NO certificate validation: this signature is recorded, not validated, " +
                "and carries no qualified electronic signature or compliance claim.",

            RepositoryType.ApprovedInterimRepository =>
                "The repository is an APPROVED INTERIM REPOSITORY and shall NOT be represented or used as a " +
                "validated DMS. Its native approval/sharing capability is NOT a regulated electronic signature. " +
                "This record is an internal attestation for traceability only.",

            RepositoryType.SeparateApprovalMechanism =>
                "Approval for this repository is performed through a SEPARATE assessed mechanism. This signature " +
                "record REFERENCES that mechanism's evidence; it does not itself constitute the regulated approval " +
                "and makes no validated DMS claim.",

            RepositoryType.UnapprovedRepository =>
                "The repository is UNAPPROVED. A regulated electronic signature cannot be based on it; any record " +
                "referencing it carries no approval, validation or compliance claim whatsoever.",

            _ =>
                "Repository category unrecognised. No validated DMS claim and no regulated electronic signature " +
                "claim is permitted for this record."
        };

        if (!approved && repositoryType != RepositoryType.UnapprovedRepository)
        {
            core += " NOTE: the linked repository assessment is not in an Approved state, which further limits " +
                    "what this record may be relied upon for.";
        }

        return Append($"{core} {ElectronicSignatureWire.BoundaryStatement}", policy);
    }

    /// <summary>Tenant wording is APPENDED to the generated statement. It can add context; it can never replace it.</summary>
    private static string Append(string statement, DocumentSignaturePolicy? policy) =>
        string.IsNullOrWhiteSpace(policy?.InterimRepositoryBoundaryStatement)
            ? statement
            : $"{statement} Tenant policy note: {policy!.InterimRepositoryBoundaryStatement!.Trim()}";
}
