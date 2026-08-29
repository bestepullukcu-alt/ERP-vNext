using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — entity to DTO and input to entity, in one place so a read and a write can never describe the same
/// template differently.
/// <para>Child ids (<c>BindingId</c> / <c>LineId</c> / <c>AllocationId</c>) are ALWAYS generated here and never taken
/// from the caller: an id from another template could otherwise be smuggled in, and a <c>new-version</c> clone must get
/// fresh ids so the two versions are never confused for one another.</para>
/// <para>Every list is emitted in the deterministic order <c>SortOrder</c> then child id. No DateTimeOffset is ever a
/// sort key (BSON arrays, parallel-array trap).</para>
/// </summary>
public static class StrategyTemplateMapper
{
    // ---------------------------------------------------------------------------------------------------------
    // Input -> entity
    // ---------------------------------------------------------------------------------------------------------

    public static List<StrategyTemplateSegmentBinding> ToSegmentBindings(
        IReadOnlyList<StrategyTemplateSegmentBindingInput>? inputs)
        => (inputs ?? Array.Empty<StrategyTemplateSegmentBindingInput>())
            .Select(input => new StrategyTemplateSegmentBinding
            {
                BindingId = Guid.NewGuid(),
                SegmentId = input.SegmentId,
                BindingRole = StrategySegmentBindingRoles.Normalize(input.BindingRole),
                SortOrder = input.SortOrder,
                Notes = StrategyTemplateValidation.Trim(input.Notes)
                // SegmentLineageId / SegmentVersionAtBinding / SegmentCodeDisplay are stamped by the binding validator
                // from the segment itself — never from the payload.
            })
            .ToList();

    public static StrategyTemplateFrequencyIntent ToFrequencyIntent(StrategyTemplateFrequencyIntentInput? input)
    {
        if (input is null)
        {
            return new StrategyTemplateFrequencyIntent { Mode = StrategyFrequencyIntentModes.None };
        }

        var mode = StrategyFrequencyIntentModes.Normalize(input.Mode);
        return new StrategyTemplateFrequencyIntent
        {
            Mode = mode,
            VisitFrequencyPolicyId = input.VisitFrequencyPolicyId,
            FrequencyType = StrategyTemplateValidation.Trim(input.FrequencyType)?.ToLowerInvariant(),
            RequiredVisitCount = input.RequiredVisitCount,
            PeriodType = StrategyTemplateValidation.Trim(input.PeriodType)?.ToLowerInvariant(),
            IntentNote = StrategyTemplateValidation.Trim(input.IntentNote)
        };
    }

    public static List<StrategyTemplateProductLine> ToProductLines(
        IReadOnlyList<StrategyTemplateProductLineInput>? inputs)
        => (inputs ?? Array.Empty<StrategyTemplateProductLineInput>())
            .Select(input =>
            {
                var lineId = Guid.NewGuid();
                return new StrategyTemplateProductLine
                {
                    LineId = lineId,
                    GlobalProductId = input.GlobalProductId,
                    GlobalProductCodeDisplay = StrategyTemplateValidation.Trim(input.GlobalProductCodeDisplay),
                    LineWeightPercentage = input.LineWeightPercentage,
                    SkuAllocationMode = StrategySkuAllocationModes.Normalize(input.SkuAllocationMode),
                    SortOrder = input.SortOrder,
                    Notes = StrategyTemplateValidation.Trim(input.Notes),
                    SkuAllocations = (input.SkuAllocations ?? Array.Empty<StrategyTemplateSkuAllocationInput>())
                        .Select(allocation => new StrategyTemplateSkuAllocation
                        {
                            AllocationId = Guid.NewGuid(),
                            GskuId = allocation.GskuId,
                            GskuCanonicalCodeDisplay =
                                StrategyTemplateValidation.Trim(allocation.GskuCanonicalCodeDisplay),
                            Percentage = allocation.Percentage,
                            SortOrder = allocation.SortOrder
                        })
                        .ToList()
                };
            })
            .ToList();

