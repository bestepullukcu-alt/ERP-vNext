using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.InterfaceRegistry;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.InterfaceRegistry.Validators;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.InterfaceRegistry;

public sealed class InterfaceRegistryBatch1Tests
{
    [Theory]
    [InlineData(" bank.transactions.list ", "BANK.TRANSACTIONS.LIST")]
    [InlineData("ap.invoices.get", "AP.INVOICES.GET")]
    public void Normalize_interface_code_returns_canonical_code(string input, string expected)
    {
        Assert.Equal(expected, InterfaceCodeNormalizer.Normalize(input));
        Assert.True(InterfaceCodeNormalizer.IsValid(input));
    }

    [Theory]
    [InlineData("GET", "/api//bank/transactions/", "v1", "GET:/api/bank/transactions:v1")]
    [InlineData(" post ", " api/platform/interface-registry/manifests/import ", " V1 ", "POST:/api/platform/interface-registry/manifests/import:v1")]
    public void Endpoint_key_normalizer_returns_canonical_key(string method, string route, string version, string expected)
    {
        Assert.Equal(expected, EndpointKeyNormalizer.Create(method, route, version));
        Assert.True(EndpointKeyNormalizer.IsValid(method, route, version));
    }

    [Fact]
    public void Abstraction_attribute_exposes_default_values()
    {
        var attribute = new InterfaceRegistryAttribute("BANK.TRANSACTIONS.LIST", "BANK", "v1");

        Assert.Equal("BANK.TRANSACTIONS.LIST", attribute.Code);
        Assert.Equal(InterfaceStability.Stable, attribute.Stability);
        Assert.Equal(InterfaceVisibility.Platform, attribute.Visibility);
        Assert.Equal(InterfaceLifecycleStatus.Discovered, attribute.LifecycleStatus);
    }

    [Fact]
    public void Import_validator_rejects_duplicate_endpoint_key()
    {
        var validator = new ImportInterfaceManifestRequestValidator();
        var manifest = ValidManifest() with
        {
            Interfaces =
            [
                ValidDefinition() with
                {
                    Endpoints =
                    [
                        new InterfaceEndpointManifest("GET", "/api/bank/transactions", "v1"),
                        new InterfaceEndpointManifest("get", "/api//bank/transactions/", "V1")
                    ]
                }
            ]
        };

        var result = validator.Validate(new ImportInterfaceManifestRequest(manifest));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Manifest contains duplicate EndpointKey values.");
    }

