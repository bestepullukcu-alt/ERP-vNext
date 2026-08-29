namespace Diten.CrmService.Application.Features.Territory;

/// <summary>
/// MOD-0151 required MOD-0048 / PSS-012 reference set codes (F1 authoring template). CRM never seeds or hardcodes
/// reference VALUES — only these SET CODES and the readiness-descriptor expected counts (below) live in code.
/// The expected counts describe the F1 template so the contract endpoint can report readiness; they are NEVER used
/// as a validation fallback (an unpublished set always fails closed).
/// </summary>
public static class TerritoryReferenceSets
{
    public const string TerritoryLevel = "territory-level";
    public const string TerritoryModelStatus = "territory-model-status";
    public const string TerritoryNodeStatus = "territory-node-status";
    public const string TerritoryAssignmentStatus = "territory-assignment-status";
    public const string TerritoryAssignmentSource = "territory-assignment-source";
    public const string TerritoryResourceRole = "territory-resource-role";
    public const string TerritoryRuleType = "territory-rule-type";
    public const string TerritoryConflictPolicy = "territory-conflict-policy";
    public const string TerritoryCoverageScope = "territory-coverage-scope";
    public const string BusinessScopeType = "business-scope-type";

    /// <summary>FU02A: the only business scope classification the Model form supports. Brand/product are later FUs.</summary>
    public const string BusinessUnitScopeType = "business-unit";

    /// <summary>FU02A: MOD-0048 value set holding the actual business-unit values (e.g. alpha/beta/gamma). NOT a
    /// MOD-0151 "required" set (its readiness is not gated by the contract); consumed only when a scope is selected.</summary>
    public const string BusinessUnitValueSet = "business-unit";

    /// <summary>territory-level rank metadata key (drives the child-rank &gt; parent-rank hierarchy rule).</summary>
    public const string RankMetadataKey = "rank";
    public const string SortOrderMetadataKey = "sortOrder";

    public const string DraftStatus = "draft";

    /// <summary>Every required set for MOD-0151 (F1 template: 10 required sets), in readiness-report order.</summary>
    public static readonly IReadOnlyList<TerritoryReferenceSetDescriptor> Required = new[]
    {
        new TerritoryReferenceSetDescriptor(TerritoryLevel, 6, new[] { RankMetadataKey, SortOrderMetadataKey }),
        // 7/5 since the 2026-07-28 lifecycle reconciliation: model-status gained `inactive` and node-status gained
        // `archived` so the FU02B lifecycle (pack 22.1) is expressible alongside the FU06 one (pack 13.1).
        new TerritoryReferenceSetDescriptor(TerritoryModelStatus, 7, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryNodeStatus, 5, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryAssignmentStatus, 4, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryAssignmentSource, 4, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryResourceRole, 11, new[] { "defaultCoverageScope", "isSalesRole", "isManagementRole", "canBePrimary" }),
        new TerritoryReferenceSetDescriptor(TerritoryRuleType, 9, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryConflictPolicy, 4, Array.Empty<string>()),
        new TerritoryReferenceSetDescriptor(TerritoryCoverageScope, 7, new[] { "requiresTerritoryId", "requiresBusinessScope", "allowsTerritoryId", "allowsBusinessScope" }),
        new TerritoryReferenceSetDescriptor(BusinessScopeType, 7, new[] { "isSalesScopeDefault", "includeInSalesPerformanceDefault" }),
    };
}

/// <summary>Readiness descriptor for a required set: expected value count (F1 template) + the metadata keys that
/// every active value MUST carry for the set to be "metadata ready".</summary>
public sealed record TerritoryReferenceSetDescriptor(string SetCode, int ExpectedValueCount, IReadOnlyList<string> RequiredMetadataKeys);
