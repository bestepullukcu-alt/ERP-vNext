using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU23 — electronic signature foundation tests (GMG-QMS-SOP-0001 §11.2). Tenant-aware in-memory fakes
/// exercise the policy selection, the request nomination rules, the sign path controls, the fingerprint binding and
/// the boundary evaluator.
///
/// The assertions that matter most are the ones about what this feature REFUSES to do: it will not backdate a
/// signature, will not fabricate a second factor, will not claim provider validation, will not let an interim
/// repository read as a validated DMS, and will not report a signature as valid once its object has changed.
/// </summary>
public sealed class DocumentElectronicSignatureTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid SignerUserId = Guid.Parse("cccccccc-1111-2222-3333-444444444423");
    private static readonly Guid OtherUserId = Guid.Parse("dddddddd-1111-2222-3333-444444444423");
    private static readonly Guid RegisterEntryId = Guid.Parse("50000000-0000-0000-0000-000000000023");
    private const string Corr = "fu23-corr-1";
    private const string Statement = "I approve this record as reviewer.";

    // ── policy ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_signature_policy()
    {
        var f = Fixture();

        var r = await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Draft", r.Data!.PolicyStatus);
        Assert.Contains("no 21 CFR Part 11", r.Data.BoundaryStatement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_signature_policy_requires_key_and_name()
    {
        var f = Fixture();

        var noKey = await f.Policies.CreateAsync(Policy() with { PolicyKey = " " }, Corr, CancellationToken.None);
        var noName = await f.Policies.CreateAsync(Policy() with { PolicyName = "" }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.PolicyKeyRequired, noKey.ReasonCode);
        Assert.Equal(ElectronicSignatureReasonCodes.PolicyNameRequired, noName.ReasonCode);
        Assert.Empty(f.PolicyRepo.Items);
    }

    [Fact]
    public async Task Activate_signature_policy()
    {
        var f = Fixture();
        var created = await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        var activated = await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);
        Assert.Equal("Active", activated.Data!.PolicyStatus);

        var retired = await f.Policies.RetireAsync(created.Data.Id, Corr, CancellationToken.None);
        Assert.Equal("Retired", retired.Data!.PolicyStatus);

        // A retired policy is not resurrected — it would silently start governing signatures again.
        var reactivated = await f.Policies.ActivateAsync(created.Data.Id, Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.PolicyInvalidState, reactivated.ReasonCode);
    }

    [Fact]
    public async Task Policy_key_is_unique_per_tenant()
    {
        var f = Fixture();
        await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        var duplicate = await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.PolicyKeyDuplicate, duplicate.ReasonCode);
        Assert.Single(f.PolicyRepo.Items);
    }

    [Fact]
    public async Task Most_restrictive_active_policy_wins()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, "lenient", requiresRepositoryAssessment: false, allowInterim: true);
        await ActivePolicyAsync(f, "strict", requiresRepositoryAssessment: true, allowInterim: false);

        var chosen = await f.Policies.ResolveApplicableAsync(
            SignableSubjectType.TrainingAssignment, SignatureMeaning.TrainingAcknowledgement, CancellationToken.None);

        Assert.Equal("strict", chosen!.PolicyKey);
    }

    // ── request ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_signature_request()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Requests.CreateAsync(Request(assignment.Id), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Pending", r.Data!.RequestStatus);
        Assert.StartsWith("SRQ-", r.Data.SignatureRequestNumber);
    }

    [Fact]
    public async Task Signature_request_requires_a_signer_user_or_role()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Requests.CreateAsync(
            Request(assignment.Id) with { RequestedSignerUserId = null, RequestedSignerRole = null },
            Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.SignerRequired, r.ReasonCode);
        Assert.Empty(f.RequestRepo.Items);
    }

    [Fact]
    public async Task Signature_request_due_date_cannot_be_in_the_past()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Requests.CreateAsync(
            Request(assignment.Id) with { DueDate = DateTimeOffset.UtcNow.AddDays(-1) }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.DueDateInPast, r.ReasonCode);
    }

    [Fact]
    public async Task Cancel_request_requires_a_reason()
    {
        var f = Fixture();
        var request = await CreateRequestAsync(f);

        var noReason = await f.Requests.CancelAsync(
            request.Id, new CancelSignatureRequestInput(" "), Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.ReasonRequired, noReason.ReasonCode);

        var ok = await f.Requests.CancelAsync(
            request.Id, new CancelSignatureRequestInput("Superseded by SRQ-2"), Corr, CancellationToken.None);
        Assert.Equal("Cancelled", ok.Data!.RequestStatus);
    }

    [Fact]
    public async Task Reject_request_requires_reason_and_evidence()
    {
        var f = Fixture();
        var request = await CreateRequestAsync(f);

        var noEvidence = await f.Requests.RejectAsync(request.Id,
            new RejectSignatureRequestInput("Not my scope", " ", SignerUserId), Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.RejectionEvidenceRequired, noEvidence.ReasonCode);

        var ok = await f.Requests.RejectAsync(request.Id,
            new RejectSignatureRequestInput("Not my scope", "REJ-1", SignerUserId), Corr, CancellationToken.None);
        Assert.Equal("Rejected", ok.Data!.RequestStatus);
    }

    [Fact]
    public async Task Request_cannot_be_cancelled_or_rejected_after_it_is_signed()
    {
        var f = Fixture();
        var request = await CreateRequestAsync(f);

        var signed = await f.Signatures.SignAsync(
            Sign(request.SubjectId) with { SignatureRequestId = request.Id }, Corr, CancellationToken.None);
        Assert.True(signed.IsSuccessful);

        var cancel = await f.Requests.CancelAsync(
            request.Id, new CancelSignatureRequestInput("changed my mind"), Corr, CancellationToken.None);
        var reject = await f.Requests.RejectAsync(request.Id,
            new RejectSignatureRequestInput("changed my mind", "REJ-1", SignerUserId), Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.RequestAlreadySigned, cancel.ReasonCode);
        Assert.Equal(ElectronicSignatureReasonCodes.RequestAlreadySigned, reject.ReasonCode);
    }

    [Fact]
    public async Task Request_signer_user_must_match()
    {
        var f = Fixture();
        // The request nominates somebody else; the signing user is SignerUserId.
        var request = await CreateRequestAsync(f, signerUserId: OtherUserId);

        var r = await f.Signatures.SignAsync(
            Sign(request.SubjectId) with { SignatureRequestId = request.Id }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.SignerNotNominated, r.ReasonCode);
        Assert.Empty(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Request_signer_role_must_match()
    {
        var f = Fixture();
        var request = await CreateRequestAsync(f, signerUserId: null, signerRole: "QA");

        var wrongRole = await f.Signatures.SignAsync(
            Sign(request.SubjectId) with { SignatureRequestId = request.Id, SignerRole = "Production" },
            Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.SignerNotNominated, wrongRole.ReasonCode);

        var rightRole = await f.Signatures.SignAsync(
            Sign(request.SubjectId) with { SignatureRequestId = request.Id, SignerRole = "qa" },
            Corr, CancellationToken.None);
        Assert.True(rightRole.IsSuccessful);
        Assert.Equal("Signed", f.RequestRepo.Items.Single(x => x.Id == request.Id).RequestStatus.ToString());
    }

    // ── sign ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sign_requires_a_meaning_statement()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Signatures.SignAsync(
            Sign(assignment.Id) with { MeaningStatement = "  " }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.MeaningStatementRequired, r.ReasonCode);
        Assert.Empty(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Sign_uses_a_server_side_signed_at_timestamp()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var before = DateTimeOffset.UtcNow;

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        Assert.True(r.IsSuccessful);
        Assert.InRange(r.Data!.SignedAt, before, after);
        Assert.StartsWith("SIG-", r.Data.SignatureNumber);
    }

    [Fact]
    public async Task Sign_generates_an_object_fingerprint_and_snapshot()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(r.Data!.ObjectFingerprint));
        Assert.Equal("CanonicalJsonSha256", r.Data.FingerprintAlgorithm);
        Assert.Contains("status=Assigned", r.Data.ObjectSnapshotSummary);
        var fingerprint = Assert.Single(f.FingerprintRepo.Items);
        Assert.Equal(r.Data.ObjectFingerprint, fingerprint.FingerprintValue);
    }

    [Fact]
    public async Task Duplicate_signature_for_same_subject_meaning_and_fingerprint_returns_the_existing_record()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var first = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);
        var second = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Wet_signature_requires_an_evidence_reference()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var blocked = await f.Signatures.SignAsync(Sign(assignment.Id) with
        {
            SignatureMethod = nameof(SignatureMethod.WetSignatureEvidence),
            SignatureEvidenceReference = null
        }, Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.WetSignatureEvidenceRequired, blocked.ReasonCode);

        var ok = await f.Signatures.SignAsync(Sign(assignment.Id) with
        {
            SignatureMethod = nameof(SignatureMethod.WetSignatureEvidence),
            SignatureEvidenceReference = "SCAN-2026-07-21-001"
        }, Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Contains("not an electronic signature", ok.Data!.ValidationDetails!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Internal_attestation_states_it_is_not_a_qualified_signature()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.Equal("InternalAttestation", r.Data!.SignatureMethod);
        Assert.Contains("NOT a qualified electronic signature", r.Data.ValidationDetails!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("NotValidated", r.Data.ValidationResult);
    }

    [Fact]
    public async Task External_provider_method_requires_a_reference_and_performs_no_provider_call()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var blocked = await f.Signatures.SignAsync(Sign(assignment.Id) with
        {
            SignatureMethod = nameof(SignatureMethod.ExternalProviderReference),
            ExternalProviderReference = null
        }, Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.ExternalProviderReferenceRequired, blocked.ReasonCode);

        var ok = await f.Signatures.SignAsync(Sign(assignment.Id) with
        {
            SignatureMethod = nameof(SignatureMethod.ExternalProviderReference),
            ExternalProviderReference = "provider:abc-123"
        }, Corr, CancellationToken.None);

        Assert.Equal("provider:abc-123", ok.Data!.ExternalProviderReference);
        Assert.Equal("NotValidated", ok.Data.ValidationResult);
        Assert.Contains("No provider API was called", ok.Data.ValidationDetails!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Qualified_signature_reference_never_claims_it_was_validated()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id) with
        {
            SignatureMethod = nameof(SignatureMethod.QualifiedElectronicSignatureReference),
            ExternalProviderReference = "qes:eidas:999"
        }, Corr, CancellationToken.None);

        Assert.Equal("NotValidated", r.Data!.ValidationResult);
        Assert.Contains("no certificate chain", r.Data.ValidationDetails!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ValidatedByProvider", r.Data.ValidationResult);
    }

    [Fact]
    public async Task Second_factor_policy_blocks_rather_than_fabricating_a_second_factor()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        await ActivePolicyAsync(f, "2fa-required", requiresSecondFactor: true);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.SecondFactorNotAvailable, r.ReasonCode);
        Assert.Equal(501, r.StatusCode);
        Assert.Empty(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Re_authentication_policy_requires_an_authentication_context_reference()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        await ActivePolicyAsync(f, "reauth-required", requiresReAuthentication: true);

        var blocked = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.ReAuthenticationRequired, blocked.ReasonCode);

        var ok = await f.Signatures.SignAsync(
            Sign(assignment.Id) with { AuthenticationContextReference = "authctx:session-991" },
            Corr, CancellationToken.None);

        Assert.True(ok.IsSuccessful);
        Assert.True(ok.Data!.ReAuthenticationPerformed);
        // Never asserted, in any code path.
        Assert.False(ok.Data.SecondFactorPerformed);
    }

    [Fact]
    public async Task Signing_does_not_mutate_the_signed_subject()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var statusBefore = assignment.Status;
        var evidenceBefore = assignment.CompletionEvidenceReference;

        await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        var after = f.TrainingRepo.Items.Single(x => x.Id == assignment.Id);
        Assert.Equal(statusBefore, after.Status);
        Assert.Equal(evidenceBefore, after.CompletionEvidenceReference);
    }

    // ── repository boundary ───────────────────────────────────────────────────

    [Fact]
    public async Task Interim_repository_signature_cannot_claim_validated_DMS()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var assessment = SeedAssessment(f, RepositoryType.ApprovedInterimRepository);

        var r = await f.Signatures.SignAsync(
            Sign(assignment.Id) with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("ApprovedInterimRepository", r.Data!.RepositoryTypeAtSigning);
        Assert.Contains("shall NOT be represented or used as a validated DMS",
            r.Data.RepositoryBoundaryStatement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT a regulated electronic signature",
            r.Data.RepositoryBoundaryStatement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unapproved_repository_blocks_a_regulated_signature()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var assessment = SeedAssessment(f, RepositoryType.UnapprovedRepository);

        var r = await f.Signatures.SignAsync(
            Sign(assignment.Id) with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.RepositoryNotApproved, r.ReasonCode);
        Assert.Empty(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Validated_DMS_still_claims_no_provider_validation()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var assessment = SeedAssessment(f, RepositoryType.ValidatedDms);

        var r = await f.Signatures.SignAsync(
            Sign(assignment.Id) with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.Equal("ValidatedDms", r.Data!.RepositoryTypeAtSigning);
        Assert.Equal("NotValidated", r.Data.ValidationResult);
        Assert.Contains("NO provider validation", r.Data.RepositoryBoundaryStatement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_repository_assessment_states_the_boundary_is_unknown()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.Null(r.Data!.RepositoryTypeAtSigning);
        Assert.Contains("boundary UNKNOWN", r.Data.RepositoryBoundaryStatement, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Policy_requiring_a_repository_assessment_blocks_when_none_is_linked()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        await ActivePolicyAsync(f, "assessment-required", requiresRepositoryAssessment: true);

        var r = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.RepositoryAssessmentRequired, r.ReasonCode);
    }

    // ── verification / invalidation ───────────────────────────────────────────

    [Fact]
    public async Task Verify_reports_valid_when_the_fingerprint_matches()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var signed = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        var r = await f.Verification.VerifyAsync(signed.Data!.Id, Corr, CancellationToken.None);

        Assert.True(r.Data!.FingerprintMatches);
        Assert.Equal("FingerprintMatches", r.Data.Outcome);
        Assert.Equal("Valid", r.Data.SignatureStatusAfter);
        Assert.Contains("object integrity only", r.Data.VerificationNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_requires_resign_when_the_signed_object_changed()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var signed = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        // The subject moves on after signing — exactly the case the fingerprint exists to catch.
        assignment.Status = TrainingAssignmentStatus.Completed;
        assignment.CompletionEvidenceReference = "LMS-9001";

        var r = await f.Verification.VerifyAsync(signed.Data!.Id, Corr, CancellationToken.None);

        Assert.False(r.Data!.FingerprintMatches);
        Assert.Equal("ObjectChanged", r.Data.Outcome);
        Assert.Equal("RequiresResign", r.Data.SignatureStatusAfter);

        // The signature and its original fingerprint survive: nothing is deleted or rewritten.
        var stored = f.SignatureRepo.Items.Single();
        Assert.Equal(signed.Data.ObjectFingerprint, stored.ObjectFingerprint);
        Assert.Equal(2, f.FingerprintRepo.Items.Count);
    }

    [Fact]
    public async Task Verify_fails_closed_when_the_subject_cannot_be_resolved()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var signed = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        f.TrainingRepo.Items.Clear();

        var r = await f.Verification.VerifyAsync(signed.Data!.Id, Corr, CancellationToken.None);

        Assert.Equal("SubjectUnresolvable", r.Data!.Outcome);
        Assert.Equal("RequiresResign", r.Data.SignatureStatusAfter);
        Assert.False(r.Data.FingerprintMatches);
    }

    [Fact]
    public async Task Manual_invalidation_requires_a_reason_and_deletes_nothing()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var signed = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);

        var noReason = await f.Verification.InvalidateAsync(
            signed.Data!.Id, new InvalidateSignatureInput(" "), Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.InvalidationReasonRequired, noReason.ReasonCode);

        var ok = await f.Verification.InvalidateAsync(signed.Data.Id,
            new InvalidateSignatureInput("Signed against the wrong revision."), Corr, CancellationToken.None);

        Assert.Equal("Invalidated", ok.Data!.SignatureStatus);
        Assert.Equal("Signed against the wrong revision.", ok.Data.InvalidationReason);
        Assert.NotNull(ok.Data.InvalidatedAt);
        Assert.Single(f.SignatureRepo.Items);

        // Re-verifying an invalidated signature must never resurrect it.
        var reverify = await f.Verification.VerifyAsync(signed.Data.Id, Corr, CancellationToken.None);
        Assert.Equal("AlreadyInvalidated", reverify.Data!.Outcome);
        Assert.Equal("Invalidated", reverify.Data.SignatureStatusAfter);
    }

    [Fact]
    public async Task Signature_history_by_subject_returns_every_record_including_invalidated_ones()
    {
        var f = Fixture();
        var assignment = SeedTrainingAssignment(f);
        var first = await f.Signatures.SignAsync(Sign(assignment.Id), Corr, CancellationToken.None);
        await f.Verification.InvalidateAsync(
            first.Data!.Id, new InvalidateSignatureInput("Wrong meaning."), Corr, CancellationToken.None);
        await f.Signatures.SignAsync(
            Sign(assignment.Id) with { SignatureMeaning = nameof(SignatureMeaning.ReviewerApproval) },
            Corr, CancellationToken.None);

        var history = await f.Signatures.GetBySubjectAsync(
            nameof(SignableSubjectType.TrainingAssignment), assignment.Id, Corr, CancellationToken.None);

        Assert.Equal(2, history.Data!.Count);
        Assert.Contains(history.Data, s => s.SignatureStatus == "Invalidated");
    }

    // ── subject resolution / tenancy ──────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_subject_cannot_be_signed_or_requested()
    {
        var f = Fixture();
        // The assignment belongs to another tenant, so it must not resolve for this caller.
        var foreign = SeedTrainingAssignment(f, tenantId: OtherTenantId);

        var request = await f.Requests.CreateAsync(Request(foreign.Id), Corr, CancellationToken.None);
        var signature = await f.Signatures.SignAsync(Sign(foreign.Id), Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.SubjectNotFound, request.ReasonCode);
        Assert.Equal(ElectronicSignatureReasonCodes.SubjectNotFound, signature.ReasonCode);
        Assert.Empty(f.RequestRepo.Items);
        Assert.Empty(f.SignatureRepo.Items);
    }

    [Fact]
    public async Task Unresolvable_subject_types_are_blocked_rather_than_silently_signed()
    {
        var f = Fixture();

        var r = await f.Signatures.SignAsync(Sign(Guid.NewGuid()) with
        {
            SubjectType = nameof(SignableSubjectType.GDocPCorrectionReview)
        }, Corr, CancellationToken.None);

        Assert.Equal(ElectronicSignatureReasonCodes.SubjectNotResolvable, r.ReasonCode);
    }

    [Fact]
    public async Task Approval_evidence_subject_requires_a_register_entry_id()
    {
        var f = Fixture();
        var evidence = SeedApprovalEvidence(f);

        var missing = await f.Signatures.SignAsync(Sign(evidence.Id) with
        {
            SubjectType = nameof(SignableSubjectType.ApprovalEvidence),
            SignatureMeaning = nameof(SignatureMeaning.ReviewerApproval),
            RegisterEntryId = null
        }, Corr, CancellationToken.None);
        Assert.Equal(ElectronicSignatureReasonCodes.RegisterEntryRequiredForSubject, missing.ReasonCode);

        var ok = await f.Signatures.SignAsync(Sign(evidence.Id) with
        {
            SubjectType = nameof(SignableSubjectType.ApprovalEvidence),
            SignatureMeaning = nameof(SignatureMeaning.ReviewerApproval),
            RegisterEntryId = RegisterEntryId
        }, Corr, CancellationToken.None);

        Assert.True(ok.IsSuccessful);
        Assert.Equal("ApprovalEvidence", ok.Data!.SubjectType);
        Assert.Contains("action=Approved", ok.Data.ObjectSnapshotSummary);
    }

    [Fact]
    public async Task Retention_subject_types_were_appended_without_shifting_existing_ordinals()
    {
        // FU06–FU22 ordinals must not move: persisted retention subjects would silently repoint.
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(34, (int)RetentionSubjectType.GDocPCorrectionRecord);
        Assert.Equal(40, (int)RetentionSubjectType.DocumentQualityEventSourceLink);

        // FU23 appends after 40.
        Assert.Equal(41, (int)RetentionSubjectType.DocumentSignaturePolicy);
        Assert.Equal(42, (int)RetentionSubjectType.DocumentSignatureRequest);
        Assert.Equal(43, (int)RetentionSubjectType.DocumentSignatureRecord);
        Assert.Equal(44, (int)RetentionSubjectType.DocumentSignedObjectFingerprint);
    }

    // ── builders ──────────────────────────────────────────────────────────────

    private static CreateSignaturePolicyInput Policy() => new(
        "training-ack", "Training acknowledgement policy",
        nameof(SignableSubjectType.TrainingAssignment), nameof(SignatureMeaning.TrainingAcknowledgement),
        RequiresReAuthentication: false, RequiresSecondFactor: false, RequiresMeaningStatement: true,
        RequiresRepositoryAssessment: false, RequiresObjectFingerprint: true, RequiresManifestation: true,
        AllowedRepositoryTypes: null, AllowInterimRepositorySignature: true,
        InterimRepositoryBoundaryStatement: null);

    private static CreateSignatureRequestInput Request(Guid subjectId) => new(
        nameof(SignableSubjectType.TrainingAssignment), subjectId, RegisterEntryId, null,
        SignerUserId, null, nameof(SignatureMeaning.TrainingAcknowledgement),
        DateTimeOffset.UtcNow.AddDays(7), "Annual refresher", null);

    private static SignDocumentSubjectInput Sign(Guid subjectId) => new(
        null, nameof(SignableSubjectType.TrainingAssignment), subjectId, RegisterEntryId, null,
        nameof(SignatureMeaning.TrainingAcknowledgement), Statement,
        nameof(SignatureMethod.InternalAttestation), null, null, null, null, null);

    private static async Task<DocumentSignaturePolicy> ActivePolicyAsync(
        Harness f, string key,
        bool requiresSecondFactor = false,
        bool requiresReAuthentication = false,
        bool requiresRepositoryAssessment = false,
        bool allowInterim = true)
    {
        var created = await f.Policies.CreateAsync(Policy() with
        {
            PolicyKey = key,
            RequiresSecondFactor = requiresSecondFactor,
            RequiresReAuthentication = requiresReAuthentication,
            RequiresRepositoryAssessment = requiresRepositoryAssessment,
            AllowInterimRepositorySignature = allowInterim
        }, Corr, CancellationToken.None);
        await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);
        return f.PolicyRepo.Items.Single(p => p.PolicyKey == key);
    }

    private static async Task<DocumentSignatureRequest> CreateRequestAsync(
        Harness f, Guid? signerUserId = null, string? signerRole = null)
    {
        var assignment = SeedTrainingAssignment(f);
        var created = await f.Requests.CreateAsync(Request(assignment.Id) with
        {
            RequestedSignerUserId = signerRole is null ? signerUserId ?? SignerUserId : null,
            RequestedSignerRole = signerRole
        }, Corr, CancellationToken.None);
        return f.RequestRepo.Items.Single(x => x.Id == created.Data!.Id);
    }

    private static DocumentTrainingAssignment SeedTrainingAssignment(Harness f, Guid? tenantId = null)
    {
        var assignment = new DocumentTrainingAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            RegisterEntryId = RegisterEntryId,
            RequirementId = Guid.NewGuid(),
            AssignedToUserId = SignerUserId,
            TrainingType = DocumentTrainingType.ReadAndUnderstand,
            Status = TrainingAssignmentStatus.Assigned,
            DueDate = DateTimeOffset.UtcNow.AddDays(30)
        };
        f.TrainingRepo.Items.Add(assignment);
        return assignment;
    }

    private static DocumentApprovalEvidence SeedApprovalEvidence(Harness f)
    {
        var evidence = new DocumentApprovalEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RegisterEntryId = RegisterEntryId,
            RequirementId = Guid.NewGuid(),
            Action = ApprovalEvidenceAction.Approved,
            PerformedByUserId = SignerUserId,
            EvidenceReference = "APPR-EV-1"
        };
        f.ApprovalEvidenceRepo.Items.Add(evidence);
        return evidence;
    }

    private static DocumentRepositoryAssessment SeedAssessment(Harness f, RepositoryType type)
    {
        var assessment = new DocumentRepositoryAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RepositoryKey = $"repo-{type}",
            RepositoryName = $"Repository ({type})",
            RepositoryType = type,
            AssessmentStatus = RepositoryAssessmentStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        f.AssessmentRepo.Items.Add(assessment);
        return assessment;
    }

    // ── fixture ───────────────────────────────────────────────────────────────

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var policyRepo = new FakePolicyRepo(tenant);
        var requestRepo = new FakeRequestRepo(tenant);
        var signatureRepo = new FakeSignatureRepo(tenant);
        var fingerprintRepo = new FakeFingerprintRepo(tenant);

        var trainingRepo = new FakeTrainingRepo(tenant);
        var approvalEvidenceRepo = new FakeApprovalEvidenceRepo(tenant);
        var assessmentRepo = new FakeAssessmentRepo(tenant);

        var resolver = new DocumentSignableSubjectResolver(
            approvalEvidenceRepo, new FakeReleaseGateEvidenceRepo(tenant), trainingRepo,
            new FakeCorrectionRepo(tenant), new FakeQualityEventRepo(tenant), new FakeDeviationRepo(tenant),
            new FakeCapaRepo(tenant), assessmentRepo, new FakeLegalHoldRepo(tenant),
            new FakeDispositionRepo(tenant), new FakeIssueRepo(tenant), new FakeWithdrawalPlanRepo(tenant),
            new FakeExternalImpactRepo(tenant), new FakeRegisterRepo(tenant));

        var boundary = new DocumentSignatureBoundaryEvaluator(assessmentRepo);
        var policies = new DocumentSignaturePolicyService(policyRepo, tenant, user);
        var requests = new DocumentSignatureRequestService(requestRepo, resolver, policies, tenant, user);
        var signatures = new DocumentSignatureService(
            signatureRepo, requestRepo, fingerprintRepo, resolver, policies, boundary, tenant, user);
        var verification = new DocumentSignatureVerificationService(
            signatureRepo, fingerprintRepo, resolver, tenant, user);

        return new Harness(policies, requests, signatures, verification,
            policyRepo, requestRepo, signatureRepo, fingerprintRepo, trainingRepo, approvalEvidenceRepo, assessmentRepo);
    }

    private sealed record Harness(
        DocumentSignaturePolicyService Policies,
        DocumentSignatureRequestService Requests,
        DocumentSignatureService Signatures,
        DocumentSignatureVerificationService Verification,
        FakePolicyRepo PolicyRepo,
        FakeRequestRepo RequestRepo,
        FakeSignatureRepo SignatureRepo,
        FakeFingerprintRepo FingerprintRepo,
        FakeTrainingRepo TrainingRepo,
        FakeApprovalEvidenceRepo ApprovalEvidenceRepo,
        FakeAssessmentRepo AssessmentRepo);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => SignerUserId;
        public string? Email => "fu23@example.test";
        public string? DisplayName => "FU23 Tester";
        public string ActorName => "fu23@example.test";
        public bool IsAuthenticated => true;
    }

    // ── FU23 fakes ────────────────────────────────────────────────────────────

    private sealed class FakePolicyRepo(ITenantContext tenant) : IDocumentSignaturePolicyRepository
    {
        public List<DocumentSignaturePolicy> Items { get; } = [];
        private IEnumerable<DocumentSignaturePolicy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSignaturePolicy> CreateAsync(DocumentSignaturePolicy p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<DocumentSignaturePolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentSignaturePolicy?> GetByKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PolicyKey == key));
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetActiveBySubjectTypeAsync(SignableSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(
                Scoped.Where(x => x.SignableSubjectType == t && x.PolicyStatus == SignaturePolicyStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentSignaturePolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignaturePolicy>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentSignaturePolicy p, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == p.Id);
            if (i >= 0) Items[i] = p;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeRequestRepo(ITenantContext tenant) : IDocumentSignatureRequestRepository
    {
        public List<DocumentSignatureRequest> Items { get; } = [];
        private IEnumerable<DocumentSignatureRequest> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSignatureRequest> CreateAsync(DocumentSignatureRequest r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentSignatureRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSignatureRequest>> GetBySubjectAsync(SignableSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRequest>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentSignatureRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRequest>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentSignatureRequest r, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == r.Id);
            if (i >= 0) Items[i] = r;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeSignatureRepo(ITenantContext tenant) : IDocumentSignatureRecordRepository
    {
        public List<DocumentSignatureRecord> Items { get; } = [];
        private IEnumerable<DocumentSignatureRecord> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSignatureRecord> CreateAsync(DocumentSignatureRecord s, CancellationToken ct = default) { Items.Add(s); return Task.FromResult(s); }
        public Task<DocumentSignatureRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSignatureRecord>> GetBySubjectAsync(SignableSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRecord>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentSignatureRecord>> GetByRequestAsync(Guid requestId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRecord>>(Scoped.Where(x => x.SignatureRequestId == requestId).ToList());
        public Task<IReadOnlyList<DocumentSignatureRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRecord>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentSignatureRecord s, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == s.Id);
            if (i >= 0) Items[i] = s;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeFingerprintRepo(ITenantContext tenant) : IDocumentSignedObjectFingerprintRepository
    {
        public List<DocumentSignedObjectFingerprint> Items { get; } = [];
        private IEnumerable<DocumentSignedObjectFingerprint> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentSignedObjectFingerprint> CreateAsync(DocumentSignedObjectFingerprint f, CancellationToken ct = default) { Items.Add(f); return Task.FromResult(f); }
        public Task<DocumentSignedObjectFingerprint?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSignedObjectFingerprint>> GetBySubjectAsync(SignableSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignedObjectFingerprint>>(
                Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).OrderByDescending(x => x.GeneratedAt).ToList());
    }

    // ── resolver subject fakes ────────────────────────────────────────────────

    private sealed class FakeTrainingRepo(ITenantContext tenant) : IDocumentTrainingAssignmentRepository
    {
        public List<DocumentTrainingAssignment> Items { get; } = [];
        private IEnumerable<DocumentTrainingAssignment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTrainingAssignment> CreateAsync(DocumentTrainingAssignment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentTrainingAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTrainingAssignment>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentTrainingAssignment>> GetByRequirementAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTrainingAssignment>>(Scoped.Where(x => x.RequirementId == id).ToList());
        public Task<bool> UpdateAsync(DocumentTrainingAssignment a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeApprovalEvidenceRepo(ITenantContext tenant) : IDocumentApprovalEvidenceRepository
    {
        public List<DocumentApprovalEvidence> Items { get; } = [];
        private IEnumerable<DocumentApprovalEvidence> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentApprovalEvidence> CreateAsync(DocumentApprovalEvidence e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentApprovalEvidence>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRequirementAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentApprovalEvidence>>(Scoped.Where(x => x.RequirementId == id).ToList());
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : IDocumentRepositoryAssessmentRepository
    {
        public List<DocumentRepositoryAssessment> Items { get; } = [];
        private IEnumerable<DocumentRepositoryAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentRepositoryAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    // Empty stubs: these subject types are resolvable in production but unexercised by this suite.

    private sealed class FakeReleaseGateEvidenceRepo(ITenantContext tenant) : IDocumentReleaseGateEvidenceRepository
    {
        public List<DocumentReleaseGateEvidence> Items { get; } = [];
        private IEnumerable<DocumentReleaseGateEvidence> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentReleaseGateEvidence> CreateAsync(DocumentReleaseGateEvidence e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentReleaseGateEvidence>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentReleaseGateEvidence>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<DocumentReleaseGateEvidence?> GetLatestForGateAsync(Guid id, ReleaseGateKey key, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.RegisterEntryId == id && x.GateKey == key).OrderByDescending(x => x.VerificationDate).FirstOrDefault());
    }

    private sealed class FakeCorrectionRepo(ITenantContext tenant) : IDocumentGDocPCorrectionRecordRepository
    {
        public List<DocumentGDocPCorrectionRecord> Items { get; } = [];
        private IEnumerable<DocumentGDocPCorrectionRecord> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentGDocPCorrectionRecord> CreateAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentGDocPCorrectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetBySubjectAsync(GDocPSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetPendingReviewAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>([]);
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(Scoped.ToList());
        public Task<bool> UpdateReviewAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeQualityEventRepo(ITenantContext tenant) : IDocumentQualityEventRepository
    {
        public List<DocumentQualityEvent> Items { get; } = [];
        private IEnumerable<DocumentQualityEvent> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentQualityEvent> CreateAsync(DocumentQualityEvent e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentQualityEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentQualityEvent>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentQualityEvent>> GetOpenAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.Where(x => !x.IsSettled()).ToList());
        public Task<IReadOnlyList<DocumentQualityEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentQualityEvent>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentQualityEvent e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeDeviationRepo(ITenantContext tenant) : IDocumentDeviationRepository
    {
        public List<DocumentDeviation> Items { get; } = [];
        private IEnumerable<DocumentDeviation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentDeviation> CreateAsync(DocumentDeviation d, CancellationToken ct = default) { Items.Add(d); return Task.FromResult(d); }
        public Task<DocumentDeviation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentDeviation>> GetByQualityEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDeviation>>(Scoped.Where(x => x.QualityEventId == id).ToList());
        public Task<IReadOnlyList<DocumentDeviation>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDeviation>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentDeviation d, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == d.Id);
            if (i >= 0) Items[i] = d;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeCapaRepo(ITenantContext tenant) : IDocumentCAPAActionRepository
    {
        public List<DocumentCAPAAction> Items { get; } = [];
        private IEnumerable<DocumentCAPAAction> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentCAPAAction> CreateAsync(DocumentCAPAAction a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentCAPAAction?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByQualityEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.QualityEventId == id).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByDeviationAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.DeviationId == id).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentCAPAAction a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeLegalHoldRepo(ITenantContext tenant) : IDocumentLegalHoldRepository
    {
        public List<DocumentLegalHold> Items { get; } = [];
        private IEnumerable<DocumentLegalHold> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentLegalHold> CreateAsync(DocumentLegalHold h, CancellationToken ct = default) { Items.Add(h); return Task.FromResult(h); }
        public Task<DocumentLegalHold?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentLegalHold>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.Where(x => x.HoldStatus == LegalHoldStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentLegalHold>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentLegalHold h, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == h.Id);
            if (i >= 0) Items[i] = h;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeDispositionRepo(ITenantContext tenant) : IDocumentDispositionRequestRepository
    {
        public List<DocumentDispositionRequest> Items { get; } = [];
        private IEnumerable<DocumentDispositionRequest> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentDispositionRequest> CreateAsync(DocumentDispositionRequest r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentDispositionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentDispositionRequest>> GetBySubjectAsync(RetentionSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDispositionRequest>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentDispositionRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDispositionRequest>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentDispositionRequest r, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == r.Id);
            if (i >= 0) Items[i] = r;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeIssueRepo(ITenantContext tenant) : IDocumentTemporaryControlledIssueRepository
    {
        public List<DocumentTemporaryControlledIssue> Items { get; } = [];
        private IEnumerable<DocumentTemporaryControlledIssue> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default) { Items.Add(i); return Task.FromResult(i); }
        public Task<DocumentTemporaryControlledIssue?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.DowntimeEventId == id).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(x => x.Id == i.Id);
            if (idx >= 0) Items[idx] = i;
            return Task.FromResult(idx >= 0);
        }
    }

    private sealed class FakeWithdrawalPlanRepo(ITenantContext tenant) : IDocumentCopyWithdrawalPlanRepository
    {
        public List<DocumentCopyWithdrawalPlan> Items { get; } = [];
        private IEnumerable<DocumentCopyWithdrawalPlan> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentCopyWithdrawalPlan> CreateAsync(DocumentCopyWithdrawalPlan p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<DocumentCopyWithdrawalPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCopyWithdrawalPlan>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCopyWithdrawalPlan>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<DocumentCopyWithdrawalPlan?> GetOpenAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == id));
        public Task<bool> UpdateAsync(DocumentCopyWithdrawalPlan p, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == p.Id);
            if (i >= 0) Items[i] = p;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeExternalImpactRepo(ITenantContext tenant) : IExternalDocumentImpactAssessmentRepository
    {
        public List<ExternalDocumentImpactAssessment> Items { get; } = [];
        private IEnumerable<ExternalDocumentImpactAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<ExternalDocumentImpactAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(Scoped.Where(x => x.ExternalDocumentRegisterEntryId == id).ToList());
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }
}
