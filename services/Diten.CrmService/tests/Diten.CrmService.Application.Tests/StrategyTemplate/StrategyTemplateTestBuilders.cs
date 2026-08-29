using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>Command builders for the MOD-0167 FU04 tests: a valid play by default, so each test states only the one
/// thing it is about.</summary>
internal static class StrategyTemplateTestBuilders
{
    public static CreateStrategyTemplateCommand NewTemplate(
        Guid segmentId,
        string code = "cardio-core-play",
        string subjectType = StrategyTemplateSubjectTypes.Contact,
        StrategyTemplateFrequencyIntentInput? frequency = null,
        IReadOnlyList<StrategyTemplateProductLineInput>? productLines = null,
        IReadOnlyList<StrategyTemplateContentBindingInput>? contentBindings = null)
        => new(
            code,
            "Cardiology core play",
            subjectType,
            StrategyTemplateTestDoubles.Past,
            null,
            null,
            null,
            null,
            new[] { Segment(segmentId) },
            frequency ?? NoFrequency(),
            productLines,
            contentBindings);

    public static StrategyTemplateSegmentBindingInput Segment(Guid segmentId, int sortOrder = 10)
        => new(segmentId, StrategySegmentBindingRoles.Primary, sortOrder, null);

    public static StrategyTemplateFrequencyIntentInput NoFrequency()
        => new(StrategyFrequencyIntentModes.None, null, null, null, null, null);

    public static StrategyTemplateFrequencyIntentInput PolicyReference(Guid policyId)
        => new(StrategyFrequencyIntentModes.PolicyReference, policyId, null, null, null, null);

    public static StrategyTemplateFrequencyIntentInput DeclaredIntent(
        string frequencyType = FrequencyType.Weekly,
        int requiredVisitCount = 2,
        string periodType = FrequencyPeriodType.Week)
        => new(StrategyFrequencyIntentModes.DeclaredIntent, null, frequencyType, requiredVisitCount, periodType,
            "Two touches a week during launch.");

    /// <summary>A product-only line: the honest way to say "this play promotes a product with no SKU split".</summary>
    public static StrategyTemplateProductLineInput ProductOnly(Guid globalProductId, int sortOrder = 10)
        => new(globalProductId, "GP-001", null, StrategySkuAllocationModes.ProductOnly, null, sortOrder, null);

    public static StrategyTemplateProductLineInput SkuAllocated(
        Guid globalProductId,
        IReadOnlyList<(Guid GskuId, decimal Percentage)> allocations,
        decimal? lineWeight = null,
        int sortOrder = 10)
        => new(
            globalProductId,
            "GP-001",
            lineWeight,
            StrategySkuAllocationModes.SkuAllocated,
            allocations
                .Select((a, index) => new StrategyTemplateSkuAllocationInput(a.GskuId, null, a.Percentage, index * 10))
                .ToList(),
            sortOrder,
            null);

    public static StrategyTemplateContentBindingInput KnowledgePath(Guid pathId, int sortOrder = 10)
        => new(StrategyContentRefTypes.KnowledgePath, pathId, sortOrder, null);

    public static StrategyTemplateContentBindingInput Journey(Guid journeyId, int sortOrder = 20)
        => new(StrategyContentRefTypes.ContentEngagementJourney, journeyId, sortOrder, null);
}
