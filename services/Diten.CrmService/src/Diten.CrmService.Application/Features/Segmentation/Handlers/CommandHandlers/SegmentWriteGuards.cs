using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>
/// Write-path guards shared by every Segmentation command, so create / update / new-version can never drift apart.
/// Internal by design: this is not part of any contract.
/// </summary>
internal static class SegmentWriteGuards
{
    /// <summary>
    /// Class-X value proof (D6). Every criterion value that names an MDM master (a global product for
    /// <c>concept.affinity</c>, a product or brand for a consent scope) is proven to exist BEFORE anything is written.
    /// <para>404 from the dependency means the rule is not authorable: <b>400</b>. A dependency that cannot answer
    /// (timeout, 5xx, auth rejection, malformed body) means we do not know: <b>503</b>, no partial acceptance, and
    /// nothing persisted — this method is always called before <c>InsertAsync</c> / <c>ReplaceAsync</c>, never after.
    /// </para>
    /// <para>Note the asymmetry that D6 is built on: this cross-PROCESS uncertainty is a 503, while an in-service
    /// uncertainty (consent, territory, the concept graph) is an ANSWER that eliminates a candidate with a reason and
    /// lets the resolution complete.</para>
    /// </summary>
    public static async Task<SegmentValidation.Failure?> ValidateCrossServiceReferencesAsync(
        ISegmentProductReferenceValidator validator,
        IReadOnlyList<SegmentCriteriaNode> criteria,
        CancellationToken cancellationToken)
    {
        foreach (var node in criteria.Where(n => n.IsPredicate()))
        {
            var definition = SegmentAttributeCatalog.Find(node.AttributeCode);
            if (definition?.CrossServiceReferenceKind is not { } kind)
            {
                continue;
            }

            foreach (var raw in node.Values)
            {
                if (!Guid.TryParse(raw, out var referenceId))
                {
                    return new SegmentValidation.Failure(
                        $"Value '{raw}' for attribute '{definition.AttributeCode}' must be a valid reference id.",
                        SegmentErrorCodes.CriteriaReferenceNotFound);
                }

                var outcome = await validator.ValidateAsync(kind, referenceId, cancellationToken);
                switch (outcome)
                {
                    case ISegmentProductReferenceValidator.Outcome.NotFound:
                        return new SegmentValidation.Failure(
                            $"Reference '{raw}' for attribute '{definition.AttributeCode}' does not exist.",
                            SegmentErrorCodes.CriteriaReferenceNotFound);

                    case ISegmentProductReferenceValidator.Outcome.Unavailable:
                        return new SegmentValidation.Failure(
                            $"The reference master could not be reached to prove '{raw}' for attribute "
                            + $"'{definition.AttributeCode}'. Nothing was saved.",
                            SegmentErrorCodes.DependencyUnavailable, 503);
                }
            }
        }

        return null;
    }

    /// <summary>The full field + tree validation, in the order that produces the most useful first error.</summary>
    public static SegmentValidation.Failure? ValidateSegmentShape(
        string? segmentName, string? segmentType, string? subjectType, string? matchMode,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, string? businessUnitId,
        string? description, string? notes, IReadOnlyList<SegmentCriteriaNode> criteria)
        => SegmentValidation.ValidateSegmentName(segmentName)
           ?? SegmentValidation.ValidateSegmentType(segmentType)
           ?? SegmentValidation.ValidateSubjectType(subjectType)
           ?? SegmentValidation.ValidateMatchMode(matchMode)
           ?? SegmentValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
           ?? SegmentValidation.ValidateBusinessUnitId(businessUnitId)
           ?? SegmentValidation.ValidateFreeText(description, "Description", SegmentLimits.MaxDescriptionLength)
           ?? SegmentValidation.ValidateFreeText(notes, "Notes", SegmentLimits.MaxNotesLength)
           ?? SegmentValidation.ValidateCriteria(segmentType ?? string.Empty, subjectType ?? string.Empty, criteria);

    /// <summary>Formats a failure into the response envelope: the machine code first, then the human message, so a UI
    /// and the smoke script can branch on the code without parsing prose.</summary>
    public static IReadOnlyList<string> ToErrors(SegmentValidation.Failure failure)
        => failure.Code is null
            ? new[] { failure.Message }
            : new[] { failure.Code, failure.Message };
}
