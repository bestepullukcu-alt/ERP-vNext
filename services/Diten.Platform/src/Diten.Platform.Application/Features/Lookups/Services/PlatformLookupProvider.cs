using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Lookups.Services;

public sealed class PlatformLookupProvider : IPlatformLookupProvider
{
    private static readonly TimeSpan StaticLookupTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan DataBackedLookupTtl = TimeSpan.FromMinutes(5);

    private readonly IPlatformLookupCache _cache;
    private readonly IFeatureCategoryRepository _featureCategoryRepository;

    public PlatformLookupProvider(
        IPlatformLookupCache cache,
        IFeatureCategoryRepository featureCategoryRepository)
    {
        _cache = cache;
        _featureCategoryRepository = featureCategoryRepository;
    }

    public Task<IReadOnlyList<LookupOptionDto>> GetCurrenciesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.Currencies),
            StaticLookupTtl,
            _ => Task.FromResult(BuildCurrencyOptions()),
            ct);

    public Task<IReadOnlyList<LookupOptionDto>> GetLocalesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.Locales),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(
            [
                new("en", "English", "en", "PlatformLocale", 10),
                new("tr", "Turkish", "tr", "PlatformLocale", 20)
            ]),
            ct);

    public Task<IReadOnlyList<LookupOptionDto>> GetTimezonesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.Timezones),
            StaticLookupTtl,
            _ => Task.FromResult(BuildTimezoneOptions()),
            ct);

    public Task<IReadOnlyList<LookupOptionDto>> GetTenantTiersAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.TenantTiers),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(
            [
                new("STANDARD", "Standard", "Standard", "PlatformPackaging", 10),
                new("PREMIUM", "Premium", "Premium", "PlatformPackaging", 20),
                new("ENTERPRISE", "Enterprise", "Enterprise", "PlatformPackaging", 30),
                new("TRIAL", "Trial", "Trial", "PlatformPackaging", 40)
            ]),
            ct);

    public Task<IReadOnlyList<LookupOptionDto>?> GetLookupOptionsAsync(string lookupKey, CancellationToken ct)
    {
        return PlatformLookupKeys.Normalize(lookupKey) switch
        {
            PlatformLookupKeys.Currencies => Wrap(GetCurrenciesAsync(ct)),
            PlatformLookupKeys.Locales or PlatformLookupKeys.Languages => Wrap(GetLocalesAsync(ct)),
            PlatformLookupKeys.Timezones => Wrap(GetTimezonesAsync(ct)),
            PlatformLookupKeys.TenantTiers => Wrap(GetTenantTiersAsync(ct)),
            PlatformLookupKeys.FeatureCategories => Wrap(GetFeatureCategoriesAsync(ct)),
            PlatformLookupKeys.Countries => Wrap(GetCountriesAsync(ct)),
            PlatformLookupKeys.SubscriptionCycles => Wrap(GetSubscriptionCyclesAsync(ct)),
            PlatformLookupKeys.ModuleCatalogDomains => Wrap(GetEnumLookupAsync<ModuleCatalogDomain>(
                PlatformLookupKeys.ModuleCatalogDomains,
                "ModuleCatalogDomain",
                valueUsesDisplayName: true,
                ct)),
            PlatformLookupKeys.ModuleCatalogServices => Wrap(GetEnumLookupAsync<ModuleCatalogService>(
                PlatformLookupKeys.ModuleCatalogServices,
                "ModuleCatalogService",
                valueUsesDisplayName: true,
                ct)),
            PlatformLookupKeys.AuditCategories => Wrap(GetEnumLookupAsync<AuditCategory>(
                PlatformLookupKeys.AuditCategories,
                "AuditCategory",
                valueUsesDisplayName: false,
                excludeUnknown: true,
                ct)),
            PlatformLookupKeys.AuditOperations => Wrap(GetEnumLookupAsync<AuditOperation>(
                PlatformLookupKeys.AuditOperations,
                "AuditOperation",
                valueUsesDisplayName: false,
                excludeUnknown: true,
                ct)),
            PlatformLookupKeys.AuditOutcomes => Wrap(GetEnumLookupAsync<AuditOutcome>(
                PlatformLookupKeys.AuditOutcomes,
                "AuditOutcome",
                valueUsesDisplayName: false,
                excludeUnknown: true,
                ct)),
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>?>(null)
        };
    }

    private async Task<IReadOnlyList<LookupOptionDto>?> Wrap(Task<IReadOnlyList<LookupOptionDto>> task) =>
        await task;

    private Task<IReadOnlyList<LookupOptionDto>> GetFeatureCategoriesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.FeatureCategories),
            DataBackedLookupTtl,
            async token =>
            {
                var categories = await _featureCategoryRepository.GetAllAsync(FeatureCategoryStatus.Active, token);
                return categories
                    .Where(category => !category.IsDeleted && category.Status == FeatureCategoryStatus.Active)
                    .Where(category => !string.IsNullOrWhiteSpace(category.CategoryCode))
                    .GroupBy(category => category.CategoryCode.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderBy(category => category.SortOrder)
                        .ThenBy(category => category.DisplayName)
                        .First())
                    .OrderBy(category => category.SortOrder)
                    .ThenBy(category => category.DisplayName)
                    .Select(category =>
                    {
                        var code = category.CategoryCode.Trim().ToUpperInvariant();
                        return new LookupOptionDto(
                            code,
                            string.IsNullOrWhiteSpace(category.DisplayName) ? code : category.DisplayName.Trim(),
                            code,
                            "FeatureCategory",
                            category.SortOrder);
                    })
                    .ToList();
            },
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetCountriesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.Countries),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(
            [
                ProvisioningCountry("TR", "Turkey", 10),
                ProvisioningCountry("US", "United States", 20),
                ProvisioningCountry("GB", "United Kingdom", 30),
                ProvisioningCountry("DE", "Germany", 40),
                ProvisioningCountry("FR", "France", 50),
                ProvisioningCountry("ES", "Spain", 60),
                ProvisioningCountry("IT", "Italy", 70),
                ProvisioningCountry("RU", "Russia", 80),
                ProvisioningCountry("CN", "China", 90),
                ProvisioningCountry("JP", "Japan", 100),
                ProvisioningCountry("AE", "United Arab Emirates", 110),
                ProvisioningCountry("SA", "Saudi Arabia", 120),
                ProvisioningCountry("NL", "Netherlands", 130),
                ProvisioningCountry("BE", "Belgium", 140),
                ProvisioningCountry("CH", "Switzerland", 150),
                ProvisioningCountry("AT", "Austria", 160),
                ProvisioningCountry("PL", "Poland", 170),
                ProvisioningCountry("SE", "Sweden", 180),
                ProvisioningCountry("NO", "Norway", 190),
                ProvisioningCountry("DK", "Denmark", 200)
            ]),
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetSubscriptionCyclesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.SubscriptionCycles),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(
            [
                new("MONTHLY", "Monthly", "MONTHLY", "PlatformSubscriptionCycle", 10),
                new("YEARLY", "Yearly", "YEARLY", "PlatformSubscriptionCycle", 20),
                new("TRIAL", "Trial", "TRIAL", "PlatformSubscriptionCycle", 30),
                new("CUSTOM", "Custom", "CUSTOM", "PlatformSubscriptionCycle", 40)
            ]),
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetEnumLookupAsync<TEnum>(
        string lookupKey,
        string group,
        bool valueUsesDisplayName,
        bool excludeUnknown,
        CancellationToken ct)
        where TEnum : struct, Enum =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(lookupKey),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(Enum.GetValues<TEnum>()
                .Where(enumValue => !excludeUnknown || !string.Equals(enumValue.ToString(), "Unknown", StringComparison.OrdinalIgnoreCase))
                .Select((enumValue, index) =>
                {
                    var code = enumValue.ToString();
                    var displayName = GetDisplayName(enumValue);
                    return new LookupOptionDto(
                        code,
                        displayName,
                        valueUsesDisplayName ? displayName : code,
                        group,
                        (index + 1) * 10);
                })
                .OrderBy(option => option.Name)
                .ThenBy(option => option.Code)
                .ToList()),
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetEnumLookupAsync<TEnum>(
        string lookupKey,
        string group,
        bool valueUsesDisplayName,
        CancellationToken ct)
        where TEnum : struct, Enum =>
        GetEnumLookupAsync<TEnum>(lookupKey, group, valueUsesDisplayName, excludeUnknown: false, ct);

    private static IReadOnlyList<LookupOptionDto> BuildCurrencyOptions()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(TryGetRegion)
            .Where(region => region is not null)
            .Cast<RegionInfo>()
            .Where(region => !string.IsNullOrWhiteSpace(region.ISOCurrencySymbol))
            .GroupBy(region => region.ISOCurrencySymbol.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var region = group
                    .OrderBy(item => item.CurrencyEnglishName)
                    .ThenBy(item => item.EnglishName)
                    .First();
                var code = region.ISOCurrencySymbol.Trim().ToUpperInvariant();
                var metadata = string.IsNullOrWhiteSpace(region.CurrencySymbol)
                    ? null
                    : new Dictionary<string, string> { ["symbol"] = region.CurrencySymbol };

                return new LookupOptionDto(
                    code,
                    string.IsNullOrWhiteSpace(region.CurrencyEnglishName) ? code : region.CurrencyEnglishName,
                    code,
                    "Currency",
                    null,
                    metadata);
            })
            .OrderBy(option => option.Name)
            .ThenBy(option => option.Code)
            .ToList();
    }

    private static IReadOnlyList<LookupOptionDto> BuildTimezoneOptions()
    {
        var systemTimezones = TimeZoneInfo.GetSystemTimeZones()
            .Append(TimeZoneInfo.Utc)
            .GroupBy(timezone => timezone.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(timezone => new LookupOptionDto(
                timezone.Id,
                timezone.DisplayName,
                timezone.Id,
                "Timezone",
                null,
                new Dictionary<string, string>
                {
                    ["baseUtcOffset"] = timezone.BaseUtcOffset.ToString(@"hh\:mm")
                }))
            .OrderBy(option => option.Name)
            .ThenBy(option => option.Code)
            .ToList();

        return systemTimezones;
    }

    private static LookupOptionDto ProvisioningCountry(string code, string name, int sortOrder) =>
        new(
            code,
            name,
            code,
            "PlatformProvisioning",
            sortOrder,
            new Dictionary<string, string>
            {
                ["scope"] = "platform-provisioning"
            });

    private static RegionInfo? TryGetRegion(CultureInfo culture)
    {
        try
        {
            return new RegionInfo(culture.Name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string GetDisplayName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    private static string BuildCacheKey(string lookupKey) =>
        $"platform:lookups:{PlatformLookupKeys.Normalize(lookupKey)}:{CultureInfo.CurrentUICulture.Name}";
}
