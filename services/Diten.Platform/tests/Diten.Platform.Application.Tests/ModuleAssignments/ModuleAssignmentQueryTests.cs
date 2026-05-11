using Diten.Platform.Application.Features.ModuleAssignments;
using Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleAssignments;

public sealed class ModuleAssignmentQueryTests
{
    [Fact]
    public async Task Overview_returns_backed_plan_count_and_degraded_tenant_counts()
    {
        var moduleRepository = new InMemoryModuleCatalogRepository();
        var planRepository = new InMemorySubscriptionPlanRepository();
        await moduleRepository.CreateAsync(Module("CRM"));
        await planRepository.CreateAsync(Plan("PRO", "Professional", ["CRM"]));
        await planRepository.CreateAsync(Plan("ENT", "Enterprise", ["CRM"]));
        var handler = new GetModuleAssignmentOverviewQueryHandler(
            moduleRepository,
            planRepository,
            NullLogger<GetModuleAssignmentOverviewQueryHandler>.Instance);

        var response = await handler.Handle(new GetModuleAssignmentOverviewQuery("crm", "corr-1"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data?.PlanAssignmentCount);
        Assert.Null(response.Data?.TenantAssignmentCount);
        Assert.Contains(response.Data!.DependencyStates, x => x.Source == "TenantModuleAssignment" && x.Status == "Unavailable");
    }

    [Fact]
    public async Task Plan_assignments_apply_status_and_search_filters()
    {
        var moduleRepository = new InMemoryModuleCatalogRepository();
        var planRepository = new InMemorySubscriptionPlanRepository();
        await moduleRepository.CreateAsync(Module("CRM"));
        await planRepository.CreateAsync(Plan("PRO", "Professional", ["CRM"], isActive: true));
        await planRepository.CreateAsync(Plan("LEGACY", "Legacy", ["CRM"], isActive: false));
        var handler = new GetModulePlanAssignmentsQueryHandler(
            moduleRepository,
            planRepository,
            NullLogger<GetModulePlanAssignmentsQueryHandler>.Instance);

        var response = await handler.Handle(
            new GetModulePlanAssignmentsQuery("CRM", new ModulePlanAssignmentFilterRequest("Active", "pro")),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
        Assert.Equal("PRO", response.Data.Items[0].PlanCode);
    }

    [Fact]
    public async Task Missing_module_returns_404()
    {
        var handler = new GetModulePlanAssignmentsQueryHandler(
            new InMemoryModuleCatalogRepository(),
            new InMemorySubscriptionPlanRepository(),
            NullLogger<GetModulePlanAssignmentsQueryHandler>.Instance);

        var response = await handler.Handle(
            new GetModulePlanAssignmentsQuery("MISSING", new ModulePlanAssignmentFilterRequest(null, null)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_assignments_return_controlled_degraded_state_when_source_is_missing()
    {
        var moduleRepository = new InMemoryModuleCatalogRepository();
        await moduleRepository.CreateAsync(Module("CRM"));
        var handler = new GetModuleTenantAssignmentsQueryHandler(
            moduleRepository,
            NullLogger<GetModuleTenantAssignmentsQueryHandler>.Instance);

        var response = await handler.Handle(
            new GetModuleTenantAssignmentsQuery("CRM", new ModuleTenantAssignmentFilterRequest("Plan", "Enabled", null, null)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!.Items);
        Assert.Contains(response.Data.DependencyStates, x => x.Status == "Unavailable");
    }

    [Fact]
    public async Task Tenant_assignments_reject_invalid_source_filter()
    {
        var moduleRepository = new InMemoryModuleCatalogRepository();
        await moduleRepository.CreateAsync(Module("CRM"));
        var handler = new GetModuleTenantAssignmentsQueryHandler(
            moduleRepository,
            NullLogger<GetModuleTenantAssignmentsQueryHandler>.Instance);

        var response = await handler.Handle(
            new GetModuleTenantAssignmentsQuery("CRM", new ModuleTenantAssignmentFilterRequest("Bogus", null, null, null)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    private static ModuleCatalogItem Module(string moduleCode) => new()
    {
        ModuleCode = moduleCode,
        ModuleName = moduleCode,
        DisplayName = moduleCode,
        Domain = "Platform",
        Service = "Diten.Platform",
        Status = ModuleCatalogStatus.Active,
        ModuleVersion = "1.0.0",
        IsTenantAssignable = true
    };

    private static SubscriptionPlan Plan(string code, string name, IReadOnlyList<string> moduleKeys, bool isActive = true) => new()
    {
        Code = code,
        Name = name,
        IsActive = isActive,
        IncludedModuleKeys = moduleKeys,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class InMemoryModuleCatalogRepository : IModuleCatalogRepository
    {
        private readonly List<ModuleCatalogItem> _items = [];

        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default)
        {
            _items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.ModuleCode == moduleCode && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(_items.Any(x => x.ModuleCode == moduleCode && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value)));

        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var item = _items.First(x => x.Id == id);
            item.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default)
        {
            IReadOnlyList<ModuleCatalogItem> items = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult((items, (long)items.Count));
        }

        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ModuleCatalogItem> items = _items.Where(x => !x.IsDeleted && x.IsTenantAssignable).ToList();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default)
        {
            IReadOnlyDictionary<ModuleCatalogStatus, long> stats = new Dictionary<ModuleCatalogStatus, long>();
            return Task.FromResult(stats);
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
            Task.FromResult(_items.FirstOrDefault(x => x.IsActive && x.IsDefault && (!excludeId.HasValue || x.Id != excludeId.Value)));

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
}
