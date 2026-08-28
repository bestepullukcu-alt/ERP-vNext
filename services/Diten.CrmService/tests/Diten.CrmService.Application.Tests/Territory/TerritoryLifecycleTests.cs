using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Application.Features.Territory.Nodes;
using Diten.CrmService.Application.Features.Territory.Nodes.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryLifecycleTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(30);

    private static TerritoryModel Model(string status = "draft", string country = "tr", params string[] scopes)
        => new()
        {
            TenantId = TenantId,
            ModelCode = Guid.NewGuid().ToString("N"),
            Name = "Lifecycle",
            Status = status,
            CountryScope = country,
            EffectiveFrom = From,
            EffectiveTo = To,
            BusinessScopes = scopes.Select(s => new TerritoryBusinessScope
            {
                ScopeType = "business-unit", ScopeCode = s
            }).ToList()
        };

    private static TerritoryNode Node(TerritoryModel model, string status = "draft")
        => new()
        {
            TenantId = TenantId, ModelId = model.Id, TerritoryCode = Guid.NewGuid().ToString("N"),
            Name = "Node", TerritoryLevel = "region", Status = status, EffectiveFrom = From, EffectiveTo = To
        };

    private static ActivateTerritoryModelHandler Activate(
        FakeTerritoryModelRepo models, FakeTerritoryNodeRepo nodes, FakeTerritoryLifecycleAuditPublisher audit)
        => new(
            TenantFactory.Tenant(TenantId), models, nodes, new FakeTerritoryReferenceValidator(), audit,
            new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryActivationUnitOfWork(), new FakeTerritoryPlanSnapshotRepo());

    [Fact]
    public async Task Activate_Draft_Activates_Model_And_Nodes()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var audit = new FakeTerritoryLifecycleAuditPublisher();
        var model = Model(scopes: ["alpha"]);
        var node = Node(model);
        models.Items.Add(model);
        nodes.Items.Add(node);

        var response = await Activate(models, nodes, audit)
            .Handle(new ActivateTerritoryModelCommand(model.Id, "go live", "c1"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("active", model.Status);
        Assert.Equal("active", node.Status);
        Assert.Contains(audit.Events, e => e.EventName == "territory.model.activated");
    }

    [Fact]
    public async Task Activate_Archived_Fails()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model("archived");
        models.Items.Add(model);
        nodes.Items.Add(Node(model, "archived"));
        var response = await Activate(models, nodes, new FakeTerritoryLifecycleAuditPublisher())
            .Handle(new ActivateTerritoryModelCommand(model.Id, null, null), default);
        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Activate_SameScope_UnorderedBusinessUnits_Overlapping_Fails()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var active = Model("active", "TR", "beta", "alpha");
        var draft = Model("draft", "tr", "ALPHA", "BETA");
        models.Items.AddRange([active, draft]);
        nodes.Items.Add(Node(draft));
        var response = await Activate(models, nodes, new FakeTerritoryLifecycleAuditPublisher())
            .Handle(new ActivateTerritoryModelCommand(draft.Id, null, null), default);
        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Activate_SameScope_NonOverlapping_Succeeds()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var active = Model("active", "tr", "alpha");
        active.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-10);
        var draft = Model("draft", "tr", "alpha");
        draft.EffectiveFrom = DateTimeOffset.UtcNow;
        models.Items.AddRange([active, draft]);
        nodes.Items.Add(Node(draft));
        var response = await Activate(models, nodes, new FakeTerritoryLifecycleAuditPublisher())
            .Handle(new ActivateTerritoryModelCommand(draft.Id, null, null), default);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Deactivate_Active_Updates_Nodes()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model("active");
        var node = Node(model, "active");
        models.Items.Add(model);
        nodes.Items.Add(node);
        var handler = new DeactivateTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), models, nodes, new FakeTerritoryReferenceValidator(),
            new FakeTerritoryLifecycleAuditPublisher());
        var response = await handler.Handle(new DeactivateTerritoryModelCommand(model.Id, null, null), default);
        Assert.True(response.IsSuccessful);
        Assert.Equal("inactive", model.Status);
        Assert.Equal("inactive", node.Status);
    }

    [Fact]
    public async Task Archive_Active_Fails_But_Inactive_Succeeds()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model("active");
        models.Items.Add(model);
        nodes.Items.Add(Node(model, "active"));
        var handler = new ArchiveTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), models, nodes, new FakeTerritoryReferenceValidator(),
            new FakeTerritoryLifecycleAuditPublisher());
        Assert.False((await handler.Handle(new ArchiveTerritoryModelCommand(model.Id, null, null), default)).IsSuccessful);
        model.Status = "inactive";
        Assert.True((await handler.Handle(new ArchiveTerritoryModelCommand(model.Id, null, null), default)).IsSuccessful);
        Assert.Equal("archived", model.Status);
    }

    [Fact]
    public async Task SoftDelete_Draft_Cascades_To_DraftNodes()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model();
        var node = Node(model);
        models.Items.Add(model);
        nodes.Items.Add(node);
        var handler = new SoftDeleteDraftTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), models, nodes, new FakeTerritoryReferenceValidator(),
            new FakeTerritoryLifecycleAuditPublisher());
        var response = await handler.Handle(new SoftDeleteDraftTerritoryModelCommand(model.Id, null, null), default);
        Assert.True(response.IsSuccessful);
        Assert.True(model.IsDeleted);
        Assert.True(node.IsDeleted);
    }

    [Fact]
    public async Task SoftDelete_Active_Model_Fails()
    {
        var models = new FakeTerritoryModelRepo();
        var model = Model("active");
        models.Items.Add(model);
        var handler = new SoftDeleteDraftTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), models, new FakeTerritoryNodeRepo(),
            new FakeTerritoryReferenceValidator(), new FakeTerritoryLifecycleAuditPublisher());
        var response = await handler.Handle(new SoftDeleteDraftTerritoryModelCommand(model.Id, null, null), default);
        Assert.False(response.IsSuccessful);
        Assert.False(model.IsDeleted);
    }

    [Fact]
    public async Task SoftDelete_Draft_Node_Hides_From_Default_Hierarchy()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model();
        var node = Node(model);
        models.Items.Add(model);
        nodes.Items.Add(node);
        var handler = new SoftDeleteDraftTerritoryNodeHandler(
            TenantFactory.Tenant(TenantId), models, nodes, new FakeTerritoryLifecycleAuditPublisher());
        Assert.True((await handler.Handle(new SoftDeleteDraftTerritoryNodeCommand(model.Id, node.Id, null, null), default)).IsSuccessful);
        Assert.Empty(await nodes.ListByModelAsync(TenantId, model.Id, default));
    }

    [Fact]
    public void ComputedExpiry_Does_Not_Mutate_StoredStatus()
    {
        var model = Model("active");
        model.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);
        var dto = TerritoryModelMapper.ToDetail(model);
        Assert.True(dto.IsExpired);
        Assert.Equal("expired", dto.ComputedStatus);
        Assert.Equal("active", dto.StoredStatus);
        Assert.Equal("active", model.Status);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Lifecycle status vocabulary reconciliation (2026-07-28).
    //
    // The FU02B live smoke found deactivate/archive failing closed in every tenant: `territory-model-status` had no
    // `inactive` and `territory-node-status` had no `archived`, yet the whole suite was green because the reference
    // seam answered Valid for any value. The seam is now vocabulary-aware, so the tests below pin the contract
    // between the MOD-0048 authoring template and the lifecycle the handlers actually write.
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Full_Lifecycle_Chain_Runs_On_Canonical_Published_Vocabulary()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var audit = new FakeTerritoryLifecycleAuditPublisher();
        var references = new FakeTerritoryReferenceValidator();
        var tenant = TenantFactory.Tenant(TenantId);
        var model = Model();
        var node = Node(model);
        models.Items.Add(model);
        nodes.Items.Add(node);

        Assert.True((await new ActivateTerritoryModelHandler(
                tenant, models, nodes, references, audit,
                new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryActivationUnitOfWork(), new FakeTerritoryPlanSnapshotRepo())
            .Handle(new ActivateTerritoryModelCommand(model.Id, null, null), default)).IsSuccessful);
        Assert.Equal("active", model.Status);
        Assert.Equal("active", node.Status);

        Assert.True((await new DeactivateTerritoryModelHandler(tenant, models, nodes, references, audit)
            .Handle(new DeactivateTerritoryModelCommand(model.Id, null, null), default)).IsSuccessful);
        Assert.Equal("inactive", model.Status);
        Assert.Equal("inactive", node.Status);

        Assert.True((await new ArchiveTerritoryModelHandler(tenant, models, nodes, references, audit)
            .Handle(new ArchiveTerritoryModelCommand(model.Id, null, null), default)).IsSuccessful);
        Assert.Equal("archived", model.Status);
        Assert.Equal("archived", node.Status);
    }

    [Fact]
    public async Task Deactivate_Fails_Closed_When_ModelStatus_Does_Not_Publish_Inactive()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model("active");
        var node = Node(model, "active");
        models.Items.Add(model);
        nodes.Items.Add(node);
        var references = new FakeTerritoryReferenceValidator()
            .Unpublish(TerritoryReferenceSets.TerritoryModelStatus, "inactive");

        var response = await new DeactivateTerritoryModelHandler(
                TenantFactory.Tenant(TenantId), models, nodes, references, new FakeTerritoryLifecycleAuditPublisher())
            .Handle(new DeactivateTerritoryModelCommand(model.Id, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("active", model.Status);
        Assert.Equal("active", node.Status);
    }

    [Fact]
    public async Task Archive_Fails_Closed_When_NodeStatus_Does_Not_Publish_Archived()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = Model("inactive");
        var node = Node(model, "inactive");
        models.Items.Add(model);
        nodes.Items.Add(node);
        var references = new FakeTerritoryReferenceValidator()
            .Unpublish(TerritoryReferenceSets.TerritoryNodeStatus, "archived");

        var response = await new ArchiveTerritoryModelHandler(
                TenantFactory.Tenant(TenantId), models, nodes, references, new FakeTerritoryLifecycleAuditPublisher())
            .Handle(new ArchiveTerritoryModelCommand(model.Id, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("inactive", model.Status);
        Assert.Equal("inactive", node.Status);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("archived")]
    public void Every_Status_The_Lifecycle_Writes_Is_In_The_Published_Vocabulary(string status)
    {
        var references = new FakeTerritoryReferenceValidator();
        Assert.Contains(status, references.Vocabulary[TerritoryReferenceSets.TerritoryModelStatus]);
        Assert.Contains(status, references.Vocabulary[TerritoryReferenceSets.TerritoryNodeStatus]);
    }

    [Fact]
    public void Readiness_Descriptors_Match_The_Authoring_Template_Vocabulary_Size()
    {
        var references = new FakeTerritoryReferenceValidator();

        foreach (var setCode in new[] { TerritoryReferenceSets.TerritoryModelStatus, TerritoryReferenceSets.TerritoryNodeStatus })
        {
            var descriptor = TerritoryReferenceSets.Required.Single(d => d.SetCode == setCode);
            Assert.Equal(references.Vocabulary[setCode].Count, descriptor.ExpectedValueCount);
        }
    }
}
