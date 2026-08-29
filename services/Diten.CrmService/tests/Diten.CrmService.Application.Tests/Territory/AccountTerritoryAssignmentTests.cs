using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.Territory.AccountAssignments.Handlers;
using Diten.CrmService.Application.Features.Territory.AssignmentRules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class AccountTerritoryAssignmentTests
{
    private static readonly Guid TenantA = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Fact]
    public async Task Active_model_selected_preview_row_is_persisted_without_account_mutation()
    {
        var fx = Fixture();
        var before = fx.Accounts.Accounts.Single();
        var snapshot = before with { };
        var response = await fx.Apply.Handle(Command(fx), default);
        Assert.True(response.IsSuccessful);
        Assert.Single(fx.Assignments.Items);
        Assert.Equal(snapshot, fx.Accounts.Accounts.Single());
        Assert.Equal("active", fx.Assignments.Items[0].AssignmentStatus);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("inactive")]
    [InlineData("archived")]
    public async Task Non_active_model_is_rejected(string status)
    {
        var fx = Fixture(status);
        var response = await fx.Apply.Handle(Command(fx), default);
        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(fx.Assignments.Items);
    }

    [Fact]
    public async Task Overlap_returns_409_and_all_or_nothing_writes_nothing()
    {
        var fx = Fixture();
        fx.Assignments.Items.Add(Existing(fx));
        var response = await fx.Apply.Handle(Command(fx), default);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(0, fx.Assignments.InsertManyCalls);
        Assert.Single(fx.Assignments.Items);
        Assert.Equal("active", fx.Assignments.Items[0].AssignmentStatus);
    }

    [Fact]
    public async Task Override_requires_reason()
    {
        var fx = Fixture();
        fx.Assignments.Items.Add(Existing(fx));
        var response = await fx.Apply.Handle(Command(fx) with { Override = true, OverrideReason = null }, default);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(0, fx.Assignments.InsertManyCalls);
    }

    [Fact]
    public async Task Override_ends_previous_and_creates_new_history_record()
    {
        var fx = Fixture();
        fx.Assignments.Items.Add(Existing(fx));
        var response = await fx.Apply.Handle(Command(fx) with { Override = true, OverrideReason = "Territory replan" }, default);
        Assert.True(response.IsSuccessful);
        Assert.Equal(2, fx.Assignments.Items.Count);
        Assert.Equal("ended", fx.Assignments.Items[0].AssignmentStatus);
        Assert.Equal("active", fx.Assignments.Items[1].AssignmentStatus);
        Assert.Equal("override", fx.Assignments.Items[1].AssignmentSource);
    }

    [Fact]
    public async Task Future_assignment_is_history_but_not_current_coverage()
    {
        var fx = Fixture();
        var future = Existing(fx);
        future.EffectiveFrom = DateTimeOffset.UtcNow.AddDays(10);
        fx.Assignments.Items.Add(future);
        var handler = new GetTerritoryCoverageSummaryHandler(TenantFactory.Tenant(TenantA), fx.Accounts, fx.Assignments, fx.Models);
        var response = await handler.Handle(new(fx.AccountId), default);
        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.HasCurrentCoverage);
        var history = await new GetAccountTerritoryAssignmentHistoryHandler(
            TenantFactory.Tenant(TenantA), fx.Accounts, fx.Assignments).Handle(new(fx.AccountId), default);
        Assert.Single(history.Data!.Items);
    }

    [Fact]
    public async Task Cross_tenant_account_is_not_resolved()
    {
        var fx = Fixture();
        var isolatedReader = new FakeTerritoryAccountReader();
        var handler = new GetTerritoryCoverageSummaryHandler(
            TenantFactory.Tenant(Guid.NewGuid()), isolatedReader, fx.Assignments, fx.Models);
        var response = await handler.Handle(new(fx.AccountId), default);
        Assert.False(response.IsSuccessful);
    }

    private static ApplyAccountTerritoryAssignmentsCommand Command(FixtureState fx) => new(
        fx.Model.Id, Guid.NewGuid(), [new(fx.AccountId, fx.Node.Id, fx.Rule.Id)], fx.Model.BusinessScopes,
        DateTimeOffset.UtcNow, fx.Model.EffectiveTo, "block", false, null, "test");

    private static AccountTerritoryAssignment Existing(FixtureState fx) => new()
    {
        TenantId = TenantA, TerritoryModelId = fx.Model.Id, AccountId = fx.AccountId,
        AccountCode = "ACC-1", AccountDisplayName = "Account One", TerritoryNodeId = fx.Node.Id,
        TerritoryNodeCode = fx.Node.TerritoryCode, TerritoryNodeName = fx.Node.Name,
        BusinessScopes = [new() { ScopeType = "business-unit", ScopeCode = "alpha" }],
        AssignmentSource = "rule", AssignmentStatus = "active", ConflictPolicy = "block",
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10)
    };

    private static FixtureState Fixture(string status = "active")
    {
        var model = new TerritoryModel
        {
            TenantId = TenantA, Status = status, EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1),
            EffectiveTo = DateTimeOffset.UtcNow.AddYears(1),
            BusinessScopes = [new() { ScopeType = "business-unit", ScopeCode = "alpha" }]
        };
        var node = new TerritoryNode
        {
            TenantId = TenantA, ModelId = model.Id, Status = "active", TerritoryCode = "ZONE-1",
            Name = "Zone One", EffectiveFrom = model.EffectiveFrom, EffectiveTo = model.EffectiveTo
        };
        var accountId = Guid.NewGuid();
        var accounts = new FakeTerritoryAccountReader();
        accounts.Accounts.Add(new TerritoryAccountSnapshot(
            accountId, "ACC-1", "Account One", "hospital", null, "active", "TR", "IST", "BEY"));
        var models = new FakeTerritoryModelRepo(); models.Items.Add(model);
        var nodes = new FakeTerritoryNodeRepo(); nodes.Items.Add(node);
        var rules = new FakeTerritoryAssignmentRuleRepo();
        var rule = new TerritoryAssignmentRule
        {
            TenantId = TenantA, ModelId = model.Id, TerritoryId = node.Id, RuleCode = "RULE-1",
            RuleType = "account-list", ConflictPolicy = "block", EffectiveFrom = model.EffectiveFrom
        };
        rules.Items.Add(rule);
        var assignments = new FakeAccountTerritoryAssignmentRepo();
        var apply = new ApplyAccountTerritoryAssignmentsHandler(
            TenantFactory.Tenant(TenantA), models, nodes, rules, accounts, assignments, new FakeTerritoryReferenceValidator());
        return new(model, node, rule, accountId, accounts, assignments, models, apply);
    }

    private sealed record FixtureState(
        TerritoryModel Model, TerritoryNode Node, TerritoryAssignmentRule Rule, Guid AccountId,
        FakeTerritoryAccountReader Accounts, FakeAccountTerritoryAssignmentRepo Assignments,
        FakeTerritoryModelRepo Models,
        ApplyAccountTerritoryAssignmentsHandler Apply);
}