    public static List<StrategyTemplateContentBinding> ToContentBindings(
        IReadOnlyList<StrategyTemplateContentBindingInput>? inputs)
        => (inputs ?? Array.Empty<StrategyTemplateContentBindingInput>())
            .Select(input => new StrategyTemplateContentBinding
            {
                BindingId = Guid.NewGuid(),
                ContentRefType = StrategyContentRefTypes.Normalize(input.ContentRefType),
                ContentRefId = input.ContentRefId,
                SortOrder = input.SortOrder,
                Notes = StrategyTemplateValidation.Trim(input.Notes)
                // ContentCodeDisplay / ContentVersionAtBinding are stamped by the binding validator.
            })
            .ToList();

    /// <summary>Clones the binding lists for a <c>new-version</c>: same references, FRESH child ids, so nothing about
    /// the old version can be reached through the new one.</summary>
    public static (List<StrategyTemplateSegmentBinding> Segments,
        StrategyTemplateFrequencyIntent Frequency,
        List<StrategyTemplateProductLine> Products,
        List<StrategyTemplateContentBinding> Contents) CloneBindings(TemplateEntity source)
    {
        var segments = source.SegmentBindings
            .Select(b => new StrategyTemplateSegmentBinding
            {
                BindingId = Guid.NewGuid(),
                SegmentId = b.SegmentId,
                SegmentLineageId = b.SegmentLineageId,
                SegmentVersionAtBinding = b.SegmentVersionAtBinding,
                SegmentCodeDisplay = b.SegmentCodeDisplay,
                BindingRole = b.BindingRole,
                SortOrder = b.SortOrder,
                Notes = b.Notes
            })
            .ToList();

        var frequency = new StrategyTemplateFrequencyIntent
        {
            Mode = source.FrequencyIntent.Mode,
            VisitFrequencyPolicyId = source.FrequencyIntent.VisitFrequencyPolicyId,
            PolicyCodeDisplay = source.FrequencyIntent.PolicyCodeDisplay,
            FrequencyType = source.FrequencyIntent.FrequencyType,
            RequiredVisitCount = source.FrequencyIntent.RequiredVisitCount,
            PeriodType = source.FrequencyIntent.PeriodType,
            IntentNote = source.FrequencyIntent.IntentNote
        };

        var products = source.ProductLines
            .Select(l => new StrategyTemplateProductLine
            {
                LineId = Guid.NewGuid(),
                GlobalProductId = l.GlobalProductId,
                GlobalProductCodeDisplay = l.GlobalProductCodeDisplay,
                LineWeightPercentage = l.LineWeightPercentage,
                SkuAllocationMode = l.SkuAllocationMode,
                SortOrder = l.SortOrder,
                Notes = l.Notes,
                SkuAllocations = l.SkuAllocations
                    .Select(a => new StrategyTemplateSkuAllocation
                    {
                        AllocationId = Guid.NewGuid(),
                        GskuId = a.GskuId,
                        GskuCanonicalCodeDisplay = a.GskuCanonicalCodeDisplay,
                        Percentage = a.Percentage,
                        SortOrder = a.SortOrder
                    })
                    .ToList()
            })
            .ToList();

        var contents = source.ContentBindings
            .Select(c => new StrategyTemplateContentBinding
            {
                BindingId = Guid.NewGuid(),
                ContentRefType = c.ContentRefType,
                ContentRefId = c.ContentRefId,
                ContentCodeDisplay = c.ContentCodeDisplay,
                ContentVersionAtBinding = c.ContentVersionAtBinding,
                SortOrder = c.SortOrder,
                Notes = c.Notes
            })
            .ToList();

        return (segments, frequency, products, contents);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Entity -> DTO
    // ---------------------------------------------------------------------------------------------------------

    public static StrategyTemplateListItemDto ToListItem(TemplateEntity template) => new(
        template.Id,
        template.TemplateCode,
        template.TemplateName,
        template.SubjectType,
        template.TemplateStatus,
        template.TemplateVersion,
        template.VersionLineageId,
        template.IsSuperseded(),
        template.SupersededByTemplateId,
        template.BusinessUnitId,
        template.Description,
        template.EffectiveFrom,
        template.EffectiveTo,
        template.SegmentBindings.Count,
        template.FrequencyIntent.Mode,
        template.ProductLines.Count,
        template.ProductLines.Sum(l => l.SkuAllocations.Count),
        template.ContentBindings.Count,
        template.AreBindingsFrozen(),
        template.BindingsFrozenAt,
        template.ActivatedAt,
        template.IsArchived(),
        template.Version,
        template.CreatedAt,
        template.UpdatedAt);

    public static StrategyTemplateDetailDto ToDetail(TemplateEntity template) => new(
        template.Id,
        template.TemplateCode,
        template.TemplateName,
        template.SubjectType,
        template.TemplateStatus,
        template.TemplateVersion,
        template.VersionLineageId,
        template.IsSuperseded(),
        template.SupersededByTemplateId,
        template.BusinessUnitId,
        template.Description,
        template.Notes,
        template.EffectiveFrom,
        template.EffectiveTo,
        OrderedSegmentBindings(template).Select(ToDto).ToList(),
        ToDto(template.FrequencyIntent),
        OrderedProductLines(template).Select(ToDto).ToList(),
        OrderedContentBindings(template).Select(ToDto).ToList(),
        template.AreBindingsFrozen(),
        template.BindingsFrozenAt,
        template.ActivatedAt,
        template.ActivatedBy,
        template.ArchivedAt,
        template.ArchivedBy,
        template.IsArchived(),
        template.Version,
        template.CreatedAt,
        template.CreatedBy,
        template.UpdatedAt,
        template.UpdatedBy);

    public static IEnumerable<StrategyTemplateSegmentBinding> OrderedSegmentBindings(TemplateEntity template)
        => template.SegmentBindings.OrderBy(b => b.SortOrder).ThenBy(b => b.BindingId);

    public static IEnumerable<StrategyTemplateProductLine> OrderedProductLines(TemplateEntity template)
        => template.ProductLines.OrderBy(l => l.SortOrder).ThenBy(l => l.LineId);

    public static IEnumerable<StrategyTemplateContentBinding> OrderedContentBindings(TemplateEntity template)
        => template.ContentBindings.OrderBy(c => c.SortOrder).ThenBy(c => c.BindingId);

    public static StrategyTemplateSegmentBindingDto ToDto(StrategyTemplateSegmentBinding binding) => new(
        binding.BindingId,
        binding.SegmentId,
        binding.SegmentLineageId,
        binding.SegmentVersionAtBinding,
        binding.SegmentCodeDisplay,
        binding.BindingRole,
        binding.SortOrder,
        binding.Notes);

    public static StrategyTemplateFrequencyIntentDto ToDto(StrategyTemplateFrequencyIntent intent) => new(
        intent.Mode,
        intent.VisitFrequencyPolicyId,
        intent.PolicyCodeDisplay,
        intent.FrequencyType,
        intent.RequiredVisitCount,
        intent.PeriodType,
        intent.IntentNote);

    public static StrategyTemplateProductLineDto ToDto(StrategyTemplateProductLine line) => new(
        line.LineId,
        line.GlobalProductId,
        line.GlobalProductCodeDisplay,
        line.LineWeightPercentage,
        line.SkuAllocationMode,
        line.SkuAllocations
            .OrderBy(a => a.SortOrder).ThenBy(a => a.AllocationId)
            .Select(ToDto)
            .ToList(),
        StrategyTemplateAllocationRules.TotalOf(line),
        line.SortOrder,
        line.Notes);

    public static StrategyTemplateSkuAllocationDto ToDto(StrategyTemplateSkuAllocation allocation) => new(
        allocation.AllocationId,
        allocation.GskuId,
        allocation.GskuCanonicalCodeDisplay,
        allocation.Percentage,
        allocation.SortOrder);

    public static StrategyTemplateContentBindingDto ToDto(StrategyTemplateContentBinding binding) => new(
        binding.BindingId,
        binding.ContentRefType,
        binding.ContentRefId,
        binding.ContentCodeDisplay,
        binding.ContentVersionAtBinding,
        binding.SortOrder,
        binding.Notes);
}
