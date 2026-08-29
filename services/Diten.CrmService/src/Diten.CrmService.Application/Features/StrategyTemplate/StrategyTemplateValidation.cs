using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 shared write-path validation: template fields, the four embedded binding lists and the percentage
/// arithmetic. Kept in ONE place so create / update / new-version can never drift apart.
/// <para>Everything here is <b>structural and in-domain</b> (D-VOCAB = A) and performs <b>no I/O</b>: proving that a
/// bound segment / policy / content row exists is an in-service repository step, and proving an MDM product or SKU
/// exists is an explicitly fail-closed cross-service step — both run in the handler, before anything is persisted.</para>
/// <para>The declared frequency intent is validated against MOD-0165's OWN constants
/// (<see cref="FrequencyType"/> / <see cref="FrequencyPeriodType"/>), read-only. They are not copied here: a copy would
/// be a second source of truth that drifts the first time MOD-0165 adds a value.</para>
/// </summary>
public static class StrategyTemplateValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler must answer with. Nested so this file still declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ---------------------------------------------------------------------------------------------------------
    // Template fields
    // ---------------------------------------------------------------------------------------------------------

    public static Failure? ValidateTemplateCode(string? templateCode)
    {
        var code = Trim(templateCode);
        if (code is null)
        {
            return new Failure("TemplateCode is required.", null);
        }

        if (code.Length > StrategyTemplateLimits.MaxTemplateCodeLength)
        {
            return new Failure(
                $"TemplateCode must be at most {StrategyTemplateLimits.MaxTemplateCodeLength} characters.", null);
        }

        return System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9][a-z0-9-]*$")
            ? null
            : new Failure("TemplateCode must be lowercase and may contain only letters, digits and hyphens.", null);
    }

    public static Failure? ValidateTemplateName(string? templateName)
    {
        var name = Trim(templateName);
        if (name is null)
        {
            return new Failure("TemplateName is required.", null);
        }

        return name.Length > StrategyTemplateLimits.MaxTemplateNameLength
            ? new Failure(
                $"TemplateName must be at most {StrategyTemplateLimits.MaxTemplateNameLength} characters.", null)
            : null;
    }

    public static Failure? ValidateSubjectType(string? subjectType)
        => StrategyTemplateSubjectTypes.IsValid(subjectType)
            ? null
            : new Failure(
                $"SubjectType must be one of: {string.Join(", ", StrategyTemplateSubjectTypes.All)}.", null);

    public static Failure? ValidateTemplateStatus(string? templateStatus)
        => StrategyTemplateStatuses.IsValid(templateStatus)
            ? null
            : new Failure(
                $"TemplateStatus must be one of: {string.Join(", ", StrategyTemplateStatuses.All)}.", null);

    public static Failure? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to <= effectiveFrom
            ? new Failure("EffectiveTo must be later than EffectiveFrom.", null)
            : null;

    public static Failure? ValidateBusinessUnitId(string? businessUnitId)
    {
        var value = Trim(businessUnitId);
        if (value is null)
        {
            return null;
        }

        return value.Length > StrategyTemplateLimits.MaxBusinessUnitIdLength
            ? new Failure(
                $"BusinessUnitId must be at most {StrategyTemplateLimits.MaxBusinessUnitIdLength} characters.", null)
            : null;
    }

    public static Failure? ValidateFreeText(string? value, string fieldName, int maxLength)
    {
        var text = Trim(value);
        return text is not null && text.Length > maxLength
            ? new Failure($"{fieldName} must be at most {maxLength} characters.", null)
            : null;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Segment bindings — "who"
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Shape only: existence, archive state and the subject-type match are in-service repository checks the
    /// handler runs (they need the segment).</summary>
    public static Failure? ValidateSegmentBindings(IReadOnlyList<StrategyTemplateSegmentBinding> bindings)
    {
        if (bindings.Count == 0)
        {
            return new Failure(
                "A strategy template must bind at least one segment: without it the play never answers 'who?'.",
                null);
        }

        if (bindings.Count > StrategyTemplateLimits.MaxSegmentBindings)
        {
            return new Failure(
                $"A template may bind at most {StrategyTemplateLimits.MaxSegmentBindings} segments.", null);
        }

        foreach (var binding in bindings)
        {
            if (binding.SegmentId == Guid.Empty)
            {
                return new Failure("Every segment binding needs a SegmentId.", null);
            }

            if (binding.BindingRole is not null && !StrategySegmentBindingRoles.IsValid(binding.BindingRole))
            {
                return new Failure(
                    $"BindingRole must be one of: {string.Join(", ", StrategySegmentBindingRoles.All)}.", null);
            }

            var notesFailure = ValidateFreeText(
                binding.Notes, "Segment binding notes", StrategyTemplateLimits.MaxBindingNotesLength);
            if (notesFailure is not null)
            {
                return notesFailure;
            }
        }

        var duplicate = bindings.GroupBy(b => b.SegmentId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return new Failure(
                $"Segment '{duplicate.Key}' is bound more than once.",
                StrategyTemplateErrorCodes.SegmentBindingDuplicate);
        }

        return ValidateDistinctSortOrder(bindings.Select(b => b.SortOrder), "segment bindings");
    }

    // ---------------------------------------------------------------------------------------------------------
    // Frequency intent — "how often" (never written as a policy)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Exactly one shape. A mixed shape is refused rather than resolved, because resolving it would require a
    /// precedence rule — an engine this FU does not have.</summary>
    public static Failure? ValidateFrequencyIntent(StrategyTemplateFrequencyIntent intent)
    {
        if (!StrategyFrequencyIntentModes.IsValid(intent.Mode))
        {
            return new Failure(
                $"FrequencyIntent.Mode must be one of: {string.Join(", ", StrategyFrequencyIntentModes.All)}.",
                StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid);
        }

        var noteFailure = ValidateFreeText(
            intent.IntentNote, "FrequencyIntent.IntentNote", StrategyTemplateLimits.MaxIntentNoteLength);
        if (noteFailure is not null)
        {
            return noteFailure;
        }

        var hasDeclaration = intent.FrequencyType is not null
                             || intent.RequiredVisitCount is not null
                             || intent.PeriodType is not null;

        switch (intent.Mode)
        {
            case StrategyFrequencyIntentModes.PolicyReference:
                if (intent.VisitFrequencyPolicyId is null || intent.VisitFrequencyPolicyId == Guid.Empty)
                {
                    return new Failure(
                        "A policy-reference intent needs VisitFrequencyPolicyId.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid);
                }

                return hasDeclaration
                    ? new Failure(
                        "A policy-reference intent may not also declare a rhythm: one shape only.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid)
                    : null;

            case StrategyFrequencyIntentModes.DeclaredIntent:
                if (intent.VisitFrequencyPolicyId is not null)
                {
                    return new Failure(
                        "A declared-intent may not also reference a policy: one shape only.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid);
                }

                // MOD-0165's own constants, read-only. Not copied, not re-declared.
                if (!FrequencyType.IsValid(intent.FrequencyType))
                {
                    return new Failure(
                        $"FrequencyIntent.FrequencyType must be one of: {string.Join(", ", FrequencyType.All)}.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid);
                }

                if (!FrequencyPeriodType.IsValid(intent.PeriodType))
                {
                    return new Failure(
                        $"FrequencyIntent.PeriodType must be one of: {string.Join(", ", FrequencyPeriodType.All)}.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid);
                }

                return intent.RequiredVisitCount is not { } count
                       || count <= 0
                       || count > StrategyTemplateLimits.MaxRequiredVisitCount
                    ? new Failure(
                        "FrequencyIntent.RequiredVisitCount must be between 1 and "
                        + $"{StrategyTemplateLimits.MaxRequiredVisitCount}.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid)
                    : null;

            default:
                return intent.VisitFrequencyPolicyId is not null || hasDeclaration
                    ? new Failure(
                        "A 'none' intent carries neither a policy reference nor a declared rhythm.",
                        StrategyTemplateErrorCodes.FrequencyIntentShapeInvalid)
                    : null;
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Product lines + SKU allocations — "what"
    // ---------------------------------------------------------------------------------------------------------

    public static Failure? ValidateProductLines(IReadOnlyList<StrategyTemplateProductLine> lines)
    {
        if (lines.Count > StrategyTemplateLimits.MaxProductLines)
        {
            return new Failure(
                $"A template may carry at most {StrategyTemplateLimits.MaxProductLines} product lines.", null);
        }

        foreach (var line in lines)
        {
            var lineFailure = ValidateProductLine(line);
            if (lineFailure is not null)
            {
                return lineFailure;
            }
        }

        var duplicate = lines.GroupBy(l => l.GlobalProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return new Failure(
                $"Global product '{duplicate.Key}' appears on more than one line.",
                StrategyTemplateErrorCodes.ProductLineDuplicate);
        }

        var sortFailure = ValidateDistinctSortOrder(lines.Select(l => l.SortOrder), "product lines");
        if (sortFailure is not null)
        {
            return sortFailure;
        }

        return ValidateLineWeights(lines);
    }

    private static Failure? ValidateProductLine(StrategyTemplateProductLine line)
    {
        if (line.GlobalProductId == Guid.Empty)
        {
            return new Failure("Every product line needs a GlobalProductId.", null);
        }

        if (!StrategySkuAllocationModes.IsValid(line.SkuAllocationMode))
        {
            return new Failure(
                $"SkuAllocationMode must be one of: {string.Join(", ", StrategySkuAllocationModes.All)}.", null);
        }

        var notesFailure = ValidateFreeText(
            line.Notes, "Product line notes", StrategyTemplateLimits.MaxBindingNotesLength);
        if (notesFailure is not null)
        {
            return notesFailure;
        }

        if (!line.IsSkuAllocated())
        {
            return line.SkuAllocations.Count > 0
                ? new Failure(
                    $"Line '{line.LineId}' is product-only, so it may not carry SKU allocations.",
                    StrategyTemplateErrorCodes.SkuAllocationModeMismatch)
                : null;
        }

        if (line.SkuAllocations.Count == 0)
        {
            return new Failure(
                $"Line '{line.LineId}' is sku-allocated, so it needs at least one SKU allocation.",
                StrategyTemplateErrorCodes.SkuAllocationModeMismatch);
        }

        if (line.SkuAllocations.Count > StrategyTemplateLimits.MaxSkuAllocationsPerLine)
        {
            return new Failure(
                $"A product line may carry at most {StrategyTemplateLimits.MaxSkuAllocationsPerLine} SKU allocations.",
                null);
        }

        foreach (var allocation in line.SkuAllocations)
        {
            if (allocation.GskuId == Guid.Empty)
            {
                return new Failure("Every SKU allocation needs a GskuId.", null);
            }

            var percentageFailure = StrategyTemplateAllocationRules.ValidatePercentage(
                allocation.Percentage, line.LineId);
            if (percentageFailure is not null)
            {
                return percentageFailure;
            }
        }

        var duplicate = line.SkuAllocations.GroupBy(a => a.GskuId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return new Failure(
                $"SKU '{duplicate.Key}' appears more than once on line '{line.LineId}'.",
                StrategyTemplateErrorCodes.SkuAllocationDuplicate);
        }

        var sortFailure = ValidateDistinctSortOrder(
            line.SkuAllocations.Select(a => a.SortOrder), $"SKU allocations of line '{line.LineId}'");
        if (sortFailure is not null)
        {
            return sortFailure;
        }

        return StrategyTemplateAllocationRules.ValidateLineTotal(line);
    }

    private static Failure? ValidateLineWeights(IReadOnlyList<StrategyTemplateProductLine> lines)
        => StrategyTemplateAllocationRules.ValidateLineWeights(lines);

    // ---------------------------------------------------------------------------------------------------------
    // Content bindings — "which story"
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Shape only: existence, archive state and the published requirement are in-service repository checks the
    /// handler runs.</summary>
    public static Failure? ValidateContentBindings(IReadOnlyList<StrategyTemplateContentBinding> bindings)
    {
        if (bindings.Count > StrategyTemplateLimits.MaxContentBindings)
        {
            return new Failure(
                $"A template may bind at most {StrategyTemplateLimits.MaxContentBindings} content rows.", null);
        }

        foreach (var binding in bindings)
        {
            if (!StrategyContentRefTypes.IsValid(binding.ContentRefType))
            {
                return new Failure(
                    $"ContentRefType must be one of: {string.Join(", ", StrategyContentRefTypes.All)}.", null);
            }

            if (binding.ContentRefId == Guid.Empty)
            {
                return new Failure("Every content binding needs a ContentRefId.", null);
            }

            var notesFailure = ValidateFreeText(
                binding.Notes, "Content binding notes", StrategyTemplateLimits.MaxBindingNotesLength);
            if (notesFailure is not null)
            {
                return notesFailure;
            }
        }

        var duplicate = bindings
            .GroupBy(b => (b.ContentRefType, b.ContentRefId))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return new Failure(
                $"Content '{duplicate.Key.ContentRefType}:{duplicate.Key.ContentRefId}' is bound more than once.",
                StrategyTemplateErrorCodes.ContentBindingDuplicate);
        }

        return ValidateDistinctSortOrder(bindings.Select(b => b.SortOrder), "content bindings");
    }

    // ---------------------------------------------------------------------------------------------------------
    // Shared
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>SortOrder is part of the deterministic read order, so duplicates are refused rather than broken by an
    /// arbitrary tie-break.</summary>
    private static Failure? ValidateDistinctSortOrder(IEnumerable<int> sortOrders, string scope)
    {
        var values = sortOrders.ToList();
        return values.Distinct().Count() == values.Count
            ? null
            : new Failure($"SortOrder must be unique among the {scope}.", null);
    }

    /// <summary>Total distinct MDM references in one write (products + SKUs), used to enforce the fan-out ceiling
    /// BEFORE any call is made.</summary>
    public static int CountDistinctReferences(IReadOnlyList<StrategyTemplateProductLine> lines)
        => lines.Select(l => l.GlobalProductId)
            .Concat(lines.SelectMany(l => l.SkuAllocations.Select(a => a.GskuId)))
            .Distinct()
            .Count();
}
