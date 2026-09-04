using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Diten.Platform.Application.Contracts;
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
    private readonly IModuleDomainRepository _moduleDomainRepository;
    private readonly IModuleServiceRepository _moduleServiceRepository;
    private readonly IAuthPermissionModulesClient _authPermissionModulesClient;

    public PlatformLookupProvider(
        IPlatformLookupCache cache,
        IFeatureCategoryRepository featureCategoryRepository,
        IModuleDomainRepository moduleDomainRepository,
        IModuleServiceRepository moduleServiceRepository,
        IAuthPermissionModulesClient authPermissionModulesClient)
    {
        _cache = cache;
        _featureCategoryRepository = featureCategoryRepository;
        _moduleDomainRepository = moduleDomainRepository;
        _moduleServiceRepository = moduleServiceRepository;
        _authPermissionModulesClient = authPermissionModulesClient;
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
            // Görev 4: domains now come from the operator-managed platform_module_domains collection (DB),
            // not the ModuleCatalogDomain enum. Same option shape (code / displayName / value) so the
            // Module Catalog form is unaffected; value stays = displayName (previous valueUsesDisplayName: true).
            PlatformLookupKeys.ModuleCatalogDomains => Wrap(GetModuleCatalogDomainsAsync(ct)),
            // UI #3 Görev 4: services now come from the operator-managed platform_module_services collection (DB),
            // not the ModuleCatalogService enum. Same option shape (code / displayName / value), value = displayName,
            // so the Module Catalog Service dropdown is unaffected.
            PlatformLookupKeys.ModuleCatalogServices => Wrap(GetModuleCatalogServicesAsync(ct)),
            // UI #4: ModuleCode is chosen from AuthService's distinct permission modules so catalog ModuleCode == auth
            // Permission.Module (entitlement bridge match). value == code (the module string), NOT a displayName.
            PlatformLookupKeys.ModuleCatalogPermissionModules => Wrap(GetPermissionModulesAsync(ct)),
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
            /*
             * MOD-0027-FU02: notification enum lookups consumed by the Platform Admin notification UI.
             *
             * ⚠ InApp IS EXCLUDED, AND THE EXCLUSION IS THE POINT. This lookup feeds exactly one thing: the
             * channel selector on the Platform Admin notification TEMPLATE and EVENT screens. Templates are a
             * property of the e-mail pipeline — QueueEmailNotificationHandler resolves one by
             * {key, locale, Channel=Email} and renders a subject and an HTML body. The in-app channel
             * (BL-025) does not render templates at all: it writes a UserNotification row carrying the event
             * code and the source record's own title, precisely so that nothing has to be authored per
             * language up front.
             *
             * So an "InApp" option here would let an operator author a template that NOTHING will ever read,
             * and it would render as the bare literal "InApp" in all seven languages besides — the enum
             * carries no [Display] attribute and no resx key exists for channel VALUES (only for the column
             * label). Both are UI defects, and neither is what adding the channel was for.
             *
             * This comes off the moment a template-rendering in-app pipeline exists. Pinned by
             * PlatformLookupProviderTests so it cannot come off by accident.
             */
            PlatformLookupKeys.NotificationChannels => Wrap(GetEnumLookupAsync<NotificationChannelCode>(
                PlatformLookupKeys.NotificationChannels,
                "NotificationChannel",
                valueUsesDisplayName: false,
                excludeUnknown: false,
                ct,
                excludeCodes: [nameof(NotificationChannelCode.InApp)])),
            PlatformLookupKeys.MessagingProviders => Wrap(GetEnumLookupAsync<MessagingProviderCode>(
                PlatformLookupKeys.MessagingProviders,
                "MessagingProvider",
                valueUsesDisplayName: false,
                excludeUnknown: false,
                ct)),
            PlatformLookupKeys.NotificationTemplateStatuses => Wrap(GetEnumLookupAsync<NotificationTemplateStatus>(
                PlatformLookupKeys.NotificationTemplateStatuses,
                "NotificationTemplateStatus",
                valueUsesDisplayName: false,
                excludeUnknown: false,
                ct)),
            PlatformLookupKeys.NotificationFallbackPolicies => Wrap(GetEnumLookupAsync<NotificationFallbackPolicy>(
                PlatformLookupKeys.NotificationFallbackPolicies,
                "NotificationFallbackPolicy",
                valueUsesDisplayName: false,
                excludeUnknown: false,
                ct)),
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>?>(null)
        };
    }

    private async Task<IReadOnlyList<LookupOptionDto>?> Wrap(Task<IReadOnlyList<LookupOptionDto>> task) =>
        await task;

    // MC-3a — bare infrastructure/service namespaces that are NOT tenant-assignable module codes.
    private static readonly HashSet<string> UmbrellaNamespaces =
        new(StringComparer.OrdinalIgnoreCase) { "auth", "platform", "mdm" };

    private static bool IsUmbrellaNamespace(string module) =>
        UmbrellaNamespaces.Contains(module.Trim());

    private Task<IReadOnlyList<LookupOptionDto>> GetPermissionModulesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.ModuleCatalogPermissionModules),
            DataBackedLookupTtl,
            async token =>
            {
                // Best-effort: an empty list (auth down) is fine — the form's ModuleCode select degrades to free-typed tags.
                var modules = await _authPermissionModulesClient.GetModulesAsync(token);
                return modules
                    .Where(module => !string.IsNullOrWhiteSpace(module.Module))
                    // MC-3a — drop pure-infra/non-assignable umbrella namespaces (bare auth/platform/mdm); they are
                    // permission top-segments, not assignable module codes, and would leak into the catalog picker.
                    .Where(module => !IsUmbrellaNamespace(module.Module))
                    .Select((module, index) => new LookupOptionDto(
                        module.Module,
                        module.Module,
                        module.Module, // value == code: catalog ModuleCode must equal auth Permission.Module exactly
                        "PermissionModule",
                        (index + 1) * 10))
                    .ToList();
            },
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetModuleCatalogServicesAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.ModuleCatalogServices),
            DataBackedLookupTtl,
            async token =>
            {
                var services = await _moduleServiceRepository.GetActiveAsync(token);
                // FIX-DOMAIN-SERVICE-CANONICAL — option VALUE is the canonical Code (label stays DisplayName), so the
                // form submits and stores the Code, not the DisplayName. Keeps catalog Domain/Service drift-free.
                return services
                    .Select(service => new LookupOptionDto(
                        service.Code,
                        service.DisplayName,
                        service.Code,
                        "ModuleCatalogService",
                        service.SortOrder))
                    .ToList();
            },
            ct);

    private Task<IReadOnlyList<LookupOptionDto>> GetModuleCatalogDomainsAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(PlatformLookupKeys.ModuleCatalogDomains),
            DataBackedLookupTtl,
            async token =>
            {
                var domains = await _moduleDomainRepository.GetActiveAsync(token);
                // FIX-DOMAIN-SERVICE-CANONICAL — option VALUE is the canonical Code (label stays DisplayName), so the
                // form submits and stores the Code, not the DisplayName. Keeps catalog Domain/Service drift-free.
                return domains
                    .Select(domain => new LookupOptionDto(
                        domain.Code,
                        domain.DisplayName,
                        domain.Code,
                        "ModuleCatalogDomain",
                        domain.SortOrder))
                    .ToList();
            },
            ct);

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
                // Regional priority block first (10-80): the markets this platform is actually provisioned for.
                // The rest follow from 90 in their previous relative order, so an existing selection never moves
                // to a different country — only its position in the list changes.
                ProvisioningCountry("TR", "Turkey", 10),
                ProvisioningCountry("BY", "Belarus", 20),
                ProvisioningCountry("UZ", "Uzbekistan", 30),
                ProvisioningCountry("TM", "Turkmenistan", 40),
                ProvisioningCountry("GE", "Georgia", 50),
                ProvisioningCountry("AZ", "Azerbaijan", 60),
                ProvisioningCountry("KZ", "Kazakhstan", 70),
                ProvisioningCountry("UA", "Ukraine", 80),
                ProvisioningCountry("US", "United States", 90),
                ProvisioningCountry("GB", "United Kingdom", 100),
                ProvisioningCountry("DE", "Germany", 110),
                ProvisioningCountry("FR", "France", 120),
                ProvisioningCountry("ES", "Spain", 130),
                ProvisioningCountry("IT", "Italy", 140),
                ProvisioningCountry("RU", "Russia", 150),
                ProvisioningCountry("CN", "China", 160),
                ProvisioningCountry("JP", "Japan", 170),
                ProvisioningCountry("AE", "United Arab Emirates", 180),
                ProvisioningCountry("SA", "Saudi Arabia", 190),
                ProvisioningCountry("NL", "Netherlands", 200),
                ProvisioningCountry("BE", "Belgium", 210),
                ProvisioningCountry("CH", "Switzerland", 220),
                ProvisioningCountry("AT", "Austria", 230),
                ProvisioningCountry("PL", "Poland", 240),
                ProvisioningCountry("SE", "Sweden", 250),
                ProvisioningCountry("NO", "Norway", 260),
                ProvisioningCountry("DK", "Denmark", 270)
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
        CancellationToken ct,
        IReadOnlyCollection<string>? excludeCodes = null)
        where TEnum : struct, Enum =>
        _cache.GetOrCreateAsync(
            BuildCacheKey(lookupKey),
            StaticLookupTtl,
            _ => Task.FromResult<IReadOnlyList<LookupOptionDto>>(Enum.GetValues<TEnum>()
                .Where(enumValue => !excludeUnknown || !string.Equals(enumValue.ToString(), "Unknown", StringComparison.OrdinalIgnoreCase))
                .Where(enumValue => excludeCodes is null
                    || !excludeCodes.Contains(enumValue.ToString(), StringComparer.OrdinalIgnoreCase))
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
