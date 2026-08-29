using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementRetention.Services;

/// <summary>
/// MOD-0029-FU15 — resolves WHEN the retention clock starts for a subject (GMG-QMS-SOP-0001 §22).
///
/// RESOLUTION TABLE (what this FU can resolve on its own):
/// • DocumentMasterRegisterEntry / ControlledDocument / ControlledDocumentVersion → the register entry's
///   retirement/supersession transition date (LastTransitionAt) when retired or superseded, else EffectiveDate,
///   else CreatedAt. The entry also tells us whether the document is STILL EFFECTIVE, which drives
///   RetainWhileEffective.
/// • PeriodicReview → CompletedAt, else ReviewDueDate.
/// • IdentifierAllocationLedger → AllocatedAt (permanent retention anyway — the ledger is never purge eligible).
/// • ExternalDocumentImpactAssessment → CompletedAt, else DueDate.
///
/// EXTENSION POINT — everything else (approval evidence, release gate evidence, training, suspension, controlled
/// copy, repository assessment, …) requires a CALLER-SUPPLIED trigger date. Their repositories do not currently
/// expose a by-id lookup, and adding methods to those FU09/FU10/FU11 interfaces would break their existing test
/// doubles. A later hardening FU should widen those contracts and move the resolution in here. Until then a
/// missing trigger date is reported as <see cref="RetentionEvaluationStatus.MissingTriggerDate"/> and the subject
/// stays FAIL-CLOSED (never disposition eligible).
/// </summary>
public sealed class DocumentRetentionTriggerDateResolver
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentPeriodicReviewRepository _periodicReviews;
    private readonly IDocumentIdentifierAllocationRepository _allocations;
    private readonly IExternalDocumentImpactAssessmentRepository _externalImpacts;

    public DocumentRetentionTriggerDateResolver(
        IDocumentMasterRegisterRepository register,
        IDocumentPeriodicReviewRepository periodicReviews,
        IDocumentIdentifierAllocationRepository allocations,
        IExternalDocumentImpactAssessmentRepository externalImpacts)
    {
        _register = register;
        _periodicReviews = periodicReviews;
        _allocations = allocations;
        _externalImpacts = externalImpacts;
    }

    /// <param name="IsStillEffective">
    /// True only when the governing register entry is currently Effective. Drives the SOP rule that an effective
    /// controlled document is retained regardless of elapsed time.
    /// </param>
    public sealed record Result(DateTimeOffset? TriggerDate, bool IsStillEffective, string? RetentionClass);

    public async Task<Result> ResolveAsync(EvaluateRetentionInput input, RetentionSubjectType subjectType, CancellationToken ct)
    {
        // An explicitly supplied trigger date always wins — it is how the caller covers the subject types this
        // resolver cannot reach yet.
        var supplied = input.TriggerDate;

        switch (subjectType)
        {
            case RetentionSubjectType.DocumentMasterRegisterEntry:
            case RetentionSubjectType.ControlledDocument:
            case RetentionSubjectType.ControlledDocumentVersion:
            {
                var entryId = input.RegisterEntryId ?? input.SubjectId;
                var entry = await _register.GetByIdAsync(entryId, ct);
                if (entry is null)
                {
                    return new Result(supplied, IsStillEffective: false, input.RetentionClass);
                }

                var stillEffective = entry.LifecycleStatus == ControlledDocumentLifecycleStatus.Effective;
                var trigger = entry.LifecycleStatus is ControlledDocumentLifecycleStatus.Retired
                    or ControlledDocumentLifecycleStatus.Superseded
                    ? entry.LastTransitionAt ?? entry.EffectiveDate ?? entry.CreatedAt
                    : entry.EffectiveDate ?? entry.CreatedAt;

                return new Result(supplied ?? trigger, stillEffective, input.RetentionClass ?? entry.RetentionClass);
            }

            case RetentionSubjectType.PeriodicReview:
            {
                var review = await _periodicReviews.GetByIdAsync(input.SubjectId, ct);
                return new Result(supplied ?? review?.CompletedAt ?? review?.ReviewDueDate, false, input.RetentionClass);
            }

            case RetentionSubjectType.IdentifierAllocationLedger:
            {
                var allocation = await _allocations.GetByIdAsync(input.SubjectId, ct);
                return new Result(supplied ?? allocation?.AllocatedAt, false, input.RetentionClass);
            }

            case RetentionSubjectType.ExternalDocumentImpactAssessment:
            {
                var assessment = await _externalImpacts.GetByIdAsync(input.SubjectId, ct);
                return new Result(supplied ?? assessment?.CompletedAt ?? assessment?.DueDate, false, input.RetentionClass);
            }

            default:
                // Caller-supplied only — see the extension point note above.
                return new Result(supplied, IsStillEffective: false, input.RetentionClass);
        }
    }
}
