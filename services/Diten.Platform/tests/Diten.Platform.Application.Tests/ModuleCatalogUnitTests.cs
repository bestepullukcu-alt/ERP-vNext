using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers;
using Diten.Platform.Application.Features.ModuleCatalog.Validators;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class ModuleCatalogUnitTests
{
    [Fact]
    public void CodeNormalizer_ShouldProduceStableUppercaseCodes()
    {
        var code = ModuleCatalogCodeNormalizer.NormalizeToCode("Çözüm Suite 2026");
        Assert.Equal("COZUM-SUITE-2026", code);
    }

    [Fact]
    public void CreateModuleDefinitionValidator_ShouldFail_WhenRequiredFieldsMissing()
    {
        var validator = new CreateModuleDefinitionCommandValidator();
        var result = validator.Validate(new CreateModuleDefinitionCommand("", "", Guid.Empty, Guid.Empty, Guid.Empty));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ImportHandler_ShouldBeIdempotent_AndAvoidPartialCreationForInvalidRows()
    {
        var domainRepo = new InMemoryDomainLandscapeRepository();
        var suiteRepo = new InMemorySuitePlatformRepository();
        var capabilityRepo = new InMemoryCapabilityGroupRepository();
        var moduleRepo = new InMemoryModuleDefinitionRepository();
        var handler = new ImportModuleCatalogCommandHandler(
            domainRepo,
            suiteRepo,
            capabilityRepo,
            moduleRepo,
            new FakeCurrentUserContext());

        var validRow = new ModuleCatalogImportRowDto(
            "MOD-0014",
            "Platform",
            "Catalog",
            "Registry",
            "Module Boundary Registry",
            "Ready",
            "Central catalog foundation",
            "Admin",
            "Platform Team",
            false,
            true,
            "Active");

        var invalidRow = validRow with
        {
            ModuleId = "MOD-INVALID",
            DomainLandscape = "Operations",
            SuitePlatform = "Broken Suite",
            CapabilityGroup = "Broken Capability",
            Status = "UnknownStatus"
        };

        var first = await handler.Handle(new ImportModuleCatalogCommand([validRow, invalidRow]), CancellationToken.None);
        var second = await handler.Handle(new ImportModuleCatalogCommand([validRow]), CancellationToken.None);

        Assert.Equal(1, first.CreatedCount);
        Assert.Equal(1, first.FailedCount);
        Assert.Single(await moduleRepo.GetAllAsync(CancellationToken.None));
        Assert.Single(await domainRepo.GetAllAsync(CancellationToken.None));
        Assert.Single(await suiteRepo.GetAllAsync(CancellationToken.None));
        Assert.Single(await capabilityRepo.GetAllAsync(CancellationToken.None));
        Assert.Contains("invalid", first.FailedRows[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(0, second.UpdatedCount);
        Assert.Equal(1, second.SkippedCount);
        Assert.Single(await moduleRepo.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateModulePageDefinition_ShouldPersistPageUnderModule()
    {
        var moduleRepo = new InMemoryModuleDefinitionRepository();
        var pageRepo = new InMemoryModulePageDefinitionRepository();
        await moduleRepo.CreateAsync(CreateModule("MOD-0014"), CancellationToken.None);

        var handler = new CreateModulePageDefinitionCommandHandler(moduleRepo, pageRepo, new FakeCurrentUserContext());

        var page = await handler.Handle(new CreateModulePageDefinitionCommand(
            "MOD-0014",
            "product_list",
            "Product List",
            RoutePath: "products",
            PageType: "List",
            IsNavigationCandidate: true,
            IsActive: true), CancellationToken.None);

        Assert.Equal("MOD-0014", page.ModuleId);
        Assert.Equal("PRODUCT_LIST", page.PageCode);
        Assert.Equal("/products", page.RoutePath);
        Assert.Equal("List", page.PageType);
        Assert.Single(await pageRepo.GetByModuleIdAsync("MOD-0014", CancellationToken.None));
    }

    [Fact]
    public async Task CreateModulePageDefinition_ShouldBlockDuplicatePageCodeWithinSameModule()
    {
        var moduleRepo = new InMemoryModuleDefinitionRepository();
        var pageRepo = new InMemoryModulePageDefinitionRepository();
        await moduleRepo.CreateAsync(CreateModule("MOD-0014"), CancellationToken.None);

        var handler = new CreateModulePageDefinitionCommandHandler(moduleRepo, pageRepo, new FakeCurrentUserContext());
        var command = new CreateModulePageDefinitionCommand("MOD-0014", "PRODUCT_LIST", "Product List");

        await handler.Handle(command, CancellationToken.None);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("already exists", error.Message);
    }

    [Fact]
    public async Task CreateModulePageDefinition_ShouldBlockInvalidModuleId()
    {
        var handler = new CreateModulePageDefinitionCommandHandler(
            new InMemoryModuleDefinitionRepository(),
            new InMemoryModulePageDefinitionRepository(),
            new FakeCurrentUserContext());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateModulePageDefinitionCommand("MOD-MISSING", "PRODUCT_LIST", "Product List"),
            CancellationToken.None));

        Assert.Contains("could not be found", error.Message);
    }

    [Fact]
    public async Task UpdateModulePageDefinition_ShouldUpdateEditableMetadata()
    {
        var moduleRepo = new InMemoryModuleDefinitionRepository();
        var pageRepo = new InMemoryModulePageDefinitionRepository();
        await moduleRepo.CreateAsync(CreateModule("MOD-0014"), CancellationToken.None);
        var createHandler = new CreateModulePageDefinitionCommandHandler(moduleRepo, pageRepo, new FakeCurrentUserContext());
        await createHandler.Handle(new CreateModulePageDefinitionCommand("MOD-0014", "PRODUCT_LIST", "Product List"), CancellationToken.None);

        var updateHandler = new UpdateModulePageDefinitionCommandHandler(moduleRepo, pageRepo, new FakeCurrentUserContext());
        var updated = await updateHandler.Handle(new UpdateModulePageDefinitionCommand(
            "MOD-0014",
            "PRODUCT_LIST",
            "Products",
            RoutePath: "/products/list",
            PageType: "List",
            IsNavigationCandidate: false,
            IsActive: true), CancellationToken.None);

        Assert.Equal("Products", updated.PageName);
        Assert.Equal("/products/list", updated.RoutePath);
        Assert.False(updated.IsNavigationCandidate);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }

    private abstract class InMemoryGlobalRepository<TEntity> where TEntity : Diten.Platform.Common.Persistence.GlobalEntity
    {
        protected readonly List<TEntity> Items = [];

        public Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TEntity>>(Items.ToArray());

        public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<TEntity> CreateAsync(TEntity entity, CancellationToken ct = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryDomainLandscapeRepository : InMemoryGlobalRepository<DomainLandscape>, IDomainLandscapeRepository
    {
        public Task<DomainLandscape?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Code == code && !x.IsDeleted));
    }

    private sealed class InMemorySuitePlatformRepository : InMemoryGlobalRepository<SuitePlatform>, ISuitePlatformRepository
    {
        public Task<SuitePlatform?> GetByCodeAsync(Guid domainLandscapeId, string code, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.DomainLandscapeId == domainLandscapeId && x.Code == code && !x.IsDeleted));
    }

    private sealed class InMemoryCapabilityGroupRepository : InMemoryGlobalRepository<CapabilityGroup>, ICapabilityGroupRepository
    {
        public Task<CapabilityGroup?> GetByCodeAsync(Guid suitePlatformId, string code, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.SuitePlatformId == suitePlatformId && x.Code == code && !x.IsDeleted));
    }

    private sealed class InMemoryModuleDefinitionRepository : InMemoryGlobalRepository<ModuleDefinition>, IModuleDefinitionRepository
    {
        public Task<ModuleDefinition?> GetByModuleIdAsync(string moduleId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ModuleId == moduleId && !x.IsDeleted));

        public Task<(IReadOnlyList<ModuleDefinition> Items, long TotalCount)> QueryAsync(ModuleDefinitionQuery query, CancellationToken ct = default)
        {
            IReadOnlyList<ModuleDefinition> snapshot = Items.Where(x => !x.IsDeleted).ToArray();
            return Task.FromResult((snapshot, (long)snapshot.Count));
        }
    }

    private sealed class InMemoryModulePageDefinitionRepository : InMemoryGlobalRepository<ModulePageDefinition>, IModulePageDefinitionRepository
    {
        public Task<IReadOnlyList<ModulePageDefinition>> GetByModuleIdAsync(string moduleId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModulePageDefinition>>(Items.Where(x => x.ModuleId == moduleId && !x.IsDeleted).OrderBy(x => x.PageCode).ToArray());

        public Task<ModulePageDefinition?> GetByCodeAsync(string moduleId, string pageCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ModuleId == moduleId && x.PageCode == pageCode && !x.IsDeleted));

        public Task<bool> ExistsByCodeAsync(string moduleId, string pageCode, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x => x.ModuleId == moduleId && x.PageCode == pageCode && !x.IsDeleted && x.Id != excludeId));
    }

    private static ModuleDefinition CreateModule(string moduleId) => new()
    {
        ModuleId = moduleId,
        ModuleName = "Module Boundary Registry",
        DomainLandscapeId = Guid.NewGuid(),
        SuitePlatformId = Guid.NewGuid(),
        CapabilityGroupId = Guid.NewGuid()
    };
}
