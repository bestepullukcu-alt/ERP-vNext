using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Application.Features.Territory.PlanVsCurrent;
using Diten.CrmService.Application.Features.Territory.PlanVsCurrent.Handlers;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

/// <summary>
/// MOD-0151 FU04B — plan baseline capture + read-time plan-vs-current diff. Everything here is read-only except the
/// activation-time snapshot write, which is asserted to live and die with the activation itself.
/// </summary>
public sealed class TerritoryPlanVsCurrentFu04BTests
{
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Context(
        FakeTerritoryModelRepo Models,
        FakeTerritoryNodeRepo Nodes,
        FakeTerritoryResourceAssignmentRepo Assignments,
        FakeTerritoryReferenceValidator References,
        TerritoryModel Model,
        TerritoryNode Kesan,
        TerritoryNode Suleymanpasa);

    private static Context NewContext(string status = "draft")
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = new TerritoryModel
        {
            TenantId = TenantId,
            ModelCode = "FU04B",
            Name = "FU04B Model",
            Status = status,
            CountryScope = "tr",
            EffectiveFrom = Now.AddDays(-30),
            EffectiveTo = Now.AddDays(365),
            BusinessScopes = [new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" }]
        };
        TerritoryNode Node(string code) => new()
        {
            TenantId = TenantId,
            ModelId = model.Id,
            TerritoryCode = code,
            Name = code,
            TerritoryLevel = "zone",
            Status = status,
            EffectiveFrom = model.EffectiveFrom,
            EffectiveTo = model.EffectiveTo
        };
        var kesan = Node("KESAN");
        var suleymanpasa = Node("SULEYMANPASA");
        models.Items.Add(model);
        nodes.Items.AddRange([kesan, suleymanpasa]);
        return new(models, nodes, new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryReferenceValidator(),
            model, kesan, suleymanpasa);
    }

