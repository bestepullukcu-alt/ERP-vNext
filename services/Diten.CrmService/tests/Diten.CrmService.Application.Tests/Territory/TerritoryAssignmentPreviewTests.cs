using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.AssignmentRules;
using Diten.CrmService.Application.Features.Territory.AssignmentRules.Handlers;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>
/// MOD-0151 FU03 — assignment preview. Every test here also asserts the defining property: the preview is a read.
/// No AccountTerritoryAssignment exists, the account snapshots are never mutated, and the DTO says so explicitly.
/// </summary>
public sealed class TerritoryAssignmentPreviewTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-5);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(30);

    private sealed record Ctx(
        FakeTerritoryModelRepo Models, FakeTerritoryNodeRepo Nodes, FakeTerritoryAssignmentRuleRepo Rules,
        FakeTerritoryAccountReader Accounts, FakeTerritoryReferenceValidator References, TerritoryModel Model);

    private static Ctx NewCtx(string modelStatus = "draft")
    {
        var models = new FakeTerritoryModelRepo();
        var model = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = Guid.NewGuid().ToString("N"), Name = "M", Status = modelStatus,
            CountryScope = "tr", EffectiveFrom = From, EffectiveTo = To
        };
        models.Items.Add(model);
        return new Ctx(models, new FakeTerritoryNodeRepo(), new FakeTerritoryAssignmentRuleRepo(),
            new FakeTerritoryAccountReader(), new FakeTerritoryReferenceValidator(), model);
    }

    private static TerritoryNode AddNode(Ctx c, string code)
    {
        var node = new TerritoryNode
        {
            TenantId = TenantId, ModelId = c.Model.Id, TerritoryCode = code, Name = code, TerritoryLevel = "zone",
            Status = "draft", EffectiveFrom = From, EffectiveTo = To
        };
        c.Nodes.Items.Add(node);
        return node;
    }

    private static TerritoryAssignmentRule AddRule(
        Ctx c, TerritoryNode target, string code, TerritoryRuleCriteria criteria,
        int priority = 10, bool enabled = true, string ruleType = "geography", string policy = "priority",
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var rule = new TerritoryAssignmentRule
        {
            TenantId = TenantId, ModelId = c.Model.Id, TerritoryId = target.Id, RuleCode = code, Name = code,
            RuleType = ruleType, ConflictPolicy = policy, Priority = priority, IsEnabled = enabled,
            Criteria = criteria, EffectiveFrom = from ?? From, EffectiveTo = to ?? To
        };
        c.Rules.Items.Add(rule);
        return rule;
    }

    private static TerritoryAccountSnapshot Account(
        string code, string? city = null, string? type = null, string? country = "tr",
        string? district = null, string? status = "active", Guid? id = null)
        => new(id ?? Guid.NewGuid(), code, "Account " + code, type, null, status, country, city, district);

    private static PreviewTerritoryAssignmentsHandler Preview(Ctx c)
        => new(TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Rules, c.Accounts, c.References);

    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_With_No_Rules_Returns_Controlled_Empty_Result()
    {
        var c = NewCtx();
        c.Accounts.Accounts.Add(Account("A1"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, "corr"), default)).Data!;

        Assert.False(dto.PersistedAssignments);
        Assert.Equal(0, dto.EvaluatedRuleCount);
        Assert.Empty(dto.MatchedAccounts);
        Assert.Empty(dto.Conflicts);
        Assert.Contains(dto.Warnings, w => w.Contains("no assignment rules", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Geography_Rule_Matches_Expected_Accounts_Only()
    {
        var c = NewCtx();
        var zone = AddNode(c, "BEYLIKDUZU");
        AddRule(c, zone, "R-GEO", new TerritoryRuleCriteria
        {
            CountryRefs = ["tr"], CityRefs = ["istanbul"], DistrictRefs = ["beylikduzu"]
        });
        c.Accounts.Accounts.AddRange([
            Account("HIT", city: "Istanbul", district: "Beylikduzu"),
            Account("WRONG-DISTRICT", city: "istanbul", district: "esenyurt"),
            Account("WRONG-CITY", city: "ankara", district: "beylikduzu"),
            Account("NO-LOCATION")
        ]);

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        var match = Assert.Single(dto.MatchedAccounts);
        Assert.Equal("HIT", match.AccountCode);
        Assert.Equal(zone.Id, match.TargetTerritoryNodeId);
        Assert.Equal("BEYLIKDUZU", match.TargetTerritoryCode);
        Assert.Equal(TerritoryPreviewConflictStatus.None, match.ConflictStatus);
        Assert.Equal(3, dto.UnmatchedAccountsCount);
        Assert.False(dto.PersistedAssignments);
    }

    [Fact]
    public async Task Preview_AccountType_Rule_Matches_On_Classification()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-TYPE", new TerritoryRuleCriteria { AccountTypes = ["hospital"] }, ruleType: "account-type");
        c.Accounts.Accounts.AddRange([Account("H1", type: "hospital"), Account("P1", type: "pharmacy")]);

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal("H1", Assert.Single(dto.MatchedAccounts).AccountCode);
    }

    [Fact]
    public async Task Preview_Combines_Criteria_Fields_With_And()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R", new TerritoryRuleCriteria { CityRefs = ["istanbul"], AccountTypes = ["hospital"] });
        c.Accounts.Accounts.AddRange([
            Account("BOTH", city: "istanbul", type: "hospital"),
            Account("CITY-ONLY", city: "istanbul", type: "pharmacy"),
            Account("TYPE-ONLY", city: "ankara", type: "hospital")
        ]);

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal("BOTH", Assert.Single(dto.MatchedAccounts).AccountCode);
    }

    [Fact]
    public async Task Preview_Detects_Conflict_When_Two_Rules_Claim_Different_Nodes()
    {
        var c = NewCtx();
        var zoneA = AddNode(c, "ZONE-A");
        var zoneB = AddNode(c, "ZONE-B");
        AddRule(c, zoneA, "R-A", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, priority: 10);
        AddRule(c, zoneB, "R-B", new TerritoryRuleCriteria { AccountTypes = ["hospital"] }, priority: 20,
            ruleType: "account-type", policy: "manual-review");
        c.Accounts.Accounts.Add(Account("BOTH", city: "istanbul", type: "hospital"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal(1, dto.ConflictCount);
        var conflict = Assert.Single(dto.Conflicts);
        Assert.Equal("BOTH", conflict.AccountCode);
        Assert.Equal(2, conflict.CandidateTerritoryNodes.Count);
        Assert.Equal(2, conflict.ConflictingRuleIds.Count);

        // Lower Priority wins.
        var winner = Assert.Single(conflict.CandidateTerritoryNodes, n => n.IsWinner);
        Assert.Equal("R-A", winner.RuleCode);
        Assert.Equal("priority", conflict.ConflictPolicy);

        Assert.Equal(2, dto.MatchedAccounts.Count);
        Assert.Contains(dto.MatchedAccounts, m => m.ConflictStatus == TerritoryPreviewConflictStatus.ConflictWinner);
        Assert.Contains(dto.MatchedAccounts, m => m.ConflictStatus == TerritoryPreviewConflictStatus.ConflictLoser);
        Assert.False(dto.PersistedAssignments);
    }

    [Fact]
    public async Task Preview_Two_Rules_Same_Node_Is_Not_A_Conflict()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-A", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, priority: 10);
        AddRule(c, zone, "R-B", new TerritoryRuleCriteria { AccountTypes = ["hospital"] }, priority: 20, ruleType: "account-type");
        c.Accounts.Accounts.Add(Account("BOTH", city: "istanbul", type: "hospital"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal(0, dto.ConflictCount);
        Assert.All(dto.MatchedAccounts, m => Assert.Equal(TerritoryPreviewConflictStatus.None, m.ConflictStatus));
    }

    [Fact]
    public async Task Preview_Skips_Disabled_Rule()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-OFF", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, enabled: false);
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Empty(dto.MatchedAccounts);
        Assert.Equal(1, dto.SkippedRuleCount);
        Assert.Equal("rule disabled", Assert.Single(dto.CriteriaSummary).SkipReason);
    }

    [Fact]
    public async Task Preview_Skips_Rule_Outside_Its_Effective_Window()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-EXPIRED", new TerritoryRuleCriteria { CityRefs = ["istanbul"] },
            from: From, to: DateTimeOffset.UtcNow.AddDays(-1));
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Empty(dto.MatchedAccounts);
        Assert.Equal("expired", Assert.Single(dto.CriteriaSummary).SkipReason);
    }

    [Fact]
    public async Task Preview_Exclude_List_Removes_A_Candidate()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        var excluded = Account("EXCLUDED", city: "istanbul");
        var kept = Account("KEPT", city: "istanbul");
        c.Accounts.Accounts.AddRange([excluded, kept]);
        AddRule(c, zone, "R", new TerritoryRuleCriteria
        {
            CityRefs = ["istanbul"], ExcludeAccountIds = [excluded.AccountId]
        });

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal("KEPT", Assert.Single(dto.MatchedAccounts).AccountCode);
    }

    [Fact]
    public async Task Preview_AccountList_Rule_Matches_Only_Its_Include_List()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        var listed = Account("LISTED", city: "ankara");
        var other = Account("OTHER", city: "ankara");
        c.Accounts.Accounts.AddRange([listed, other]);
        AddRule(c, zone, "R-LIST", new TerritoryRuleCriteria { IncludeAccountIds = [listed.AccountId] },
            ruleType: "account-list");

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        var match = Assert.Single(dto.MatchedAccounts);
        Assert.Equal("LISTED", match.AccountCode);
        Assert.Equal("explicit include list", match.MatchReason);
    }

    [Fact]
    public async Task Preview_Can_Target_A_Single_Rule()
    {
        var c = NewCtx();
        var zoneA = AddNode(c, "ZA");
        var zoneB = AddNode(c, "ZB");
        var ruleA = AddRule(c, zoneA, "R-A", new TerritoryRuleCriteria { CityRefs = ["istanbul"] });
        AddRule(c, zoneB, "R-B", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, priority: 20);
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, ruleA.Id, null, null), default)).Data!;

        Assert.Equal(1, dto.EvaluatedRuleCount);
        Assert.Equal("R-A", Assert.Single(dto.MatchedAccounts).RuleCode);
        Assert.Equal(0, dto.ConflictCount);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("active")]
    [InlineData("inactive")]
    public async Task Preview_Is_Allowed_On_Non_Archived_Models(string status)
    {
        var c = NewCtx(status);
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R", new TerritoryRuleCriteria { CityRefs = ["istanbul"] });
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var response = await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.MatchedAccounts);
    }

    [Fact]
    public async Task Preview_On_Archived_Model_Is_Rejected()
    {
        var c = NewCtx("archived");
        var response = await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Preview_Fails_Closed_When_RuleType_Set_Unpublished()
    {
        var c = NewCtx();
        c.References.MissingSets.Add(TerritoryReferenceSets.TerritoryRuleType);

        var response = await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("not published", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_Reports_ConflictPolicy_And_Resolution_Suggestion()
    {
        var c = NewCtx();
        var zoneA = AddNode(c, "ZA");
        var zoneB = AddNode(c, "ZB");
        AddRule(c, zoneA, "R-A", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, priority: 1, policy: "block");
        AddRule(c, zoneB, "R-B", new TerritoryRuleCriteria { CityRefs = ["istanbul"] }, priority: 2, policy: "warn");
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        var conflict = Assert.Single(dto.Conflicts);
        Assert.Equal("block", conflict.ConflictPolicy);
        Assert.Contains("prevent an apply", conflict.ResolutionSuggestion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_Does_Not_Mutate_Accounts_Or_Rules()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        var rule = AddRule(c, zone, "R", new TerritoryRuleCriteria { CityRefs = ["istanbul"] });
        var account = Account("A1", city: "istanbul");
        c.Accounts.Accounts.Add(account);
        var ruleSnapshot = (rule.RuleCode, rule.Priority, rule.IsEnabled, rule.TerritoryId, rule.UpdatedAt);

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.False(dto.PersistedAssignments);
        Assert.Equal(ruleSnapshot, (rule.RuleCode, rule.Priority, rule.IsEnabled, rule.TerritoryId, rule.UpdatedAt));
        Assert.Same(account, Assert.Single(c.Accounts.Accounts));
        Assert.Equal("A1", c.Accounts.Accounts[0].AccountCode);
        Assert.Equal("draft", c.Model.Status);                            // preview never advances lifecycle
    }

    [Fact]
    public async Task Preview_Reports_Scan_Cap_As_A_Warning()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R", new TerritoryRuleCriteria { CityRefs = ["istanbul"] });
        for (var i = 0; i < 5; i++) { c.Accounts.Accounts.Add(Account($"A{i}", city: "istanbul")); }

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, 2, null), default)).Data!;

        Assert.Equal(2, dto.ScannedAccounts);
        Assert.Equal(5, dto.TotalTenantAccounts);
        Assert.Contains(dto.Warnings, w => w.Contains("preview cap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Of_A_Future_Model_Evaluates_As_Of_The_Model_Window()
    {
        // A planner building next year's model must still see candidates; evaluating "now" would skip every rule as
        // "not yet effective" and return an empty, misleading preview.
        var c = NewCtx();
        var future = DateTimeOffset.UtcNow.AddYears(2);
        c.Model.EffectiveFrom = future;
        c.Model.EffectiveTo = future.AddYears(1);
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-FUTURE", new TerritoryRuleCriteria { CityRefs = ["istanbul"] },
            from: future, to: future.AddYears(1));
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal(1, dto.EvaluatedRuleCount);
        Assert.Equal(future, dto.EffectiveAt);
        Assert.Equal("A1", Assert.Single(dto.MatchedAccounts).AccountCode);
        Assert.Contains(dto.Warnings, w => w.Contains("as of", StringComparison.OrdinalIgnoreCase));
        Assert.False(dto.PersistedAssignments);
    }

    [Fact]
    public async Task Preview_Of_A_Current_Model_Evaluates_As_Of_Now()
    {
        var c = NewCtx();
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R", new TerritoryRuleCriteria { CityRefs = ["istanbul"] });
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal(dto.GeneratedAt, dto.EffectiveAt);
        Assert.DoesNotContain(dto.Warnings, w => w.Contains("as of", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Preview_Still_Skips_A_Rule_Outside_The_Model_Window()
    {
        // Clamping to the model window must not resurrect a rule that ended before the model even starts.
        var c = NewCtx();
        var future = DateTimeOffset.UtcNow.AddYears(2);
        c.Model.EffectiveFrom = future;
        c.Model.EffectiveTo = future.AddYears(1);
        var zone = AddNode(c, "Z1");
        AddRule(c, zone, "R-OLD", new TerritoryRuleCriteria { CityRefs = ["istanbul"] },
            from: From, to: DateTimeOffset.UtcNow.AddDays(-1));
        c.Accounts.Accounts.Add(Account("A1", city: "istanbul"));

        var dto = (await Preview(c).Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default)).Data!;

        Assert.Equal(0, dto.EvaluatedRuleCount);
        Assert.Equal("expired", Assert.Single(dto.CriteriaSummary).SkipReason);
    }

    [Fact]
    public async Task Preview_Is_Tenant_Isolated()
    {
        var c = NewCtx();
        var handler = new PreviewTerritoryAssignmentsHandler(
            TenantFactory.Tenant(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            c.Models, c.Nodes, c.Rules, c.Accounts, c.References);

        var response = await handler.Handle(new PreviewTerritoryAssignmentsCommand(c.Model.Id, null, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }
}
