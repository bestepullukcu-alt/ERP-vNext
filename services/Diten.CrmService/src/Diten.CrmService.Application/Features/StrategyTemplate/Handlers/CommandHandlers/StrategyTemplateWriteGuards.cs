using System.Globalization;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Write-path guards shared by every StrategyTemplate command, so create / update / new-version / activate can never
/// drift apart. Internal by design: this is not part of any contract.
/// </summary>
internal static class StrategyTemplateWriteGuards
{
    /// <summary>The full in-domain shape validation, in the order that produces the most useful first error. No I/O.</summary>
    public static StrategyTemplateValidation.Failure? ValidateShape(
        string? templateName,
        string? subjectType,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? businessUnitId,
        string? description,
        string? notes,
        IReadOnlyList<StrategyTemplateSegmentBinding> segmentBindings,
        StrategyTemplateFrequencyIntent frequencyIntent,
        IReadOnlyList<StrategyTemplateProductLine> productLines,
        IReadOnlyList<StrategyTemplateContentBinding> contentBindings)
        => StrategyTemplateValidation.ValidateTemplateName(templateName)
           ?? StrategyTemplateValidation.ValidateSubjectType(subjectType)
           ?? StrategyTemplateValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
           ?? StrategyTemplateValidation.ValidateBusinessUnitId(businessUnitId)
           ?? StrategyTemplateValidation.ValidateFreeText(
               description, "Description", StrategyTemplateLimits.MaxDescriptionLength)
           ?? StrategyTemplateValidation.ValidateFreeText(
               notes, "Notes", StrategyTemplateLimits.MaxNotesLength)
           ?? StrategyTemplateValidation.ValidateSegmentBindings(segmentBindings)
           ?? StrategyTemplateValidation.ValidateFrequencyIntent(frequencyIntent)
           ?? StrategyTemplateValidation.ValidateProductLines(productLines)
           ?? StrategyTemplateValidation.ValidateContentBindings(contentBindings);

    /// <summary>
    /// Cross-service value proof. Every MDM reference a product line binds (a GlobalProduct, and every Gsku under it) is
    /// proven to exist BEFORE anything is written.
    /// <para>404 from the dependency means the binding is not authorable: <b>400</b>. A dependency that cannot answer
    /// (timeout, 5xx, an auth rejection, a malformed body) means we do not know: <b>503</b>, no partial acceptance and
    /// nothing persisted — this method is always called before <c>InsertAsync</c> / <c>ReplaceAsync</c>, never after.
    /// </para>
    /// <para>Each DISTINCT id is proven once per request (the same SKU on two lines makes one call, not two). That is a
    /// per-request dedup and NOT a cache: a later request proves it again, because a reference that vanished between two
    /// writes must be caught.</para>
    /// <para><b>What is deliberately not proven:</b> that a Gsku belongs to the line's GlobalProduct. MDM's Gsku carries
    /// no GlobalProductId and its selector offers no product filter, and this FU may not open a new MDM read surface —
    /// so containment stays the author's responsibility and the contract reports
    /// <c>supportsProductSkuContainmentValidation: false</c> rather than implying a check that never runs.</para>
    /// </summary>
    public static async Task<StrategyTemplateValidation.Failure?> ValidateCrossServiceReferencesAsync(
        IStrategyTemplateProductReferenceValidator validator,
        IReadOnlyList<StrategyTemplateProductLine> productLines,
        CancellationToken cancellationToken)
    {
        var fanout = StrategyTemplateValidation.CountDistinctReferences(productLines);
        if (fanout > StrategyTemplateLimits.MaxReferenceFanout)
        {
            return new StrategyTemplateValidation.Failure(
                $"This write would need {fanout} reference proofs; the ceiling is "
                + $"{StrategyTemplateLimits.MaxReferenceFanout}. Split the play.",
                StrategyTemplateErrorCodes.ReferenceFanoutExceeded, 422);
        }

        var proven = new HashSet<Guid>();

        foreach (var line in productLines)
        {
            if (proven.Add(line.GlobalProductId))
            {
                var outcome = await validator.ValidateAsync(
                    IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct,
                    line.GlobalProductId,
                    cancellationToken);

                var failure = Interpret(
                    outcome,
                    $"Global product '{line.GlobalProductId}' does not exist.",
                    StrategyTemplateErrorCodes.ProductReferenceNotFound);
                if (failure is not null)
                {
                    return failure;
                }
            }

            foreach (var allocation in line.SkuAllocations)
            {
                if (!proven.Add(allocation.GskuId))
                {
                    continue;
                }

                var outcome = await validator.ValidateAsync(
                    IStrategyTemplateProductReferenceValidator.ReferenceKind.Gsku,
                    allocation.GskuId,
                    cancellationToken);

                var failure = Interpret(
                    outcome,
                    $"SKU '{allocation.GskuId}' does not exist.",
                    StrategyTemplateErrorCodes.SkuReferenceNotFound);
                if (failure is not null)
                {
                    return failure;
                }
            }
        }

        return null;
    }

