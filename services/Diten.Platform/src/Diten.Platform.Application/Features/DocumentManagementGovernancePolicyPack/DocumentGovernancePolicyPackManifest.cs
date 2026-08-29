using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

/// <summary>
/// MOD-0029-FU31 — the SINGLE SOURCE OF TRUTH for the default MOD-0029 governance policy pack (GMG-QMS-SOP-0001).
/// Static, code-based, deterministic: the same manifest every tenant receives. The seeder materialises these into
/// tenant-scoped <c>Active</c> policies, creating only what is missing (idempotent). Editing a default here changes
/// what a FUTURE apply creates — it never rewrites an already-seeded tenant's policies.
/// </summary>
public static class DocumentGovernancePolicyPackManifest
{
    public const string PackKey = "MOD0029_DOCUMENT_CONTROL_DEFAULT_GOVERNANCE_PACK";
    public const string PackName = "MOD-0029 Document Control — Default Governance Policy Pack";
    public const string PackVersion = "1.0.0";
    public const string AppliesToModule = "MOD-0029";
    public const string SopReference = "GMG-QMS-SOP-0001";

    // SOP §11.2 — appended to (never replaces) the generated boundary statement on interim-repository signatures.
    public const string InterimBoundaryStatement =
        "Approved interim repository cannot be presented as a validated DMS; the signature record is an internal " +
        "attestation / separate evidence reference, not a qualified regulated electronic signature.";

    private const string Sop22 = "GMG-QMS-SOP-0001 §22";

    private static readonly IReadOnlyList<RepositoryType> DefaultAllowedRepositories =
        [RepositoryType.ValidatedDms, RepositoryType.ApprovedInterimRepository, RepositoryType.SeparateApprovalMechanism];

    public static GovernancePolicyPackManifestModel Get() => new(
        PackKey, PackName, PackVersion, AppliesToModule, SopReference,
        RetentionPolicies(), GDocPCorrectionPolicies(), SignaturePolicies());

