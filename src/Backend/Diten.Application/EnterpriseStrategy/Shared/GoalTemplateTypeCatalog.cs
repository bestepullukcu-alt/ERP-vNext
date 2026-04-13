namespace Diten.Application.EnterpriseStrategy.Shared;

public static class GoalTemplateTypeCatalog
{
    public static IReadOnlyList<string> AllowedTypes { get; } = new[]
    {
        "Growth",
        "Financial",
        "Market",
        "Operations",
        "Innovation",
        "Capability",
        "Risk",
        "Transformation",
        "ESG",
        "People",
        "Customer",
        "Pharmacovigilance"
    };

    public static IReadOnlyDictionary<string, string> LegacyAliases { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Profitability"] = "Financial",
        ["Market Position"] = "Market",
        ["Risk/Compliance"] = "Risk",
        ["Portfolio"] = "Financial",
        ["Cash/Liquidity"] = "Financial",
        ["Supply Chain"] = "Operations",
        ["Regulatory"] = "Risk",
        ["Partner Effectiveness"] = "Market"
    };

    public static string Normalize(string? value)
    {
        var candidate = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        if (LegacyAliases.TryGetValue(candidate, out var normalized))
            return normalized;

        return AllowedTypes.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase))
            ?? candidate;
    }

    public static string NormalizeOrDefault(string? value, string fallbackType = "Growth")
    {
        var normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallbackType : normalized;
    }
}
