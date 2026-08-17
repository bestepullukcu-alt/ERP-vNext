using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.SubscriptionFeatures;
using Diten.Platform.Application.Features.SubscriptionFeatures.Commands;
using Diten.Platform.Application.Features.SubscriptionFeatures.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.SubscriptionFeatures.Validators;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.SubscriptionFeatures;

public sealed class SubscriptionFeaturesRulesTests
{
    [Theory]
    [InlineData(" analytics__dashboard ", "ANALYTICS-DASHBOARD")]
    [InlineData("-security   core-", "SECURITY-CORE")]
    [InlineData("integration.api", "INTEGRATION-API")]
    public void Normalize_feature_code_returns_canonical_code(string input, string expected)
    {
        Assert.Equal(expected, SubscriptionFeatureCodeNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(" Analytics__Dashboard ", "analytics-dashboard")]
    [InlineData("-Security   Core-", "security-core")]
    [InlineData("Integration.API", "integration-api")]
    public void Normalize_feature_slug_returns_kebab_slug(string input, string expected)
    {
        Assert.Equal(expected, SubscriptionFeatureSlugNormalizer.Normalize(input));
    }

    [Fact]
    public void Create_feature_validator_requires_category_when_active()
    {
        var validator = new CreateFeatureDefinitionCommandValidator();
        var command = new CreateFeatureDefinitionCommand(new CreateFeatureDefinitionRequest(
            "SECURITY-CORE",
            "security-core",
            "Security Core",
            null,
            null,
            "Active",
            true,
            0,
            null));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage == "CategoryId is required when Status is Active.");
    }

