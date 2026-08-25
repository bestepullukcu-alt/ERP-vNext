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
        var handler = new CreateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

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
        Assert.Contains("MODULE_CODE_IN_USE", response.Errors);
    }

    [Fact]
    public async Task Create_handler_allows_recreating_soft_deleted_module_code()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var existing = await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "MODULE-CATALOG", ModuleName = "Existing", DisplayName = "Existing", Domain = "Platform", Service = "Diten.Platform" });
        await repository.DeleteAsync(existing.Id);
        var handler = new CreateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

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

        // ExistsByCodeAsync canlı-bazlı olduğu için silinen kod yeni create'i bloke etmez.
        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public async Task Create_handler_persists_workflow_binding_with_server_resolved_object_identity()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var targetTenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var handler = new CreateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "b09-fu01",
            "B09 FU01",
            "B09 FU01",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            null,
            WorkflowBinding: new ModuleCatalogWorkflowBindingRequest(
                targetTenantId,
                true,
                WorkflowDefinitionKey: "B09-FU01",
                CorrelationId: "catalog-binding-corr"))), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var item = await repository.GetByCodeAsync("B09-FU01");
        Assert.NotNull(item);
        Assert.NotNull(item!.WorkflowBinding);
        Assert.Equal("ModuleCatalogItem", item.WorkflowBinding!.ObjectType);
        Assert.Equal(item.Id.ToString(), item.WorkflowBinding.ObjectId);
        Assert.Equal("ModuleCatalogItem:B09-FU01", item.WorkflowBinding.ObjectRef);
        Assert.Equal(targetTenantId, item.WorkflowBinding.TargetTenantId);
        Assert.Equal("WorkflowBindingMetadata", item.WorkflowBinding.TargetTenantSource);
        Assert.True(item.WorkflowBinding.RequiresWorkflowGate);
        Assert.Equal("B09-FU01", item.WorkflowBinding.WorkflowDefinitionKey);
        Assert.Equal("catalog-binding-corr", item.WorkflowBinding.CorrelationId);
    }

    [Fact]
    public void Create_validator_rejects_incomplete_workflow_binding()
    {
        var validator = new CreateModuleCatalogItemCommandValidator();
        var command = new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "b09-fu01",
            "B09 FU01",
            "B09 FU01",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            null,
            WorkflowBinding: new ModuleCatalogWorkflowBindingRequest(Guid.Empty, true)));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.ErrorMessage == "WorkflowBinding.TargetTenantId is required.");
        Assert.Contains(result.Errors, x => x.ErrorMessage == "WorkflowBinding requires WorkflowDefinitionKey or WorkflowTemplateId.");
    }

    [Fact]
    public async Task ExistsByCode_ignores_soft_deleted_records()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var existing = await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "DUP-CODE", ModuleName = "Dup", DisplayName = "Dup", Domain = "Platform", Service = "Diten.Platform" });

        Assert.True(await repository.ExistsByCodeAsync("DUP-CODE"));

        await repository.DeleteAsync(existing.Id);

        Assert.False(await repository.ExistsByCodeAsync("DUP-CODE"));
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
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

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
    public async Task Update_handler_preserves_existing_workflow_binding_when_legacy_request_omits_it()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var binding = new ModuleCatalogWorkflowBindingMetadata
        {
            ObjectType = "ModuleCatalogItem",
            ObjectId = "existing-id",
            ObjectRef = "ModuleCatalogItem:ORIGINAL-CODE",
            TargetTenantId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            TargetTenantSource = "WorkflowBindingMetadata",
            RequiresWorkflowGate = true,
            WorkflowDefinitionKey = "B09-FU01",
            CorrelationId = "existing-corr"
        };
        var item = await repository.CreateAsync(new ModuleCatalogItem
        {
            ModuleCode = "ORIGINAL-CODE",
            ModuleName = "Original",
            DisplayName = "Original",
            Domain = "Platform",
            Service = "Diten.Platform",
            WorkflowBinding = binding
        });
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(new UpdateModuleCatalogItemCommand(item.Id, new UpdateModuleCatalogItemRequest(
            "ORIGINAL-CODE",
            "Original",
            "Updated Display",
            null,
            "Platform",
            "Diten.Platform",
            "Draft",
            "1.0.0",
            false,
            true,
            0)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Same(binding, item.WorkflowBinding);
        Assert.Equal("Updated Display", item.DisplayName);
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

    // ── MC-1b lifecycle ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(ModuleCatalogStatus.Draft, "Preview")]
    [InlineData(ModuleCatalogStatus.Draft, "Beta")]
    [InlineData(ModuleCatalogStatus.Draft, "Active")]
    [InlineData(ModuleCatalogStatus.Preview, "Beta")]
    [InlineData(ModuleCatalogStatus.Preview, "Active")]
    [InlineData(ModuleCatalogStatus.Beta, "Active")]
    [InlineData(ModuleCatalogStatus.Active, "Inactive")]
    [InlineData(ModuleCatalogStatus.Inactive, "Active")]
    [InlineData(ModuleCatalogStatus.Active, "Deprecated")]
    public async Task Update_handler_allows_approved_lifecycle_transitions(ModuleCatalogStatus from, string to)
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(Item("LE", from));
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(UpdateStatus(item, to), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(Enum.Parse<ModuleCatalogStatus>(to), item.Status);
    }

    [Theory]
    [InlineData(ModuleCatalogStatus.Active, "Draft")]   // no demotion
    [InlineData(ModuleCatalogStatus.Active, "Beta")]    // no demotion
    [InlineData(ModuleCatalogStatus.Beta, "Preview")]   // no demotion
    [InlineData(ModuleCatalogStatus.Beta, "Draft")]
    [InlineData(ModuleCatalogStatus.Preview, "Draft")]
    [InlineData(ModuleCatalogStatus.Deprecated, "Active")]
    public async Task Update_handler_rejects_invalid_transitions(ModuleCatalogStatus from, string to)
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(Item("LE", from));
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(UpdateStatus(item, to), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(from, item.Status);
    }

    // ── MC-4 origin governance ────────────────────────────────────────────────
    [Fact]
    public async Task Create_handler_rejects_manual_create_of_self_registered_code()
    {
        var repository = new InMemoryModuleCatalogRepository();
        await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "WORKFLOW", ModuleName = "Workflow", DisplayName = "Workflow", Domain = "P", Service = "S", Origin = ModuleCatalogOrigin.SelfRegistered });
        var handler = new CreateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "workflow", "Workflow", "Workflow", null, "P", "S", "Draft", "1.0.0", false, true, null)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.ModuleManagedByCode, response.Errors);
    }

    [Fact]
    public async Task Create_handler_sets_manual_origin()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var handler = new CreateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(new CreateModuleCatalogItemCommand(new CreateModuleCatalogItemRequest(
            "newmod", "New", "New", null, "P", "S", "Beta", "1.0.0", false, true, null)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = await repository.GetByCodeAsync("NEWMOD");
        Assert.NotNull(created);
        Assert.Equal(ModuleCatalogOrigin.Manual, created!.Origin);
    }

    [Fact]
    public async Task Update_handler_allows_soft_edit_of_self_registered_but_blocks_hard_change()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "WORKFLOW", ModuleName = "Workflow", DisplayName = "Workflow", Domain = "P", Service = "S", Status = ModuleCatalogStatus.Active, ModuleVersion = "1.0.0", Origin = ModuleCatalogOrigin.SelfRegistered });
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        // SOFT-only change (Domain) → allowed.
        var soft = await handler.Handle(UpdateStatus(item, "Active", domain: "Finance"), CancellationToken.None);
        Assert.True(soft.IsSuccessful);
        Assert.Equal("Finance", item.Domain);

        // HARD change (ModuleName) → 409 managed-by-code.
        var hard = await handler.Handle(UpdateStatus(item, "Active", moduleName: "Renamed"), CancellationToken.None);
        Assert.False(hard.IsSuccessful);
        Assert.Equal(409, hard.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.ModuleManagedByCode, hard.Errors);
        Assert.Equal("Workflow", item.ModuleName);
    }

    [Fact]
    public async Task Delete_handler_rejects_self_registered_item()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem { ModuleCode = "WORKFLOW", ModuleName = "Workflow", DisplayName = "Workflow", Domain = "P", Service = "S", Origin = ModuleCatalogOrigin.SelfRegistered });
        var handler = new DeleteModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new DeleteModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.ModuleManagedByCode, response.Errors);
        Assert.False(item.IsDeleted);
    }

    // ── FIX-BASELINE-NO-DEACTIVATE — baseline modules must stay Active (they reach every tenant) ───────────────
    [Fact]
    public async Task Deactivate_handler_refuses_a_baseline_module()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem
        {
            ModuleCode = "ACCESS-GOVERNANCE", ModuleName = "Access Governance", DisplayName = "Access Governance",
            Domain = "P", Service = "S", ModuleVersion = "1.0.0", Status = ModuleCatalogStatus.Active,
            Origin = ModuleCatalogOrigin.SelfRegistered, IsBaseline = true
        });
        var handler = new DeactivateModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new DeactivateModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.BaselineCannotBeDeactivated, response.Errors);
        Assert.Equal(ModuleCatalogStatus.Active, item.Status); // unchanged
    }

    [Fact]
    public async Task Deactivate_handler_still_works_for_a_non_baseline_active_module()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(Item("REGULAR", ModuleCatalogStatus.Active));
        var handler = new DeactivateModuleCatalogItemCommandHandler(repository);

        var response = await handler.Handle(new DeactivateModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(ModuleCatalogStatus.Inactive, item.Status);
    }

    [Fact]
    public async Task Update_handler_refuses_moving_a_baseline_module_off_active()
    {
        var repository = new InMemoryModuleCatalogRepository();
        var item = await repository.CreateAsync(new ModuleCatalogItem
        {
            ModuleCode = "TENANT-SETTINGS", ModuleName = "Tenant Settings", DisplayName = "Tenant Settings",
            Domain = "P", Service = "S", ModuleVersion = "1.0.0", Status = ModuleCatalogStatus.Active,
            Origin = ModuleCatalogOrigin.SelfRegistered, IsBaseline = true
        });
        var handler = new UpdateModuleCatalogItemCommandHandler(repository, PassthroughTaxonomyResolver.Instance);

        var response = await handler.Handle(UpdateStatus(item, "Inactive"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.BaselineCannotBeDeactivated, response.Errors);
        Assert.Equal(ModuleCatalogStatus.Active, item.Status); // unchanged
    }

    private static ModuleCatalogItem Item(string code, ModuleCatalogStatus status) =>
        new() { ModuleCode = code, ModuleName = code, DisplayName = code, Domain = "P", Service = "S", ModuleVersion = "1.0.0", Status = status };

    private static UpdateModuleCatalogItemCommand UpdateStatus(ModuleCatalogItem item, string status, string? moduleName = null, string? domain = null) =>
        new(item.Id, new UpdateModuleCatalogItemRequest(
            item.ModuleCode,
            moduleName ?? item.ModuleName,
            item.DisplayName,
            item.Description,
            domain ?? item.Domain,
            item.Service,
            status,
            item.ModuleVersion,
            item.IsCoreModule,
            item.IsTenantAssignable,
            item.SortOrder));

    // Identity resolver — these tests assert pre-canonicalization rules (codes, status, soft-delete), so the
    // taxonomy resolution is a passthrough (returns the trimmed input unchanged).
    private sealed class PassthroughTaxonomyResolver : Diten.Platform.Application.Features.ModuleCatalog.Services.IModuleTaxonomyResolver
    {
        public static readonly PassthroughTaxonomyResolver Instance = new();

        public Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default)
            => Task.FromResult(rawDomain?.Trim() ?? string.Empty);

        public Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default)
            => Task.FromResult(rawService?.Trim() ?? string.Empty);
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