    [Fact]
    public async Task Import_handler_rejects_unknown_owner_module()
    {
        var registry = new InMemoryInterfaceRegistryRepository();
        var modules = new InMemoryModuleCatalogRepository();
        var handler = new ImportInterfaceManifestRequestHandler(registry, modules);

        var response = await handler.Handle(new ImportInterfaceManifestRequest(ValidManifest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(await registry.GetBatchesAsync());
    }

    [Fact]
    public async Task Import_handler_is_idempotent_by_manifest_hash()
    {
        var registry = new InMemoryInterfaceRegistryRepository();
        var modules = new InMemoryModuleCatalogRepository();
        await modules.CreateAsync(new ModuleCatalogItem { ModuleCode = "BANK", ModuleName = "Bank", DisplayName = "Bank", Domain = "PSS", Service = "Diten.Platform" });
        var handler = new ImportInterfaceManifestRequestHandler(registry, modules);
        var command = new ImportInterfaceManifestRequest(ValidManifest());

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.BatchId, second.Data!.BatchId);
        Assert.Single(await registry.GetBatchesAsync());
        Assert.Single(await registry.GetDiffItemsAsync(first.Data.BatchId));
    }

    [Fact]
    public async Task Import_handler_creates_changed_diff_against_active_snapshot()
    {
        var registry = new InMemoryInterfaceRegistryRepository();
        var modules = new InMemoryModuleCatalogRepository();
        await modules.CreateAsync(new ModuleCatalogItem { ModuleCode = "BANK", ModuleName = "Bank", DisplayName = "Bank", Domain = "PSS", Service = "Diten.Platform" });
        registry.ActiveSnapshots.Add(new InterfaceActiveSnapshot
        {
            InterfaceCode = "BANK.TRANSACTIONS.LIST",
            InterfaceVersion = "v1",
            SnapshotHash = "previous",
            Definition = new InterfaceDefinitionSnapshot
            {
                InterfaceCode = "BANK.TRANSACTIONS.LIST",
                InterfaceVersion = "v1",
                DisplayName = "Old",
                OwnerModuleCode = "BANK",
                ProviderService = "Diten.Platform",
                Endpoints = []
            }
        });
        var handler = new ImportInterfaceManifestRequestHandler(registry, modules);

        var response = await handler.Handle(new ImportInterfaceManifestRequest(ValidManifest()), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, response.Data!.ChangedCount);
        Assert.Equal(InterfaceChangeType.Changed, response.Data.DiffItems[0].ChangeType);
    }

    [Fact]
    public async Task Confirm_diff_item_updates_active_snapshot_and_review_metadata()
    {
        var registry = new InMemoryInterfaceRegistryRepository();
        var modules = new InMemoryModuleCatalogRepository();
        await modules.CreateAsync(new ModuleCatalogItem { ModuleCode = "BANK", ModuleName = "Bank", DisplayName = "Bank", Domain = "PSS", Service = "Diten.Platform" });
        var import = await new ImportInterfaceManifestRequestHandler(registry, modules)
            .Handle(new ImportInterfaceManifestRequest(ValidManifest()), CancellationToken.None);
        var diffItemId = import.Data!.DiffItems[0].DiffItemId;
        var handler = new ConfirmInterfaceDiffItemRequestHandler(registry, new TestCurrentUserContext(), new TestAuditSink());

        var response = await handler.Handle(new ConfirmInterfaceDiffItemRequest(diffItemId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(InterfaceReviewDecision.Confirmed, response.Data!.Decision);
        Assert.Equal("platform-admin", response.Data.ReviewedBy);
        Assert.NotNull(await registry.GetActiveSnapshotAsync("BANK.TRANSACTIONS.LIST", "v1"));
    }

    [Fact]
    public async Task Reject_diff_item_requires_reason_and_does_not_update_active_snapshot()
    {
        var validator = new RejectInterfaceDiffItemRequestValidator();
        var validation = validator.Validate(new RejectInterfaceDiffItemRequest(Guid.NewGuid(), " "));

        Assert.False(validation.IsValid);

        var registry = new InMemoryInterfaceRegistryRepository();
        var modules = new InMemoryModuleCatalogRepository();
        await modules.CreateAsync(new ModuleCatalogItem { ModuleCode = "BANK", ModuleName = "Bank", DisplayName = "Bank", Domain = "PSS", Service = "Diten.Platform" });
        var import = await new ImportInterfaceManifestRequestHandler(registry, modules)
            .Handle(new ImportInterfaceManifestRequest(ValidManifest()), CancellationToken.None);
        var diffItemId = import.Data!.DiffItems[0].DiffItemId;
        var handler = new RejectInterfaceDiffItemRequestHandler(registry, new TestCurrentUserContext(), new TestAuditSink());

        var response = await handler.Handle(new RejectInterfaceDiffItemRequest(diffItemId, "Not approved."), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(InterfaceReviewDecision.Rejected, response.Data!.Decision);
        Assert.Null(await registry.GetActiveSnapshotAsync("BANK.TRANSACTIONS.LIST", "v1"));
    }

    [Fact]
    public async Task Deprecate_requires_reason_and_marks_active_snapshot_deprecated()
    {
        var validator = new DeprecateInterfaceRequestValidator();
        var validation = validator.Validate(new DeprecateInterfaceRequest("BANK.TRANSACTIONS.LIST", "v1", ""));

        Assert.False(validation.IsValid);

        var registry = new InMemoryInterfaceRegistryRepository();
        registry.ActiveSnapshots.Add(new InterfaceActiveSnapshot
        {
            InterfaceCode = "BANK.TRANSACTIONS.LIST",
            InterfaceVersion = "v1",
            SnapshotHash = "current",
            Definition = InterfaceRegistryMapper.ToSnapshot(ValidDefinition())
        });
        var handler = new DeprecateInterfaceRequestHandler(registry, new TestCurrentUserContext(), new TestAuditSink());

        var response = await handler.Handle(new DeprecateInterfaceRequest("BANK.TRANSACTIONS.LIST", "v1", "Will be replaced."), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(InterfaceLifecycleStatus.Deprecated, response.Data!.LifecycleStatus);
        Assert.Equal("Will be replaced.", response.Data.DeprecationReason);
    }

    private static InterfaceManifestDocument ValidManifest() =>
        new("Diten.Platform", "BANK", [ValidDefinition()]);

    private static InterfaceDefinitionManifest ValidDefinition() =>
        new(
            "BANK.TRANSACTIONS.LIST",
            "Bank transactions list",
            null,
            "BANK",
            "Diten.Platform",
            "v1",
            InterfaceStability.Stable,
            InterfaceVisibility.Platform,
            InterfaceLifecycleStatus.Discovered,
            [new InterfaceEndpointManifest("GET", "/api/bank/transactions", "v1")],
            []);

    private sealed class InMemoryInterfaceRegistryRepository : IInterfaceRegistryRepository
    {
        private readonly List<InterfaceDiscoveryBatch> _batches = [];
        private readonly List<InterfaceDiscoveryDiffItem> _diffItems = [];
        private readonly List<InterfaceDefinition> _definitions = [];
        public List<InterfaceActiveSnapshot> ActiveSnapshots { get; } = [];

        public Task<InterfaceDiscoveryBatch> CreateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default)
        {
            _batches.Add(batch);
            return Task.FromResult(batch);
        }

        public Task CreateDiffItemsAsync(IReadOnlyList<InterfaceDiscoveryDiffItem> diffItems, CancellationToken ct = default)
        {
            _diffItems.AddRange(diffItems);
            return Task.CompletedTask;
        }

        public Task<InterfaceDiscoveryBatch?> GetBatchByIdAsync(Guid batchId, CancellationToken ct = default) =>
            Task.FromResult(_batches.FirstOrDefault(x => x.BatchId == batchId));

        public Task<InterfaceDiscoveryBatch?> GetBatchByManifestHashAsync(string sourceService, string sourceModuleCode, string manifestHash, CancellationToken ct = default) =>
            Task.FromResult(_batches.FirstOrDefault(x => x.SourceService == sourceService && x.SourceModuleCode == sourceModuleCode && x.ManifestHash == manifestHash));

        public Task<IReadOnlyList<InterfaceDiscoveryBatch>> GetBatchesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<InterfaceDiscoveryBatch>>(_batches.ToList());

        public Task UpdateBatchAsync(InterfaceDiscoveryBatch batch, CancellationToken ct = default)
        {
            var index = _batches.FindIndex(x => x.BatchId == batch.BatchId);
            if (index >= 0)
            {
                _batches[index] = batch;
            }
            return Task.CompletedTask;
        }

        public Task<InterfaceDiscoveryDiffItem?> GetDiffItemByIdAsync(Guid diffItemId, CancellationToken ct = default) =>
            Task.FromResult(_diffItems.FirstOrDefault(x => x.DiffItemId == diffItemId));

        public Task<IReadOnlyList<InterfaceDiscoveryDiffItem>> GetDiffItemsAsync(Guid batchId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<InterfaceDiscoveryDiffItem>>(_diffItems.Where(x => x.BatchId == batchId).ToList());

        public Task UpdateDiffItemAsync(InterfaceDiscoveryDiffItem diffItem, CancellationToken ct = default)
        {
            var index = _diffItems.FindIndex(x => x.DiffItemId == diffItem.DiffItemId);
            if (index >= 0)
            {
                _diffItems[index] = diffItem;
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsDefinitionVersionAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default) =>
            Task.FromResult(_definitions.Any(x => x.InterfaceCode == interfaceCode && x.InterfaceVersion == interfaceVersion));

        public Task<InterfaceActiveSnapshot?> GetActiveSnapshotAsync(string interfaceCode, string interfaceVersion, CancellationToken ct = default) =>
            Task.FromResult(ActiveSnapshots.FirstOrDefault(x => x.InterfaceCode == interfaceCode && x.InterfaceVersion == interfaceVersion));

        public Task<IReadOnlyList<InterfaceActiveSnapshot>> GetActiveSnapshotsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<InterfaceActiveSnapshot>>(ActiveSnapshots.ToList());

        public Task UpsertActiveSnapshotAsync(InterfaceActiveSnapshot snapshot, CancellationToken ct = default)
        {
            var index = ActiveSnapshots.FindIndex(x => x.InterfaceCode == snapshot.InterfaceCode && x.InterfaceVersion == snapshot.InterfaceVersion);
            if (index >= 0)
            {
                ActiveSnapshots[index] = snapshot;
            }
            else
            {
                ActiveSnapshots.Add(snapshot);
            }
            return Task.CompletedTask;
        }

        public Task UpsertDefinitionAsync(InterfaceDefinition definition, CancellationToken ct = default)
        {
            var index = _definitions.FindIndex(x => x.InterfaceCode == definition.InterfaceCode && x.InterfaceVersion == definition.InterfaceVersion);
            if (index >= 0)
            {
                _definitions[index] = definition;
            }
            else
            {
                _definitions.Add(definition);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public string? Email { get; } = "admin@example.test";
        public string? DisplayName { get; } = "Platform Admin";
        public string ActorName { get; } = "platform-admin";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class TestAuditSink : IInterfaceRegistryAuditSink
    {
        public List<string> Events { get; } = [];

        public Task EmitAsync(string eventName, IReadOnlyDictionary<string, string?> metadata, CancellationToken ct = default)
        {
            Events.Add(eventName);
            return Task.CompletedTask;
        }
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

        public Task UpdateAsync(ModuleCatalogItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.First(x => x.Id == id).IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<ModuleCatalogItem> Items, long TotalCount)> QueryAsync(ModuleCatalogQuery query, CancellationToken ct = default) =>
            Task.FromResult(((IReadOnlyList<ModuleCatalogItem>)_items.ToList(), (long)_items.Count));

        public Task<IReadOnlyList<ModuleCatalogItem>> GetAssignableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModuleCatalogItem>>(_items.ToList());

        public Task<IReadOnlyDictionary<Diten.Platform.Domain.Enums.ModuleCatalogStatus, long>> GetStatsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Diten.Platform.Domain.Enums.ModuleCatalogStatus, long>>(
                new Dictionary<Diten.Platform.Domain.Enums.ModuleCatalogStatus, long>());
    }
}