    private static TerritoryResourceAssignment Assignment(
        Context c, TerritoryNode node, string resourceId, string status = "proposed",
        string positionCode = "medical-representative", string scope = "alpha", bool primary = true)
        => new()
        {
            TenantId = TenantId,
            ModelId = c.Model.Id,
            TerritoryId = node.Id,
            Resource = new TerritoryResourceRef { ResourceId = resourceId, ResourceType = "person", DisplayName = resourceId },
            Position = new TerritoryPositionRef
            {
                PositionId = Guid.NewGuid(),
                PositionCode = positionCode,
                PositionTitle = "Medical Representative",
                PositionType = "person-position",
                SourceSystem = "organization-directory",
                ValidationMode = "policy-validated",
                PolicySource = TerritoryPositionPolicy.BuiltInSource
            },
            CoverageScope = "exact-territory",
            BusinessScopes = [new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = scope }],
            Status = status,
            AssignmentSource = "manual",
            IsPrimary = primary,
            ValidFrom = Now.AddDays(-5),
            ValidTo = Now.AddDays(100),
            ChangeReason = "initial"
        };

    private static ActivateTerritoryModelHandler Activate(Context c, FakeTerritoryActivationUnitOfWork uow)
        => new(TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.References,
            new FakeTerritoryLifecycleAuditPublisher(), c.Assignments, uow, uow.Snapshots);

    private static TerritoryResourceAssignmentPlanSnapshot Snapshot(
        Context c, params TerritoryResourceAssignment[] proposed)
        => new()
        {
            TenantId = TenantId,
            TerritoryModelId = c.Model.Id,
            CapturedBy = "authenticated-user",
            SnapshotVersion = 1,
            Lines = proposed.Select(a => new TerritoryResourceAssignmentPlanSnapshotLine
            {
                TerritoryNodeId = a.TerritoryId,
                TerritoryNodeCode = c.Nodes.Items.First(n => n.Id == a.TerritoryId).TerritoryCode,
                TerritoryNodeName = c.Nodes.Items.First(n => n.Id == a.TerritoryId).Name,
                BusinessScopes = a.BusinessScopes.Select(s => s.ScopeCode).ToList(),
                PositionCode = a.EffectivePositionCode,
                PositionTitle = a.EffectivePositionTitle,
                PositionType = a.Position.PositionType,
                ResourceId = a.Resource.ResourceId,
                ResourceType = a.Resource.ResourceType,
                ResourceDisplayName = a.Resource.DisplayName,
                PlannedEffectiveFrom = a.ValidFrom,
                PlannedEffectiveTo = a.ValidTo,
                IsPrimary = a.IsPrimary,
                SourceAssignmentId = a.Id
            }).ToList()
        };

    private static IReadOnlyList<TerritoryPlanVsCurrentRowDto> Diff(
        Context c, TerritoryResourceAssignmentPlanSnapshot snapshot, params TerritoryResourceAssignment[] live)
        => TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
            c.Model.Id, c.Model.ModelCode, snapshot, live, c.Nodes.Items.ToDictionary(n => n.Id), Now));

    // -----------------------------------------------------------------------------------------------------------
    // Snapshot capture
    // -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Activation_Captures_Plan_Snapshot()
    {
        var c = NewContext();
        var proposed = Assignment(c, c.Kesan, "ayse");
        c.Assignments.Items.Add(proposed);
        var uow = new FakeTerritoryActivationUnitOfWork();

        var result = await Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", "fu04b"), default);

        Assert.True(result.IsSuccessful);
        var snapshot = Assert.Single(uow.Snapshots.Items);
        Assert.Equal(c.Model.Id, snapshot.TerritoryModelId);
        Assert.Equal(1, snapshot.SnapshotVersion);
        Assert.Equal("fu04b", snapshot.ActivationCorrelationId);
        var line = Assert.Single(snapshot.Lines);
        Assert.Equal("ayse", line.ResourceId);
        Assert.Equal("KESAN", line.TerritoryNodeCode);
        Assert.Equal(proposed.Id, line.SourceAssignmentId);
    }

    [Fact]
    public async Task Activation_Failing_Closed_Writes_No_Snapshot()
    {
        var c = NewContext();
        // Two proposed primaries on the same node/position/scope: the FU04A conflict guard fails the activation closed.
        c.Assignments.Items.AddRange([Assignment(c, c.Kesan, "ayse"), Assignment(c, c.Kesan, "mehmet")]);
        var uow = new FakeTerritoryActivationUnitOfWork();

        var result = await Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", "fu04b"), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Empty(uow.Snapshots.Items);
    }

    [Fact]
    public async Task Activation_Commit_Failure_Leaves_No_Snapshot()
    {
        var c = NewContext();
        c.Assignments.Items.Add(Assignment(c, c.Kesan, "ayse"));
        var uow = new FakeTerritoryActivationUnitOfWork { FailCommit = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", "fu04b"), default));

        Assert.Empty(uow.Snapshots.Items);
    }

    [Fact]
    public async Task Reactivation_Adds_A_New_Version_And_Keeps_The_Old_One()
    {
        var c = NewContext();
        c.Assignments.Items.Add(Assignment(c, c.Kesan, "ayse"));
        var uow = new FakeTerritoryActivationUnitOfWork();

        await Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "v1", "corr-1"), default);
        c.Model.Status = "inactive";
        c.Assignments.Items.Add(Assignment(c, c.Suleymanpasa, "mehmet"));
        await Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "v2", "corr-2"), default);

        Assert.Equal(2, uow.Snapshots.Items.Count);
        Assert.Equal([1, 2], uow.Snapshots.Items.Select(s => s.SnapshotVersion).OrderBy(v => v));
        Assert.Equal("corr-1", uow.Snapshots.Items.Single(s => s.SnapshotVersion == 1).ActivationCorrelationId);
    }

    [Fact]
    public async Task Snapshot_Is_Position_Based_And_Never_Carries_A_Legacy_Role_Code()
    {
        var c = NewContext();
        var proposed = Assignment(c, c.Kesan, "ayse");
        proposed.PositionCode = "LEGACY-MR-ROLE";  // deprecated flat field kept only for old records
        proposed.PositionName = "Legacy MR";
        c.Assignments.Items.Add(proposed);
        var uow = new FakeTerritoryActivationUnitOfWork();

        await Activate(c, uow).Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", null), default);

        var line = Assert.Single(uow.Snapshots.Items.Single().Lines);
        Assert.Equal("medical-representative", line.PositionCode);   // canonical PositionRef wins
        Assert.Equal("Medical Representative", line.PositionTitle);
        Assert.Equal("person-position", line.PositionType);
        Assert.DoesNotContain("LEGACY", line.PositionCode, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(typeof(TerritoryResourceAssignmentPlanSnapshotLine)
            .GetProperties().Where(p => p.Name.Contains("Role", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Snapshot_Repository_Exposes_No_Update_Or_Delete_Member()
    {
        var members = typeof(ITerritoryResourceAssignmentPlanSnapshotRepository).GetMethods().Select(m => m.Name).ToList();
        Assert.DoesNotContain(members, m => m.Contains("Update", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, m => m.Contains("Insert", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------------------------------------------
    // Diff types
    // -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Unchanged_When_Plan_Matches_Current()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        var row = Assert.Single(Diff(c, Snapshot(c, live), live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.Unchanged, row.DiffType);
        Assert.Equal("ayse", row.PlannedResourceId);
        Assert.Equal("ayse", row.CurrentResourceId);
    }

    [Fact]
    public void Replaced_When_The_Same_Slot_Is_Held_By_Another_Resource()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, planned);

        var replacement = Assignment(c, c.Kesan, "mehmet", "active");
        replacement.ReplacedAssignmentId = planned.Id;
        replacement.ReplacementReason = "maternity cover";
        planned.Status = "ended";
        planned.ValidTo = Now.AddDays(-1);
        planned.ReplacementAssignmentId = replacement.Id;

        var row = Assert.Single(Diff(c, snapshot, planned, replacement));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.Replaced, row.DiffType);
        Assert.Equal("ayse", row.PlannedResourceId);
        Assert.Equal("mehmet", row.CurrentResourceId);
        Assert.Equal("maternity cover", row.ReplacementReason);
        Assert.Equal(planned.Id, row.ReplacedAssignmentId);
    }

    [Fact]
    public void TransferredOut_And_TransferredIn_Are_Linked()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, planned);

        var target = Assignment(c, c.Suleymanpasa, "ayse", "active");
        target.TransferFromAssignmentId = planned.Id;
        target.TransferReason = "district rebalance";
        planned.Status = "ended";
        planned.ValidTo = Now.AddDays(-1);
        planned.TransferToAssignmentId = target.Id;

        var rows = Diff(c, snapshot, planned, target);
        var outRow = Assert.Single(rows, r => r.DiffType == TerritoryPlanVsCurrentDiffTypes.TransferredOut);
        var inRow = Assert.Single(rows, r => r.DiffType == TerritoryPlanVsCurrentDiffTypes.TransferredIn);
        Assert.Equal("KESAN", outRow.TerritoryNodeCode);
        Assert.Equal("ayse", outRow.PlannedResourceId);
        Assert.Null(outRow.CurrentResourceId);
        Assert.Equal("SULEYMANPASA", inRow.TerritoryNodeCode);
        Assert.Equal("ayse", inRow.CurrentResourceId);
        Assert.Equal(planned.Id, inRow.TransferFromAssignmentId);
        Assert.Equal("district rebalance", inRow.TransferReason);
    }

    [Fact]
    public void AddedAfterActivation_For_A_Current_Row_The_Baseline_Never_Saw()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, planned);
        var extra = Assignment(c, c.Suleymanpasa, "mehmet", "active");

        var rows = Diff(c, snapshot, planned, extra);
        var added = Assert.Single(rows, r => r.DiffType == TerritoryPlanVsCurrentDiffTypes.AddedAfterActivation);
        Assert.Equal("mehmet", added.CurrentResourceId);
        Assert.Null(added.PlannedResourceId);
    }

    [Fact]
    public void EndedAfterActivation_When_The_Planned_Slot_Was_Closed()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, planned);
        planned.Status = "ended";
        planned.ValidTo = Now.AddDays(-1);
        planned.ChangeReason = "left the company";

        var row = Assert.Single(Diff(c, snapshot, planned));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.EndedAfterActivation, row.DiffType);
        Assert.Equal("ayse", row.PlannedResourceId);
        Assert.Null(row.CurrentResourceId);
        Assert.Equal("left the company", row.ChangeReason);
    }

    [Fact]
    public void MissingCurrent_When_The_Baseline_Chain_Cannot_Be_Resolved()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, planned);

        // The live chain no longer exposes the source assignment: an integrity signal, not an error.
        var row = Assert.Single(Diff(c, snapshot));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.MissingCurrent, row.DiffType);
        Assert.Equal("ayse", row.PlannedResourceId);
    }

    [Fact]
    public void DateChanged_When_The_Effective_Window_Moved()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, live);
        snapshot.Lines[0].PlannedEffectiveTo = Now.AddDays(30);

        var row = Assert.Single(Diff(c, snapshot, live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.DateChanged, row.DiffType);
    }

    [Fact]
    public void ScopeChanged_When_Business_Scope_Or_Primary_Moved()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, live);
        snapshot.Lines[0].BusinessScopes = ["beta"];

        var row = Assert.Single(Diff(c, snapshot, live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.ScopeChanged, row.DiffType);
    }

    [Fact]
    public void PositionChanged_When_The_Canonical_Position_Code_Moved()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, live);
        snapshot.Lines[0].PositionCode = "area-manager";

        var row = Assert.Single(Diff(c, snapshot, live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.PositionChanged, row.DiffType);
    }

    [Fact]
    public void Precedence_Is_Deterministic_And_Secondary_Differences_Are_Reported()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        var snapshot = Snapshot(c, live);
        // Three differences at once: date wins over scope wins over position (pack §22.4 order).
        snapshot.Lines[0].PlannedEffectiveTo = Now.AddDays(30);
        snapshot.Lines[0].BusinessScopes = ["beta"];
        snapshot.Lines[0].PositionCode = "area-manager";

        var row = Assert.Single(Diff(c, snapshot, live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.DateChanged, row.DiffType);
        Assert.Contains(TerritoryPlanVsCurrentDiffTypes.ScopeChanged, row.SecondaryDifferences);
        Assert.Contains(TerritoryPlanVsCurrentDiffTypes.PositionChanged, row.SecondaryDifferences);
        Assert.DoesNotContain(TerritoryPlanVsCurrentDiffTypes.DateChanged, row.SecondaryDifferences);
    }

    [Fact]
    public void Position_Code_Matching_Is_Case_And_Whitespace_Insensitive_But_Never_Role_Based()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        live.PositionCode = "totally-different-legacy-role";  // deprecated field must not affect the diff
        var snapshot = Snapshot(c, live);
        snapshot.Lines[0].PositionCode = "  Medical-Representative ";

        var row = Assert.Single(Diff(c, snapshot, live));
        Assert.Equal(TerritoryPlanVsCurrentDiffTypes.Unchanged, row.DiffType);
        Assert.Equal("totally-different-legacy-role", row.LegacyRoleCode);
    }

    [Fact]
    public void Proposed_Records_Are_Never_Current()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse");   // still proposed
        var row = Assert.Single(Diff(c, Snapshot(c, planned), planned));
        Assert.NotEqual(TerritoryPlanVsCurrentDiffTypes.Unchanged, row.DiffType);
        Assert.Null(row.CurrentResourceId);
    }

    // -----------------------------------------------------------------------------------------------------------
    // Filters
    // -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Filters_Narrow_By_Node_BusinessUnit_Position_Resource_And_DiffType()
    {
        var c = NewContext("active");
        var kesan = Assignment(c, c.Kesan, "ayse", "active");
        var other = Assignment(c, c.Suleymanpasa, "mehmet", "active", "area-manager", "beta");
        var snapshot = Snapshot(c, kesan, other);
        var rows = Diff(c, snapshot, kesan, other);

        Assert.Equal(2, rows.Count);
        Assert.Single(TerritoryPlanVsCurrentEngine.Filter(rows, c.Kesan.Id, null, null, null, null));
        Assert.Single(TerritoryPlanVsCurrentEngine.Filter(rows, null, "beta", null, null, null));
        Assert.Single(TerritoryPlanVsCurrentEngine.Filter(rows, null, null, "area-manager", null, null));
        Assert.Single(TerritoryPlanVsCurrentEngine.Filter(rows, null, null, null, "ayse", null));
        Assert.Equal(2, TerritoryPlanVsCurrentEngine
            .Filter(rows, null, null, null, null, TerritoryPlanVsCurrentDiffTypes.Unchanged).Count);
        Assert.Empty(TerritoryPlanVsCurrentEngine
            .Filter(rows, null, null, null, null, TerritoryPlanVsCurrentDiffTypes.Replaced));
    }

    [Fact]
    public void EffectiveAt_Decides_Whether_A_Future_Assignment_Is_Current()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        live.ValidFrom = Now.AddDays(10);
        var snapshot = Snapshot(c, live);

        var today = TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
            c.Model.Id, c.Model.ModelCode, snapshot, [live], c.Nodes.Items.ToDictionary(n => n.Id), Now));
        var later = TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
            c.Model.Id, c.Model.ModelCode, snapshot, [live], c.Nodes.Items.ToDictionary(n => n.Id), Now.AddDays(20)));

        Assert.Null(Assert.Single(today).CurrentResourceId);
        Assert.Equal("ayse", Assert.Single(later).CurrentResourceId);
    }

    // -----------------------------------------------------------------------------------------------------------
    // States (handler level)
    // -----------------------------------------------------------------------------------------------------------

    private static GetTerritoryPlanVsCurrentHandler PlanVsCurrent(Context c, FakeTerritoryPlanSnapshotRepo snapshots)
        => new(TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, snapshots);

    [Fact]
    public async Task Draft_Model_Reports_Not_Yet_Activated()
    {
        var c = NewContext();
        c.Assignments.Items.Add(Assignment(c, c.Kesan, "ayse"));

        var result = await PlanVsCurrent(c, new FakeTerritoryPlanSnapshotRepo())
            .Handle(new GetTerritoryPlanVsCurrentQuery(c.Model.Id, null, null, null, null, null, null), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TerritoryPlanVsCurrentStates.NotYetActivated, result.Data!.State);
        Assert.Empty(result.Data.Rows);
    }

    [Fact]
    public async Task Active_Model_Without_A_Baseline_Reports_NotCaptured_And_Not_404()
    {
        var c = NewContext("active");
        c.Assignments.Items.Add(Assignment(c, c.Kesan, "ayse", "active"));

        var result = await PlanVsCurrent(c, new FakeTerritoryPlanSnapshotRepo())
            .Handle(new GetTerritoryPlanVsCurrentQuery(c.Model.Id, null, null, null, null, null, null), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(TerritoryPlanVsCurrentStates.NotCaptured, result.Data!.State);
    }

    [Fact]
    public async Task Archived_Model_Returns_A_Read_Only_Historical_Comparison()
    {
        var c = NewContext("archived");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        c.Assignments.Items.Add(live);
        var snapshots = new FakeTerritoryPlanSnapshotRepo();
        snapshots.Items.Add(Snapshot(c, live));

        var result = await PlanVsCurrent(c, snapshots)
            .Handle(new GetTerritoryPlanVsCurrentQuery(c.Model.Id, null, null, null, null, null, null), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TerritoryPlanVsCurrentStates.Available, result.Data!.State);
        Assert.True(result.Data.IsHistorical);
        Assert.Single(result.Data.Rows);
    }

    [Fact]
    public async Task Cross_Tenant_Model_Is_404()
    {
        var c = NewContext("active");
        var handler = new GetTerritoryPlanVsCurrentHandler(
            TenantFactory.Tenant(Guid.NewGuid()), c.Models, c.Nodes, c.Assignments, new FakeTerritoryPlanSnapshotRepo());

        var result = await handler.Handle(
            new GetTerritoryPlanVsCurrentQuery(c.Model.Id, null, null, null, null, null, null), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Snapshot_Endpoint_Reports_State_And_Versions()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        c.Assignments.Items.Add(live);
        var snapshots = new FakeTerritoryPlanSnapshotRepo();
        snapshots.Items.Add(Snapshot(c, live));

        var handler = new GetTerritoryResourceAssignmentPlanSnapshotHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, snapshots);
        var result = await handler.Handle(new GetTerritoryResourceAssignmentPlanSnapshotQuery(c.Model.Id), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TerritoryPlanVsCurrentStates.Available, result.Data!.State);
        Assert.Equal(1, result.Data.LineCount);
        Assert.Equal([1], result.Data.AvailableVersions);
    }

    [Fact]
    public async Task Resource_Level_View_Shows_Planned_Versus_Current_Drift()
    {
        var c = NewContext("active");
        var planned = Assignment(c, c.Kesan, "ayse", "active");
        var snapshots = new FakeTerritoryPlanSnapshotRepo();
        snapshots.Items.Add(Snapshot(c, planned));

        var target = Assignment(c, c.Suleymanpasa, "ayse", "active");
        target.TransferFromAssignmentId = planned.Id;
        target.TransferReason = "district rebalance";
        planned.Status = "ended";
        planned.ValidTo = Now.AddDays(-1);
        planned.TransferToAssignmentId = target.Id;
        c.Assignments.Items.AddRange([planned, target]);

        var handler = new GetResourcePlanVsCurrentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, snapshots);
        var result = await handler.Handle(new GetResourcePlanVsCurrentQuery("ayse", null, null, null, null, null), default);

        Assert.True(result.IsSuccessful);
        Assert.Contains(result.Data!.Rows, r => r.DiffType == TerritoryPlanVsCurrentDiffTypes.TransferredOut);
        Assert.Contains(result.Data.Rows, r => r.DiffType == TerritoryPlanVsCurrentDiffTypes.TransferredIn);
        Assert.Equal("ayse", result.Data.ResourceId);
    }

    [Fact]
    public async Task Resource_Level_View_Requires_A_Resource_Id()
    {
        var c = NewContext("active");
        var handler = new GetResourcePlanVsCurrentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, new FakeTerritoryPlanSnapshotRepo());

        var result = await handler.Handle(new GetResourcePlanVsCurrentQuery("  ", null, null, null, null, null), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    // -----------------------------------------------------------------------------------------------------------
    // Guards
    // -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Reading_Plan_Vs_Current_Mutates_Nothing()
    {
        var c = NewContext("active");
        var live = Assignment(c, c.Kesan, "ayse", "active");
        c.Assignments.Items.Add(live);
        var snapshots = new FakeTerritoryPlanSnapshotRepo();
        snapshots.Items.Add(Snapshot(c, live));

        var before = (live.Status, live.ValidFrom, live.ValidTo, live.IsPrimary, c.Model.Status,
            c.Assignments.Items.Count, snapshots.Items.Count);

        await PlanVsCurrent(c, snapshots)
            .Handle(new GetTerritoryPlanVsCurrentQuery(c.Model.Id, null, null, null, null, null, null), default);

        Assert.Equal(before, (live.Status, live.ValidFrom, live.ValidTo, live.IsPrimary, c.Model.Status,
            c.Assignments.Items.Count, snapshots.Items.Count));
    }

    [Fact]
    public void Summary_Counts_Rows_By_Diff_Type()
    {
        var c = NewContext("active");
        var kesan = Assignment(c, c.Kesan, "ayse", "active");
        var extra = Assignment(c, c.Suleymanpasa, "mehmet", "active");
        var rows = Diff(c, Snapshot(c, kesan), kesan, extra);

        var summary = TerritoryPlanVsCurrentEngine.Summarize(1, 2, rows);
        Assert.Equal(1, summary.PlannedCount);
        Assert.Equal(2, summary.CurrentCount);
        Assert.Equal(2, summary.RowCount);
        Assert.Equal(1, summary.ChangedCount);
        Assert.Equal(1, summary.CountsByDiffType[TerritoryPlanVsCurrentDiffTypes.AddedAfterActivation]);
    }
}
