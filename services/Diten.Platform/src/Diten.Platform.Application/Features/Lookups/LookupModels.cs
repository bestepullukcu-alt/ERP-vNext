namespace Diten.Platform.Application.Features.Lookups;

public sealed record LookupOptionDto(
    string Code,
    string Name,
    string Value,
    string? Group = null,
    int? SortOrder = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public static class PlatformLookupKeys
{
    public const string Currencies = "currencies";
    public const string Locales = "locales";
    public const string Languages = "languages";
    public const string Timezones = "timezones";
    public const string TenantTiers = "tenant-tiers";
    public const string FeatureCategories = "feature-categories";
    public const string Countries = "countries";
    public const string SubscriptionCycles = "subscription-cycles";
    public const string ModuleCatalogDomains = "module-catalog/domains";
    public const string ModuleCatalogServices = "module-catalog/services";
    public const string AuditCategories = "audit/categories";
    public const string AuditOperations = "audit/operations";
    public const string AuditOutcomes = "audit/outcomes";

    public static string Normalize(string lookupKey) =>
        lookupKey.Trim().Trim('/').ToLowerInvariant();
}
