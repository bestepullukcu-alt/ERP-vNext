using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU18 — variant translation / site-adoption governance tests (GMG-QMS-SOP-0001 §13.2). Tenant-aware
/// in-memory fakes exercise classification validation, the bilingual review and local approval trails, the
/// parent change assessment, the temporary English master exception, and the local-use readiness decision.
///
/// The compatibility assertions matter as much as the feature ones: a variant with no localization profile must
/// behave exactly as it did before FU18.
/// </summary>
public sealed class TemplateVariantLocalizationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Author = Guid.Parse("a0000000-0000-0000-0000-000000000018");
    private static readonly Guid Reviewer = Guid.Parse("b0000000-0000-0000-0000-000000000018");
    private static readonly Guid Approver = Guid.Parse("c0000000-0000-0000-0000-000000000018");
    private const string Corr = "fu18-corr-1";

    // ── classification / profile validation ───────────────────────────────────

    [Fact]
    public async Task Create_translation_profile_requires_language()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var noLanguage = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { VariantLanguageCode = " " }, Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.LanguageRequired, noLanguage.ReasonCode);

        var ok = await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("tr", ok.Data!.VariantLanguageCode);
        Assert.True(ok.Data.IsTranslationVariant);
    }

    [Fact]
    public async Task Site_adopted_variant_requires_country_or_site()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var neither = await f.Service.UpsertProfileAsync(v.Id,
            SiteAdopted() with { CountryCode = null, SiteCode = null }, Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.CountryOrSiteRequired, neither.ReasonCode);

        var withSite = await f.Service.UpsertProfileAsync(v.Id,
            SiteAdopted() with { CountryCode = null, SiteCode = "IST-01" }, Corr, CancellationToken.None);
        Assert.True(withSite.IsSuccessful);
    }

    [Fact]
    public async Task Profile_inherits_parent_linkage_from_the_variant_and_never_loses_it()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var created = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { ParentTemplateMasterId = null, ParentTemplateMasterVersionId = null },
            Corr, CancellationToken.None);
        Assert.Equal(v.TemplateMasterId, created.Data!.ParentTemplateMasterId);

        // A later update that omits parent linkage must not null it out.
        var updated = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { ParentTemplateMasterId = null, VariantLanguageName = "Türkçe" },
            Corr, CancellationToken.None);
        Assert.Equal(v.TemplateMasterId, updated.Data!.ParentTemplateMasterId);
    }

    [Fact]
    public async Task Translation_variant_automatically_requires_bilingual_review()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var r = await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None);

        Assert.True(r.Data!.RequiresBilingualReview);
        Assert.Equal("Pending", r.Data.BilingualReviewStatus);
        Assert.Equal("Pending", r.Data.TranslationReadinessStatus);
    }

    [Fact]
    public async Task Site_adopted_variant_automatically_requires_local_approval()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var r = await f.Service.UpsertProfileAsync(v.Id, SiteAdopted(), Corr, CancellationToken.None);

        Assert.True(r.Data!.RequiresLocalApproval);
        Assert.Equal("Pending", r.Data.LocalApprovalStatus);
    }

    // ── bilingual review ──────────────────────────────────────────────────────

    [Fact]
    public async Task Bilingual_review_completion_requires_evidence_and_reviewer()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        var noEvidence = await f.Service.RecordBilingualReviewAsync(v,
            new RecordBilingualReviewInput(Reviewer, "QA Translator", "  ", null), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.ReviewEvidenceRequired, noEvidence.ReasonCode);

        var noReviewer = await f.Service.RecordBilingualReviewAsync(v,
            new RecordBilingualReviewInput(null, null, "BR-1", null), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.ReviewerRequired, noReviewer.ReasonCode);
    }

    [Fact]
    public async Task Author_cannot_be_the_sole_bilingual_reviewer()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        var r = await f.Service.RecordBilingualReviewAsync(v,
            new RecordBilingualReviewInput(Author, "Author", "BR-1", null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(VariantLocalizationReasonCodes.ReviewerCannotBeAuthor, r.ReasonCode);
    }

    [Fact]
    public async Task Bilingual_review_completion_sets_translation_ready()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        var r = await CompleteReviewAsync(f, v);

        Assert.Equal("Completed", r.Data!.BilingualReviewStatus);
        Assert.Equal("Ready", r.Data.TranslationReadinessStatus);
        Assert.NotNull(r.Data.BilingualReviewCompletedAt);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.True(readiness.Data!.TranslationReady);
        Assert.True(readiness.Data.LocalUseAllowed);
    }

    [Fact]
    public async Task Bilingual_review_rejection_blocks_translation_readiness()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f); // local language NOT mandatory

        var r = await f.Service.RejectBilingualReviewAsync(v,
            new RejectVariantReviewInput("Terminology inconsistent with source", "BR-REJ-1"), Corr, CancellationToken.None);

        Assert.Equal("Rejected", r.Data!.BilingualReviewStatus);
        Assert.Equal("Blocked", r.Data.TranslationReadinessStatus);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        // The TRANSLATION is unusable...
        Assert.False(readiness.Data!.TranslationReady);
        // ...but because the local language is optional, the source-language master remains usable, so the
        // rejection surfaces as a warning rather than a hard block.
        Assert.Contains("BILINGUAL_REVIEW_REJECTED", readiness.Data.WarningReasons);
        Assert.True(readiness.Data.LocalUseAllowed);
    }

    [Fact]
    public async Task Bilingual_review_rejection_hard_blocks_when_the_local_language_is_mandatory()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);

        await f.Service.RejectBilingualReviewAsync(v,
            new RejectVariantReviewInput("Terminology inconsistent with source", "BR-REJ-1"), Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.False(readiness.Data!.TranslationReady);
        Assert.False(readiness.Data.LocalUseAllowed);
        Assert.Contains("BILINGUAL_REVIEW_REJECTED", readiness.Data.BlockingReasons);
    }

    [Fact]
    public async Task Bilingual_review_rejection_requires_a_reason()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        var r = await f.Service.RejectBilingualReviewAsync(v, new RejectVariantReviewInput(" ", null), Corr, CancellationToken.None);

        Assert.Equal(VariantLocalizationReasonCodes.ReasonRequired, r.ReasonCode);
    }

    // ── local approval ────────────────────────────────────────────────────────

    [Fact]
    public async Task Local_approval_completion_requires_evidence_and_approver()
    {
        var f = Fixture();
        var v = await SiteAdoptedVariantAsync(f);

        var noEvidence = await f.Service.RecordLocalApprovalAsync(v,
            new RecordLocalApprovalInput(Approver, "Local QA", "", null), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.ApprovalEvidenceRequired, noEvidence.ReasonCode);

        var noApprover = await f.Service.RecordLocalApprovalAsync(v,
            new RecordLocalApprovalInput(null, null, "LA-1", null), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.ApproverRequired, noApprover.ReasonCode);
    }

    [Fact]
    public async Task Local_approval_completion_makes_the_site_adopted_variant_usable()
    {
        var f = Fixture();
        var v = await SiteAdoptedVariantAsync(f);

        var r = await f.Service.RecordLocalApprovalAsync(v,
            new RecordLocalApprovalInput(Approver, "Local QA", "LA-1", "Adopted at Istanbul site"), Corr, CancellationToken.None);

        Assert.Equal("Completed", r.Data!.LocalApprovalStatus);
        Assert.Equal("Ready", r.Data.LocalAdoptionStatus);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.True(readiness.Data!.LocalApprovalReady);
        Assert.True(readiness.Data.LocalUseAllowed);
    }

    [Fact]
    public async Task Local_approval_rejection_blocks_local_adoption()
    {
        var f = Fixture();
        var v = await SiteAdoptedVariantAsync(f);

        var r = await f.Service.RejectLocalApprovalAsync(v,
            new RejectVariantReviewInput("Local annex missing", null), Corr, CancellationToken.None);

        Assert.Equal("Rejected", r.Data!.LocalApprovalStatus);
        Assert.Equal("Blocked", r.Data.LocalAdoptionStatus);
    }

    // ── local effective date ──────────────────────────────────────────────────

    [Fact]
    public async Task Local_effective_date_before_parent_effective_date_is_blocked()
    {
        var f = Fixture();
        var v = SeedVariant(f);
        f.Masters.Items.Single().EffectiveDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None);
        await CompleteReviewAsync(f, v.Id);

        var r = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { LocalEffectiveDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero) },
            Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(VariantLocalizationReasonCodes.LocalEffectiveBeforeParent, r.ReasonCode);
    }

    [Fact]
    public async Task Local_effective_date_requires_completed_review_and_approval()
    {
        var f = Fixture();
        var v = SeedVariant(f);
        await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None); // review still Pending

        var r = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { LocalEffectiveDate = DateTimeOffset.UtcNow }, Corr, CancellationToken.None);

        Assert.Equal(VariantLocalizationReasonCodes.LocalEffectiveNotReady, r.ReasonCode);
    }

    [Fact]
    public async Task Local_effective_date_is_accepted_once_review_is_complete()
    {
        var f = Fixture();
        var v = SeedVariant(f);
        f.Masters.Items.Single().EffectiveDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None);
        await CompleteReviewAsync(f, v.Id);

        var localEffective = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var r = await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { LocalEffectiveDate = localEffective }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(localEffective, r.Data!.LocalEffectiveDate);
    }

    // ── temporary English master ──────────────────────────────────────────────

    [Fact]
    public async Task Temporary_english_master_allowance_requires_evidence_approver_and_expiry()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);

        var noEvidence = await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Urgent rollout", "GQD", DateTimeOffset.UtcNow.AddDays(30), ""), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.TemporaryEnglishEvidenceRequired, noEvidence.ReasonCode);

        var noApprover = await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Urgent rollout", " ", DateTimeOffset.UtcNow.AddDays(30), "TE-1"), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.TemporaryEnglishApproverRequired, noApprover.ReasonCode);

        var pastExpiry = await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Urgent rollout", "GQD", DateTimeOffset.UtcNow.AddDays(-1), "TE-1"), Corr, CancellationToken.None);
        Assert.Equal(VariantLocalizationReasonCodes.TemporaryEnglishExpiryRequired, pastExpiry.ReasonCode);
    }

    [Fact]
    public async Task Local_language_mandatory_blocks_use_until_the_variant_is_ready()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.False(readiness.Data!.LocalUseAllowed);
        Assert.Contains("LOCAL_LANGUAGE_MANDATORY_VARIANT_NOT_READY", readiness.Data.BlockingReasons);
    }

    [Fact]
    public async Task Local_language_mandatory_allows_use_with_a_valid_temporary_english_allowance()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);

        await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Translation in progress", "GQD", DateTimeOffset.UtcNow.AddDays(30), "TE-1"),
            Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.True(readiness.Data!.LocalUseAllowed);
        Assert.True(readiness.Data.TemporaryEnglishMasterActive);
        Assert.Contains("LOCAL_USE_UNDER_TEMPORARY_ENGLISH_MASTER_ALLOWANCE", readiness.Data.WarningReasons);
    }

    [Fact]
    public async Task Expired_temporary_english_master_blocks_local_use()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);
        await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Translation in progress", "GQD", DateTimeOffset.UtcNow.AddDays(30), "TE-1"),
            Corr, CancellationToken.None);

        // Wind the allowance into the past.
        f.Profiles.Items.Single().TemporaryEnglishMasterExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.False(readiness.Data!.LocalUseAllowed);
        Assert.False(readiness.Data.TemporaryEnglishMasterActive);
        Assert.Contains("TEMPORARY_ENGLISH_MASTER_ALLOWANCE_EXPIRED", readiness.Data.WarningReasons);
    }

    [Fact]
    public async Task Revoking_the_temporary_allowance_blocks_local_use_again()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);
        await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Translation in progress", "GQD", DateTimeOffset.UtcNow.AddDays(30), "TE-1"),
            Corr, CancellationToken.None);

        await f.Service.RevokeTemporaryEnglishMasterAsync(v, Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.LocalUseAllowed);

        // The original allowance evidence survives the revocation.
        Assert.Contains(f.Evidence.Items, e => e.EvidenceType == VariantReviewEvidenceType.TemporaryEnglishMasterAllowance);
    }

    [Fact]
    public async Task Optional_local_language_does_not_block_use_when_the_variant_is_unready()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f); // translation, but local language NOT mandatory

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        // The source-language master remains usable; the gaps are warnings, not blocks.
        Assert.True(readiness.Data!.LocalUseAllowed);
        Assert.False(readiness.Data.TranslationReady);
        Assert.Contains("BILINGUAL_REVIEW_NOT_COMPLETED", readiness.Data.WarningReasons);
        Assert.Empty(readiness.Data.BlockingReasons);
    }

    // ── parent change assessment ──────────────────────────────────────────────

    [Fact]
    public async Task Parent_in_sync_produces_an_in_sync_assessment()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        var r = await f.ParentEvaluator.EvaluateAsync(v, "PCA-1", Corr, CancellationToken.None);

        Assert.Equal("InSync", r.Data!.AssessmentStatus);
        Assert.False(r.Data.RequiresVariantRevision);
        Assert.Equal("Effective", r.Data.ObservedParentStatus);
    }

    [Fact]
    public async Task Parent_version_change_marks_the_variant_outdated_and_revision_required()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);
        await CompleteReviewAsync(f, v); // translation was Ready...

        AdvanceMasterVersion(f, 2); // ...then the master publishes v2

        var r = await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        Assert.Equal("TranslationUpdateRequired", r.Data!.AssessmentStatus);
        Assert.True(r.Data.RequiresVariantRevision);
        Assert.True(r.Data.RequiresBilingualReview);

        var profile = f.Profiles.Items.Single();
        Assert.Equal(ParentChangeStatus.ParentUpdated, profile.ParentChangeStatus);
        Assert.Equal(TranslationReadinessStatus.Outdated, profile.TranslationReadinessStatus);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.ParentCurrent);
        Assert.False(readiness.Data.TranslationReady);
    }

    [Fact]
    public async Task Parent_superseded_blocks_local_use_and_requires_suspension()
    {
        var f = Fixture();
        var v = await LinkedRegisterVariantAsync(f, ControlledDocumentLifecycleStatus.Superseded);

        var r = await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        Assert.Equal("SuspensionRequired", r.Data!.AssessmentStatus);
        Assert.True(r.Data.RequiresSuspension);
        Assert.Equal("Superseded", r.Data.ObservedParentStatus);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.LocalUseAllowed);
        Assert.Contains("PARENT_PARENTSUPERSEDED", readiness.Data.BlockingReasons);
    }

    [Fact]
    public async Task Parent_suspended_blocks_local_use()
    {
        var f = Fixture();
        var v = await LinkedRegisterVariantAsync(f, ControlledDocumentLifecycleStatus.Suspended);

        await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.LocalUseAllowed);
        Assert.Equal(LocalAdoptionStatus.Suspended, f.Profiles.Items.Single().LocalAdoptionStatus);
    }

    [Fact]
    public async Task Parent_retired_blocks_local_use()
    {
        var f = Fixture();
        var v = await LinkedRegisterVariantAsync(f, ControlledDocumentLifecycleStatus.Retired);

        await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);
        Assert.False(readiness.Data!.LocalUseAllowed);
        Assert.Contains("PARENT_PARENTRETIRED", readiness.Data.BlockingReasons);
    }

    [Fact]
    public async Task A_dead_parent_cannot_be_overridden_by_a_temporary_english_allowance()
    {
        var f = Fixture();
        var v = await LinkedRegisterVariantAsync(f, ControlledDocumentLifecycleStatus.Superseded);
        await f.Service.AllowTemporaryEnglishMasterAsync(v,
            new AllowTemporaryEnglishMasterInput("Urgent", "GQD", DateTimeOffset.UtcNow.AddDays(30), "TE-1"),
            Corr, CancellationToken.None);
        await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.False(readiness.Data!.LocalUseAllowed);
    }

    [Fact]
    public async Task Parent_change_assessment_history_is_preserved()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        await f.ParentEvaluator.EvaluateAsync(v, "PCA-1", Corr, CancellationToken.None);
        AdvanceMasterVersion(f, 2);
        await f.ParentEvaluator.EvaluateAsync(v, "PCA-2", Corr, CancellationToken.None);

        var history = await f.ParentEvaluator.GetAssessmentsAsync(v, Corr, CancellationToken.None);
        Assert.Equal(2, history.Data!.Count);
        Assert.DoesNotContain(f.Assessments.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Parent_change_evaluation_never_mutates_the_variant_aggregate()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);
        var before = f.Variants.Items.Single();
        var (status, contentSource, lastRebased, linkedTemplate) =
            (before.Status, before.ContentSource, before.LastRebasedMasterVersionNumber, before.LinkedTemplateDocumentId);

        AdvanceMasterVersion(f, 3);
        await f.ParentEvaluator.EvaluateAsync(v, null, Corr, CancellationToken.None);

        var after = f.Variants.Items.Single();
        Assert.Equal(status, after.Status);
        Assert.Equal(contentSource, after.ContentSource);
        Assert.Equal(lastRebased, after.LastRebasedMasterVersionNumber);
        Assert.Equal(linkedTemplate, after.LinkedTemplateDocumentId);
    }

    // ── readiness surface ─────────────────────────────────────────────────────

    [Fact]
    public async Task Readiness_returns_blocking_and_warning_reasons()
    {
        var f = Fixture();
        var v = await MandatoryLanguageVariantAsync(f);

        var r = await f.Service.GetReadinessAsync(v, Corr, CancellationToken.None);

        Assert.NotEmpty(r.Data!.BlockingReasons);
        Assert.Contains("BILINGUAL_REVIEW_NOT_COMPLETED", r.Data.BlockingReasons);
        Assert.Contains("LOCAL_APPROVAL_NOT_COMPLETED", r.Data.BlockingReasons);
        Assert.Contains("metadata and evidence only", r.Data.BoundaryStatement);
    }

    /// <summary>A variant that never opted into FU18 governance must behave exactly as it did before.</summary>
    [Fact]
    public async Task Variant_without_a_localization_profile_is_unaffected()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var readiness = await f.Service.GetReadinessAsync(v.Id, Corr, CancellationToken.None);

        Assert.True(readiness.Data!.LocalUseAllowed);
        Assert.True(readiness.Data.TranslationReady);
        Assert.True(readiness.Data.LocalApprovalReady);
        Assert.True(readiness.Data.ParentCurrent);
        Assert.Empty(readiness.Data.BlockingReasons);
        Assert.Empty(readiness.Data.WarningReasons);
        Assert.Empty(f.Profiles.Items);
    }

    [Fact]
    public async Task Parent_change_evaluation_without_a_profile_is_refused_cleanly()
    {
        var f = Fixture();
        var v = SeedVariant(f);

        var r = await f.ParentEvaluator.EvaluateAsync(v.Id, null, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(VariantLocalizationReasonCodes.ProfileNotFound, r.ReasonCode);
        Assert.Empty(f.Assessments.Items);
    }

    // ── evidence trail ────────────────────────────────────────────────────────

    [Fact]
    public async Task Evidence_history_is_append_only_with_no_hard_delete()
    {
        var f = Fixture();
        var v = await TranslationVariantAsync(f);

        await f.Service.RejectBilingualReviewAsync(v, new RejectVariantReviewInput("First pass poor", null), Corr, CancellationToken.None);
        await CompleteReviewAsync(f, v);

        var evidence = await f.Service.GetEvidenceAsync(v, Corr, CancellationToken.None);

        // The rejection is NOT erased by the later approval — both decisions remain visible.
        Assert.Equal(2, evidence.Data!.Count);
        Assert.Contains(evidence.Data, e => e.Status == "Rejected");
        Assert.Contains(evidence.Data, e => e.Status == "Completed");
        Assert.DoesNotContain(f.Evidence.Items, e => e.IsDeleted);
    }

    [Fact]
    public void No_variant_localization_repository_contract_exposes_a_delete_operation()
    {
        var contracts = new[]
        {
            typeof(ITemplateVariantLocalizationProfileRepository),
            typeof(ITemplateVariantReviewEvidenceRepository),
            typeof(ITemplateVariantParentChangeAssessmentRepository)
        };

        foreach (var contract in contracts)
        {
            Assert.DoesNotContain(contract.GetMethods(), m =>
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>No FU18 aggregate can carry translated content — this module stores references, not documents.</summary>
    [Fact]
    public void No_variant_localization_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(TemplateVariantLocalizationProfile), typeof(TemplateVariantReviewEvidence),
            typeof(TemplateVariantParentChangeAssessment)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    // ── isolation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_variant_profile_is_blocked()
    {
        var f = Fixture();
        var foreign = new TemplateVariant
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, TemplateMasterId = Guid.NewGuid(),
            TemplateMasterVersionId = Guid.NewGuid(), VariantCode = "FOREIGN", VariantName = "Foreign variant",
            ScopeId = Guid.NewGuid()
        };
        f.Variants.Items.Add(foreign);

        var upsert = await f.Service.UpsertProfileAsync(foreign.Id, Translation(), Corr, CancellationToken.None);
        var readiness = await f.Service.GetReadinessAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.Equal(404, upsert.StatusCode);
        Assert.Equal(404, readiness.StatusCode);
        Assert.Empty(f.Profiles.Items);
    }

    [Fact]
    public async Task Cross_tenant_profile_is_not_readable()
    {
        var f = Fixture();
        var v = SeedVariant(f);
        f.Profiles.Items.Add(new TemplateVariantLocalizationProfile
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, TemplateVariantId = v.Id, IsTranslationVariant = true
        });

        var r = await f.Service.GetProfileAsync(v.Id, Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
        Assert.Equal(VariantLocalizationReasonCodes.ProfileNotFound, r.ReasonCode);
    }

    /// <summary>FU18 appended its subject types to the FU15 retention vocabulary without shifting any ordinal.</summary>
    [Fact]
    public void Retention_subject_types_include_variant_evidence_without_shifting_existing_ordinals()
    {
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(26, (int)RetentionSubjectType.ExternalDocumentInternalLink);
        Assert.Equal(28, (int)RetentionSubjectType.TemplateVariantLocalizationProfile);
        Assert.Equal(29, (int)RetentionSubjectType.TemplateVariantReviewEvidence);
        Assert.Equal(30, (int)RetentionSubjectType.TemplateVariantParentChangeAssessment);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Response_> CompleteReviewAsync(Harness f, Guid variantId) => new(
        await f.Service.RecordBilingualReviewAsync(variantId,
            new RecordBilingualReviewInput(Reviewer, "QA Translator", "BR-1", "Verified against source"),
            Corr, CancellationToken.None));

    /// <summary>Tiny alias so the assertions read naturally without repeating the long generic.</summary>
    private sealed record Response_(Diten.Platform.Application.Common.Response<VariantLocalizationProfileModel> Result)
    {
        public VariantLocalizationProfileModel? Data => Result.Data;
    }

    private async Task<Guid> TranslationVariantAsync(Harness f)
    {
        var v = SeedVariant(f);
        await f.Service.UpsertProfileAsync(v.Id, Translation(), Corr, CancellationToken.None);
        return v.Id;
    }

    private async Task<Guid> SiteAdoptedVariantAsync(Harness f)
    {
        var v = SeedVariant(f);
        await f.Service.UpsertProfileAsync(v.Id, SiteAdopted(), Corr, CancellationToken.None);
        return v.Id;
    }

    private async Task<Guid> MandatoryLanguageVariantAsync(Harness f)
    {
        var v = SeedVariant(f);
        await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { IsSiteAdoptedVariant = true, CountryCode = "TR", IsLocalLanguageMandatory = true },
            Corr, CancellationToken.None);
        return v.Id;
    }

    private async Task<Guid> LinkedRegisterVariantAsync(Harness f, ControlledDocumentLifecycleStatus parentStatus)
    {
        var v = SeedVariant(f);
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop, DocumentType = DocumentType.Sop,
            Criticality = DocumentCriticality.Critical, LifecycleStatus = parentStatus,
            RegisterStatus = DocumentRegisterStatus.Active, PermanentUid = "UID-0000001",
            DocumentCode = "GMG-QMS-SOP-0001"
        };
        f.Register.Items.Add(entry);
        await f.Service.UpsertProfileAsync(v.Id,
            Translation() with { ParentRegisterEntryId = entry.Id }, Corr, CancellationToken.None);
        return v.Id;
    }

    private static void AdvanceMasterVersion(Harness f, int versionNumber)
    {
        var master = f.Masters.Items.Single();
        master.CurrentMasterVersion = versionNumber;
        master.CurrentVersionId = Guid.NewGuid();
    }

    private static VariantLocalizationProfileInput Translation() => new(
        VariantIdentifier: "VAR-TR-01",
        VariantLanguageCode: "tr",
        VariantLanguageName: "Turkish",
        SourceLanguageCode: "en",
        CountryCode: null,
        SiteCode: null,
        IsTranslationVariant: true,
        IsSiteAdoptedVariant: false,
        IsLocalLanguageMandatory: false,
        ParentTemplateMasterId: null,
        ParentTemplateMasterVersionId: null,
        ParentRegisterEntryId: null,
        ParentDocumentUid: "UID-0000001",
        ParentDocumentCode: "GMG-QMS-SOP-0001",
        ParentVersionLabel: "1.0",
        LocalDocumentRegisterEntryId: null,
        AuthorUserId: Author,
        BilingualReviewerUserId: null,
        BilingualReviewerRole: null,
        LocalApproverUserId: null,
        LocalApproverRole: null,
        LocalEffectiveDate: null);

    private static VariantLocalizationProfileInput SiteAdopted() => Translation() with
    {
        IsTranslationVariant = false,
        IsSiteAdoptedVariant = true,
        VariantLanguageCode = null,
        CountryCode = "TR",
        SiteCode = "IST-01"
    };

    private static TemplateVariant SeedVariant(Harness f)
    {
        var master = f.Masters.Items.SingleOrDefault();
        if (master is null)
        {
            master = new TemplateMaster
            {
                Id = Guid.NewGuid(), TenantId = TenantId, MasterCode = "TM-0001", TemplateName = "SOP Template",
                Classification = "Sop", Status = TemplateMasterStatus.Published, CurrentMasterVersion = 1,
                CurrentVersionId = Guid.NewGuid(), EffectiveDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            };
            f.Masters.Items.Add(master);
        }

        var variant = new TemplateVariant
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TemplateMasterId = master.Id,
            TemplateMasterVersionId = master.CurrentVersionId!.Value,
            VariantCode = $"VAR-{f.Variants.Items.Count + 1:D3}",
            VariantName = "Turkish variant",
            ScopeType = TemplateVariantScopeType.Site,
            ScopeId = Guid.NewGuid(),
            Status = TemplateVariantStatus.Active,
            ContentSource = TemplateVariantContentSource.MasterVersion,
            LastRebasedMasterVersionNumber = master.CurrentMasterVersion,
            LastRebasedMasterVersionId = master.CurrentVersionId
        };
        f.Variants.Items.Add(variant);
        return variant;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var variants = new FakeVariantRepo(tenant);
        var profiles = new FakeProfileRepo(tenant);
        var evidence = new FakeEvidenceRepo(tenant);
        var assessments = new FakeAssessmentRepo(tenant);
        var masters = new FakeMasterRepo(tenant);
        var register = new FakeRegisterRepo(tenant);

        return new Harness(
            new TemplateVariantLocalizationService(variants, profiles, evidence, masters, tenant, user),
            new TemplateVariantParentChangeEvaluator(variants, profiles, assessments, masters, register, tenant, user),
            variants, profiles, evidence, assessments, masters, register);
    }

    private sealed record Harness(
        TemplateVariantLocalizationService Service,
        TemplateVariantParentChangeEvaluator ParentEvaluator,
        FakeVariantRepo Variants,
        FakeProfileRepo Profiles,
        FakeEvidenceRepo Evidence,
        FakeAssessmentRepo Assessments,
        FakeMasterRepo Masters,
        FakeRegisterRepo Register);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444418");
        public string? Email => "fu18@example.test";
        public string? DisplayName => "FU18 Tester";
        public string ActorName => "fu18@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeVariantRepo(ITenantContext tenant) : ITemplateVariantRepository
    {
        public List<TemplateVariant> Items { get; } = [];
        private IEnumerable<TemplateVariant> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemplateVariant> CreateAsync(TemplateVariant v, CancellationToken ct = default) { Items.Add(v); return Task.FromResult(v); }
        public Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<TemplateVariant?> GetByScopeAndCodeAsync(TemplateVariantScopeType scopeType, Guid scopeId, string variantCode, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.ScopeType == scopeType && x.ScopeId == scopeId && x.VariantCode == variantCode));
        public Task<IReadOnlyList<TemplateVariant>> ListAsync(Guid? templateMasterId, string? scopeType, Guid? scopeId, string? status, string? approvalStatus, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariant>>(Scoped.ToList());
        public Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariant>>(Scoped.Where(x => x.TemplateMasterId == templateMasterId).ToList());
        public Task<bool> UpdateAsync(TemplateVariant v, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == v.Id);
            if (i >= 0) Items[i] = v;
            return Task.FromResult(i >= 0);
        }
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var found = Items.FirstOrDefault(x => x.Id == id);
            if (found is not null) found.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileRepo(ITenantContext tenant) : ITemplateVariantLocalizationProfileRepository
    {
        public List<TemplateVariantLocalizationProfile> Items { get; } = [];
        private IEnumerable<TemplateVariantLocalizationProfile> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemplateVariantLocalizationProfile> CreateAsync(TemplateVariantLocalizationProfile p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<TemplateVariantLocalizationProfile?> GetByVariantAsync(Guid variantId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.TemplateVariantId == variantId));
        public Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetByParentMasterAsync(Guid masterId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariantLocalizationProfile>>(Scoped.Where(x => x.ParentTemplateMasterId == masterId).ToList());
        public Task<IReadOnlyList<TemplateVariantLocalizationProfile>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariantLocalizationProfile>>(Scoped.ToList());
        public Task<bool> UpdateAsync(TemplateVariantLocalizationProfile p, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == p.Id);
            if (i >= 0) Items[i] = p;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeEvidenceRepo(ITenantContext tenant) : ITemplateVariantReviewEvidenceRepository
    {
        public List<TemplateVariantReviewEvidence> Items { get; } = [];
        private IEnumerable<TemplateVariantReviewEvidence> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemplateVariantReviewEvidence> CreateAsync(TemplateVariantReviewEvidence e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<TemplateVariantReviewEvidence>> GetByVariantAsync(Guid variantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariantReviewEvidence>>(Scoped.Where(x => x.TemplateVariantId == variantId).ToList());
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : ITemplateVariantParentChangeAssessmentRepository
    {
        public List<TemplateVariantParentChangeAssessment> Items { get; } = [];
        private IEnumerable<TemplateVariantParentChangeAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemplateVariantParentChangeAssessment> CreateAsync(TemplateVariantParentChangeAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<IReadOnlyList<TemplateVariantParentChangeAssessment>> GetByVariantAsync(Guid variantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateVariantParentChangeAssessment>>(
                Scoped.Where(x => x.TemplateVariantId == variantId).OrderByDescending(x => x.AssessedAt).ToList());
        public Task<TemplateVariantParentChangeAssessment?> GetLatestAsync(Guid variantId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Where(x => x.TemplateVariantId == variantId).OrderByDescending(x => x.AssessedAt).FirstOrDefault());
    }

    private sealed class FakeMasterRepo(ITenantContext tenant) : ITemplateMasterRepository
    {
        public List<TemplateMaster> Items { get; } = [];
        private IEnumerable<TemplateMaster> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<TemplateMaster> CreateAsync(TemplateMaster m, CancellationToken ct = default) { Items.Add(m); return Task.FromResult(m); }
        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<TemplateMaster?> GetByMasterCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.MasterCode == code));
        public Task<IReadOnlyList<TemplateMaster>> ListAsync(string? status, string? classification, Guid? collectionDefinitionId, string? canonicalId, string? variantPolicy, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemplateMaster>>(Scoped.ToList());
        public Task<bool> UpdateAsync(TemplateMaster m, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == m.Id);
            if (i >= 0) Items[i] = m;
            return Task.FromResult(i >= 0);
        }
        public Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.Count(x => x.CurrentVersionId == templateMasterVersionId));
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var found = Items.FirstOrDefault(x => x.Id == id);
            if (found is not null) found.IsDeleted = true;
            return Task.CompletedTask;
        }
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            var affected = Items.Where(x => ids.Contains(x.Id)).ToList();
            foreach (var found in affected) found.IsDeleted = true;
            return Task.FromResult(affected.Count);
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
