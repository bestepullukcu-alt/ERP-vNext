using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.AssignmentRules;
using Diten.CrmService.Application.Features.Territory.AssignmentRules.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>MOD-0151 FU03 — assignment rule CRUD. Rules are draft-only, reference-validated and criteria-whitelisted.</summary>
public sealed class TerritoryAssignmentRuleTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(30);

    private static TerritoryModel Model(string status = "draft")
        => new()
        {
            TenantId = TenantId, ModelCode = Guid.NewGuid().ToString("N"), Name = "M", Status = status,
            CountryScope = "tr", EffectiveFrom = From, EffectiveTo = To
        };

    private static TerritoryNode Node(TerritoryModel model, string code = "Z1")
        => new()
        {
            TenantId = TenantId, ModelId = model.Id, TerritoryCode = code, Name = code, TerritoryLevel = "zone",
            Status = "draft", EffectiveFrom = From, EffectiveTo = To
        };

    private sealed record Ctx(
        FakeTerritoryModelRepo Models, FakeTerritoryNodeRepo Nodes, FakeTerritoryAssignmentRuleRepo Rules,
        FakeTerritoryReferenceValidator References, TerritoryModel Model, TerritoryNode TargetNode);

    private static Ctx NewCtx(string modelStatus = "draft")
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var rules = new FakeTerritoryAssignmentRuleRepo();
        var model = Model(modelStatus);
        var node = Node(model);
        models.Items.Add(model);
        nodes.Items.Add(node);
        return new Ctx(models, nodes, rules, new FakeTerritoryReferenceValidator(), model, node);
    }

    private static CreateTerritoryAssignmentRuleHandler Create(Ctx c)
        => new(TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules, c.References);

    private static CreateTerritoryAssignmentRuleCommand Cmd(
        Ctx c, string code = "R1", string ruleType = "geography", string policy = "priority",
        int priority = 10, bool enabled = true, TerritoryRuleCriteriaInput? criteria = null, Guid? nodeId = null)
        => new(c.Model.Id, code, "Rule " + code, nodeId ?? c.TargetNode.Id, ruleType, policy, priority, enabled,
            criteria ?? new TerritoryRuleCriteriaInput(CountryRefs: ["tr"]), From, To, "corr");

    // ---------------------------------------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Rule_Succeeds_On_Draft_Model()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var rule = Assert.Single(c.Rules.Items);
        Assert.Equal("R1", rule.RuleCode);
        Assert.Equal(TenantId, rule.TenantId);
        Assert.Equal(c.TargetNode.Id, rule.TerritoryId);
        Assert.Equal(["tr"], rule.Criteria.CountryRefs);
    }

    [Fact]
    public async Task Create_Duplicate_RuleCode_Fails_409()
    {
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c), default)).IsSuccessful);

        var response = await Create(c).Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Single(c.Rules.Items);
    }

    [Fact]
    public async Task Create_Unpublished_RuleType_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, ruleType: "not-a-published-type"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(c.Rules.Items);
    }

    [Fact]
    public async Task Create_Published_But_Unsupported_RuleType_Fails_400_As_LaterFu()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, ruleType: "product-portfolio"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("later FU", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Invalid_ConflictPolicy_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, policy: "whatever"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Fails_Closed_When_RuleType_Set_Unpublished()
    {
        var c = NewCtx();
        c.References.MissingSets.Add(TerritoryReferenceSets.TerritoryRuleType);

        var response = await Create(c).Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("not published", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_TargetNode_Outside_Model_Fails_404()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, nodeId: Guid.NewGuid()), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("archived")]
    public async Task Create_On_NonDraft_Model_Fails_409(string status)
    {
        var c = NewCtx(status);
        var response = await Create(c).Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(c.Rules.Items);
    }

    [Fact]
    public async Task Create_Empty_Criteria_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, criteria: new TerritoryRuleCriteriaInput()), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Geography_Rule_Without_Geography_Criteria_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, ruleType: "geography", criteria: new TerritoryRuleCriteriaInput(AccountTypes: ["hospital"])), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_AccountList_Rule_Without_Include_Ids_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, ruleType: "account-list", criteria: new TerritoryRuleCriteriaInput(CountryRefs: ["tr"])), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Rejects_Account_Id_In_Both_Include_And_Exclude()
    {
        var c = NewCtx();
        var shared = Guid.NewGuid();
        var response = await Create(c).Handle(
            Cmd(c, ruleType: "account-list",
                criteria: new TerritoryRuleCriteriaInput(IncludeAccountIds: [shared], ExcludeAccountIds: [shared])), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Rule_Outside_Model_Window_Fails_400()
    {
        var c = NewCtx();
        var cmd = Cmd(c) with { EffectiveTo = To.AddDays(60) };

        var response = await Create(c).Handle(cmd, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Normalizes_Criteria_Trim_And_Dedupe()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, criteria: new TerritoryRuleCriteriaInput(CountryRefs: [" tr ", "TR", "", "us"])), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(["tr", "us"], c.Rules.Items[0].Criteria.CountryRefs);
    }

    [Fact]
    public async Task Create_Is_Tenant_Isolated()
    {
        var c = NewCtx();
        var handler = new CreateTerritoryAssignmentRuleHandler(
            TenantFactory.Tenant(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            c.Models, c.Nodes, c.Rules, c.References);

        var response = await handler.Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Update / soft delete
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_Rule_Succeeds_On_Draft_Model()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var rule = c.Rules.Items[0];

        var handler = new UpdateTerritoryAssignmentRuleHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules, c.References);
        var response = await handler.Handle(new UpdateTerritoryAssignmentRuleCommand(
            c.Model.Id, rule.Id, "renamed", c.TargetNode.Id, "account-type", "warn", 5, false,
            new TerritoryRuleCriteriaInput(AccountTypes: ["hospital"]), From, To, "corr2"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("renamed", rule.Name);
        Assert.Equal("account-type", rule.RuleType);
        Assert.Equal("warn", rule.ConflictPolicy);
        Assert.False(rule.IsEnabled);
        Assert.Equal(5, rule.Priority);
    }

    [Fact]
    public async Task SoftDelete_Rule_Hides_It_And_Keeps_The_Record()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var rule = c.Rules.Items[0];

        var handler = new SoftDeleteTerritoryAssignmentRuleHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules, c.References);
        var response = await handler.Handle(
            new SoftDeleteTerritoryAssignmentRuleCommand(c.Model.Id, rule.Id, "cleanup", "corr"), default);

        Assert.True(response.IsSuccessful);
        Assert.True(rule.IsDeleted);
        Assert.NotNull(rule.DeletedAt);
        Assert.Single(c.Rules.Items);                                    // no hard delete
        Assert.Empty(await c.Rules.ListByModelAsync(TenantId, c.Model.Id, default));
    }

    [Fact]
    public async Task SoftDelete_On_Active_Model_Fails_409()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var rule = c.Rules.Items[0];
        c.Model.Status = "active";

        var handler = new SoftDeleteTerritoryAssignmentRuleHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules, c.References);
        var response = await handler.Handle(
            new SoftDeleteTerritoryAssignmentRuleCommand(c.Model.Id, rule.Id, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.False(rule.IsDeleted);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Queries
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_Returns_Rules_With_Target_Node_And_Editability()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);

        var handler = new GetTerritoryAssignmentRuleListHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules);
        var dto = (await handler.Handle(new GetTerritoryAssignmentRuleListQuery(c.Model.Id), default)).Data!;

        Assert.Equal(1, dto.TotalCount);
        Assert.Equal(1, dto.EnabledCount);
        Assert.True(dto.IsEditable);
        Assert.Equal(c.TargetNode.TerritoryCode, dto.Items[0].TerritoryCode);
        Assert.Contains("country=tr", dto.Items[0].CriteriaSummary);
    }

    [Fact]
    public async Task List_Marks_Archived_Model_As_Not_Editable()
    {
        var c = NewCtx("archived");
        var handler = new GetTerritoryAssignmentRuleListHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules);

        var dto = (await handler.Handle(new GetTerritoryAssignmentRuleListQuery(c.Model.Id), default)).Data!;

        Assert.False(dto.IsEditable);
    }
}
