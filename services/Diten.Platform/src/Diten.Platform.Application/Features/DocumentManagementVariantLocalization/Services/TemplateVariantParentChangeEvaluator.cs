using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Services;

/// <summary>
/// MOD-0029-FU18 — assesses a variant against the CURRENT state of its parent and records the verdict as history
/// (GMG-QMS-SOP-0001 §13.2).
///
/// SOP rules implemented:
/// • The master published a newer version than the variant was last rebased/assessed against → the variant is in
///   revision: translation becomes Outdated, local adoption returns to Pending, revision + re-review required.
/// • The parent is superseded / retired / suspended → local use must stop and suspension is required.
/// • The parent is deprecated / archived at template level → treated as no longer current.
///
/// EXPLICIT NON-BEHAVIOURS — this evaluator:
/// • never overwrites variant content (there is no content here at all),
/// • never mutates the FU03 TemplateVariant.Status or its computed drift,
/// • never transitions the parent, and never performs a rebase (rebase stays FU03, metadata-only),
/// • never compares document content — it compares lineage metadata only.
/// It writes the assessment record and updates the FU18 profile's governance fields. Acting on the resulting
/// requirements stays with a human (or a later FU).
///
/// The parent state is read from the TemplateMaster / master version, and — when the variant's profile links to a
/// Document Master Register entry — that entry's FU08 lifecycle status takes precedence, because Suspended /
/// Retired / Superseded only exist there.
/// </summary>
public sealed class TemplateVariantParentChangeEvaluator
{
    private readonly ITemplateVariantRepository _variants;
    private readonly ITemplateVariantLocalizationProfileRepository _profiles;
    private readonly ITemplateVariantParentChangeAssessmentRepository _assessments;
    private readonly ITemplateMasterRepository _masters;
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public TemplateVariantParentChangeEvaluator(
        ITemplateVariantRepository variants,
        ITemplateVariantLocalizationProfileRepository profiles,
        ITemplateVariantParentChangeAssessmentRepository assessments,
        ITemplateMasterRepository masters,
        IDocumentMasterRegisterRepository register,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _variants = variants;
        _profiles = profiles;
        _assessments = assessments;
        _masters = masters;
        _register = register;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<VariantParentChangeAssessmentModel>> EvaluateAsync(
        Guid variantId, string? evidenceReference, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return Fail("Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId);
        }

        var profile = await _profiles.GetByVariantAsync(variantId, ct);
        if (profile is null)
        {
            return Fail("This variant has no localization profile; create one before assessing parent change.",
                404, VariantLocalizationReasonCodes.ProfileNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var master = await _masters.GetByIdAsync(profile.ParentTemplateMasterId ?? variant.TemplateMasterId, ct);
        var registerEntry = profile.ParentRegisterEntryId is { } entryId
            ? await _register.GetByIdAsync(entryId, ct)
            : null;

        var observed = ResolveObservedStatus(master, registerEntry);
        var parentVersionNumber = master?.CurrentMasterVersion;
        var parentMoved = ParentMovedAhead(variant, master);

        var assessment = new TemplateVariantParentChangeAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateVariantId = variantId,
            ParentTemplateMasterId = master?.Id ?? profile.ParentTemplateMasterId,
            ParentTemplateMasterVersionId = profile.ParentTemplateMasterVersionId ?? variant.TemplateMasterVersionId,
            ParentDocumentUid = profile.ParentDocumentUid ?? registerEntry?.PermanentUid,
            ParentDocumentCode = profile.ParentDocumentCode ?? registerEntry?.DocumentCode,
            ObservedParentStatus = observed,
            ObservedParentVersionLabel = profile.ParentVersionLabel ?? registerEntry?.CurrentVersionLabel,
            ObservedParentVersionNumber = parentVersionNumber,
            ObservedParentEffectiveDate = registerEntry?.EffectiveDate ?? master?.EffectiveDate,
            AssessedAt = now,
            AssessedBy = _currentUser.ActorName,
            AssessmentEvidenceReference = Trim(evidenceReference),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        ApplyVerdict(assessment, profile, observed, parentMoved);
        await _assessments.CreateAsync(assessment, ct);

        // Carry the verdict onto the profile so readiness reflects it without re-running the assessment.
        profile.ParentChangeStatus = MapParentChangeStatus(observed, parentMoved);
        profile.LastParentAssessmentAt = now;
        profile.ParentEffectiveDateAtLastAssessment = assessment.ObservedParentEffectiveDate;

        if (assessment.RequiresSuspension)
        {
            profile.LocalAdoptionStatus = LocalAdoptionStatus.Suspended;
            if (profile.IsTranslationVariant)
            {
                profile.TranslationReadinessStatus = TranslationReadinessStatus.Blocked;
            }
        }
        else if (parentMoved)
        {
            // A newly effective master puts the variant into revision: the translation no longer represents it.
            if (profile.IsTranslationVariant)
            {
                profile.TranslationReadinessStatus = TranslationReadinessStatus.Outdated;
            }

            if (profile.LocalAdoptionStatus != LocalAdoptionStatus.NotRequired)
            {
                profile.LocalAdoptionStatus = LocalAdoptionStatus.Pending;
            }
        }

        profile.UpdatedAt = now;
        profile.UpdatedBy = _currentUser.ActorName;
        await _profiles.UpdateAsync(profile, ct);

        return Response<VariantParentChangeAssessmentModel>.Success(
            VariantLocalizationWire.ToAssessment(assessment), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<VariantParentChangeAssessmentModel>>> GetAssessmentsAsync(
        Guid variantId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var variant = await _variants.GetByIdAsync(variantId, ct);
        if (variant is null)
        {
            return Response<IReadOnlyList<VariantParentChangeAssessmentModel>>.Fail(
                "Template variant not found.", 404, VariantLocalizationReasonCodes.VariantNotFound, correlationId);
        }

        var rows = await _assessments.GetByVariantAsync(variantId, ct);
        return Response<IReadOnlyList<VariantParentChangeAssessmentModel>>.Success(
            rows.Select(VariantLocalizationWire.ToAssessment).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The register entry's FU08 lifecycle status wins when present: Suspended / Retired / Superseded are states
    /// only the controlled-document lifecycle knows about. Otherwise fall back to the template master's status.
    /// </summary>
    private static ObservedParentStatus ResolveObservedStatus(TemplateMaster? master, DocumentMasterRegisterEntry? entry)
    {
        if (entry is not null)
        {
            return entry.LifecycleStatus switch
            {
                ControlledDocumentLifecycleStatus.Effective => ObservedParentStatus.Effective,
                ControlledDocumentLifecycleStatus.Superseded => ObservedParentStatus.Superseded,
                ControlledDocumentLifecycleStatus.Retired => ObservedParentStatus.Retired,
                ControlledDocumentLifecycleStatus.Suspended => ObservedParentStatus.Suspended,
                _ => ObservedParentStatus.Unknown
            };
        }

        return master?.Status switch
        {
            TemplateMasterStatus.Published => ObservedParentStatus.Effective,
            TemplateMasterStatus.Deprecated => ObservedParentStatus.Deprecated,
            TemplateMasterStatus.Archived => ObservedParentStatus.Archived,
            _ => ObservedParentStatus.Unknown
        };
    }

    /// <summary>Lineage comparison only: has the master published a version beyond the variant's last rebase?</summary>
    private static bool ParentMovedAhead(TemplateVariant variant, TemplateMaster? master)
    {
        if (master is null)
        {
            return false;
        }

        var variantBaseline = variant.LastRebasedMasterVersionNumber;
        return variantBaseline is { } baseline
            ? master.CurrentMasterVersion > baseline
            : master.CurrentVersionId is not null && master.CurrentVersionId != variant.TemplateMasterVersionId;
    }

    private static void ApplyVerdict(
        TemplateVariantParentChangeAssessment a,
        TemplateVariantLocalizationProfile profile,
        ObservedParentStatus observed,
        bool parentMoved)
    {
        var parentStopped = observed is ObservedParentStatus.Superseded or ObservedParentStatus.Retired
            or ObservedParentStatus.Suspended or ObservedParentStatus.Deprecated or ObservedParentStatus.Archived;

        if (parentStopped)
        {
            a.AssessmentStatus = ParentChangeAssessmentStatus.SuspensionRequired;
            a.RequiresSuspension = true;
            a.RequiresVariantRevision = true;
            a.AssessmentNote = $"Parent is {observed}; local use of this variant must stop pending review.";
            return;
        }

        if (observed == ObservedParentStatus.Unknown)
        {
            a.AssessmentStatus = ParentChangeAssessmentStatus.Blocked;
            a.AssessmentNote = "Parent state could not be established; the variant cannot be confirmed current.";
            return;
        }

        if (!parentMoved)
        {
            a.AssessmentStatus = ParentChangeAssessmentStatus.InSync;
            a.AssessmentNote = "Variant lineage matches the current parent version.";
            return;
        }

        // The parent moved: work out the heaviest follow-up the variant's classification demands.
        a.RequiresVariantRevision = true;
        a.RequiresBilingualReview = profile.IsTranslationVariant || profile.RequiresBilingualReview;
        a.RequiresLocalApproval = profile.IsSiteAdoptedVariant || profile.RequiresLocalApproval;

        a.AssessmentStatus = a.RequiresBilingualReview
            ? ParentChangeAssessmentStatus.TranslationUpdateRequired
            : a.RequiresLocalApproval
                ? ParentChangeAssessmentStatus.LocalApprovalRequired
                : ParentChangeAssessmentStatus.RebaseRequired;

        a.AssessmentNote = "Parent published a newer version; the variant is in revision until reassessed.";
    }

    private static ParentChangeStatus MapParentChangeStatus(ObservedParentStatus observed, bool parentMoved) => observed switch
    {
        ObservedParentStatus.Superseded => ParentChangeStatus.ParentSuperseded,
        ObservedParentStatus.Retired or ObservedParentStatus.Archived => ParentChangeStatus.ParentRetired,
        ObservedParentStatus.Suspended or ObservedParentStatus.Deprecated => ParentChangeStatus.ParentSuspended,
        _ => parentMoved ? ParentChangeStatus.ParentUpdated : ParentChangeStatus.InSync
    };

    private static Response<VariantParentChangeAssessmentModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<VariantParentChangeAssessmentModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
