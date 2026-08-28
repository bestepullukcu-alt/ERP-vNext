namespace Diten.CrmService.Application.Features.StrategyTemplate.Contract;

/// <summary>
/// What this FU can and cannot do, stated as data. The <c>false</c> flags are deliberate and load-bearing: a template
/// BINDS and never produces, so every "generate" capability a consumer might assume is denied out loud rather than left
/// to be discovered.
/// </summary>
public sealed record StrategyTemplateFeatureFlags(
    bool SupportsStrategyTemplateDefinition,
    bool SupportsSegmentBinding,
    bool SupportsMultiSegmentBinding,
    bool SupportsFrequencyIntentPolicyReference,
    bool SupportsFrequencyIntentDeclared,
    bool SupportsProductSkuMix,
    bool SupportsSkuAllocationTotalValidation,
    bool SupportsContentBindingKnowledgePath,
    bool SupportsContentBindingEngagementJourney,
    bool SupportsTemplateVersioning,
    bool SupportsEffectiveDating,
    bool SupportsCrossServiceProductValidation,
    bool SupportsBindingStalenessHints,
    bool SupportsStrategyApply,
    bool SupportsMicroTargetGeneration,
    bool SupportsCyclePeriod,
    bool SupportsFrequencyPolicyWrite,
    bool SupportsCampaignTargetGeneration,
    bool SupportsSegmentMembershipResolution,
    bool SupportsUcln,
    bool SupportsLoyaltyPlanning,
    bool SupportsPromoWeekPlanning,
    bool SupportsPatientNumberPlanning,
    bool SupportsSubjectListAggregate,
    bool SupportsAudienceAggregate,
    bool SupportsBrandBinding,
    bool SupportsLskuBinding,
    bool SupportsProductSkuContainmentValidation,
    bool SupportsStrategyEngine)
{
    public static StrategyTemplateFeatureFlags Current => new(
        SupportsStrategyTemplateDefinition: true,
        SupportsSegmentBinding: true,
        SupportsMultiSegmentBinding: true,
        SupportsFrequencyIntentPolicyReference: true,
        SupportsFrequencyIntentDeclared: true,
        SupportsProductSkuMix: true,
        SupportsSkuAllocationTotalValidation: true,
        SupportsContentBindingKnowledgePath: true,
        SupportsContentBindingEngagementJourney: true,
        SupportsTemplateVersioning: true,
        SupportsEffectiveDating: true,
        SupportsCrossServiceProductValidation: true,
        SupportsBindingStalenessHints: true,

        // Closed on purpose. Each one names the module that owns it, so nobody has to guess.
        SupportsStrategyApply: false,                   // MOD-0155 FU05 (MicroTarget)
        SupportsMicroTargetGeneration: false,           // MOD-0155
        SupportsCyclePeriod: false,                     // MOD-0165 (not built)
        SupportsFrequencyPolicyWrite: false,            // MOD-0165 owns VisitFrequencyPolicy
        SupportsCampaignTargetGeneration: false,        // MOD-0165
        SupportsSegmentMembershipResolution: false,     // MOD-0167 FU02 (.resolve) — this FU sees no member
        SupportsUcln: false,                            // MOD-0155 (plan) + MDM (classification)
        SupportsLoyaltyPlanning: false,                 // MOD-0155
        SupportsPromoWeekPlanning: false,               // MOD-0155
        SupportsPatientNumberPlanning: false,           // MOD-0155
        SupportsSubjectListAggregate: false,            // legacy name; its real role is ProductLines here
        SupportsAudienceAggregate: false,               // legacy ForWhom → MOD-0162, already exists
        SupportsBrandBinding: false,                    // D-BRAND — the product does not use brands
        SupportsLskuBinding: false,                     // F-LSKU
        SupportsProductSkuContainmentValidation: false, // D-SKU-LINK — NOT verified, and not pretended to be
        SupportsStrategyEngine: false);                 // no scoring, no recommendation, no best-play
}