    /// <summary>The asymmetry D6 is built on: "it is not there" is a 400 the author can fix, "I could not ask" is a 503
    /// with nothing written.</summary>
    private static StrategyTemplateValidation.Failure? Interpret(
        IStrategyTemplateProductReferenceValidator.Outcome outcome, string notFoundMessage, string notFoundCode)
        => outcome switch
        {
            IStrategyTemplateProductReferenceValidator.Outcome.NotFound =>
                new StrategyTemplateValidation.Failure(notFoundMessage, notFoundCode),
            IStrategyTemplateProductReferenceValidator.Outcome.Unavailable =>
                new StrategyTemplateValidation.Failure(
                    "The product master could not be reached to prove the bound references. Nothing was saved.",
                    StrategyTemplateErrorCodes.DependencyUnavailable, 503),
            _ => null
        };

    /// <summary>
    /// A stable signature of what the play BINDS, ignoring the generated child ids and the display stamps. The freeze
    /// guard compares signatures, so re-sending the same bindings on a frozen template is a no-op rather than a 409 —
    /// which is what makes "edit the name of a live play" possible with a full-document payload.
    /// </summary>
    public static string BindingSignature(
        IReadOnlyList<StrategyTemplateSegmentBinding> segments,
        StrategyTemplateFrequencyIntent frequency,
        IReadOnlyList<StrategyTemplateProductLine> products,
        IReadOnlyList<StrategyTemplateContentBinding> contents)
    {
        var culture = CultureInfo.InvariantCulture;

        var segmentPart = string.Join("|", segments
            .OrderBy(b => b.SortOrder).ThenBy(b => b.SegmentId)
            .Select(b => $"{b.SegmentId:D}:{b.BindingRole}:{b.SortOrder}:{b.Notes}"));

        var frequencyPart = string.Join(":", new[]
        {
            frequency.Mode,
            frequency.VisitFrequencyPolicyId?.ToString("D") ?? string.Empty,
            frequency.FrequencyType ?? string.Empty,
            frequency.RequiredVisitCount?.ToString(culture) ?? string.Empty,
            frequency.PeriodType ?? string.Empty,
            frequency.IntentNote ?? string.Empty
        });

        var productPart = string.Join("|", products
            .OrderBy(l => l.SortOrder).ThenBy(l => l.GlobalProductId)
            .Select(l =>
                $"{l.GlobalProductId:D}:{l.SkuAllocationMode}:"
                + $"{l.LineWeightPercentage?.ToString("0.##", culture) ?? string.Empty}:{l.SortOrder}:{l.Notes}:"
                + string.Join(",", l.SkuAllocations
                    .OrderBy(a => a.SortOrder).ThenBy(a => a.GskuId)
                    .Select(a => $"{a.GskuId:D}={a.Percentage.ToString("0.##", culture)}@{a.SortOrder}"))));

        var contentPart = string.Join("|", contents
            .OrderBy(c => c.SortOrder).ThenBy(c => c.ContentRefId)
            .Select(c => $"{c.ContentRefType}:{c.ContentRefId:D}:{c.SortOrder}:{c.Notes}"));

        return $"S[{segmentPart}] F[{frequencyPart}] P[{productPart}] C[{contentPart}]";
    }

    public static string BindingSignature(TemplateEntity template)
        => BindingSignature(
            template.SegmentBindings, template.FrequencyIntent, template.ProductLines, template.ContentBindings);

    /// <summary>Formats a failure into the response envelope: the machine code first, then the human message, so a UI
    /// and the smoke script can branch on the code without parsing prose.</summary>
    public static IReadOnlyList<string> ToErrors(StrategyTemplateValidation.Failure failure)
        => failure.Code is null
            ? new[] { failure.Message }
            : new[] { failure.Code, failure.Message };
}
