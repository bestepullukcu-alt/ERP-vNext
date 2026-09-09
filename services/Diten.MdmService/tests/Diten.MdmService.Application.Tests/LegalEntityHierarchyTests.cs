using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.LegalEntity.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.LegalEntity.Queries;
using Diten.MdmService.Application.Features.LegalEntity.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LegalEntityHierarchyTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static (InMemoryLegalEntityRepository Repo, LegalEntity Holding, LegalEntity Medikal, LegalEntity Teknoloji) SeedTree()
    {
        var holding = new LegalEntity { Id = Guid.NewGuid(), TenantId = TenantId, Code = "GRAND-HOLDING", LegalName = "GRAND HOLDING", LifecycleStatus = LegalEntityLifecycleStatus.Active };
        var medikal = new LegalEntity { Id = Guid.NewGuid(), TenantId = TenantId, Code = "GRAND-MEDIKAL", LegalName = "GRAND MEDIKAL", ParentId = holding.Id, LifecycleStatus = LegalEntityLifecycleStatus.Active };
        var teknoloji = new LegalEntity { Id = Guid.NewGuid(), TenantId = TenantId, Code = "GRAND-TEKNOLOJI", LegalName = "GRAND TEKNOLOJI", ParentId = holding.Id, LifecycleStatus = LegalEntityLifecycleStatus.Active };
        var repo = new InMemoryLegalEntityRepository(TenantId, new[] { holding, medikal, teknoloji });
        return (repo, holding, medikal, teknoloji);
    }

    [Fact]
    public async Task Descendants_of_root_returns_self_plus_all_children()
    {
        var (repo, holding, medikal, teknoloji) = SeedTree();
        var handler = new GetLegalEntityDescendantsHandler(new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new GetLegalEntityDescendantsQuery(holding.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var ids = response.Data!.LegalEntityIds;
        Assert.Equal(3, ids.Count);
        Assert.Contains(holding.Id, ids);
        Assert.Contains(medikal.Id, ids);
        Assert.Contains(teknoloji.Id, ids);
    }

    [Fact]
    public async Task Descendants_of_leaf_returns_only_self()
    {
        var (repo, _, medikal, _) = SeedTree();
        var handler = new GetLegalEntityDescendantsHandler(new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new GetLegalEntityDescendantsQuery(medikal.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.LegalEntityIds);
        Assert.Equal(medikal.Id, response.Data!.LegalEntityIds[0]);
    }

    [Fact]
    public async Task Descendants_of_unknown_root_returns_404()
    {
        var (repo, _, _, _) = SeedTree();
        var handler = new GetLegalEntityDescendantsHandler(new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new GetLegalEntityDescendantsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_valid_parent_sets_parent()
    {
        var (repo, holding, _, _) = SeedTree();
        var handler = new CreateLegalEntityHandler(repo);

        var response = await handler.Handle(new CreateLegalEntityCommand("GRAND-LOJISTIK", "GRAND LOJISTIK", null, holding.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var created = repo.Entities.Single(e => e.Code == "GRAND-LOJISTIK");
        Assert.Equal(holding.Id, created.ParentId);
    }

    [Fact]
    public async Task Create_with_unknown_parent_fails()
    {
        var (repo, _, _, _) = SeedTree();
        var handler = new CreateLegalEntityHandler(repo);

        var response = await handler.Handle(new CreateLegalEntityCommand("GRAND-LOJISTIK", "GRAND LOJISTIK", null, Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task SetParent_self_is_rejected()
    {
        var (repo, holding, _, _) = SeedTree();
        var handler = new SetLegalEntityParentHandler(repo, new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new SetLegalEntityParentCommand(holding.Id, holding.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task SetParent_cycle_is_rejected()
    {
        // holding -> medikal. Attempt to set holding's parent = medikal (its own descendant) -> cycle.
        var (repo, holding, medikal, _) = SeedTree();
        var handler = new SetLegalEntityParentHandler(repo, new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new SetLegalEntityParentCommand(holding.Id, medikal.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task SetParent_to_missing_parent_is_rejected()
    {
        var (repo, _, medikal, _) = SeedTree();
        var handler = new SetLegalEntityParentHandler(repo, new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new SetLegalEntityParentCommand(medikal.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task SetParent_valid_reparent_succeeds()
    {
        // Add an orphan root, then parent it under holding.
        var (repo, holding, _, _) = SeedTree();
        var orphan = await repo.CreateAsync(new LegalEntity { Id = Guid.NewGuid(), Code = "GRAND-ORPHAN", LegalName = "GRAND ORPHAN", LifecycleStatus = LegalEntityLifecycleStatus.Active }, CancellationToken.None);
        var handler = new SetLegalEntityParentHandler(repo, new LegalEntityHierarchyResolver(repo));

        var response = await handler.Handle(new SetLegalEntityParentCommand(orphan.Id, holding.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Equal(holding.Id, repo.Entities.Single(e => e.Id == orphan.Id).ParentId);
    }

    [Fact]
    public async Task List_returns_all_tenant_entities()
    {
        var (repo, _, _, _) = SeedTree();
        var handler = new GetLegalEntitiesHandler(repo);

        var response = await handler.Handle(new GetLegalEntitiesQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(3, response.Data!.Count);
    }
}
