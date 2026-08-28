using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Services;

/// <summary>
/// MOD-0029-FU16 — pure repository/DMS boundary evaluator (GMG-QMS-SOP-0001 §11). Given an assessment's CONTENT it
/// computes the findings (missing mandatory elements, expired interim period, native e-signature misuse risk), whether
/// the repository can support the release gate, whether it can support REGULATED electronic approval (only a validated
/// DMS can), and a plain boundary statement. It never claims validation — it only classifies. No I/O.
/// </summary>
public sealed class DocumentRepositoryAssessmentEvaluator
{
    public sealed record FindingSpec(RepositoryFindingType Type, RepositoryFindingSeverity Severity, string Description)
    {
        public string Key => Type.ToString();
    }

    public sealed record Result(
        IReadOnlyList<FindingSpec> Findings,
        bool CanSupportReleaseGate,
        bool CanSupportRegulatedESignature,
        string BoundaryStatement);

    public Result Evaluate(DocumentRepositoryAssessment a, DateTimeOffset now)
    {
        var findings = new List<FindingSpec>();

        // Common minimum content (SOP §11.1) for any assessed repository.
        Require(findings, a.RepositoryOwnerUserId is not null || !string.IsNullOrWhiteSpace(a.RepositoryOwnerRole),
            RepositoryFindingType.MissingOwner, RepositoryFindingSeverity.Critical, "A named repository owner (IT/CSV + QA) is required.");
        Require(findings, !string.IsNullOrWhiteSpace(a.ExactLocation),
            RepositoryFindingType.MissingExactLocation, RepositoryFindingSeverity.Critical, "The exact location (name, path, boundary) is required.");
        Require(findings, !string.IsNullOrWhiteSpace(a.AccessModelDescription),
            RepositoryFindingType.MissingAccessModel, RepositoryFindingSeverity.Critical, "An access model (create/publish/read/archive/administer) is required.");
        Require(findings, !string.IsNullOrWhiteSpace(a.BackupMethodDescription),
            RepositoryFindingType.MissingBackup, RepositoryFindingSeverity.Critical, "A backup method is required.");
        Require(findings, !string.IsNullOrWhiteSpace(a.ApprovalMechanismDescription),
            RepositoryFindingType.MissingApprovalMechanism, RepositoryFindingSeverity.Critical, "An authorised approval mechanism / release route is required.");
        Require(findings, !string.IsNullOrWhiteSpace(a.EffectiveCopyControlDescription),
            RepositoryFindingType.MissingEffectiveCopyControl, RepositoryFindingSeverity.Critical, "Effective-copy control (locked, read-only effective copies) is required.");

        bool canRegulatedESignature;
        string boundary;

        switch (a.RepositoryType)
        {
            case RepositoryType.ValidatedDms:
                Require(findings, !string.IsNullOrWhiteSpace(a.AuditTrailDescription),
                    RepositoryFindingType.MissingAuditTrail, RepositoryFindingSeverity.Critical, "A non-disableable audit trail is required for a validated DMS.");
                Require(findings, !string.IsNullOrWhiteSpace(a.ChangeControlDescription),
                    RepositoryFindingType.MissingChangeControl, RepositoryFindingSeverity.Critical, "Change control is required for a validated DMS.");
                Require(findings, !string.IsNullOrWhiteSpace(a.RestoreTestFrequency),
                    RepositoryFindingType.MissingRestoreTest, RepositoryFindingSeverity.Critical, "A restore-test frequency (at least annual) is required.");
                Require(findings, !string.IsNullOrWhiteSpace(a.ValidationEvidenceReference),
                    RepositoryFindingType.Other, RepositoryFindingSeverity.Critical, "Validation evidence is required for a system used for regulated electronic approval.");
                canRegulatedESignature = findings.All(f => f.Severity != RepositoryFindingSeverity.Critical);
                boundary = "Validated DMS — supports controlled workflow, electronic approval, effective release and archival with a permanently-linked audit trail.";
                break;

            case RepositoryType.ApprovedInterimRepository:
                Require(findings, !string.IsNullOrWhiteSpace(a.AccessReviewFrequency),
                    RepositoryFindingType.MissingAccessReview, RepositoryFindingSeverity.Critical, "An access-review frequency is required and must be performed.");
                Require(findings, !string.IsNullOrWhiteSpace(a.RestoreTestFrequency),
                    RepositoryFindingType.MissingRestoreTest, RepositoryFindingSeverity.Major, "A restore-test plan (at least annual) is required.");
                Require(findings, a.MaxInterimPeriodDays is > 0 || a.InterimCheckpointDueDate is not null,
                    RepositoryFindingType.Other, RepositoryFindingSeverity.Critical, "A maximum interim period and adequacy checkpoint (GQD-approved) are required.");
                if (a.InterimCheckpointDueDate is { } checkpoint && now > checkpoint)
                {
                    findings.Add(new FindingSpec(RepositoryFindingType.InterimPeriodExpired, RepositoryFindingSeverity.Critical,
                        "The interim adequacy checkpoint is overdue; continued interim use requires re-approval."));
                }

                // SOP §11: an interim repository shall NOT be relied upon for regulated e-signature/approval.
                findings.Add(new FindingSpec(RepositoryFindingType.NativeESignatureMisuseRisk, RepositoryFindingSeverity.Warning,
                    "Native e-signature/sharing features of an interim repository shall not be relied upon for regulated approval; use a separate approval mechanism."));
                canRegulatedESignature = false;
                boundary = "Approved interim repository — NOT a validated DMS. Controlled storage/distribution only; regulated approval requires a separate, authorised mechanism reconciled to the record.";
                break;

            case RepositoryType.SeparateApprovalMechanism:
                boundary = "Separate approval mechanism — wet signature or an independently qualified electronic signature, reconciled to the repository record.";
                canRegulatedESignature = false; // e-signature itself is out of scope in this FU.
                break;

            case RepositoryType.UnapprovedRepository:
            default:
                boundary = "Unapproved repository — shall not be used as a controlled-document store or release route.";
                canRegulatedESignature = false;
                break;
        }

        if (a.MigrationReconciliationRequired && string.IsNullOrWhiteSpace(a.MigrationReconciliationReference))
        {
            findings.Add(new FindingSpec(RepositoryFindingType.MigrationReconciliationMissing, RepositoryFindingSeverity.Major,
                "Migration reconciliation is required but no reconciliation reference is recorded."));
        }

        var canSupportReleaseGate = a.RepositoryType != RepositoryType.UnapprovedRepository
            && findings.All(f => f.Severity != RepositoryFindingSeverity.Critical);

        return new Result(findings, canSupportReleaseGate, canRegulatedESignature, boundary);
    }

    private static void Require(List<FindingSpec> findings, bool ok, RepositoryFindingType type, RepositoryFindingSeverity severity, string description)
    {
        if (!ok)
        {
            findings.Add(new FindingSpec(type, severity, description));
        }
    }
}
