namespace Diten.CrmService.Application.Features.Territory.AssignmentRules;

/// <summary>
/// The <c>territory-rule-type</c> value codes FU03 can actually evaluate.
///
/// <para><b>These are not a fallback vocabulary.</b> Every rule type is still validated against the MOD-0048
/// published <c>territory-rule-type</c> set first (fail-closed); this list only says which of the published types the
/// FU03 matcher implements. A published-but-unimplemented type is rejected with a controlled 400 rather than being
/// accepted and silently matching nothing — the same reasoning as the FU02B lifecycle vocabulary reconciliation.</para>
/// </summary>
public static class TerritoryRuleTypes
{
    /// <summary>Match on account location: country / city / district.</summary>
    public const string Geography = "geography";

    /// <summary>Match on account classification: type / category / status.</summary>
    public const string AccountType = "account-type";

    /// <summary>Match on an explicit account id list.</summary>
    public const string AccountList = "account-list";

    public static readonly IReadOnlyList<string> Fu03Supported = new[] { Geography, AccountType, AccountList };

    /// <summary>Published types FU03 knowingly defers (product portfolio / business scope / channel / segment /
    /// manual / import). Listed so the rejection message can say "later FU", not "invalid".</summary>
    public static readonly IReadOnlyList<string> DeferredToLaterFu = new[]
    {
        "product-portfolio", "business-scope", "channel", "segment", "manual", "import"
    };

    public static bool IsSupported(string? ruleType)
        => Fu03Supported.Any(t => string.Equals(t, ruleType, StringComparison.OrdinalIgnoreCase));

    public static bool IsDeferred(string? ruleType)
        => DeferredToLaterFu.Any(t => string.Equals(t, ruleType, StringComparison.OrdinalIgnoreCase));
}
