using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>
/// MOD-0151 FU04 — resource assignments. Every rule here is resolved from the published MOD-0048 metadata
/// (coverage scope requires/allows a territory id, role may be primary, source requires a reason), so these tests
/// also pin the code↔vocabulary contract the FU02B reconciliation taught us to guard.
/// </summary>
public sealed class TerritoryResourceAssignmentTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SamplePositionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(120);

    private sealed record Ctx(
        FakeTerritoryModelRepo Models, FakeTerritoryNodeRepo Nodes, FakeTerritoryResourceAssignmentRepo Assignments,
        FakeTerritoryReferenceValidator References, TerritoryModel Model, TerritoryNode Zone, TerritoryNode Area);

    private static Ctx NewCtx(string modelStatus = "draft", params string[] modelScopes)
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var scopes = modelScopes.Length == 0 ? new[] { "alpha", "beta" } : modelScopes;
        var model = new TerritoryModel
        {
            TenantId = TenantId, ModelCode = Guid.NewGuid().ToString("N"), Name = "M", Status = modelStatus,
            CountryScope = "tr", EffectiveFrom = From, EffectiveTo = To,
            BusinessScopes = scopes.Select(s => new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = s }).ToList()
        };
        models.Items.Add(model);

        TerritoryNode Node(string code, string level)
        {
            var n = new TerritoryNode
            {
                TenantId = TenantId, ModelId = model.Id, TerritoryCode = code, Name = code, TerritoryLevel = level,
                Status = "draft", EffectiveFrom = From, EffectiveTo = To
            };
            nodes.Items.Add(n);
            return n;
        }

        return new Ctx(models, nodes, new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryReferenceValidator(),
            model, Node("BEYLIKDUZU", "zone"), Node("IST-AVRUPA", "area"));
    }

    private static CreateTerritoryResourceAssignmentHandler Create(Ctx c)
        => new(TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);

    private static TerritoryResourceRefInput Res(string id = "p-ayse", string name = "Ayşe")
        => new(id, "person", name, null);

    /// <summary>Defaults the target node to the zone. Pass <paramref name="nodeless"/> to send NO node at all —
    /// a plain <c>nodeId: null</c> cannot express that here, since null means "use the default".</summary>
    private static CreateTerritoryResourceAssignmentCommand Cmd(
        Ctx c, Guid? nodeId = null, bool nodeless = false, string role = "medical-representative", string? coverage = "exact-territory",
        string[]? scopes = null, bool primary = true, string? source = null, string? reason = "initial assignment",
        TerritoryResourceRefInput? resource = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
        // Role became Position: the former role code rides as the position code (exclusivity keys on it); coverage is
        // now supplied explicitly since the role-metadata default is gone.
        => new(c.Model.Id, nodeless ? null : nodeId ?? c.Zone.Id, resource ?? Res(), SamplePositionId, role, role, coverage,
            scopes ?? ["alpha"], primary, source, from ?? From, to ?? To, reason, "corr");

    // ---------------------------------------------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Mr_On_Zone_Succeeds()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var a = Assert.Single(c.Assignments.Items);
        Assert.Equal("medical-representative", a.PositionCode);
        Assert.Equal("exact-territory", a.CoverageScope);
        Assert.Equal("proposed", a.Status);
        Assert.Equal("manual", a.AssignmentSource);
        Assert.Equal(["alpha"], a.BusinessScopes.Select(s => s.ScopeCode));
        Assert.True(a.IsPrimary);
        Assert.Equal(TenantId, a.TenantId);
    }

    [Fact(Skip = "Role removed (MOD-0151 → Position): coverage no longer defaults from role metadata.")]
    public async Task Create_Defaults_CoverageScope_From_Role_Metadata()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, nodeless: true, role: "business-unit-manager", coverage: null, scopes: ["alpha"]), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("business-unit", c.Assignments.Items[0].CoverageScope);
        Assert.Null(c.Assignments.Items[0].TerritoryId);
    }

    [Fact(Skip = "Role removed (MOD-0151 → Position): position code is a free snapshot, not reference-validated.")]
    public async Task Create_Invalid_Role_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, role: "chief-of-nothing"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(c.Assignments.Items);
    }

    [Fact]
    public async Task Create_Invalid_CoverageScope_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, coverage: "whole-universe"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact(Skip = "Role removed (MOD-0151 → Position): there is no role reference set to fail closed on.")]
    public async Task Create_Fails_Closed_When_Role_Set_Unpublished()
    {
        var c = NewCtx();
        c.References.MissingSets.Add(TerritoryReferenceSets.TerritoryResourceRole);

        var response = await Create(c).Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("not published", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Requires_TerritoryId_When_Coverage_Scope_Says_So()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, nodeless: true, coverage: "exact-territory"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("requires a territory node", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Rejects_TerritoryId_When_Coverage_Scope_Forbids_It()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, nodeId: c.Zone.Id, role: "business-unit-manager", coverage: "business-unit"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("does not allow a territory node", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Node_Outside_Model_Fails_404()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, nodeId: Guid.NewGuid()), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Create_Mr_On_Area_Node_Fails_409()
    {
        // Pack §10: an MR belongs on a zone/microzone.
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, nodeId: c.Area.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("zone, microzone", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    public async Task Create_On_NonDraft_Model_Fails_409(string status)
    {
        var c = NewCtx(status);
        var response = await Create(c).Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(c.Assignments.Items);
    }

    [Fact]
    public async Task Create_On_Active_Model_Creates_Operational_Assignment()
    {
        var c = NewCtx("active");
        var response = await Create(c).Handle(Cmd(c), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TerritoryResourceAssignmentValidation.ActiveStatus, Assert.Single(c.Assignments.Items).Status);
    }

    [Fact]
    public async Task Create_BusinessScope_Outside_Model_Scope_Fails_400()
    {
        var c = NewCtx("draft", "alpha");
        var response = await Create(c).Handle(Cmd(c, scopes: ["gamma"]), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("outside the territory model scope", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Requires_BusinessScope_When_Role_Says_So()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, nodeless: true, role: "product-manager", coverage: "product-portfolio", scopes: []), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("requires at least one business unit scope", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Rejects_BusinessScope_When_Coverage_Scope_Forbids_It()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, nodeless: true, role: "admin", coverage: "model-wide", scopes: ["alpha"], primary: false), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("does not allow business unit scopes", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Role removed (MOD-0151 → Position): the canBePrimary gate is gone; any position may be primary.")]
    public async Task Create_Primary_On_A_Role_That_Cannot_Be_Primary_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(
            Cmd(c, nodeless: true, role: "viewer", coverage: "model-wide", scopes: [], primary: true), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("cannot hold a primary assignment", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Manual_Source_Without_Reason_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, source: "manual", reason: null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("requires a change reason", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Without_ResourceId_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, resource: new TerritoryResourceRefInput("", "person", "Ayşe", null)), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Invalid_Dates_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, from: To, to: From), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Outside_Model_Window_Fails_400()
    {
        var c = NewCtx();
        var response = await Create(c).Handle(Cmd(c, to: To.AddDays(60)), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Is_Tenant_Isolated()
    {
        var c = NewCtx();
        var handler = new CreateTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            c.Models, c.Nodes, c.Assignments, c.References);

        var response = await handler.Handle(Cmd(c), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Exclusivity / overlap
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Duplicate_Primary_On_Same_Node_Role_And_Scope_Fails_409()
    {
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c), default)).IsSuccessful);

        var response = await Create(c).Handle(Cmd(c, resource: Res("p-mehmet", "Mehmet")), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Single(c.Assignments.Items);
    }

    [Fact]
    public async Task Same_Node_Different_Business_Scope_Is_Allowed()
    {
        // Beylikdüzü + Alpha → Ayşe, Beylikdüzü + Beta → Mehmet (the pack's own example).
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c, scopes: ["alpha"]), default)).IsSuccessful);

        var response = await Create(c).Handle(Cmd(c, scopes: ["beta"], resource: Res("p-mehmet", "Mehmet")), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, c.Assignments.Items.Count);
    }

    [Fact]
    public async Task Non_Primary_Assignment_Bypasses_Exclusivity()
    {
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c), default)).IsSuccessful);

        var response = await Create(c).Handle(Cmd(c, primary: false, resource: Res("p-backup", "Backup")), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, c.Assignments.Items.Count);
    }

    [Fact]
    public async Task Non_Overlapping_Periods_Are_Allowed()
    {
        var c = NewCtx();
        var first = Cmd(c, from: From, to: From.AddDays(10));
        Assert.True((await Create(c).Handle(first, default)).IsSuccessful);

        var response = await Create(c).Handle(
            Cmd(c, from: From.AddDays(11), to: To, resource: Res("p-mehmet", "Mehmet")), default);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Same_Mr_Primary_In_Two_Business_Scopes_Fails_409()
    {
        // Pack §10 block: one MR cannot be primary across different business scopes in overlapping periods.
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c, scopes: ["alpha"]), default)).IsSuccessful);

        var zone2 = new TerritoryNode
        {
            TenantId = TenantId, ModelId = c.Model.Id, TerritoryCode = "ESENYURT", Name = "Esenyurt",
            TerritoryLevel = "zone", Status = "draft", EffectiveFrom = From, EffectiveTo = To
        };
        c.Nodes.Items.Add(zone2);

        var response = await Create(c).Handle(Cmd(c, nodeId: zone2.Id, scopes: ["beta"]), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Contains("another business-unit scope", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Same_Mr_Across_Scopes_Is_Allowed_With_Override_Source_And_Reason()
    {
        var c = NewCtx();
        Assert.True((await Create(c).Handle(Cmd(c, scopes: ["alpha"]), default)).IsSuccessful);

        var zone2 = new TerritoryNode
        {
            TenantId = TenantId, ModelId = c.Model.Id, TerritoryCode = "ESENYURT", Name = "Esenyurt",
            TerritoryLevel = "zone", Status = "draft", EffectiveFrom = From, EffectiveTo = To
        };
        c.Nodes.Items.Add(zone2);

        var response = await Create(c).Handle(
            Cmd(c, nodeId: zone2.Id, scopes: ["beta"], source: "override", reason: "temporary cover"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, c.Assignments.Items.Count);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Update / end / soft delete
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_Proposed_Assignment_Succeeds()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];

        var handler = new UpdateTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(new UpdateTerritoryResourceAssignmentCommand(
            c.Model.Id, a.Id, c.Zone.Id, Res("p-mehmet", "Mehmet"), SamplePositionId, "medical-representative", "medical-representative",
            "exact-territory", ["beta"], true, "manual", From, To, "reassigned", "corr"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("p-mehmet", a.Resource.ResourceId);
        Assert.Equal(["beta"], a.BusinessScopes.Select(s => s.ScopeCode));
    }

    [Fact]
    public async Task Update_An_Ended_Assignment_Fails_409()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];
        a.Status = "ended";

        var handler = new UpdateTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(new UpdateTerritoryResourceAssignmentCommand(
            c.Model.Id, a.Id, c.Zone.Id, Res(), SamplePositionId, "medical-representative", "medical-representative",
            "exact-territory", ["alpha"], true, "manual", From, To, "x", null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task End_Assignment_Sets_Ended_Status_And_ValidTo_Without_Deleting()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];
        var endDate = DateTimeOffset.UtcNow.AddDays(5);

        var handler = new EndTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(
            new EndTerritoryResourceAssignmentCommand(c.Model.Id, a.Id, endDate, "left the team", "corr"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("ended", a.Status);
        Assert.Equal(endDate, a.ValidTo);
        Assert.False(a.IsDeleted);                                  // ending is NOT deleting
        Assert.Single(c.Assignments.Items);
    }

    [Fact]
    public async Task End_A_Future_Assignment_Collapses_It_To_Its_Start_Date()
    {
        // A plan that has not started yet must still be cancellable. Defaulting the end date to a bare "now" would be
        // earlier than ValidFrom and would reject exactly that case.
        var c = NewCtx();
        var future = DateTimeOffset.UtcNow.AddDays(30);
        c.Model.EffectiveFrom = future;
        c.Model.EffectiveTo = future.AddDays(300);
        await Create(c).Handle(Cmd(c, from: future, to: future.AddDays(200)), default);
        var a = c.Assignments.Items[0];

        var handler = new EndTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(
            new EndTerritoryResourceAssignmentCommand(c.Model.Id, a.Id, null, "plan cancelled", null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("ended", a.Status);
        Assert.Equal(future, a.ValidTo);                            // never took effect
        Assert.False(a.IsDeleted);
    }

    [Fact]
    public async Task End_With_An_Explicit_Date_Before_ValidFrom_Is_Still_Rejected()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];

        var handler = new EndTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(new EndTerritoryResourceAssignmentCommand(
            c.Model.Id, a.Id, a.ValidFrom.AddDays(-5), "typo", null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("proposed", a.Status);
    }

    [Fact]
    public async Task Ended_Assignment_Frees_The_Scope_For_A_New_Primary()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];
        a.Status = "ended";
        a.ValidTo = DateTimeOffset.UtcNow;

        var response = await Create(c).Handle(Cmd(c, resource: Res("p-mehmet", "Mehmet")), default);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task SoftDelete_Proposed_Assignment_Succeeds_And_Keeps_The_Record()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];

        var handler = new SoftDeleteTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(
            new SoftDeleteTerritoryResourceAssignmentCommand(c.Model.Id, a.Id, "created by mistake", "corr"), default);

        Assert.True(response.IsSuccessful);
        Assert.True(a.IsDeleted);
        Assert.Single(c.Assignments.Items);                          // no hard delete
        Assert.Empty(await c.Assignments.ListByModelAsync(TenantId, c.Model.Id, default));
    }

    [Fact]
    public async Task SoftDelete_An_Active_Assignment_Fails_409()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var a = c.Assignments.Items[0];
        a.Status = "active";

        var handler = new SoftDeleteTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var response = await handler.Handle(
            new SoftDeleteTerritoryResourceAssignmentCommand(c.Model.Id, a.Id, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.False(a.IsDeleted);
        Assert.Contains("end the assignment instead", response.Errors!.First(), StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Queries + conflict report
    // ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_Returns_Assignments_With_Node_And_Model_Scopes()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);

        var handler = new GetTerritoryResourceAssignmentListHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments);
        var dto = (await handler.Handle(new GetTerritoryResourceAssignmentListQuery(c.Model.Id), default)).Data!;

        Assert.Equal(1, dto.TotalCount);
        Assert.True(dto.IsEditable);
        Assert.Equal("BEYLIKDUZU", dto.Items[0].TerritoryCode);
        Assert.Equal("zone", dto.Items[0].TerritoryLevel);
        Assert.Equal(["alpha", "beta"], dto.ModelBusinessUnitScopes);
    }

    [Fact]
    public async Task ConflictReport_Flags_Multi_Node_Coverage_As_A_Warning_Not_A_Conflict()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c, scopes: ["alpha"]), default);

        var zone2 = new TerritoryNode
        {
            TenantId = TenantId, ModelId = c.Model.Id, TerritoryCode = "ESENYURT", Name = "Esenyurt",
            TerritoryLevel = "zone", Status = "draft", EffectiveFrom = From, EffectiveTo = To
        };
        c.Nodes.Items.Add(zone2);
        await Create(c).Handle(Cmd(c, nodeId: zone2.Id, scopes: ["alpha"]), default);

        var handler = new ValidateTerritoryResourceConflictsHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments);
        var report = (await handler.Handle(new ValidateTerritoryResourceConflictsCommand(c.Model.Id), default)).Data!;

        Assert.Equal(0, report.ConflictCount);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(TerritoryResourceConflictKinds.MultiNodeCoverage, report.Warnings[0].Kind);
    }

    [Fact(Skip = "Role removed (MOD-0151 → Position): role→node-level recommendation warnings are gone.")]
    public async Task ConflictReport_Flags_An_Unexpected_Node_Level_As_A_Warning()
    {
        var c = NewCtx();
        // area-manager on a ZONE node: allowed, but not the recommended level.
        await Create(c).Handle(
            Cmd(c, nodeId: c.Zone.Id, role: "area-manager", coverage: "territory-subtree", scopes: ["alpha"]), default);

        var handler = new ValidateTerritoryResourceConflictsHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments);
        var report = (await handler.Handle(new ValidateTerritoryResourceConflictsCommand(c.Model.Id), default)).Data!;

        Assert.Equal(0, report.ConflictCount);
        Assert.Contains(report.Warnings, w => w.Kind == TerritoryResourceConflictKinds.UnexpectedNodeLevel);
    }

    [Fact]
    public async Task ConflictReport_Is_Read_Only()
    {
        var c = NewCtx();
        await Create(c).Handle(Cmd(c), default);
        var snapshot = c.Assignments.Items.Select(a => (a.Id, a.Status, a.IsPrimary, a.UpdatedAt)).ToList();

        var handler = new ValidateTerritoryResourceConflictsHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments);
        await handler.Handle(new ValidateTerritoryResourceConflictsCommand(c.Model.Id), default);

        Assert.Equal(snapshot, c.Assignments.Items.Select(a => (a.Id, a.Status, a.IsPrimary, a.UpdatedAt)).ToList());
    }
}
