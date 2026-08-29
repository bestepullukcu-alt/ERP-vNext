namespace Diten.CrmService.Application.Features.Segmentation.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing: advertising
/// a capability that does not exist is how a consumer silently builds on sand.
/// </summary>
public sealed record SegmentFeatureFlags(
    bool SupportsSegmentDefinition,
    bool SupportsStaticSegments,
    bool SupportsDynamicSegments,
    bool SupportsHybridSegments,
    bool SupportsCriteriaTree,
    bool SupportsRealTimeMembershipResolution,
    bool SupportsManualTargetCustomer,
    bool SupportsSegmentVersioning,
    bool SupportsEffectiveDating,
    bool SupportsAttributeCatalog,
    bool SupportsMembershipReasonCodes,
    bool SupportsCrossServiceAttributeValidation,
    bool SupportsProductAffinityAttributes,
    bool SupportsConceptGraphDerivedAttributes,
    bool SupportsMaterializedMembership,
    bool SupportsMembershipRefreshJob,
    bool SupportsMembershipHistory,
    bool SupportsSegmentOfSegment,
    bool SupportsIcpScoring,
    bool SupportsComputedAttributes,
    bool SupportsSegmentUsageLog,
    bool SupportsStrategyTemplate,
    bool SupportsSubjectList,
    bool SupportsUcln,
    bool SupportsCampaignTargetGeneration,
    bool SupportsFrequencyPolicyWrite,
    bool SupportsConceptGraphAuthoring,
    bool SupportsConceptGraphTraversalEngine)
{
    public static SegmentFeatureFlags Current => new(
        SupportsSegmentDefinition: true,
        SupportsStaticSegments: true,
        SupportsDynamicSegments: true,
        SupportsHybridSegments: true,
        SupportsCriteriaTree: true,
        SupportsRealTimeMembershipResolution: true,
        SupportsManualTargetCustomer: true,
        SupportsSegmentVersioning: true,
        SupportsEffectiveDating: true,
        SupportsAttributeCatalog: true,
        SupportsMembershipReasonCodes: true,
        SupportsCrossServiceAttributeValidation: true,
        SupportsProductAffinityAttributes: true,
        SupportsConceptGraphDerivedAttributes: true,

        // Closed on purpose. Each one names the FU that owns it, so nobody has to guess.
        SupportsMaterializedMembership: false,      // FU-B
        SupportsMembershipRefreshJob: false,        // FU-B
        SupportsMembershipHistory: false,           // FU-B
        SupportsSegmentOfSegment: false,            // FU-B (a segment inside a segment is a cycle risk)
        SupportsIcpScoring: false,                  // FU-D
        SupportsComputedAttributes: false,          // FU-D
        SupportsSegmentUsageLog: false,             // FU-D
        SupportsStrategyTemplate: false,            // FU-C
        SupportsSubjectList: false,                 // FU-C
        SupportsUcln: false,                        // FU-C
        SupportsCampaignTargetGeneration: false,    // MOD-0165
        SupportsFrequencyPolicyWrite: false,        // MOD-0165
        SupportsConceptGraphAuthoring: false,       // MOD-0162 FU03 - this FU reads the graph, never writes it
        SupportsConceptGraphTraversalEngine: false);// no transitive closure, no best-path, no scoring (depth <= 2)
}