    // ── B. Default retention policy pack (SOP §22 — longest applicable requirement) ────────────────────
    private static IReadOnlyList<RetentionPolicyDefinition> RetentionPolicies() =>
    [
        new("RETENTION_CONTROLLED_DOCUMENT_10Y_AFTER_RETIREMENT_OR_SUPERSESSION", "Controlled document — retain while effective + 10 years",
            RetentionSubjectType.ControlledDocument, 10, RetentionTrigger.RetirementDate, RetainWhileEffective: true, 10, 10, false, Sop22),
        new("RETENTION_CONTROLLED_DOCUMENT_VERSION_10Y", "Controlled document version — 10 years after supersession",
            RetentionSubjectType.ControlledDocumentVersion, 10, RetentionTrigger.SupersessionDate, false, null, 10, false, Sop22),
        new("RETENTION_MASTER_REGISTER_PERMANENT_PLUS_10Y", "Document master register — permanent record",
            RetentionSubjectType.DocumentMasterRegisterEntry, 10, RetentionTrigger.CreationDate, false, null, null, IsPermanentRetention: true, Sop22),
        new("RETENTION_IDENTIFIER_LEDGER_PERMANENT", "Identifier allocation ledger — permanent (UIDs/codes never reused)",
            RetentionSubjectType.IdentifierAllocationLedger, 0, RetentionTrigger.CreationDate, false, null, null, IsPermanentRetention: true, Sop22),
        new("RETENTION_APPROVAL_EVIDENCE_10Y", "Approval evidence — 10 years after completion",
            RetentionSubjectType.ApprovalEvidence, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_RELEASE_GATE_EVIDENCE_10Y", "Release gate evidence — 10 years after completion",
            RetentionSubjectType.ReleaseGateEvidence, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_TRAINING_EVIDENCE_10Y", "Training evidence — 10 years after completion",
            RetentionSubjectType.TrainingAssignment, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_PERIODIC_REVIEW_10Y", "Periodic review — 10 years after completion",
            RetentionSubjectType.PeriodicReview, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_SUSPENSION_RETIREMENT_CASE_10Y", "Suspension / retirement case — 10 years after closure",
            RetentionSubjectType.SuspensionCase, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22),
        new("RETENTION_REPOSITORY_ASSESSMENT_10Y", "Repository assessment — 10 years after completion",
            RetentionSubjectType.RepositoryAssessment, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_CONTROLLED_COPY_10Y", "Controlled copy — 10 years after closure",
            RetentionSubjectType.ControlledCopy, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22),
        new("RETENTION_OBSOLETE_COPY_FINDING_10Y", "Obsolete copy finding — 10 years after closure",
            RetentionSubjectType.ObsoleteCopyFinding, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22),
        new("RETENTION_EXTERNAL_IMPACT_ASSESSMENT_10Y", "External document impact assessment — 10 years after completion",
            RetentionSubjectType.ExternalDocumentImpactAssessment, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        // RetentionSubjectType has no LegalHold / DispositionRequest member (FU15 subject vocabulary); modelled as
        // Other + a RetentionClass narrowing so the row is unambiguous. See remaining gaps.
        new("RETENTION_LEGAL_HOLD_PERMANENT_WHILE_ACTIVE", "Legal hold — permanent while active, release evidence retained",
            RetentionSubjectType.Other, 0, RetentionTrigger.CreationDate, false, null, null, IsPermanentRetention: true, Sop22, RetentionClass: "LEGAL_HOLD"),
        new("RETENTION_DISPOSITION_REQUEST_10Y", "Disposition request — 10 years after closure",
            RetentionSubjectType.Other, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22, RetentionClass: "DISPOSITION_REQUEST"),
        new("RETENTION_GDOCP_CORRECTION_10Y", "GDocP correction record — 10 years after completion",
            RetentionSubjectType.GDocPCorrectionRecord, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_QUALITY_EVENT_DEVIATION_CAPA_10Y", "Quality event / deviation / CAPA — 10 years after closure",
            RetentionSubjectType.DocumentQualityEvent, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22),
        new("RETENTION_SIGNATURE_RECORD_10Y", "Signature record — 10 years after signing",
            RetentionSubjectType.DocumentSignatureRecord, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
        new("RETENTION_DOWNTIME_TEMP_ISSUE_10Y", "Downtime temporary controlled issue — 10 years after closure",
            RetentionSubjectType.TemporaryControlledIssue, 10, RetentionTrigger.ClosureDate, false, null, null, false, Sop22),
        new("RETENTION_VARIANT_LOCALIZATION_EVIDENCE_10Y", "Variant / translation evidence — 10 years after completion",
            RetentionSubjectType.TemplateVariantReviewEvidence, 10, RetentionTrigger.CompletionDate, false, null, null, false, Sop22),
    ];

    // ── C. Default GDocP correction policy pack (SOP §21 — most restrictive wins) ──────────────────────
    // reason + evidence + review required across the board; sensitivity flags raise the risk classification.
    private static IReadOnlyList<GDocPPolicyDefinition> GDocPCorrectionPolicies() =>
    [
        new("GDOCP_REGULATED_TIMESTAMP_CORRECTION", "Regulated timestamp correction (backdating sensitive)",
            GDocPSubjectType.Other, "*Date", true, true, true, RequiresDeviationReferenceForHighRisk: true,
            AllowCorrectionAfterApproval: true, AllowCorrectionAfterEffective: true, IsBackdatingSensitive: true, false, false),
        new("GDOCP_STATUS_CORRECTION", "Lifecycle / approval status correction (status sensitive)",
            GDocPSubjectType.Other, "*Status", true, true, true, true, true, true, false, IsStatusSensitive: true, false),
        new("GDOCP_EVIDENCE_REFERENCE_CORRECTION", "Evidence reference correction (evidence sensitive)",
            GDocPSubjectType.Other, "*EvidenceReference", true, true, true, true, true, true, false, false, IsEvidenceSensitive: true),
        new("GDOCP_RECONSTRUCTION_CORRECTION", "Reconstruction of a lost value (high risk)",
            GDocPSubjectType.Other, "*", true, true, true, RequiresDeviationReferenceForHighRisk: true, true, true, false, false, false),
        new("GDOCP_DATA_INTEGRITY_CORRECTION", "Data-integrity correction (high risk)",
            GDocPSubjectType.Other, "*", true, true, true, RequiresDeviationReferenceForHighRisk: true, true, true, false, false, false),
        new("GDOCP_APPROVED_RECORD_METADATA_CORRECTION", "Metadata correction after approval",
            GDocPSubjectType.Other, "*", true, true, true, true, AllowCorrectionAfterApproval: true, AllowCorrectionAfterEffective: true, false, false, false),
        new("GDOCP_EFFECTIVE_RECORD_METADATA_CORRECTION", "Metadata correction after effective",
            GDocPSubjectType.Other, "*", true, true, true, true, AllowCorrectionAfterApproval: true, AllowCorrectionAfterEffective: true, false, false, false),
        new("GDOCP_LEGAL_HOLD_CORRECTION", "Legal hold record correction (highly restricted)",
            GDocPSubjectType.LegalHold, "*", true, true, true, true, AllowCorrectionAfterApproval: false, AllowCorrectionAfterEffective: false, false, true, true),
        new("GDOCP_SIGNATURE_RECORD_CORRECTION", "Signature record correction (highly restricted)",
            GDocPSubjectType.Other, "*", true, true, true, true, AllowCorrectionAfterApproval: false, AllowCorrectionAfterEffective: false, false, false, IsEvidenceSensitive: true,
            Notes: "Immutable signature fields (SignedAt/CreatedAt/Id/TenantId) should be non-correctable; the policy expresses the most-restrictive available control. See gaps."),
        new("GDOCP_RETENTION_DISPOSITION_CORRECTION", "Retention / disposition record correction (high risk)",
            GDocPSubjectType.DispositionRequest, "*", true, true, true, RequiresDeviationReferenceForHighRisk: true, true, true, false, false, false),
    ];

    // ── D. Default signature policy pack (SOP §11.2 — safe defaults, no compliance claim) ──────────────
    // meaning statement + object fingerprint + manifestation + repository assessment required; second factor NEVER
    // required by default (no platform 2FA context — a policy demanding it would block signing). Unapproved
    // repository is never in the allow-list; the boundary evaluator floors it anyway.
    private static IReadOnlyList<SignaturePolicyDefinition> SignaturePolicies() =>
    [
        Sign("SIGN_APPROVAL_EVIDENCE_QA_GQD", "QA/GQD approval signature", SignableSubjectType.ApprovalEvidence, SignatureMeaning.QAGQDApproval),
        Sign("SIGN_RELEASE_GATE_VERIFICATION", "Release gate verification signature", SignableSubjectType.ReleaseGateEvidence, SignatureMeaning.GateVerification),
        Sign("SIGN_TRAINING_ACKNOWLEDGEMENT", "Training acknowledgement signature", SignableSubjectType.TrainingAssignment, SignatureMeaning.TrainingAcknowledgement),
        Sign("SIGN_TRAINING_EFFECTIVENESS", "Training effectiveness signature", SignableSubjectType.TrainingEffectiveness, SignatureMeaning.EffectivenessConfirmation),
        Sign("SIGN_GDOCP_CORRECTION_REVIEW", "GDocP correction review signature", SignableSubjectType.GDocPCorrectionReview, SignatureMeaning.CorrectionReview),
        Sign("SIGN_DEVIATION_CLOSURE", "Deviation closure signature", SignableSubjectType.Deviation, SignatureMeaning.DeviationClosureApproval),
        Sign("SIGN_CAPA_COMPLETION", "CAPA completion signature", SignableSubjectType.CAPAAction, SignatureMeaning.CAPACompletionApproval),
        Sign("SIGN_CAPA_EFFECTIVENESS", "CAPA effectiveness signature", SignableSubjectType.CAPAAction, SignatureMeaning.CAPAEffectivenessApproval),
        Sign("SIGN_REPOSITORY_ASSESSMENT_APPROVAL", "Repository assessment approval signature", SignableSubjectType.RepositoryAssessment, SignatureMeaning.RepositoryAssessmentApproval),
        Sign("SIGN_LEGAL_HOLD_RELEASE", "Legal hold release signature", SignableSubjectType.LegalHold, SignatureMeaning.LegalHoldReleaseApproval),
        Sign("SIGN_DISPOSITION_APPROVAL", "Disposition approval signature", SignableSubjectType.DispositionRequest, SignatureMeaning.DispositionApproval),
        Sign("SIGN_TEMPORARY_CONTROLLED_ISSUE_APPROVAL", "Temporary controlled issue approval signature", SignableSubjectType.TemporaryControlledIssue, SignatureMeaning.ReleaseAuthorization),
    ];

    private static SignaturePolicyDefinition Sign(string key, string name, SignableSubjectType subject, SignatureMeaning meaning) =>
        new(key, name, subject, meaning,
            RequiresReAuthentication: false,
            RequiresSecondFactor: false,
            RequiresMeaningStatement: true,
            RequiresRepositoryAssessment: true,
            RequiresObjectFingerprint: true,
            RequiresManifestation: true,
            DefaultAllowedRepositories,
            AllowInterimRepositorySignature: true,
            InterimBoundaryStatement);
}
