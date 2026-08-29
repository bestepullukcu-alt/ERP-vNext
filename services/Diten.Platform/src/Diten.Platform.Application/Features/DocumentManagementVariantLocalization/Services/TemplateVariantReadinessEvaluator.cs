using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Services;

/// <summary>
/// MOD-0029-FU18 — computes whether a variant may actually be relied upon locally (GMG-QMS-SOP-0001 §13.2).
///
/// A pure function over the profile: no repository access, no side effects, no persistence. That keeps the SOP
/// rules in one readable place and makes every branch directly testable.
///
/// THE RULES, in the order they bite:
/// • A superseded / retired / suspended parent stops local use outright — nothing can override that.
/// • A translation variant needs a language and a completed bilingual review.
/// • A site-adopted variant needs a completed local approval.
/// • A parent that has moved on makes a previously-ready translation Outdated: not usable until reassessed.
/// • Where the local language is MANDATORY, local use requires a ready variant — unless a valid, unexpired,
///   evidenced temporary English master allowance is in force (SOP's conditional exception).
/// • Where the local language is NOT mandatory, an unready variant is a WARNING, not a block: the source-language
///   master remains usable.
/// • A variant with neither classification is out of FU18's scope and behaves exactly as before.
/// </summary>
public static class TemplateVariantReadinessEvaluator
{
    public static VariantReadinessModel Evaluate(TemplateVariantLocalizationProfile? profile, Guid variantId, DateTimeOffset now)
    {
        // No profile → the variant predates/opts out of FU18 governance. Existing behaviour is preserved exactly.
        if (profile is null)
        {
            return new VariantReadinessModel(
                variantId, TranslationReady: true, LocalApprovalReady: true, ParentCurrent: true,
                LocalUseAllowed: true, nameof(TranslationReadinessStatus.NotRequired),
                nameof(LocalAdoptionStatus.NotRequired), nameof(ParentChangeStatus.InSync),
                TemporaryEnglishMasterActive: false, null, [], [], VariantLocalizationWire.BoundaryStatement);
        }

        var blocking = new List<string>();
        var warnings = new List<string>();

        // ── parent standing ───────────────────────────────────────────────────
        var parentCurrent = profile.ParentChangeStatus == ParentChangeStatus.InSync;
        var parentStops = profile.ParentChangeStatus is ParentChangeStatus.ParentSuperseded
            or ParentChangeStatus.ParentRetired or ParentChangeStatus.ParentSuspended;

        if (parentStops)
        {
            blocking.Add($"PARENT_{profile.ParentChangeStatus.ToString().ToUpperInvariant()}");
        }
        else if (profile.ParentChangeStatus == ParentChangeStatus.ParentUpdated)
        {
            blocking.Add("PARENT_UPDATED_REASSESSMENT_REQUIRED");
        }

        // ── translation readiness ─────────────────────────────────────────────
        var translationReady = true;
        if (profile.IsTranslationVariant)
        {
            if (string.IsNullOrWhiteSpace(profile.VariantLanguageCode))
            {
                blocking.Add("VARIANT_LANGUAGE_CODE_MISSING");
                translationReady = false;
            }

            switch (profile.BilingualReviewStatus)
            {
                case BilingualReviewStatus.Completed:
                    break;
                case BilingualReviewStatus.Rejected:
                    blocking.Add("BILINGUAL_REVIEW_REJECTED");
                    translationReady = false;
                    break;
                default:
                    blocking.Add("BILINGUAL_REVIEW_NOT_COMPLETED");
                    translationReady = false;
                    break;
            }

            if (profile.TranslationReadinessStatus == TranslationReadinessStatus.Outdated)
            {
                blocking.Add("TRANSLATION_OUTDATED_PARENT_CHANGED");
                translationReady = false;
            }
        }
        else if (profile.RequiresBilingualReview && profile.BilingualReviewStatus != BilingualReviewStatus.Completed)
        {
            blocking.Add("BILINGUAL_REVIEW_NOT_COMPLETED");
            translationReady = false;
        }

        // ── local approval readiness ──────────────────────────────────────────
        var localApprovalReady = true;
        if (profile.IsSiteAdoptedVariant || profile.RequiresLocalApproval)
        {
            if (profile.IsSiteAdoptedVariant
                && string.IsNullOrWhiteSpace(profile.CountryCode)
                && string.IsNullOrWhiteSpace(profile.SiteCode))
            {
                blocking.Add("VARIANT_COUNTRY_OR_SITE_MISSING");
                localApprovalReady = false;
            }

            switch (profile.LocalApprovalStatus)
            {
                case LocalApprovalStatus.Completed:
                    break;
                case LocalApprovalStatus.Rejected:
                    blocking.Add("LOCAL_APPROVAL_REJECTED");
                    localApprovalReady = false;
                    break;
                default:
                    blocking.Add("LOCAL_APPROVAL_NOT_COMPLETED");
                    localApprovalReady = false;
                    break;
            }
        }

        if (profile.LocalAdoptionStatus == LocalAdoptionStatus.Suspended)
        {
            blocking.Add("LOCAL_ADOPTION_SUSPENDED");
        }

        // ── the local-use decision ────────────────────────────────────────────
        var variantReady = translationReady && localApprovalReady && !parentStops
                           && profile.ParentChangeStatus != ParentChangeStatus.ParentUpdated
                           && profile.LocalAdoptionStatus != LocalAdoptionStatus.Suspended;

        var temporaryActive = profile.HasValidTemporaryEnglishAllowance(now);
        if (profile.TemporaryEnglishMasterAllowed && !temporaryActive)
        {
            warnings.Add(profile.TemporaryEnglishMasterExpiresAt is { } expired && now > expired
                ? "TEMPORARY_ENGLISH_MASTER_ALLOWANCE_EXPIRED"
                : "TEMPORARY_ENGLISH_MASTER_ALLOWANCE_INCOMPLETE");
        }

        bool localUseAllowed;
        if (parentStops)
        {
            // A dead parent can never be used locally, temporary allowance or not.
            localUseAllowed = false;
        }
        else if (variantReady)
        {
            localUseAllowed = true;
        }
        else if (profile.IsLocalLanguageMandatory)
        {
            // SOP: the master revision is not locally effective until the local variant is ready — unless a valid
            // temporary English master allowance is in force.
            localUseAllowed = temporaryActive;
            if (temporaryActive)
            {
                warnings.Add("LOCAL_USE_UNDER_TEMPORARY_ENGLISH_MASTER_ALLOWANCE");
            }
            else
            {
                blocking.Add("LOCAL_LANGUAGE_MANDATORY_VARIANT_NOT_READY");
            }
        }
        else
        {
            // Local language optional: the source-language master remains usable; the gaps are warnings.
            localUseAllowed = true;
            warnings.AddRange(blocking);
            blocking.Clear();
        }

        return new VariantReadinessModel(
            variantId, translationReady, localApprovalReady, parentCurrent, localUseAllowed,
            profile.TranslationReadinessStatus.ToString(), profile.LocalAdoptionStatus.ToString(),
            profile.ParentChangeStatus.ToString(), temporaryActive, profile.TemporaryEnglishMasterExpiresAt,
            blocking, warnings, VariantLocalizationWire.BoundaryStatement);
    }
}
