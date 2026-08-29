using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryResourceAssignmentFu04ATests
{
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Context(
        FakeTerritoryModelRepo Models,
        FakeTerritoryNodeRepo Nodes,
        FakeTerritoryResourceAssignmentRepo Assignments,
        FakeTerritoryReferenceValidator References,
        TerritoryModel Model,
        TerritoryNode Zone,
        TerritoryNode OtherZone);

    private static Context NewContext(string status = "active")
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var model = new TerritoryModel
        {
            TenantId = TenantId,
            ModelCode = "FU04A",
            Name = "FU04A",
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
        var zone = Node("KESAN");
        var other = Node("SULEYMANPASA");
        models.Items.Add(model);
        nodes.Items.AddRange([zone, other]);
        return new(models, nodes, new FakeTerritoryResourceAssignmentRepo(),
            new FakeTerritoryReferenceValidator(), model, zone, other);
    }

    private static TerritoryResourceAssignment Assignment(Context c, string status = "active", string resourceId = "ayse")
        => new()
        {
            TenantId = TenantId,
            ModelId = c.Model.Id,
            TerritoryId = c.Zone.Id,
            Resource = new TerritoryResourceRef
            {
                ResourceId = resourceId,
                ResourceType = "person",
                DisplayName = resourceId
            },
            Position = new TerritoryPositionRef
            {
                PositionId = Guid.NewGuid(),
                PositionCode = "medical-representative",
                PositionTitle = "Medical Representative",
                PositionType = "person-position",
                SourceSystem = "organization-directory",
                ValidationMode = "policy-validated",
                PolicySource = TerritoryPositionPolicy.BuiltInSource
            },
            CoverageScope = "exact-territory",
            BusinessScopes = [new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" }],
            Status = status,
            AssignmentSource = "manual",
            IsPrimary = true,
            ValidFrom = Now.AddDays(-5),
            ValidTo = Now.AddDays(100),
            ChangeReason = "initial"
        };

    [Fact]
    public async Task Activation_Transitions_Proposed_To_Active()
    {
        var c = NewContext("draft");
        var proposed = Assignment(c, "proposed");
        c.Assignments.Items.Add(proposed);
        var handler = new ActivateTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.References,
            new FakeTerritoryLifecycleAuditPublisher(), c.Assignments, new FakeTerritoryActivationUnitOfWork(), new FakeTerritoryPlanSnapshotRepo());

        var result = await handler.Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", "fu04a"), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal("active", proposed.Status);
        Assert.Equal("fu04a", proposed.CorrelationId);
    }

    [Fact]
    public async Task Activation_Fails_Closed_On_Proposed_Duplicate()
    {
        var c = NewContext("draft");
        c.Assignments.Items.AddRange([Assignment(c, "proposed", "ayse"), Assignment(c, "proposed", "mehmet")]);
        var handler = new ActivateTerritoryModelHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.References,
            new FakeTerritoryLifecycleAuditPublisher(), c.Assignments, new FakeTerritoryActivationUnitOfWork(), new FakeTerritoryPlanSnapshotRepo());

        var result = await handler.Handle(new ActivateTerritoryModelCommand(c.Model.Id, "go live", "fu04a"), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("draft", c.Model.Status);
    }

    [Fact]
    public async Task Current_Excludes_Draft_Proposed_And_Includes_Active()
    {
        var c = NewContext("draft");
        c.Assignments.Items.Add(Assignment(c, "proposed"));
        var handler = new GetCurrentTerritoryResourceResponsibilitiesHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments);

        var draft = await handler.Handle(
            new GetCurrentTerritoryResourceResponsibilitiesQuery(c.Model.Id, c.Zone.Id, "alpha",
                "medical-representative", Now), default);
        c.Model.Status = "active";
        c.Assignments.Items[0].Status = "active";
        var active = await handler.Handle(
            new GetCurrentTerritoryResourceResponsibilitiesQuery(c.Model.Id, c.Zone.Id, "alpha",
                "medical-representative", Now), default);

        Assert.Empty(draft.Data!);
        Assert.Single(active.Data!);
    }

    [Fact]
    public async Task Replace_Ends_Source_Creates_Active_And_Sets_Provenance()
    {
        var c = NewContext();
        var source = Assignment(c);
        c.Assignments.Items.Add(source);
        var handler = new ReplaceTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);

        var result = await handler.Handle(new ReplaceTerritoryResourceAssignmentCommand(
            c.Model.Id, source.Id, new TerritoryResourceRefInput("mehmet", "person", "Mehmet", null),
            source.Position.PositionId, source.EffectivePositionCode, source.EffectivePositionTitle,
            source.Position.PositionType, source.Position.SourceSystem, Now, "Ayşe transfer edildi.", "replace-1"), default);

        Assert.True(result.IsSuccessful);
        var ended = c.Assignments.Items.Single(a => a.Id == source.Id);
        var created = c.Assignments.Items.Single(a => a.Id == result.Data);
        Assert.Equal("ended", ended.Status);
        Assert.Equal("active", created.Status);
        Assert.Equal(ended.Id, created.ReplacedAssignmentId);
        Assert.Equal(created.Id, ended.ReplacementAssignmentId);
    }

    [Fact]
    public async Task Replace_Requires_Reason_And_Rolls_Back_On_Commit_Failure()
    {
        var c = NewContext();
        var source = Assignment(c);
        c.Assignments.Items.Add(source);
        var handler = new ReplaceTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);
        var missingReason = await handler.Handle(new ReplaceTerritoryResourceAssignmentCommand(
            c.Model.Id, source.Id, new TerritoryResourceRefInput("mehmet", "person", "Mehmet", null),
            source.Position.PositionId, source.EffectivePositionCode, source.EffectivePositionTitle,
            source.Position.PositionType, source.Position.SourceSystem, Now, null, "replace-2"), default);

        c.Assignments.FailLifecycleTransition = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ReplaceTerritoryResourceAssignmentCommand(
            c.Model.Id, source.Id, new TerritoryResourceRefInput("mehmet", "person", "Mehmet", null),
            source.Position.PositionId, source.EffectivePositionCode, source.EffectivePositionTitle,
            source.Position.PositionType, source.Position.SourceSystem, Now, "reason", "replace-3"), default));

        Assert.False(missingReason.IsSuccessful);
        Assert.Equal("active", source.Status);
        Assert.Single(c.Assignments.Items);
    }

    [Fact]
    public async Task Transfer_Ends_Source_And_Links_Target()
    {
        var c = NewContext();
        var source = Assignment(c);
        c.Assignments.Items.Add(source);
        var handler = new TransferTerritoryResourceAssignmentHandler(
            TenantFactory.Tenant(TenantId), c.Models, c.Nodes, c.Assignments, c.References);

        var result = await handler.Handle(new TransferTerritoryResourceAssignmentCommand(
            c.Model.Id, source.Id, c.OtherZone.Id, "exact-territory", ["alpha"], Now,
            "Süleymanpaşa transferi", "transfer-1"), default);

        Assert.True(result.IsSuccessful);
        var ended = c.Assignments.Items.Single(a => a.Id == source.Id);
        var created = c.Assignments.Items.Single(a => a.Id == result.Data);
        Assert.Equal("ended", ended.Status);
        Assert.Equal(c.OtherZone.Id, created.TerritoryId);
        Assert.Equal(source.Id, created.TransferFromAssignmentId);
        Assert.Equal(created.Id, ended.TransferToAssignmentId);
    }

    [Fact]
    public void Conflict_Uses_Position_And_Produces_MultiNode_Warning()
    {
        var c = NewContext();
        var left = Assignment(c);
        var right = Assignment(c);
        right.TerritoryId = c.OtherZone.Id;

        var report = TerritoryResourceConflictEngine.Report(
            [left, right], c.Nodes.Items.ToDictionary(n => n.Id));

        Assert.Empty(report.Conflicts);
        Assert.Contains(report.Warnings, w => w.Kind == TerritoryResourceConflictKinds.MultiNodeCoverage);
    }
}
