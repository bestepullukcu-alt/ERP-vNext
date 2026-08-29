using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryVersioningFu05BTests
{
    private static readonly Guid TenantId = Guid.Parse("91919191-9191-9191-9191-919191919191");

    [Fact]
    public async Task Create_from_active_model_clones_hierarchy_and_rules_with_new_ids()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var rules = new FakeTerritoryAssignmentRuleRepo();
        var clone = new FakeTerritoryDraftCloneUnitOfWork();
        var source = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = "TM-V3", Name = "Setonda TR", Status = "active", VersionNumber = 3
        };
        models.Items.Add(source);
        var parent = new TerritoryNode
        {
            TenantId = TenantId, ModelId = source.Id, TerritoryCode = "EDIRNE", Name = "Edirne",
            TerritoryLevel = "zone", Status = "active"
        };
        var child = new TerritoryNode
        {
            TenantId = TenantId, ModelId = source.Id, ParentTerritoryId = parent.Id,
            TerritoryCode = "KESAN", Name = "Keşan", TerritoryLevel = "microzone", Status = "active"
        };
        nodes.Items.AddRange([parent, child]);
        rules.Items.Add(new TerritoryAssignmentRule
        {
            TenantId = TenantId, ModelId = source.Id, TerritoryId = child.Id, RuleCode = "KESAN-HOSPITAL",
            Name = "Keşan hospitals", RuleType = "geography", ConflictPolicy = "block", Criteria = new TerritoryRuleCriteria
            {
                CityRefs = ["edirne"], DistrictRefs = ["kesan"]
            }
        });
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantId), models,
            new FakeTerritoryReferenceValidator(), nodes, rules, clone);
        var from = DateTimeOffset.UtcNow.Date;

        var result = await handler.Handle(new CreateTerritoryModelCommand(
            "TM-V4", "Setonda TR v4", "tr", null, from, null, source.Id, "new rules", "clone-test"), default);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(clone.Model);
        Assert.Equal(4, clone.Model.VersionNumber);
        Assert.Equal(source.Id, clone.Model.BasedOnModelId);
        Assert.Equal("draft", clone.Model.Status);
        Assert.Equal(2, clone.Nodes.Count);
        Assert.All(clone.Nodes, node => Assert.Equal("draft", node.Status));
        var clonedParent = clone.Nodes.Single(node => node.TerritoryCode == "EDIRNE");
        var clonedChild = clone.Nodes.Single(node => node.TerritoryCode == "KESAN");
        Assert.NotEqual(parent.Id, clonedParent.Id);
        Assert.Equal(clonedParent.Id, clonedChild.ParentTerritoryId);
        Assert.Equal(clonedChild.Id, clone.Rules.Single().TerritoryId);
        Assert.NotSame(rules.Items.Single().Criteria, clone.Rules.Single().Criteria);
    }

    [Fact]
    public async Task Activating_draft_version_supersedes_source_and_carries_account_coverage()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var rules = new FakeTerritoryAssignmentRuleRepo();
        var resources = new FakeTerritoryResourceAssignmentRepo();
        var accounts = new FakeAccountTerritoryAssignmentRepo();
        var unitOfWork = new FakeTerritoryActivationUnitOfWork();
        var effectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1);
        var source = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = "TM-V1", Name = "Setonda TR", Status = "active",
            VersionNumber = 1, CountryScope = "tr", EffectiveFrom = effectiveFrom.AddYears(-1)
        };
        var target = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = "TM-V2", Name = "Setonda TR v2", Status = "draft",
            VersionNumber = 2, BasedOnModelId = source.Id, CountryScope = "tr", EffectiveFrom = effectiveFrom
        };
        models.Items.AddRange([source, target]);
        var targetNode = new TerritoryNode
        {
            TenantId = TenantId, ModelId = target.Id, TerritoryCode = "KESAN", Name = "Keşan",
            TerritoryLevel = "microzone", Status = "draft", EffectiveFrom = effectiveFrom
        };
        nodes.Items.Add(targetNode);
        var targetRule = new TerritoryAssignmentRule
        {
            TenantId = TenantId, ModelId = target.Id, TerritoryId = targetNode.Id,
            RuleCode = "KESAN-HOSPITAL", Name = "Keşan hospitals", RuleType = "geography", ConflictPolicy = "block"
        };
        rules.Items.Add(targetRule);
        var sourceAssignment = new AccountTerritoryAssignment
        {
            TenantId = TenantId, AccountId = Guid.NewGuid(), AccountCode = "ACC-1", AccountDisplayName = "Özel Keşan Hastanesi",
            TerritoryModelId = source.Id, TerritoryNodeId = Guid.NewGuid(), TerritoryNodeCode = "KESAN", TerritoryNodeName = "Keşan",
            AssignmentSource = "rule-preview", AssignmentStatus = "active", EffectiveFrom = effectiveFrom.AddMonths(-3),
            AppliedRuleId = Guid.NewGuid(), AppliedRuleCode = "KESAN-HOSPITAL", ConflictPolicy = "block"
        };
        accounts.Items.Add(sourceAssignment);
        var handler = new ActivateTerritoryModelHandler(TenantFactory.Tenant(TenantId), models, nodes,
            new FakeTerritoryReferenceValidator(), new FakeTerritoryLifecycleAuditPublisher(), resources, unitOfWork,
            new FakeTerritoryPlanSnapshotRepo(), accounts, rules);

        var result = await handler.Handle(new ActivateTerritoryModelCommand(target.Id, "publish version", "cutover-test"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal("active", target.Status);
        Assert.Equal("inactive", unitOfWork.SupersededSourceModel?.Status);
        Assert.Equal("ended", unitOfWork.EndedAccountAssignments.Single().AssignmentStatus);
        var carried = unitOfWork.CreatedAccountAssignments.Single();
        Assert.Equal(target.Id, carried.TerritoryModelId);
        Assert.Equal(targetNode.Id, carried.TerritoryNodeId);
        Assert.Equal(targetRule.Id, carried.AppliedRuleId);
        Assert.Equal(sourceAssignment.Id, carried.MigratedFromAssignmentId);
        Assert.Equal(source.Id, carried.MigratedFromModelId);
    }

    [Fact]
    public async Task Activation_fails_closed_when_account_territory_code_cannot_be_mapped()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var accounts = new FakeAccountTerritoryAssignmentRepo();
        var source = new TerritoryModel { TenantId = TenantId, ModelCode = "S", Name = "Source", Status = "active" };
        var target = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = "T", Name = "Target", Status = "draft", BasedOnModelId = source.Id,
            EffectiveFrom = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        models.Items.AddRange([source, target]);
        nodes.Items.Add(new TerritoryNode
        {
            TenantId = TenantId, ModelId = target.Id, TerritoryCode = "EDIRNE", Name = "Edirne", Status = "draft"
        });
        accounts.Items.Add(new AccountTerritoryAssignment
        {
            TenantId = TenantId, TerritoryModelId = source.Id, TerritoryNodeCode = "KESAN",
            AssignmentStatus = "active", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var uow = new FakeTerritoryActivationUnitOfWork();
        var handler = new ActivateTerritoryModelHandler(TenantFactory.Tenant(TenantId), models, nodes,
            new FakeTerritoryReferenceValidator(), new FakeTerritoryLifecycleAuditPublisher(),
            new FakeTerritoryResourceAssignmentRepo(), uow, new FakeTerritoryPlanSnapshotRepo(), accounts,
            new FakeTerritoryAssignmentRuleRepo());

        var result = await handler.Handle(new ActivateTerritoryModelCommand(target.Id, null, null), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Null(uow.SupersededSourceModel);
        Assert.Equal("draft", target.Status);
        Assert.Equal("active", source.Status);
    }
}
