using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Services;

/// <summary>
/// MOD-0029-FU18 — variant translation / site-adoption governance (GMG-QMS-SOP-0001 §13.2): the localization
/// profile, the bilingual review trail, the local approval trail and the temporary English master allowance.
///
/// SOP controls enforced here:
/// • A translation variant must declare its language; a site-adopted variant must declare a country or site.
/// • Parent linkage is mandatory and is never allowed to be dropped by an update.
/// • Completing a bilingual review requires a named reviewer AND an evidence reference; the variant's author may
///   not be its sole bilingual reviewer.
/// • Completing a local approval requires a named approver AND an evidence reference.
/// • A local effective date may not precede the parent's effective date unless a valid temporary English master
///   allowance is in force, and may not be set before the required review/approval are complete.
/// • A temporary English master allowance requires justification, an approver, an expiry and evidence — an
///   expired allowance is the same as none.
///
/// EXPLICIT NON-BEHAVIOURS: no e-signature, no content or binary comparison, no machine translation, no CAPA
/// record, no MOD-0023 workflow. Nothing here mutates the FU03 TemplateVariant aggregate, its drift computation,
/// its create/rebase/compare behaviour, or the parent master. Evidence is append-only and never hard-deleted.
/// </summary>
public sealed class TemplateVariantLocalizationService
{
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateVariantLocalizationProfileRepository _profiles;
    private readonly ITemplateVariantReviewEvidenceRepository _evidence;
    private readonly ITemplateMasterRepository _masters;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public TemplateVariantLocalizationService(
        ITemplateVariantRepository variants,
        ITemplateVariantLocalizationProfileRepository profiles,
        ITemplateVariantReviewEvidenceRepository evidence,
        ITemplateMasterRepository masters,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _variants = variants;
        _profiles = profiles;
        _evidence = evidence;
        _masters = masters;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── profile ───────────────────────────────────────────────────────────────

    /// <summary>Creates or replaces the variant's localization profile (idempotent upsert).</summary>
    public async Task<Response<VariantLocalizationProfileModel>> UpsertProfileAsync(
        Guid variantId, VariantLocalizationProfileInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return Fail("Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId);
        }

        if (Validate(input, variant) is { } failure)
        {
            return Fail(failure.Message, 400, failure.ReasonCode, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var profile = await _profiles.GetByVariantAsync(variantId, ct);
        var isNew = profile is null;
        profile ??= new TemplateVariantLocalizationProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateVariantId = variantId,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        // Local effective date is validated against the parent before anything is persisted.
        if (input.LocalEffectiveDate is { } localEffective)
        {
            var guard = await ValidateLocalEffectiveDateAsync(profile, variant, localEffective, now, correlationId, ct);
            if (guard is not null)
            {
                return guard;
            }
        }

        Apply(profile, input, variant);
        ApplyRequirementDefaults(profile);

        if (isNew)
        {
            await _profiles.CreateAsync(profile, ct);
        }
        else
        {
            profile.UpdatedAt = now;
            profile.UpdatedBy = _currentUser.ActorName;
            await _profiles.UpdateAsync(profile, ct);
        }

        return Response<VariantLocalizationProfileModel>.Success(
            VariantLocalizationWire.ToProfile(profile), isNew ? 201 : 200, correlationId);
    }

    public async Task<Response<VariantLocalizationProfileModel>> GetProfileAsync(Guid variantId, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        return fail ?? Response<VariantLocalizationProfileModel>.Success(
            VariantLocalizationWire.ToProfile(profile!), correlationId: correlationId);
    }

    // ── bilingual review ──────────────────────────────────────────────────────

    /// <summary>Marks a bilingual review as required and moves it to Pending.</summary>
    public async Task<Response<VariantLocalizationProfileModel>> RequireBilingualReviewAsync(
        Guid variantId, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        profile!.RequiresBilingualReview = true;
        if (profile.BilingualReviewStatus is BilingualReviewStatus.NotRequired)
        {
            profile.BilingualReviewStatus = BilingualReviewStatus.Pending;
        }

        if (profile.TranslationReadinessStatus == TranslationReadinessStatus.NotRequired)
        {
            profile.TranslationReadinessStatus = TranslationReadinessStatus.Pending;
        }

        return await PersistAsync(profile, correlationId, ct);
    }

    public async Task<Response<VariantLocalizationProfileModel>> RecordBilingualReviewAsync(
        Guid variantId, RecordBilingualReviewInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var reviewer = input.ReviewerUserId ?? profile!.BilingualReviewerUserId;
        var reviewerRole = Trim(input.ReviewerRole) ?? profile!.BilingualReviewerRole;
        if (reviewer is null && string.IsNullOrWhiteSpace(reviewerRole))
        {
            return Fail("A named bilingual reviewer (user or role) is required.", 400,
                VariantLocalizationReasonCodes.ReviewerRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return Fail("A bilingual review evidence reference is required.", 400,
                VariantLocalizationReasonCodes.ReviewEvidenceRequired, correlationId);
        }

        // SOP segregation: the author cannot be the sole verifier of their own translation.
        if (profile!.AuthorUserId is { } author && reviewer is { } r && author == r)
        {
            return Fail("The variant author cannot be the sole bilingual reviewer.", 409,
                VariantLocalizationReasonCodes.ReviewerCannotBeAuthor, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        profile.RequiresBilingualReview = true;
        profile.BilingualReviewerUserId = reviewer;
        profile.BilingualReviewerRole = reviewerRole;
        profile.BilingualReviewStatus = BilingualReviewStatus.Completed;
        profile.BilingualReviewEvidenceReference = input.EvidenceReference.Trim();
        profile.BilingualReviewCompletedAt = now;

        // A completed review clears an Outdated/Pending translation, unless the parent itself has stopped.
        if (profile.ParentChangeStatus is ParentChangeStatus.InSync or ParentChangeStatus.ParentUpdated)
        {
            profile.TranslationReadinessStatus = TranslationReadinessStatus.Ready;
        }

        await AppendEvidenceAsync(profile, VariantReviewEvidenceType.BilingualReview,
            VariantReviewEvidenceStatus.Completed, reviewer, reviewerRole, input.EvidenceReference, input.Comment, now, correlationId, ct);

        return await PersistAsync(profile, correlationId, ct);
    }

    public async Task<Response<VariantLocalizationProfileModel>> RejectBilingualReviewAsync(
        Guid variantId, RejectVariantReviewInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, VariantLocalizationReasonCodes.ReasonRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        profile!.BilingualReviewStatus = BilingualReviewStatus.Rejected;
        profile.TranslationReadinessStatus = TranslationReadinessStatus.Blocked;
        profile.BilingualReviewCompletedAt = null;

        await AppendEvidenceAsync(profile, VariantReviewEvidenceType.BilingualReview,
            VariantReviewEvidenceStatus.Rejected, profile.BilingualReviewerUserId, profile.BilingualReviewerRole,
            input.EvidenceReference ?? "REJECTED", input.Reason, now, correlationId, ct);

        return await PersistAsync(profile, correlationId, ct);
    }

    // ── local approval ────────────────────────────────────────────────────────

    public async Task<Response<VariantLocalizationProfileModel>> RequireLocalApprovalAsync(
        Guid variantId, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        profile!.RequiresLocalApproval = true;
        if (profile.LocalApprovalStatus is LocalApprovalStatus.NotRequired)
        {
            profile.LocalApprovalStatus = LocalApprovalStatus.Pending;
        }

        if (profile.LocalAdoptionStatus == LocalAdoptionStatus.NotRequired)
        {
            profile.LocalAdoptionStatus = LocalAdoptionStatus.Pending;
        }

        return await PersistAsync(profile, correlationId, ct);
    }

    public async Task<Response<VariantLocalizationProfileModel>> RecordLocalApprovalAsync(
        Guid variantId, RecordLocalApprovalInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var approver = input.ApproverUserId ?? profile!.LocalApproverUserId;
        var approverRole = Trim(input.ApproverRole) ?? profile!.LocalApproverRole;
        if (approver is null && string.IsNullOrWhiteSpace(approverRole))
        {
            return Fail("A named local approver (user or role) is required.", 400,
                VariantLocalizationReasonCodes.ApproverRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return Fail("A local approval evidence reference is required.", 400,
                VariantLocalizationReasonCodes.ApprovalEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        profile!.RequiresLocalApproval = true;
        profile.LocalApproverUserId = approver;
        profile.LocalApproverRole = approverRole;
        profile.LocalApprovalStatus = LocalApprovalStatus.Completed;
        profile.LocalApprovalEvidenceReference = input.EvidenceReference.Trim();
        profile.LocalApprovalCompletedAt = now;

        if (profile.LocalAdoptionStatus is LocalAdoptionStatus.Pending or LocalAdoptionStatus.Blocked)
        {
            profile.LocalAdoptionStatus = LocalAdoptionStatus.Ready;
        }

        await AppendEvidenceAsync(profile, VariantReviewEvidenceType.LocalApproval,
            VariantReviewEvidenceStatus.Completed, approver, approverRole, input.EvidenceReference, input.Comment, now, correlationId, ct);

        return await PersistAsync(profile, correlationId, ct);
    }

    public async Task<Response<VariantLocalizationProfileModel>> RejectLocalApprovalAsync(
        Guid variantId, RejectVariantReviewInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, VariantLocalizationReasonCodes.ReasonRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        profile!.LocalApprovalStatus = LocalApprovalStatus.Rejected;
        profile.LocalAdoptionStatus = LocalAdoptionStatus.Blocked;
        profile.LocalApprovalCompletedAt = null;

        await AppendEvidenceAsync(profile, VariantReviewEvidenceType.LocalApproval,
            VariantReviewEvidenceStatus.Rejected, profile.LocalApproverUserId, profile.LocalApproverRole,
            input.EvidenceReference ?? "REJECTED", input.Reason, now, correlationId, ct);

        return await PersistAsync(profile, correlationId, ct);
    }

    // ── temporary English master allowance ────────────────────────────────────

    /// <summary>SOP §13.2 — the temporary English master is a conditional, evidenced, time-boxed exception.</summary>
    public async Task<Response<VariantLocalizationProfileModel>> AllowTemporaryEnglishMasterAsync(
        Guid variantId, AllowTemporaryEnglishMasterInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            return Fail("Evidence is required to allow a temporary English master.", 400,
                VariantLocalizationReasonCodes.TemporaryEnglishEvidenceRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ApprovedBy))
        {
            return Fail("An approver is required to allow a temporary English master.", 400,
                VariantLocalizationReasonCodes.TemporaryEnglishApproverRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        if (input.ExpiresAt <= now)
        {
            return Fail("A temporary English master allowance requires a future expiry date.", 400,
                VariantLocalizationReasonCodes.TemporaryEnglishExpiryRequired, correlationId);
        }

        profile!.TemporaryEnglishMasterAllowed = true;
        profile.TemporaryEnglishMasterJustification = Trim(input.Justification);
        profile.TemporaryEnglishMasterApprovedBy = input.ApprovedBy.Trim();
        profile.TemporaryEnglishMasterExpiresAt = input.ExpiresAt;
        profile.TemporaryEnglishMasterEvidenceReference = input.EvidenceReference.Trim();

        await AppendEvidenceAsync(profile, VariantReviewEvidenceType.TemporaryEnglishMasterAllowance,
            VariantReviewEvidenceStatus.Completed, null, null, input.EvidenceReference, input.Justification, now, correlationId, ct);

        return await PersistAsync(profile, correlationId, ct);
    }

    /// <summary>Revocation clears the allowance but keeps the original allowance evidence in the trail.</summary>
    public async Task<Response<VariantLocalizationProfileModel>> RevokeTemporaryEnglishMasterAsync(
        Guid variantId, string correlationId, CancellationToken ct)
    {
        var (fail, _, profile) = await LoadAsync(variantId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        profile!.TemporaryEnglishMasterAllowed = false;
        profile.TemporaryEnglishMasterExpiresAt = null;
        return await PersistAsync(profile, correlationId, ct);
    }

    // ── readiness + evidence ──────────────────────────────────────────────────

    public async Task<Response<VariantReadinessModel>> GetReadinessAsync(Guid variantId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return Response<VariantReadinessModel>.Fail(
                "Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId);
        }

        var profile = await _profiles.GetByVariantAsync(variantId, ct);
        return Response<VariantReadinessModel>.Success(
            TemplateVariantReadinessEvaluator.Evaluate(profile, variantId, DateTimeOffset.UtcNow), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<VariantReviewEvidenceModel>>> GetEvidenceAsync(Guid variantId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return Response<IReadOnlyList<VariantReviewEvidenceModel>>.Fail(
                "Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId);
        }

        var rows = await _evidence.GetByVariantAsync(variantId, ct);
        return Response<IReadOnlyList<VariantReviewEvidenceModel>>.Success(
            rows.Select(VariantLocalizationWire.ToEvidence).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed record ValidationFailure(string Message, string ReasonCode);

    private static ValidationFailure? Validate(VariantLocalizationProfileInput i, TemplateVariant variant)
    {
        if (i.IsTranslationVariant && string.IsNullOrWhiteSpace(i.VariantLanguageCode))
        {
            return new ValidationFailure("A translation variant requires a variant language code.",
                VariantLocalizationReasonCodes.LanguageRequired);
        }

        if (i.IsSiteAdoptedVariant && string.IsNullOrWhiteSpace(i.CountryCode) && string.IsNullOrWhiteSpace(i.SiteCode))
        {
            return new ValidationFailure("A site-adopted variant requires a country code or a site code.",
                VariantLocalizationReasonCodes.CountryOrSiteRequired);
        }

        // Parent linkage must exist. The variant's own TemplateMasterId is an acceptable source, so this only
        // fails when neither the input nor the variant carries a parent.
        var hasParent = i.ParentTemplateMasterId is not null
                        || i.ParentRegisterEntryId is not null
                        || !string.IsNullOrWhiteSpace(i.ParentDocumentUid)
                        || !string.IsNullOrWhiteSpace(i.ParentDocumentCode)
                        || variant.TemplateMasterId != Guid.Empty;
        return hasParent
            ? null
            : new ValidationFailure("Parent linkage (master, register entry, uid or code) is required.",
                VariantLocalizationReasonCodes.ParentLinkageRequired);
    }

    /// <summary>
    /// SOP §13.2 — a local effective date may not precede the parent's effective date, and may not be set while a
    /// required bilingual review / local approval is still outstanding. A valid temporary English master allowance
    /// is the only exception to the date ordering.
    /// </summary>
    private async Task<Response<VariantLocalizationProfileModel>?> ValidateLocalEffectiveDateAsync(
        TemplateVariantLocalizationProfile profile,
        TemplateVariant variant,
        DateTimeOffset localEffective,
        DateTimeOffset now,
        string correlationId,
        CancellationToken ct)
    {
        var master = await _masters.GetByIdAsync(profile.ParentTemplateMasterId ?? variant.TemplateMasterId, ct);
        var parentEffective = master?.EffectiveDate ?? profile.ParentEffectiveDateAtLastAssessment;

        if (parentEffective is { } parent && localEffective < parent && !profile.HasValidTemporaryEnglishAllowance(now))
        {
            return Fail(
                $"The local effective date ({localEffective:yyyy-MM-dd}) cannot precede the parent effective date ({parent:yyyy-MM-dd}).",
                409, VariantLocalizationReasonCodes.LocalEffectiveBeforeParent, correlationId);
        }

        var reviewOutstanding = profile.RequiresBilingualReview && profile.BilingualReviewStatus != BilingualReviewStatus.Completed;
        var approvalOutstanding = profile.RequiresLocalApproval && profile.LocalApprovalStatus != LocalApprovalStatus.Completed;
        if (reviewOutstanding || approvalOutstanding)
        {
            return Fail("A local effective date requires the bilingual review and local approval to be complete.",
                409, VariantLocalizationReasonCodes.LocalEffectiveNotReady, correlationId);
        }

        return null;
    }

    private static void Apply(TemplateVariantLocalizationProfile p, VariantLocalizationProfileInput i, TemplateVariant variant)
    {
        p.VariantIdentifier = Trim(i.VariantIdentifier) ?? p.VariantIdentifier ?? variant.VariantCode;
        p.VariantLanguageCode = Trim(i.VariantLanguageCode);
        p.VariantLanguageName = Trim(i.VariantLanguageName);
        p.SourceLanguageCode = Trim(i.SourceLanguageCode);
        p.CountryCode = Trim(i.CountryCode);
        p.SiteCode = Trim(i.SiteCode);
        p.IsTranslationVariant = i.IsTranslationVariant;
        p.IsSiteAdoptedVariant = i.IsSiteAdoptedVariant;
        p.IsLocalLanguageMandatory = i.IsLocalLanguageMandatory;

        // Parent linkage falls back to the variant's own lineage and is never nulled out by an update.
        p.ParentTemplateMasterId = i.ParentTemplateMasterId ?? p.ParentTemplateMasterId ?? variant.TemplateMasterId;
        p.ParentTemplateMasterVersionId = i.ParentTemplateMasterVersionId ?? p.ParentTemplateMasterVersionId ?? variant.TemplateMasterVersionId;
        p.ParentRegisterEntryId = i.ParentRegisterEntryId ?? p.ParentRegisterEntryId;
        p.ParentDocumentUid = Trim(i.ParentDocumentUid) ?? p.ParentDocumentUid;
        p.ParentDocumentCode = Trim(i.ParentDocumentCode) ?? p.ParentDocumentCode;
        p.ParentVersionLabel = Trim(i.ParentVersionLabel) ?? p.ParentVersionLabel;
        p.LocalDocumentRegisterEntryId = i.LocalDocumentRegisterEntryId ?? p.LocalDocumentRegisterEntryId;

        p.AuthorUserId = i.AuthorUserId ?? p.AuthorUserId;
        p.BilingualReviewerUserId = i.BilingualReviewerUserId ?? p.BilingualReviewerUserId;
        p.BilingualReviewerRole = Trim(i.BilingualReviewerRole) ?? p.BilingualReviewerRole;
        p.LocalApproverUserId = i.LocalApproverUserId ?? p.LocalApproverUserId;
        p.LocalApproverRole = Trim(i.LocalApproverRole) ?? p.LocalApproverRole;
        p.LocalEffectiveDate = i.LocalEffectiveDate ?? p.LocalEffectiveDate;
    }

    /// <summary>
    /// Classification drives what is required. A translation (or a mandatory local language) needs a bilingual
    /// review; a site adoption needs a local approval. Statuses only move up from NotRequired — an already
    /// completed or rejected decision is never silently reset by a profile edit.
    /// </summary>
    private static void ApplyRequirementDefaults(TemplateVariantLocalizationProfile p)
    {
        if (p.IsTranslationVariant || p.IsLocalLanguageMandatory)
        {
            p.RequiresBilingualReview = true;
            if (p.BilingualReviewStatus == BilingualReviewStatus.NotRequired)
            {
                p.BilingualReviewStatus = BilingualReviewStatus.Pending;
            }

            if (p.TranslationReadinessStatus == TranslationReadinessStatus.NotRequired)
            {
                p.TranslationReadinessStatus = TranslationReadinessStatus.Pending;
            }
        }

        if (p.IsSiteAdoptedVariant || p.IsLocalLanguageMandatory)
        {
            p.RequiresLocalApproval = true;
            if (p.LocalApprovalStatus == LocalApprovalStatus.NotRequired)
            {
                p.LocalApprovalStatus = LocalApprovalStatus.Pending;
            }

            if (p.LocalAdoptionStatus == LocalAdoptionStatus.NotRequired)
            {
                p.LocalAdoptionStatus = LocalAdoptionStatus.Pending;
            }
        }
    }

    private async Task AppendEvidenceAsync(
        TemplateVariantLocalizationProfile profile,
        VariantReviewEvidenceType type,
        VariantReviewEvidenceStatus status,
        Guid? performedByUserId,
        string? performedByRole,
        string evidenceReference,
        string? comment,
        DateTimeOffset now,
        string correlationId,
        CancellationToken ct) =>
        await _evidence.CreateAsync(new TemplateVariantReviewEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            TemplateVariantId = profile.TemplateVariantId,
            EvidenceType = type,
            Status = status,
            PerformedByUserId = performedByUserId ?? _currentUser.UserId,
            PerformedByRole = performedByRole,
            PerformedAt = now,
            EvidenceReference = evidenceReference.Trim(),
            Comment = Trim(comment),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

    private async Task<Response<VariantLocalizationProfileModel>> PersistAsync(
        TemplateVariantLocalizationProfile profile, string correlationId, CancellationToken ct)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        profile.UpdatedBy = _currentUser.ActorName;
        await _profiles.UpdateAsync(profile, ct);
        return Response<VariantLocalizationProfileModel>.Success(
            VariantLocalizationWire.ToProfile(profile), correlationId: correlationId);
    }

    private async Task<(Response<VariantLocalizationProfileModel>? Fail, TemplateVariant? Variant, TemplateVariantLocalizationProfile? Profile)>
        LoadAsync(Guid variantId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return (Fail("Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId), null, null);
        }

        var profile = await _profiles.GetByVariantAsync(variantId, ct);
        return profile is null
            ? (Fail("This variant has no localization profile.", 404, VariantLocalizationReasonCodes.ProfileNotFound, correlationId), variant, null)
            : (null, variant, profile);
    }

    private static Response<VariantLocalizationProfileModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<VariantLocalizationProfileModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
