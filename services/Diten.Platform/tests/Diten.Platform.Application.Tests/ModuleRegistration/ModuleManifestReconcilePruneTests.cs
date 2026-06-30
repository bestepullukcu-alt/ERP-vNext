using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleRegistration;

// MC-6 — reconcile is authoritative: pages/actions the manifest no longer declares are soft-deleted, so the DB
// mirrors the manifest with zero drift. Unit-level manifest tests can't catch this — these verify reconcile STATE.
public sealed class ModuleManifestReconcilePruneTests
{
    private static RegisterModuleManifestCommandHandler Handler(FakePages pages, FakeActions actions, FakeCatalog catalog) =>
        new(catalog, pages, actions, new NoopPermissionSync(), new PassthroughTaxonomyResolver(), NullLogger<RegisterModuleManifestCommandHandler>.Instance);

    // Identity resolver — these tests assert page/action prune STATE, not taxonomy resolution.
    private sealed class PassthroughTaxonomyResolver : Diten.Platform.Application.Features.ModuleCatalog.Services.IModuleTaxonomyResolver
    {
        public Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default)
            => Task.FromResult(rawDomain?.Trim() ?? string.Empty);

        public Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default)
            => Task.FromResult(rawService?.Trim() ?? string.Empty);
    }

    private static ModuleManifestDocument Manifest(params ModuleManifestPage[] pages) =>
        new("recon-test", "Recon", "Recon", "D", "S", "1.0.0", true, 10, pages);

    private static ModuleManifestPage Page(string code, string route, params ModuleManifestAction[] actions) =>
        new(code, code, route, "platform.recon.read", null, true, "List", 10, actions);

    private static ModuleManifestAction Action(string code, string perm) =>
        new(code, code, perm, "Toolbar", 10, false, true, false);

    [Fact]
    public async Task Reconcile_prunes_removed_action_and_removed_page()
    {
        var pages = new FakePages();
        var actions = new FakeActions();
        var catalog = new FakeCatalog();
        var handler = Handler(pages, actions, catalog);

        // Manifest A: PAGE-A [ACT-X, ACT-Y], PAGE-B [ACT-Z].
        var manifestA = Manifest(
            Page("PAGE_A", "/a", Action("ACT_X", "platform.recon.x"), Action("ACT_Y", "platform.recon.y")),
            Page("PAGE_B", "/b", Action("ACT_Z", "platform.recon.z")));
        await handler.Handle(new RegisterModuleManifestCommand(manifestA), CancellationToken.None);

        Assert.Equal(2, pages.Live().Count);
        Assert.Equal(3, actions.Live().Count);

        // Manifest B: ACT-X removed from PAGE-A; PAGE-B removed entirely.
        var manifestB = Manifest(
            Page("PAGE_A", "/a", Action("ACT_Y", "platform.recon.y")));
        var response = await handler.Handle(new RegisterModuleManifestCommand(manifestB), CancellationToken.None);

        // DB == manifest B exactly.
        var livePages = pages.Live();
        Assert.Single(livePages);
        Assert.Equal("PAGE_A", livePages[0].PageCode);

        var liveActions = actions.Live();
        Assert.Single(liveActions);
        Assert.Equal("ACT_Y", liveActions[0].ActionCode);

        // The removed action + the removed page's action are soft-deleted (not hard-gone).
        Assert.Contains(actions.All, a => a.ActionCode == "ACT_X" && a.IsDeleted);
        Assert.Contains(actions.All, a => a.ActionCode == "ACT_Z" && a.IsDeleted);
        Assert.Contains(pages.All, p => p.PageCode == "PAGE_B" && p.IsDeleted);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, response.Data!.PagesPruned);   // PAGE-B
        Assert.Equal(2, response.Data!.ActionsPruned);  // ACT-X + ACT-Z
    }

    [Fact]
    public async Task Reprune_is_idempotent_no_op()
    {
        var pages = new FakePages();
        var actions = new FakeActions();
        var handler = Handler(pages, actions, new FakeCatalog());

        var manifestA = Manifest(Page("PAGE_A", "/a", Action("ACT_X", "platform.recon.x")));
        await handler.Handle(new RegisterModuleManifestCommand(manifestA), CancellationToken.None);

        var manifestB = Manifest(Page("PAGE_A", "/a")); // ACT-X removed
        var first = await handler.Handle(new RegisterModuleManifestCommand(manifestB), CancellationToken.None);
        Assert.Equal(1, first.Data!.ActionsPruned);

        // Second push of B: ACT-X already soft-deleted → nothing left to prune.
        var second = await handler.Handle(new RegisterModuleManifestCommand(manifestB), CancellationToken.None);
        Assert.Equal(0, second.Data!.ActionsPruned);
        Assert.Equal(0, second.Data!.PagesPruned);
        Assert.Empty(actions.Live());
    }

    // ── Fakes (live = IsDeleted==false; Delete soft-deletes) ──────────────────
    private sealed class FakePages : IModulePageDescriptorRepository
    {
        public List<ModulePageDescriptor> All { get; } = [];
        public List<ModulePageDescriptor> Live() => All.Where(p => !p.IsDeleted).ToList();

        public Task<ModulePageDescriptor> CreateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default)
        {
            All.Add(descriptor);
            return Task.FromResult(descriptor);
        }

        public Task<IReadOnlyList<ModulePageDescriptor>> GetByModuleAsync(string moduleCode, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ModulePageDescriptor>>(All.Where(p => p.ModuleCode == moduleCode && !p.IsDeleted).ToList());

        public Task UpdateAsync(ModulePageDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var p = All.FirstOrDefault(x => x.Id == id);
            if (p is not null) p.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ModulePageDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ModuleExistsAsync(string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByPageCodeAsync(string moduleCode, string pageCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByRoutePathAsync(string moduleCode, string routePath, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModulePageDescriptor> Items, long TotalCount)> SearchAsync(ModulePageDescriptorQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeActions : IModulePageActionDescriptorRepository
    {
        public List<ModulePageActionDescriptor> All { get; } = [];
        public List<ModulePageActionDescriptor> Live() => All.Where(a => !a.IsDeleted).ToList();

        public Task<ModulePageActionDescriptor> CreateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default)
        {
            All.Add(descriptor);
            return Task.FromResult(descriptor);
        }

        public Task<IReadOnlyList<ModulePageActionDescriptor>> GetByPageAsync(Guid pageDescriptorId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ModulePageActionDescriptor>>(All.Where(a => a.PageDescriptorId == pageDescriptorId && !a.IsDeleted).ToList());

        public Task UpdateAsync(ModulePageActionDescriptor descriptor, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var a = All.FirstOrDefault(x => x.Id == id);
            if (a is not null) a.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<ModulePageActionDescriptor?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByActionCodeAsync(Guid pageDescriptorId, string actionCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeCatalog : IModuleCatalogRepository
    {
        private readonly List<ModuleCatalogItem> _items = [];

        public Task<ModuleCatalogItem> CreateAsync(ModuleCatalogItem item, CancellationToken ct = default)
        {
            _items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ModuleCatalogItem?> GetByCodeAsync(string moduleCode, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.ModuleCode == moduleCode && !x.IsDeleted));

        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task<ModuleCatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string moduleCode, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Diten.Platform.Domain.Enums.ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoopPermissionSync : ICatalogPermissionSyncService
    {
        public Task<CatalogPermissionSyncStatus> SyncPermissionAsync(string? permissionKey, string? displayName, CancellationToken ct)
            => Task.FromResult(CatalogPermissionSyncStatus.Synced);
    }
}
