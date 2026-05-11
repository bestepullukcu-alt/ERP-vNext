using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.ModuleCatalog.Validators;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

public sealed class ModuleCatalogRulesTests
{
    [Theory]
    [InlineData(" module__catalog ", "MODULE-CATALOG")]
    [InlineData("-module-_-catalog-", "MODULE-CATALOG")]
    [InlineData("module   catalog", "MODULE-CATALOG")]
    [InlineData("module @ catalog!", "MODULE-CATALOG")]
    public void Normalize_module_code_returns_canonical_code(string input, string expected)
    {
        Assert.Equal(expected, ModuleCatalogCodeNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("AB", true)]
    [InlineData("A", false)]
    public void Create_validator_enforces_module_code_min_length(string moduleCode, bool expectedValid)
    {
        var validator = new CreateModuleCatalogItemCommandValidator();
        var command = ValidCreateCommand(moduleCode);

        var result = validator.Validate(command);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Create_validator_rejects_module_code_longer_than_80_after_normalization()
    {
        var validator = new CreateModuleCatalogItemCommandValidator();
        var command = ValidCreateCommand(new string('A', 81));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("active", "1.0.0", 0)]
    [InlineData("Active", "latest", 0)]
    [InlineData("Active", "v1", 0)]
    [InlineData("Active", "1.0.0", -1)]
    public void Create_validator_rejects_status_version_and_negative_sort_order(string status, string version, int sortOrder)
    {
        var validator = new CreateModuleCatalogItemCommandValidator();
        var command = new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "MOD-001",
            "Module",
            "Module",
            null,
            "Platform",
            "Diten.Platform",
            status,
            version,
            false,
            true,
            sortOrder));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Create_handler_rejects_duplicate_canonical_module_code()
    {
        var repository = new InMemoryModuleCatalogRepository();
        await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "MODULE-CATALOG", ModuleName = "Existing", DisplayName = "Existing", Domain = "Platform", Service = "Diten.Platform" });
        var handler = new CreateModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "module__catalog",
            "Module Catalog",
            "Module Catalog",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Delete_handler_rejects_core_module()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem
        {
            ModuleCode = "CORE",
            ModuleName = "Core",
            DisplayName = "Core",
            Domain = "Platform",
            Service = "Diten.Platform",
            IsCoreModule = true
        });
        var handler = new DeleteModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new DeleteModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.False(item.IsDeleted);
    }

    [Fact]
    public async Task Update_handler_rejects_module_code_change_after_creation()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem
        {
            ModuleCode = "ORIGINAL-CODE",
            ModuleName = "Original",
            DisplayName = "Original",
            Domain = "Platform",
            Service = "Diten.Platform"
        });
        var handler = new UpdateModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new UpdateModuleCatalogItemCommand(item.Id, new UpdateModuleCatalogItemRequest(
            "CHANGED-CODE",
            "Original",
            "Original",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            0)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("ORIGINAL-CODE", item.ModuleCode);
    }

    [Fact]
    public async Task Assignable_repository_returns_only_active_assignable_non_deleted_items()
    {
        var repository = new InMemoryModuleCatalogRepository();
        await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "A", ModuleName = "A", DisplayName = "A", Domain = "P", Service = "S", Status = ModuleCatalogStatus.Active, IsTenantAssignable = true });
        await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "B", ModuleName = "B", DisplayName = "B", Domain = "P", Service = "S", Status = ModuleCatalogStatus.Inactive, IsTenantAssignable = true });
        await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "C", ModuleName = "C", DisplayName = "C", Domain = "P", Service = "S", Status = ModuleCatalogStatus.Active, IsTenantAssignable = false });
        var deleted = await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "D", ModuleName = "D", DisplayName = "D", Domain = "P", Service = "S", Status = ModuleCatalogStatus.Active, IsTenantAssignable = true });
        await repository.DeleteAsync(deleted.Id);

        var items = await repository.GetAssignableAsync();

        Assert.Single(items);
        Assert.Equal("A", items[0].ModuleCode);
    }

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

        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default)
        {
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var item = _items.First(x => x.Id == id);
            item.IsDeleted = true;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default)
        {
            IReadOnlyList<ModuleCatalogItem> items = _items.Where(x => !x.IsDeleted).ToList();
            return Task.FromResult((items, (long)items.Count));
        }

        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ModuleCatalogItem> items = _items
                .Where(x => !x.IsDeleted && x.Status == ModuleCatalogStatus.Active && x.IsTenantAssignable)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.ModuleCode)
                .ToList();
            return Task.FromResult(items);
        }

        public Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default)
        {
            IReadOnlyDictionary<ModuleCatalogStatus, long> stats = _items
                .Where(x => !x.IsDeleted)
                .GroupBy(x => x.Status)
                .ToDictionary(x => x.Key, x => (long)x.Count());

            return Task.FromResult(stats);
        }
    }

    private static CreateModuleCatalogItemCommand ValidCreateCommand(string moduleCode) =>
        new(new CreateModuleCatalogItemRequest(
            moduleCode,
            "Module",
            "Module",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            0));
}
