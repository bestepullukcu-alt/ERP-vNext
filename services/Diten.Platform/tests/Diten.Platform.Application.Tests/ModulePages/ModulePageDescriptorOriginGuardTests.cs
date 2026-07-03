using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModulePages;

// MC-7 — page/action mutations are refused on a self-registered (code-owned) module; manual modules are unaffected.
public sealed class ModulePageDescriptorOriginGuardTests
{
    private static CreateModulePageDescriptorRequest CreatePageReq() =>
        new("workflow", "PAGE", "Page", "/workflow/page", null, null, true, "List", "Active", 0, null);

    [Fact]
    public async Task Create_page_on_self_registered_module_is_rejected()
    {
        var handler = new CreateModulePageDescriptorCommandHandler(
            new FakePages(), new FakeCatalog(ModuleCatalogOrigin.SelfRegistered), new NoopSync());

        var response = await handler.Handle(new CreateModulePageDescriptorCommand(CreatePageReq()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.ModuleManagedByCode, response.Errors);
    }

    [Fact]
    public async Task Create_page_on_manual_module_succeeds()
    {
        var handler = new CreateModulePageDescriptorCommandHandler(
            new FakePages(), new FakeCatalog(ModuleCatalogOrigin.Manual), new NoopSync());

        var response = await handler.Handle(new CreateModulePageDescriptorCommand(CreatePageReq()), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public async Task Delete_page_on_self_registered_module_is_rejected()
    {
        var pages = new FakePages();
        var descriptor = new ModulePageDescriptor { TenantId = Guid.Empty, ModuleCode = "WORKFLOW", PageCode = "P", DisplayName = "P", RoutePath = "/p" };
        pages.Stored = descriptor;
        var handler = new DeleteModulePageDescriptorCommandHandler(
            pages, new FakeCatalog(ModuleCatalogOrigin.SelfRegistered), new FakeActions(), new NoopSync(),
            NullLogger<DeleteModulePageDescriptorCommandHandler>.Instance);

        var response = await handler.Handle(new DeleteModulePageDescriptorCommand(descriptor.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains(ModuleCatalogErrorCodes.ModuleManagedByCode, response.Errors);
        Assert.False(descriptor.IsDeleted);
    }

    [Fact]
    public async Task Delete_action_on_self_registered_module_is_rejected()
    {
        var actions = new FakeActions();
        var descriptor = new ModulePageActionDescriptor { TenantId = Guid.Empty, ModuleCode = "WORKFLOW", PageCode = "P", ActionCode = "A", DisplayName = "A" };
        actions.Stored = descriptor;
        var handler = new DeleteModulePageActionDescriptorCommandHandler(
            actions, new FakeCatalog(ModuleCatalogOrigin.SelfRegistered), new FakePages(), new NoopSync(),
            NullLogger<DeleteModulePageActionDescriptorCommandHandler>.Instance);

        var response = await handler.Handle(new DeleteModulePageActionDescriptorCommand(descriptor.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.False(descriptor.IsDeleted);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────
    private sealed class FakeCatalog(ModuleCatalogOrigin origin) : IModuleCatalogRepository
    {
        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default)
            => Task.FromResult<ModuleCatalogItem?>(new ModuleCatalogItem { ModuleCode = moduleCode, ModuleName = "M", DisplayName = "M", Domain = "D", Service = "S", Origin = origin });

        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakePages : IModulePageDescriptorRepository
    {
        public ModulePageDescriptor? Stored { get; set; }

        public Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ModulePageDescriptor> CreateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => Task.FromResult(descriptor);
        public Task<ModulePageDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Stored);

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Stored is not null) Stored.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> CountByRequiredPermissionAsync(string permissionKey, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeActions : IModulePageActionDescriptorRepository
    {
        public ModulePageActionDescriptor? Stored { get; set; }

        public Task<ModulePageActionDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Stored);

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Stored is not null) Stored.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ModulePageActionDescriptor> CreateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<long> CountByPermissionKeyAsync(string permissionKey, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class NoopSync : ICatalogPermissionSyncService
    {
        public Task<CatalogPermissionSyncStatus> SyncPermissionAsync(string? permissionKey, string? displayName, CancellationToken ct)
            => Task.FromResult(CatalogPermissionSyncStatus.Synced);
        public Task<CatalogPermissionSyncStatus> RemovePermissionAsync(string? permissionKey, CancellationToken ct)
            => Task.FromResult(CatalogPermissionSyncStatus.Removed);
    }
}
