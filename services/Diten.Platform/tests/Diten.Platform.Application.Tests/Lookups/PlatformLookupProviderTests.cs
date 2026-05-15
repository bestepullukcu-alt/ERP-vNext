using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Lookups.Queries;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

public sealed class PlatformLookupProviderTests
{
    [Fact]
    public async Task Currencies_return_canonical_shape_without_duplicates()
    {
        var provider = CreateProvider();

        var options = await provider.GetCurrenciesAsync(CancellationToken.None);

        Assert.NotEmpty(options);
        Assert.Contains(options, option => option.Code == "USD" && option.Value == "USD");
        AssertCanonicalShape(options);
        AssertNoDuplicateCodes(options);
    }

    [Fact]
    public async Task Locales_always_include_en_and_tr()
    {
        var provider = CreateProvider();

        var options = await provider.GetLocalesAsync(CancellationToken.None);

        Assert.Contains(options, option => option.Code == "en" && option.Value == "en");
        Assert.Contains(options, option => option.Code == "tr" && option.Value == "tr");
        AssertCanonicalShape(options);
    }

    [Fact]
    public async Task Timezones_include_utc_and_use_stable_code_value_shape()
    {
        var provider = CreateProvider();

        var options = await provider.GetTimezonesAsync(CancellationToken.None);

        Assert.Contains(options, option => option.Code == "UTC" && option.Value == "UTC");
        AssertCanonicalShape(options);
        Assert.All(options, option => Assert.Equal(option.Code, option.Value));
    }

    [Fact]
    public async Task Countries_remain_platform_provisioning_lookup_not_territory_reference()
    {
        var provider = CreateProvider();

        var options = await provider.GetLookupOptionsAsync(PlatformLookupKeys.Countries, CancellationToken.None);

        Assert.NotNull(options);
        Assert.NotEmpty(options);
        AssertCanonicalShape(options);
        Assert.All(options, option =>
        {
            Assert.Equal("PlatformProvisioning", option.Group);
            Assert.Equal("platform-provisioning", option.Metadata?["scope"]);
            Assert.DoesNotContain(
                option.Metadata?.Keys ?? Enumerable.Empty<string>(),
                key => key.Contains("territory", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Feature_categories_use_active_source_of_record_only()
    {
        var repository = new InMemoryFeatureCategoryRepository();
        repository.Items.AddRange(
        [
            new FeatureCategory { CategoryCode = "core", DisplayName = "Core", SortOrder = 20, Status = FeatureCategoryStatus.Active },
            new FeatureCategory { CategoryCode = "inactive", DisplayName = "Inactive", SortOrder = 10, Status = FeatureCategoryStatus.Inactive },
            new FeatureCategory { CategoryCode = "deleted", DisplayName = "Deleted", SortOrder = 30, Status = FeatureCategoryStatus.Active, IsDeleted = true }
        ]);
        var provider = CreateProvider(repository: repository);

        var options = await provider.GetLookupOptionsAsync(PlatformLookupKeys.FeatureCategories, CancellationToken.None);

        Assert.Equal(FeatureCategoryStatus.Active, repository.LastStatus);
        var option = Assert.Single(options!);
        Assert.Equal("CORE", option.Code);
        Assert.Equal("CORE", option.Value);
        Assert.Equal("FeatureCategory", option.Group);
    }

    [Fact]
    public async Task Unknown_lookup_key_returns_controlled_not_found_response()
    {
        var handler = new GetLookupOptionsHandler(CreateProvider());

        var response = await handler.Handle(new GetLookupOptionsQuery("erp-account-reference"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Cache_hit_and_miss_return_same_response_shape()
    {
        var cache = new InMemoryLookupCache();
        var provider = CreateProvider(cache);

        var first = await provider.GetTenantTiersAsync(CancellationToken.None);
        var second = await provider.GetTenantTiersAsync(CancellationToken.None);

        Assert.Equal(1, cache.FactoryCallCount);
        Assert.Equal(first, second);
        AssertCanonicalShape(first);
        AssertCanonicalShape(second);
    }

    [Fact]
    public async Task Cache_miss_provider_exception_does_not_write_partial_entry()
    {
        var cache = new InMemoryLookupCache();
        var repository = new InMemoryFeatureCategoryRepository { ThrowOnGetAll = true };
        var provider = CreateProvider(cache, repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetLookupOptionsAsync(PlatformLookupKeys.FeatureCategories, CancellationToken.None));

        Assert.Empty(cache.Entries);
    }

    [Fact]
    public void Serialized_lookup_response_uses_camel_case_lookup_option_shape_without_tenant_id()
    {
        var response = Response<IReadOnlyList<LookupOptionDto>>.Success(
        [
            new("USD", "US Dollar", "USD", "Currency", 10)
        ]);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"code\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"value\"", json);
        Assert.DoesNotContain("\"id\"", json);
        Assert.DoesNotContain("\"tenantId\"", json);
    }

    private static PlatformLookupProvider CreateProvider(
        InMemoryLookupCache? cache = null,
        InMemoryFeatureCategoryRepository? repository = null) =>
        new(cache ?? new InMemoryLookupCache(), repository ?? new InMemoryFeatureCategoryRepository());

    private static void AssertCanonicalShape(IEnumerable<LookupOptionDto> options)
    {
        Assert.All(options, option =>
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Code));
            Assert.False(string.IsNullOrWhiteSpace(option.Name));
            Assert.False(string.IsNullOrWhiteSpace(option.Value));
        });
    }

    private static void AssertNoDuplicateCodes(IEnumerable<LookupOptionDto> options)
    {
        var duplicateCodes = options
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicateCodes);
    }

    private sealed class InMemoryLookupCache : IPlatformLookupCache
    {
        public Dictionary<string, IReadOnlyList<LookupOptionDto>> Entries { get; } = [];
        public int FactoryCallCount { get; private set; }

        public async Task<IReadOnlyList<LookupOptionDto>> GetOrCreateAsync(
            string cacheKey,
            TimeSpan absoluteExpirationRelativeToNow,
            Func<CancellationToken, Task<IReadOnlyList<LookupOptionDto>>> factory,
            CancellationToken ct)
        {
            if (Entries.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            FactoryCallCount++;
            var value = await factory(ct);
            Entries[cacheKey] = value;
            return value;
        }
    }

    private sealed class InMemoryFeatureCategoryRepository : IFeatureCategoryRepository
    {
        public List<FeatureCategory> Items { get; } = [];
        public FeatureCategoryStatus? LastStatus { get; private set; }
        public bool ThrowOnGetAll { get; init; }

        public Task<FeatureCategory> CreateAsync(FeatureCategory category, CancellationToken ct = default)
        {
            Items.Add(category);
            return Task.FromResult(category);
        }

        public Task<FeatureCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(item =>
                string.Equals(item.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase)
                && !item.IsDeleted
                && (!excludeId.HasValue || item.Id != excludeId.Value)));

        public Task<bool> UpdateAsync(FeatureCategory category, byte[]? expectedRowVersion = null, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<IReadOnlyList<FeatureCategory>> GetAllAsync(FeatureCategoryStatus? status = null, CancellationToken ct = default)
        {
            if (ThrowOnGetAll)
            {
                throw new InvalidOperationException("Feature category source unavailable.");
            }

            LastStatus = status;
            IReadOnlyList<FeatureCategory> items = Items
                .Where(item => !item.IsDeleted && (!status.HasValue || item.Status == status.Value))
                .ToList();
            return Task.FromResult(items);
        }
    }
}
