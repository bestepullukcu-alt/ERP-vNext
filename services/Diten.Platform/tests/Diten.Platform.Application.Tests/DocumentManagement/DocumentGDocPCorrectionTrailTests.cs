using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU21 — GDocP / ALCOA+ correction trail tests (GMG-QMS-SOP-0001 §21). Tenant-aware in-memory fakes
/// exercise policy resolution, risk classification, backdating and reconstruction protection, second-person
/// review, and the append-only guarantees.
///
/// The protective assertions matter most: a correction must never be recordable without a reason, a regulated
/// timestamp must never be quietly moved earlier, and a decided review must never be re-decided.
/// </summary>
public sealed class DocumentGDocPCorrectionTrailTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Reviewer = Guid.Parse("b0000000-0000-0000-0000-000000000021");
    private static readonly Guid SubjectId = Guid.Parse("50000000-0000-0000-0000-000000000021");
    private const string Corr = "fu21-corr-1";

    // ── correction policy ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_correction_policy()
    {
        var f = Fixture();

        var r = await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Draft", r.Data!.PolicyStatus);
        Assert.Equal("REGISTER-EFFECTIVE-DATE", r.Data.PolicyKey);
    }

    [Fact]
    public async Task Create_policy_validates_key_name_and_pattern()
    {
        var f = Fixture();

        var noKey = await f.Policies.CreateAsync(Policy() with { PolicyKey = " " }, Corr, CancellationToken.None);
        var noName = await f.Policies.CreateAsync(Policy() with { PolicyName = "" }, Corr, CancellationToken.None);
        var noPattern = await f.Policies.CreateAsync(Policy() with { FieldPathPattern = "" }, Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.PolicyKeyRequired, noKey.ReasonCode);
        Assert.Equal(GDocPCorrectionReasonCodes.PolicyNameRequired, noName.ReasonCode);
        Assert.Equal(GDocPCorrectionReasonCodes.FieldPathPatternRequired, noPattern.ReasonCode);
    }

    [Fact]
    public async Task Policy_key_is_unique_per_tenant()
    {
        var f = Fixture();
        await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        var duplicate = await f.Policies.CreateAsync(Policy() with { PolicyName = "Another" }, Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.PolicyKeyDuplicate, duplicate.ReasonCode);
        Assert.Single(f.PolicyRepo.Items);
    }

    [Fact]
    public async Task Activate_and_retire_correction_policy()
    {
        var f = Fixture();
        var created = await f.Policies.CreateAsync(Policy(), Corr, CancellationToken.None);

        var active = await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);
        Assert.Equal("Active", active.Data!.PolicyStatus);

        var retired = await f.Policies.RetireAsync(created.Data.Id, Corr, CancellationToken.None);
        Assert.Equal("Retired", retired.Data!.PolicyStatus);

        // Retiring is a status change, never a delete.
        Assert.Single(f.PolicyRepo.Items);
        Assert.DoesNotContain(f.PolicyRepo.Items, x => x.IsDeleted);

        var reactivate = await f.Policies.ActivateAsync(created.Data.Id, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.PolicyAlreadyRetired, reactivate.ReasonCode);
    }

    // ── recording a correction ────────────────────────────────────────────────

    [Fact]
    public async Task Record_correction_requires_subject_field_path_and_reason()
    {
        var f = Fixture();

        var noSubject = await f.Corrections.RecordCorrectionAsync(Correction() with { SubjectId = Guid.Empty }, Corr, CancellationToken.None);
        var noField = await f.Corrections.RecordCorrectionAsync(Correction() with { FieldPath = " " }, Corr, CancellationToken.None);
        var noReason = await f.Corrections.RecordCorrectionAsync(Correction() with { CorrectionReason = "" }, Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.SubjectRequired, noSubject.ReasonCode);
        Assert.Equal(GDocPCorrectionReasonCodes.FieldPathRequired, noField.ReasonCode);
        Assert.Equal(GDocPCorrectionReasonCodes.ReasonRequired, noReason.ReasonCode);
        Assert.Empty(f.RecordRepo.Items);
    }

    [Fact]
    public async Task Record_correction_stores_previous_and_new_value_snapshots()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Documnet Control", r.Data!.PreviousValueSnapshot);
        Assert.Equal("Document Control", r.Data.NewValueSnapshot);
        Assert.Equal("DocumentTitle", r.Data.FieldPath);
        Assert.Equal("Typo in the document title", r.Data.CorrectionReason);
        Assert.StartsWith("GDC-", r.Data.CorrectionNumber);
        // A routine typo fix carries no high-risk indicator and needs no review.
        Assert.False(r.Data.IsHighRiskCorrection);
        Assert.Equal("NotRequired", r.Data.ReviewStatus);
    }

    /// <summary>
    /// The structural half of the backdating protection: the input contract has NO CorrectedAt member at all, so
    /// a client cannot supply one even in principle.
    /// </summary>
    [Fact]
    public async Task CorrectedAt_is_server_stamped_and_not_client_suppliable()
    {
        var f = Fixture();
        var before = DateTimeOffset.UtcNow;

        var r = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);

        Assert.InRange(r.Data!.CorrectedAt, before, DateTimeOffset.UtcNow);
        Assert.DoesNotContain(typeof(RecordGDocPCorrectionInput).GetProperties(),
            p => p.Name.Contains("CorrectedAt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>PRODUCT DECISION: an unchanged value is refused, not silently accepted as a no-op.</summary>
    [Fact]
    public async Task Same_previous_and_new_value_is_rejected_rather_than_silently_accepted()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(
            Correction() with { PreviousValueSnapshot = "Same", NewValueSnapshot = "Same" }, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(GDocPCorrectionReasonCodes.NoChange, r.ReasonCode);
        Assert.Empty(f.RecordRepo.Items);
    }

    /// <summary>Truncating a previous value would destroy the evidence the trail exists to keep.</summary>
    [Fact]
    public async Task Oversized_snapshot_is_refused_not_truncated()
    {
        var f = Fixture();
        var huge = new string('x', GDocPCorrectionWire.MaxSnapshotLength + 1);

        var r = await f.Corrections.RecordCorrectionAsync(
            Correction() with { PreviousValueSnapshot = huge }, Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.SnapshotTooLarge, r.ReasonCode);
        Assert.Empty(f.RecordRepo.Items);
    }

    [Fact]
    public async Task Server_owned_fields_cannot_be_corrected()
    {
        var f = Fixture();

        foreach (var field in new[] { "CorrectedAt", "Id", "TenantId", "WrittenAtUtc" })
        {
            var r = await f.Corrections.RecordCorrectionAsync(
                Correction() with { FieldPath = field }, Corr, CancellationToken.None);
            Assert.Equal(GDocPCorrectionReasonCodes.ServerTimestampImmutable, r.ReasonCode);
        }

        Assert.Empty(f.RecordRepo.Items);
    }

    // ── high-risk correction types ────────────────────────────────────────────

    [Fact]
    public async Task Reconstruction_requires_evidence_deviation_and_review()
    {
        var f = Fixture();
        var reconstruction = Correction() with
        {
            CorrectionType = nameof(GDocPCorrectionType.Reconstruction),
            PreviousValueSnapshot = "lost value",
            NewValueSnapshot = "recreated value"
        };

        var noEvidence = await f.Corrections.RecordCorrectionAsync(reconstruction, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReconstructionRequiresEvidence, noEvidence.ReasonCode);

        var noDeviation = await f.Corrections.RecordCorrectionAsync(
            reconstruction with { CorrectionEvidenceReference = "EV-1" }, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.HighRiskRequiresDeviation, noDeviation.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            reconstruction with { CorrectionEvidenceReference = "EV-1", DeviationReference = "DEV-1" },
            Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.True(ok.Data!.IsHighRiskCorrection);
        Assert.Equal("PendingReview", ok.Data.ReviewStatus);
    }

    [Fact]
    public async Task DataIntegrityCorrection_requires_evidence_deviation_and_review()
    {
        var f = Fixture();
        var integrity = Correction() with { CorrectionType = nameof(GDocPCorrectionType.DataIntegrityCorrection) };

        var noEvidence = await f.Corrections.RecordCorrectionAsync(integrity, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReconstructionRequiresEvidence, noEvidence.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            integrity with { CorrectionEvidenceReference = "EV-1", DeviationReference = "DEV-1" }, Corr, CancellationToken.None);

        Assert.True(ok.Data!.IsHighRiskCorrection);
        Assert.Equal("PendingReview", ok.Data.ReviewStatus);
    }

    [Fact]
    public async Task EvidenceReferenceCorrection_is_high_risk_and_requires_evidence()
    {
        var f = Fixture();
        var swap = Correction() with
        {
            FieldPath = "ApprovalEvidenceReference",
            CorrectionType = nameof(GDocPCorrectionType.EvidenceReferenceCorrection),
            PreviousValueSnapshot = "APPR-OLD",
            NewValueSnapshot = "APPR-NEW"
        };

        var noEvidence = await f.Corrections.RecordCorrectionAsync(swap, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.EvidenceRequired, noEvidence.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            swap with { CorrectionEvidenceReference = "EV-1", DeviationReference = "DEV-1" }, Corr, CancellationToken.None);
        Assert.True(ok.Data!.IsHighRiskCorrection);
    }

    [Fact]
    public async Task StatusCorrection_is_high_risk()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            FieldPath = "LifecycleStatus",
            CorrectionType = nameof(GDocPCorrectionType.StatusCorrection),
            PreviousValueSnapshot = "Draft",
            NewValueSnapshot = "Effective",
            DeviationReference = "DEV-1"
        }, Corr, CancellationToken.None);

        Assert.True(r.Data!.IsHighRiskCorrection);
        Assert.Equal("PendingReview", r.Data.ReviewStatus);
        Assert.Contains("status", r.Data.RiskAssessmentNote, StringComparison.OrdinalIgnoreCase);
    }

    // ── backdating protection ─────────────────────────────────────────────────

    [Fact]
    public async Task Backdating_a_regulated_timestamp_requires_a_deviation_reference()
    {
        var f = Fixture();
        var backdate = Correction() with
        {
            FieldPath = "EffectiveDate",
            CorrectionType = nameof(GDocPCorrectionType.DateCorrection),
            PreviousValueSnapshot = "2026-07-15T00:00:00Z",
            NewValueSnapshot = "2026-07-01T00:00:00Z",
            ValueFormat = nameof(GDocPValueFormat.DateTime)
        };

        var blocked = await f.Corrections.RecordCorrectionAsync(backdate, Corr, CancellationToken.None);
        Assert.False(blocked.IsSuccessful);
        Assert.Equal(GDocPCorrectionReasonCodes.BackdatingRequiresDeviation, blocked.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            backdate with { DeviationReference = "DEV-BACKDATE-1" }, Corr, CancellationToken.None);

        Assert.True(ok.IsSuccessful);
        Assert.True(ok.Data!.IsBackdatingCorrection);
        Assert.True(ok.Data.IsHighRiskCorrection);
        Assert.Equal("PendingReview", ok.Data.ReviewStatus);
        Assert.Contains("EARLIER", ok.Data.RiskAssessmentNote);
    }

    [Fact]
    public async Task Moving_a_regulated_timestamp_forward_is_not_backdating_but_still_needs_review()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            FieldPath = "EffectiveDate",
            CorrectionType = nameof(GDocPCorrectionType.DateCorrection),
            PreviousValueSnapshot = "2026-07-01T00:00:00Z",
            NewValueSnapshot = "2026-07-15T00:00:00Z",
            ValueFormat = nameof(GDocPValueFormat.DateTime)
        }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.False(r.Data!.IsBackdatingCorrection);
        // The safe default still routes a regulated timestamp correction to a second person.
        Assert.Equal("PendingReview", r.Data.ReviewStatus);
    }

    [Fact]
    public async Task Unparseable_dates_do_not_falsely_assert_backdating()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            FieldPath = "EffectiveDate",
            CorrectionType = nameof(GDocPCorrectionType.DateCorrection),
            PreviousValueSnapshot = "not a date",
            NewValueSnapshot = "also not a date",
            ValueFormat = nameof(GDocPValueFormat.Text)
        }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.False(r.Data!.IsBackdatingCorrection);
    }

    // ── undocumented reconstruction protection ────────────────────────────────

    [Fact]
    public async Task Unknown_previous_value_becomes_an_explicit_sentinel_and_requires_a_deviation()
    {
        var f = Fixture();
        var unknown = Correction() with { PreviousValueSnapshot = null };

        var blocked = await f.Corrections.RecordCorrectionAsync(unknown, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.HighRiskRequiresDeviation, blocked.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            unknown with { DeviationReference = "DEV-1" }, Corr, CancellationToken.None);

        // Never a blank: a blank previous value is indistinguishable from a lost one.
        Assert.Equal(DocumentGDocPCorrectionRecord.UnknownPreviousValue, ok.Data!.PreviousValueSnapshot);
        Assert.True(ok.Data.IsHighRiskCorrection);
        Assert.Contains("could not be established", ok.Data.RiskAssessmentNote);
    }

    [Fact]
    public async Task Redacted_value_format_is_explicit_never_blank()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            ValueFormat = nameof(GDocPValueFormat.Redacted),
            PreviousValueSnapshot = null,
            NewValueSnapshot = null
        }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Redacted", r.Data!.ValueFormat);
        Assert.Equal(DocumentGDocPCorrectionRecord.RedactedMarker, r.Data.PreviousValueSnapshot);
        Assert.Equal(DocumentGDocPCorrectionRecord.RedactedMarker, r.Data.NewValueSnapshot);
    }

    [Fact]
    public async Task Clearing_a_field_records_an_explicit_empty_marker()
    {
        var f = Fixture();

        var r = await f.Corrections.RecordCorrectionAsync(
            Correction() with { NewValueSnapshot = "" }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("[EMPTY]", r.Data!.NewValueSnapshot);
    }

    // ── policy resolution ─────────────────────────────────────────────────────

    [Fact]
    public async Task Most_restrictive_policy_wins()
    {
        var f = Fixture();
        // A permissive policy and a restrictive one both match DocumentTitle.
        await ActivePolicyAsync(f, Policy() with
        {
            PolicyKey = "PERMISSIVE", FieldPathPattern = "*",
            RequiresEvidenceReference = false, RequiresReview = false,
            AllowCorrectionAfterEffective = true
        });
        await ActivePolicyAsync(f, Policy() with
        {
            PolicyKey = "RESTRICTIVE", FieldPathPattern = "DocumentTitle",
            RequiresEvidenceReference = true, RequiresReview = true,
            AllowCorrectionAfterEffective = false
        });

        // Requires-flags are OR-ed: evidence is now mandatory.
        var noEvidence = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.EvidenceRequired, noEvidence.ReasonCode);

        // Allow-flags are AND-ed: a single "no" blocks correction after effective.
        var afterEffective = await f.Corrections.RecordCorrectionAsync(
            Correction() with { CorrectionEvidenceReference = "EV-1", SubjectIsEffective = true }, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.CorrectionNotAllowedAfterEffective, afterEffective.ReasonCode);

        var ok = await f.Corrections.RecordCorrectionAsync(
            Correction() with { CorrectionEvidenceReference = "EV-1" }, Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("PendingReview", ok.Data!.ReviewStatus);
    }

    [Fact]
    public async Task With_no_policy_the_safe_default_protects_reason_and_high_risk()
    {
        var f = Fixture(); // no policies at all
        Assert.Empty(f.PolicyRepo.Items);

        // Reason is still mandatory...
        var noReason = await f.Corrections.RecordCorrectionAsync(Correction() with { CorrectionReason = " " }, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReasonRequired, noReason.ReasonCode);

        // ...and a high-risk type still demands a deviation reference.
        var noDeviation = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            CorrectionType = nameof(GDocPCorrectionType.StatusCorrection)
        }, Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.HighRiskRequiresDeviation, noDeviation.ReasonCode);

        // A routine correction still goes through.
        var ok = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task A_retired_policy_no_longer_applies()
    {
        var f = Fixture();
        var created = await f.Policies.CreateAsync(Policy() with
        {
            FieldPathPattern = "DocumentTitle", RequiresEvidenceReference = true
        }, Corr, CancellationToken.None);
        await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);

        var blocked = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.EvidenceRequired, blocked.ReasonCode);

        await f.Policies.RetireAsync(created.Data.Id, Corr, CancellationToken.None);

        var ok = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Theory]
    [InlineData("*", "AnyField", true)]
    [InlineData("DocumentTitle", "DocumentTitle", true)]
    [InlineData("DocumentTitle", "OtherField", false)]
    [InlineData("Approval*", "ApprovalEvidenceReference", true)]
    [InlineData("*Date", "EffectiveDate", true)]
    [InlineData("*Date", "DocumentTitle", false)]
    public void Policy_field_pattern_matching(string pattern, string fieldPath, bool expected)
    {
        var policy = new DocumentGDocPCorrectionPolicy
        {
            Id = Guid.NewGuid(), TenantId = TenantId, PolicyKey = "K", PolicyName = "N", FieldPathPattern = pattern
        };

        Assert.Equal(expected, policy.Matches(fieldPath));
    }

    // ── review / reject ───────────────────────────────────────────────────────

    [Fact]
    public async Task Review_requires_reviewer_and_evidence()
    {
        var f = Fixture();
        var id = await PendingReviewCorrectionAsync(f);

        var noReviewer = await f.Corrections.ReviewAsync(id,
            new ReviewGDocPCorrectionInput(null, null, "REV-1", null), Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReviewerRequired, noReviewer.ReasonCode);

        var noEvidence = await f.Corrections.ReviewAsync(id,
            new ReviewGDocPCorrectionInput(Reviewer, "QA Documentation", " ", null), Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReviewEvidenceRequired, noEvidence.ReasonCode);

        var ok = await f.Corrections.ReviewAsync(id,
            new ReviewGDocPCorrectionInput(Reviewer, "QA Documentation", "REV-1", "Verified against source"),
            Corr, CancellationToken.None);
        Assert.Equal("Reviewed", ok.Data!.ReviewStatus);
        Assert.NotNull(ok.Data.ReviewedAt);
    }

    [Fact]
    public async Task Reject_requires_reviewer_and_reason()
    {
        var f = Fixture();
        var id = await PendingReviewCorrectionAsync(f);

        var noReason = await f.Corrections.RejectAsync(id,
            new RejectGDocPCorrectionInput(Reviewer, "QA Documentation", " "), Corr, CancellationToken.None);
        Assert.Equal(GDocPCorrectionReasonCodes.ReviewReasonRequired, noReason.ReasonCode);

        var ok = await f.Corrections.RejectAsync(id,
            new RejectGDocPCorrectionInput(Reviewer, "QA Documentation", "Deviation reference does not match"),
            Corr, CancellationToken.None);
        Assert.Equal("Rejected", ok.Data!.ReviewStatus);
        Assert.Equal("Deviation reference does not match", ok.Data.ReviewComment);
    }

    [Fact]
    public async Task A_decided_review_is_final()
    {
        var f = Fixture();
        var approved = await PendingReviewCorrectionAsync(f);
        await f.Corrections.ReviewAsync(approved, Review(), Corr, CancellationToken.None);

        var reReview = await f.Corrections.ReviewAsync(approved, Review(), Corr, CancellationToken.None);
        var reReject = await f.Corrections.RejectAsync(approved,
            new RejectGDocPCorrectionInput(Reviewer, "QA", "changed my mind"), Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.AlreadyReviewed, reReview.ReasonCode);
        Assert.Equal(GDocPCorrectionReasonCodes.AlreadyReviewed, reReject.ReasonCode);
        Assert.Equal(GDocPReviewStatus.Reviewed, f.RecordRepo.Items.Single().ReviewStatus);
    }

    [Fact]
    public async Task A_rejected_correction_cannot_be_reviewed()
    {
        var f = Fixture();
        var id = await PendingReviewCorrectionAsync(f);
        await f.Corrections.RejectAsync(id, new RejectGDocPCorrectionInput(Reviewer, "QA", "Insufficient evidence"), Corr, CancellationToken.None);

        var r = await f.Corrections.ReviewAsync(id, Review(), Corr, CancellationToken.None);

        Assert.Equal(GDocPCorrectionReasonCodes.AlreadyReviewed, r.ReasonCode);
        Assert.Equal(GDocPReviewStatus.Rejected, f.RecordRepo.Items.Single().ReviewStatus);
    }

    [Fact]
    public async Task Every_review_decision_is_kept_as_its_own_record()
    {
        var f = Fixture();
        var id = await PendingReviewCorrectionAsync(f);
        await f.Corrections.ReviewAsync(id, Review(), Corr, CancellationToken.None);

        var reviews = await f.Corrections.GetReviewsAsync(id, Corr, CancellationToken.None);

        var review = Assert.Single(reviews.Data!);
        Assert.Equal("Approved", review.ReviewDecision);
        Assert.Equal("REV-1", review.ReviewEvidenceReference);
        Assert.DoesNotContain(f.ReviewRepo.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Reviewing_never_rewrites_the_recorded_correction_values()
    {
        var f = Fixture();
        var id = await PendingReviewCorrectionAsync(f);
        var before = f.RecordRepo.Items.Single();
        var (field, previous, updated, reason, correctedAt) =
            (before.FieldPath, before.PreviousValueSnapshot, before.NewValueSnapshot, before.CorrectionReason, before.CorrectedAt);

        await f.Corrections.ReviewAsync(id, Review(), Corr, CancellationToken.None);

        var after = f.RecordRepo.Items.Single();
        Assert.Equal(field, after.FieldPath);
        Assert.Equal(previous, after.PreviousValueSnapshot);
        Assert.Equal(updated, after.NewValueSnapshot);
        Assert.Equal(reason, after.CorrectionReason);
        Assert.Equal(correctedAt, after.CorrectedAt);
    }

    // ── history / queries ─────────────────────────────────────────────────────

    [Fact]
    public async Task Subject_history_returns_all_corrections_for_that_record()
    {
        var f = Fixture();
        await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        await f.Corrections.RecordCorrectionAsync(
            Correction() with { FieldPath = "OwnerFunction", PreviousValueSnapshot = "QA", NewValueSnapshot = "RA" },
            Corr, CancellationToken.None);
        // A correction on a different subject must not leak in.
        await f.Corrections.RecordCorrectionAsync(Correction() with { SubjectId = Guid.NewGuid() }, Corr, CancellationToken.None);

        var history = await f.Corrections.GetBySubjectAsync(
            nameof(GDocPSubjectType.DocumentMasterRegisterEntry), SubjectId, Corr, CancellationToken.None);

        Assert.Equal(2, history.Data!.Count);
        Assert.All(history.Data, x => Assert.Equal(SubjectId, x.SubjectId));
    }

    [Fact]
    public async Task Pending_review_queue_returns_only_awaiting_corrections()
    {
        var f = Fixture();
        await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None); // NotRequired
        var pending = await PendingReviewCorrectionAsync(f);

        var queue = await f.Corrections.GetPendingReviewAsync(Corr, CancellationToken.None);

        var row = Assert.Single(queue.Data!);
        Assert.Equal(pending, row.Id);
    }

    // ── FU15 retention integration ────────────────────────────────────────────

    [Fact]
    public void Retention_subject_types_appended_without_shifting_existing_ordinals()
    {
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(33, (int)RetentionSubjectType.DowntimeEscalation);
        Assert.Equal(34, (int)RetentionSubjectType.GDocPCorrectionRecord);
        Assert.Equal(35, (int)RetentionSubjectType.GDocPCorrectionPolicy);
        Assert.Equal(36, (int)RetentionSubjectType.GDocPCorrectionReview);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_correction_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentGDocPCorrectionRecord
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, CorrectionNumber = "GDC-FOREIGN",
            SubjectId = Guid.NewGuid(), FieldPath = "DocumentTitle", PreviousValueSnapshot = "a",
            NewValueSnapshot = "b", CorrectionReason = "foreign", ReviewStatus = GDocPReviewStatus.PendingReview
        };
        f.RecordRepo.Items.Add(foreign);

        var read = await f.Corrections.GetAsync(foreign.Id, Corr, CancellationToken.None);
        var review = await f.Corrections.ReviewAsync(foreign.Id, Review(), Corr, CancellationToken.None);

        Assert.Equal(404, read.StatusCode);
        Assert.Equal(404, review.StatusCode);
        Assert.Equal(GDocPReviewStatus.PendingReview, f.RecordRepo.Items.Single(x => x.Id == foreign.Id).ReviewStatus);
    }

    [Fact]
    public async Task Cross_tenant_policy_is_blocked_and_does_not_apply()
    {
        var f = Fixture();
        var foreign = new DocumentGDocPCorrectionPolicy
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, PolicyKey = "FOREIGN", PolicyName = "Foreign",
            FieldPathPattern = "*", PolicyStatus = GDocPCorrectionPolicyStatus.Active,
            SubjectType = GDocPSubjectType.DocumentMasterRegisterEntry, RequiresEvidenceReference = true
        };
        f.PolicyRepo.Items.Add(foreign);

        var read = await f.Policies.GetAsync(foreign.Id, Corr, CancellationToken.None);
        Assert.Equal(404, read.StatusCode);

        // The foreign policy must not tighten this tenant's requirements.
        var ok = await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
    }

    [Fact]
    public async Task Correction_trail_is_append_only_and_never_deleted()
    {
        var f = Fixture();
        await f.Corrections.RecordCorrectionAsync(Correction(), Corr, CancellationToken.None);
        var pending = await PendingReviewCorrectionAsync(f);
        await f.Corrections.RejectAsync(pending, new RejectGDocPCorrectionInput(Reviewer, "QA", "No"), Corr, CancellationToken.None);

        Assert.Equal(2, f.RecordRepo.Items.Count);
        Assert.DoesNotContain(f.RecordRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.ReviewRepo.Items, x => x.IsDeleted);
    }

    /// <summary>
    /// The correction record repository must expose no delete AND no general update — only the narrow review
    /// path may touch a recorded correction.
    /// </summary>
    [Fact]
    public void Correction_record_contract_exposes_no_delete_and_no_general_update()
    {
        var methods = typeof(IDocumentGDocPCorrectionRecordRepository).GetMethods();

        Assert.DoesNotContain(methods, m =>
            m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
            || m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
            || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase));

        // The only mutation is UpdateReviewAsync — no bare UpdateAsync exists.
        Assert.DoesNotContain(methods, m => m.Name == "UpdateAsync");
        Assert.Contains(methods, m => m.Name == "UpdateReviewAsync");

        foreach (var contract in new[] { typeof(IDocumentGDocPCorrectionPolicyRepository), typeof(IDocumentGDocPCorrectionReviewRepository) })
        {
            Assert.DoesNotContain(contract.GetMethods(), m =>
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Snapshots are field-value text — no FU21 aggregate can carry document bytes.</summary>
    [Fact]
    public void No_correction_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(DocumentGDocPCorrectionRecord), typeof(DocumentGDocPCorrectionPolicy),
            typeof(DocumentGDocPCorrectionReview)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task ActivePolicyAsync(Harness f, GDocPCorrectionPolicyInput input)
    {
        var created = await f.Policies.CreateAsync(input, Corr, CancellationToken.None);
        await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);
    }

    /// <summary>A high-risk correction that lands in PendingReview.</summary>
    private async Task<Guid> PendingReviewCorrectionAsync(Harness f)
    {
        var r = await f.Corrections.RecordCorrectionAsync(Correction() with
        {
            FieldPath = "LifecycleStatus",
            CorrectionType = nameof(GDocPCorrectionType.StatusCorrection),
            PreviousValueSnapshot = "Draft",
            NewValueSnapshot = "Effective",
            DeviationReference = "DEV-1"
        }, Corr, CancellationToken.None);
        return r.Data!.Id;
    }

    private static ReviewGDocPCorrectionInput Review() => new(Reviewer, "QA Documentation", "REV-1", "Checked");

    private static GDocPCorrectionPolicyInput Policy() => new(
        PolicyKey: "REGISTER-EFFECTIVE-DATE",
        PolicyName: "Master register effective date corrections",
        SubjectType: nameof(GDocPSubjectType.DocumentMasterRegisterEntry),
        FieldPathPattern: "EffectiveDate",
        RequiresCorrectionReason: true,
        RequiresEvidenceReference: false,
        RequiresReview: false,
        RequiresDeviationReferenceForHighRisk: true,
        AllowCorrectionAfterApproval: true,
        AllowCorrectionAfterEffective: true,
        IsBackdatingSensitive: false,
        IsStatusSensitive: false,
        IsEvidenceSensitive: false,
        Notes: null);

    private static RecordGDocPCorrectionInput Correction() => new(
        SubjectType: nameof(GDocPSubjectType.DocumentMasterRegisterEntry),
        SubjectId: SubjectId,
        FieldPath: "DocumentTitle",
        FieldDisplayName: "Document title",
        PreviousValueSnapshot: "Documnet Control",
        NewValueSnapshot: "Document Control",
        ValueFormat: nameof(GDocPValueFormat.Text),
        CorrectionType: nameof(GDocPCorrectionType.TypographicalCorrection),
        CorrectionReason: "Typo in the document title",
        CorrectionEvidenceReference: null,
        DeviationReference: null,
        RegisterEntryId: SubjectId,
        ControlledDocumentId: null,
        CorrectedByUserId: null,
        CorrectedByRole: "QA Documentation",
        RequestedBy: null);

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var records = new FakeRecordRepo(tenant);
        var policies = new FakePolicyRepo(tenant);
        var reviews = new FakeReviewRepo(tenant);
        var evaluator = new DocumentGDocPCorrectionEvaluator(policies);

        return new Harness(
            new DocumentGDocPCorrectionService(records, reviews, evaluator, tenant, user),
            new DocumentGDocPCorrectionPolicyService(policies, tenant, user),
            records, policies, reviews);
    }

    private sealed record Harness(
        DocumentGDocPCorrectionService Corrections,
        DocumentGDocPCorrectionPolicyService Policies,
        FakeRecordRepo RecordRepo,
        FakePolicyRepo PolicyRepo,
        FakeReviewRepo ReviewRepo);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444421");
        public string? Email => "fu21@example.test";
        public string? DisplayName => "FU21 Tester";
        public string ActorName => "fu21@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRecordRepo(ITenantContext tenant) : IDocumentGDocPCorrectionRecordRepository
    {
        public List<DocumentGDocPCorrectionRecord> Items { get; } = [];
        private IEnumerable<DocumentGDocPCorrectionRecord> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentGDocPCorrectionRecord> CreateAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentGDocPCorrectionRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetBySubjectAsync(GDocPSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(
                Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).OrderBy(x => x.CorrectedAt).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetPendingReviewAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(
                Scoped.Where(x => x.ReviewStatus == GDocPReviewStatus.PendingReview).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionRecord>>(Scoped.ToList());

        /// <summary>Mirrors the production repository: ONLY the review fields are applied.</summary>
        public Task<bool> UpdateReviewAsync(DocumentGDocPCorrectionRecord r, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x => x.Id == r.Id && x.TenantId == tenant.TenantId);
            if (stored is null)
            {
                return Task.FromResult(false);
            }

            stored.ReviewStatus = r.ReviewStatus;
            stored.ReviewedBy = r.ReviewedBy;
            stored.ReviewedByUserId = r.ReviewedByUserId;
            stored.ReviewedAt = r.ReviewedAt;
            stored.ReviewEvidenceReference = r.ReviewEvidenceReference;
            stored.ReviewComment = r.ReviewComment;
            stored.UpdatedAt = r.UpdatedAt;
            stored.UpdatedBy = r.UpdatedBy;
            return Task.FromResult(true);
        }
    }

    private sealed class FakePolicyRepo(ITenantContext tenant) : IDocumentGDocPCorrectionPolicyRepository
    {
        public List<DocumentGDocPCorrectionPolicy> Items { get; } = [];
        private IEnumerable<DocumentGDocPCorrectionPolicy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentGDocPCorrectionPolicy> CreateAsync(DocumentGDocPCorrectionPolicy p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<DocumentGDocPCorrectionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentGDocPCorrectionPolicy?> GetByKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PolicyKey == key));
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetActiveBySubjectTypeAsync(GDocPSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(
                Scoped.Where(x => x.SubjectType == t && x.PolicyStatus == GDocPCorrectionPolicyStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentGDocPCorrectionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionPolicy>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentGDocPCorrectionPolicy p, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == p.Id);
            if (i >= 0) Items[i] = p;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeReviewRepo(ITenantContext tenant) : IDocumentGDocPCorrectionReviewRepository
    {
        public List<DocumentGDocPCorrectionReview> Items { get; } = [];
        private IEnumerable<DocumentGDocPCorrectionReview> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentGDocPCorrectionReview> CreateAsync(DocumentGDocPCorrectionReview v, CancellationToken ct = default) { Items.Add(v); return Task.FromResult(v); }
        public Task<IReadOnlyList<DocumentGDocPCorrectionReview>> GetByCorrectionAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGDocPCorrectionReview>>(
                Scoped.Where(x => x.CorrectionRecordId == id).OrderBy(x => x.ReviewedAt).ToList());
    }
}