    [Fact]
    public async Task Create_feature_handler_rejects_duplicate_code()
    {
        var featureRepository = new InMemoryFeatureDefinitionRepository();
        var categoryRepository = new InMemoryFeatureCategoryRepository();
        await featureRepository.CreateAsync(new FeatureDefinition
        {
            FeatureCode = "SECURITY-CORE",
            FeatureSlug = "security-core",
            DisplayName = "Security Core"
        });

        var handler = CreateFeatureHandler(featureRepository, categoryRepository);
        var response = await handler.Handle(new CreateFeatureDefinitionCommand(new CreateFeatureDefinitionRequest(
            "security core",
            "security-core-v2",
            "Security Core",
            null,
            null,
            "Draft",
            false,
            null,
            null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Create_feature_handler_rejects_archived_category()
    {
        var featureRepository = new InMemoryFeatureDefinitionRepository();
        var categoryRepository = new InMemoryFeatureCategoryRepository();
        var category = await categoryRepository.CreateAsync(new FeatureCategory
        {
            CategoryCode = "SECURITY",
            DisplayName = "Security",
            Status = FeatureCategoryStatus.Archived
        });

        var handler = CreateFeatureHandler(featureRepository, categoryRepository);
        var response = await handler.Handle(new CreateFeatureDefinitionCommand(new CreateFeatureDefinitionRequest(
            "SECURITY-CORE",
            "security-core",
            "Security Core",
            null,
            category.Id,
            "Active",
            true,
            null,
            null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Update_plan_feature_mappings_rejects_inactive_plan()
    {
        var mappingRepository = new InMemoryPlanFeatureMappingRepository();
        var planRepository = new InMemorySubscriptionPlanRepository();
        var featureRepository = new InMemoryFeatureDefinitionRepository();
        var plan = await planRepository.CreateAsync(new SubscriptionPlan { Code = "FREE", Name = "Free", IsActive = false });
        var feature = await featureRepository.CreateAsync(new FeatureDefinition { FeatureCode = "SECURITY-CORE", FeatureSlug = "security-core", DisplayName = "Security Core" });
        var handler = CreateMappingHandler(mappingRepository, planRepository, featureRepository);

        var response = await handler.Handle(new UpdatePlanFeatureMappingsCommand(
            plan.Id,
            new UpdatePlanFeatureMappingsRequest([
                new PlanFeatureMappingRequest(feature.Id, "Included", null, null, null)
            ])), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Update_plan_feature_mappings_rejects_archived_feature_mapping()
    {
        var mappingRepository = new InMemoryPlanFeatureMappingRepository();
        var planRepository = new InMemorySubscriptionPlanRepository();
        var featureRepository = new InMemoryFeatureDefinitionRepository();
        var plan = await planRepository.CreateAsync(new SubscriptionPlan { Code = "PRO", Name = "Pro", IsActive = true });
        var feature = await featureRepository.CreateAsync(new FeatureDefinition
        {
            FeatureCode = "SECURITY-CORE",
            FeatureSlug = "security-core",
            DisplayName = "Security Core",
            Status = FeatureDefinitionStatus.Archived
        });
        var handler = CreateMappingHandler(mappingRepository, planRepository, featureRepository);

        var response = await handler.Handle(new UpdatePlanFeatureMappingsCommand(
            plan.Id,
            new UpdatePlanFeatureMappingsRequest([
                new PlanFeatureMappingRequest(feature.Id, "Included", null, null, null)
            ])), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    private static CreateFeatureDefinitionCommandHandler CreateFeatureHandler(
        IFeatureDefinitionRepository featureRepository,
        IFeatureCategoryRepository categoryRepository)
    {
        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(false);

        return new CreateFeatureDefinitionCommandHandler(
            featureRepository,
            categoryRepository,
            currentUser.Object,
            Mock.Of<ILogger<CreateFeatureDefinitionCommandHandler>>());
    }

    private static UpdatePlanFeatureMappingsCommandHandler CreateMappingHandler(
        IPlanFeatureMappingRepository mappingRepository,
        ISubscriptionPlanRepository planRepository,
        IFeatureDefinitionRepository featureRepository)
    {
        return new UpdatePlanFeatureMappingsCommandHandler(
            mappingRepository,
            planRepository,
            featureRepository,
            Mock.Of<ILogger<UpdatePlanFeatureMappingsCommandHandler>>());
    }

    private sealed class InMemoryFeatureDefinitionRepository : IFeatureDefinitionRepository
    {
        private readonly List<FeatureDefinition> _items = [];

        public Task<FeatureDefinition> CreateAsync(FeatureDefinition feature, CancellationToken ct = default)
        {
            _items.Add(feature);
            return Task.FromResult(feature);
        }

        public Task<FeatureDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string featureCode, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.FeatureCode == featureCode && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> ExistsBySlugAsync(string featureSlug, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.FeatureSlug == featureSlug && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> UpdateAsync(FeatureDefinition feature, byte[]? expectedRowVersion = null, CancellationToken ct = default)
        {
            feature.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(true);
        }

        public Task<(IReadOnlyList<FeatureDefinition> Items, long TotalCount)> QueryAsync(FeatureDefinitionsQuery query, CancellationToken ct = default)
        {
            IReadOnlyList<FeatureDefinition> items = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult((items, (long)items.Count));
        }
    }

    private sealed class InMemoryFeatureCategoryRepository : IFeatureCategoryRepository
    {
        private readonly List<FeatureCategory> _items = [];

        public Task<FeatureCategory> CreateAsync(FeatureCategory category, CancellationToken ct = default)
        {
            _items.Add(category);
            return Task.FromResult(category);
        }

        public Task<FeatureCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string categoryCode, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.CategoryCode == categoryCode && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<bool> UpdateAsync(FeatureCategory category, byte[]? expectedRowVersion = null, CancellationToken ct = default)
        {
            category.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<FeatureCategory>> GetAllAsync(FeatureCategoryStatus? status = null, CancellationToken ct = default)
        {
            IReadOnlyList<FeatureCategory> items = _items
                .Where(x => !x.IsDeleted && (!status.HasValue || x.Status == status.Value))
                .ToList();
            return Task.FromResult(items);
        }
    }

    private sealed class InMemorySubscriptionPlanRepository : ISubscriptionPlanRepository
    {
        private readonly List<SubscriptionPlan> _items = [];

        public Task<SubscriptionPlan> CreateAsync(SubscriptionPlan plan, CancellationToken ct = default)
        {
            _items.Add(plan);
            return Task.FromResult(plan);
        }

        public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Code == code && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task<SubscriptionPlan?> GetActiveDefaultAsync(Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.IsActive && x.IsDefault && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<SubscriptionPlan> Items, long TotalCount)> QueryAsync(SubscriptionPlansQuery query, CancellationToken ct = default)
        {
            IReadOnlyList<SubscriptionPlan> items = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult((items, (long)items.Count));
        }

        public Task<IReadOnlyList<SubscriptionPlan>> GetActiveAsync(CancellationToken ct = default)
        {
            IReadOnlyList<SubscriptionPlan> items = _items.Where(x => !x.IsDeleted && x.IsActive).ToList();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<SubscriptionPlan>> GetByIncludedModuleKeyAsync(string moduleKey, CancellationToken ct = default)
        {
            var normalized = moduleKey.Trim().ToUpperInvariant();
            IReadOnlyList<SubscriptionPlan> items = _items
                .Where(x => !x.IsDeleted && x.IncludedModuleKeys.Any(key => string.Equals(key, normalized, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return Task.FromResult(items);
        }

        public Task<SubscriptionPlanSummary> GetSummaryAsync(CancellationToken ct = default) =>
            Task.FromResult(new SubscriptionPlanSummary(_items.Count, _items.Count(x => x.IsActive), _items.Count(x => x.IsTrialPlan), _items.Count(x => !x.IsTrialPlan)));
    }

    private sealed class InMemoryPlanFeatureMappingRepository : IPlanFeatureMappingRepository
    {
        private readonly List<PlanFeatureMapping> _items = [];

        public Task<IReadOnlyList<PlanFeatureMapping>> GetByPlanIdAsync(Guid subscriptionPlanId, CancellationToken ct = default)
        {
            IReadOnlyList<PlanFeatureMapping> items = _items.Where(x => x.SubscriptionPlanId == subscriptionPlanId && !x.IsDeleted).ToList();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyList<PlanFeatureMapping>> GetByFeatureIdAsync(Guid featureDefinitionId, CancellationToken ct = default)
        {
            IReadOnlyList<PlanFeatureMapping> items = _items.Where(x => x.FeatureDefinitionId == featureDefinitionId && !x.IsDeleted).ToList();
            return Task.FromResult(items);
        }

        public Task<PlanFeatureMapping?> GetByPlanAndFeatureAsync(Guid subscriptionPlanId, Guid featureDefinitionId, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.SubscriptionPlanId == subscriptionPlanId && x.FeatureDefinitionId == featureDefinitionId && !x.IsDeleted));

        public Task<bool> UpsertAsync(PlanFeatureMapping mapping, byte[]? expectedRowVersion = null, CancellationToken ct = default)
        {
            _items.Add(mapping);
            return Task.FromResult(true);
        }
    }
}
