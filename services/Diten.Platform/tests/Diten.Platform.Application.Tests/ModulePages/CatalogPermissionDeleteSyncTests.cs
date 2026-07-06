using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Application.Features.ModulePages.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModulePages;

// FEAT-CATALOG-PERM-DELETE-SYNC — deleting a catalog page descriptor requests removal of its permission from
// AuthService ONLY when it was the last live reference; a still-referenced permission is left in place; and any
// removal failure is best-effort (the catalog delete still succeeds).
public sealed class CatalogPermissionDeleteSyncTests
{
    private const string ModuleCode = "workflow";
    private const string PermissionKey = "platform.workflow.definitions.view";

    private static DeleteModulePageDescriptorCommandHandler Handler(
        FakePages pages, FakeActions actions, CapturingSync sync) =>
        new(pages, new FakeCatalog(), actions, sync, NullLogger<DeleteModulePageDescriptorCommandHandler>.Instance);

    private static ModulePageDescriptor Page(string? requiredPermission) => new()
    {
        TenantId = Guid.Empty, ModuleCode = ModuleCode, PageCode = "DEFS", DisplayName = "Definitions",
        RoutePath = "/Platform/Workflow", RequiredPermission = requiredPermission
    };

    [Fact]
    public async Task Deleting_the_last_reference_requests_permission_removal()
    {
        var pages = new FakePages(Page(PermissionKey)) { RemainingPageRefs = 0 };
        var actions = new FakeActions { RemainingActionRefs = 0 };
        var sync = new CapturingSync();

        var response = await Handler(pages, actions, sync).Handle(new DeleteModulePageDescriptorCommand(pages.Stored!.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PermissionKey, sync.RemovedKey); // last reference → removal requested
    }

    [Fact]
    public async Task Still_referenced_permission_is_not_removed()
    {
        var pages = new FakePages(Page(PermissionKey)) { RemainingPageRefs = 1 }; // another live page still uses it
        var actions = new FakeActions { RemainingActionRefs = 0 };
        var sync = new CapturingSync();

        var response = await Handler(pages, actions, sync).Handle(new DeleteModulePageDescriptorCommand(pages.Stored!.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(sync.RemovedKey); // SkippedStillReferenced — not removed
    }

    [Fact]
    public async Task Empty_permission_key_skips_removal()
    {
        var pages = new FakePages(Page(null));
        var sync = new CapturingSync();

        var response = await Handler(pages, new FakeActions(), sync).Handle(new DeleteModulePageDescriptorCommand(pages.Stored!.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(sync.RemovedKey);
    }

    [Fact]
    public async Task Removal_failure_is_best_effort_and_delete_still_succeeds()
    {
        var pages = new FakePages(Page(PermissionKey)) { RemainingPageRefs = 0, ThrowOnCount = true };
        var sync = new CapturingSync();

        // A count/removal failure must NOT fail the already-committed delete.
        var response = await Handler(pages, new FakeActions(), sync).Handle(new DeleteModulePageDescriptorCommand(pages.Stored!.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.True(pages.Stored!.IsDeleted); // the delete stands
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────
    private sealed class CapturingSync : ICatalogPermissionSyncService
    {
        public string? RemovedKey { get; private set; }
        public Task<CatalogPermissionSyncStatus> RemovePermissionAsync(string? permissionKey, CancellationToken ct)
        {
            RemovedKey = permissionKey;
            return Task.FromResult(CatalogPermissionSyncStatus.Removed);
        }
        public Task<CatalogPermissionSyncStatus> SyncPermissionAsync(string? permissionKey, string? displayName, string? moduleCode, string? scope, CancellationToken ct)
            => Task.FromResult(CatalogPermissionSyncStatus.Synced);
    }

    private sealed class FakeCatalog : IModuleCatalogRepository
    {
        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default)
            => Task.FromResult<ModuleCatalogItem?>(new ModuleCatalogItem { ModuleCode = moduleCode, ModuleName = "M", DisplayName = "M", Domain = "D", Service = "S", Origin = ModuleCatalogOrigin.Manual });
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
        public ModulePageDescriptor? Stored { get; }
        public long RemainingPageRefs { get; set; }
        public bool ThrowOnCount { get; set; }

        public FakePages(ModulePageDescriptor stored) => Stored = stored;

        public Task<ModulePageDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Stored);
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Stored is not null) Stored.IsDeleted = true;
            return Task.CompletedTask;
        }
        public Task<long> CountByRequiredPermissionAsync(string permissionKey, CancellationToken ct = default)
            => ThrowOnCount ? throw new InvalidOperationException("db down") : Task.FromResult(RemainingPageRefs);

        public Task<ModulePageDescriptor> CreateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeActions : IModulePageActionDescriptorRepository
    {
        public long RemainingActionRefs { get; set; }
        public Task<long> CountByPermissionKeyAsync(string permissionKey, CancellationToken ct = default) => Task.FromResult(RemainingActionRefs);

        public Task<ModulePageActionDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ModulePageActionDescriptor> CreateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
