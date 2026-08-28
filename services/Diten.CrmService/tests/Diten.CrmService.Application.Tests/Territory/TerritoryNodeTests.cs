using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Nodes;
using Diten.CrmService.Application.Features.Territory.Nodes.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryNodeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset From = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        FakeTerritoryModelRepo Models,
        FakeTerritoryNodeRepo Nodes,
        FakeTerritoryReferenceValidator References,
        TerritoryModel Model,
        CreateTerritoryNodeHandler Create,
        UpdateTerritoryNodeHandler Update);

    private static Fixture NewFixture(string modelStatus = "draft")
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var references = new FakeTerritoryReferenceValidator();
        var model = new TerritoryModel
        {
            TenantId = TenantA, ModelCode = "TM-2027", Name = "Plan", Status = modelStatus,
            EffectiveFrom = From, EffectiveTo = To
        };
        models.Items.Add(model);
        var tenant = TenantFactory.Tenant(TenantA);
        return new Fixture(models, nodes, references, model,
            new CreateTerritoryNodeHandler(tenant, models, nodes, references),
            new UpdateTerritoryNodeHandler(tenant, models, nodes, references));
    }

    private static CreateTerritoryNodeCommand NewNode(
        Guid modelId, string code, string level, Guid? parent = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, MicroZoneProfileInput? profile = null) =>
        new(modelId, parent, code, $"{level} node", level, null, null, null, null, null, null,
            from ?? From, to ?? To, 0, profile, null);

    [Fact]
    public async Task Create_Root_Node_Succeeds()
    {
        var f = NewFixture();
        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country"), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Single(f.Nodes.Items);
    }

    [Fact]
    public async Task Create_Child_With_Greater_Rank_Succeeds()
    {
        var f = NewFixture();
        var parent = new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "C-TR", Name = "Country", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Nodes.Items.Add(parent);

        var response = await f.Create.Handle(NewNode(f.Model.Id, "Z-1", "zone", parent.Id), default);

        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Create_Level_Skip_Is_Allowed()
    {
        // country (20) -> zone (50): skipping region/area is allowed (child rank still greater).
        var f = NewFixture();
        var parent = new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "C-TR", Name = "Country", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Nodes.Items.Add(parent);

        var response = await f.Create.Handle(NewNode(f.Model.Id, "Z-9", "zone", parent.Id), default);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Create_Backward_Level_Is_Blocked()
    {
        // parent zone (50), child region (30): 30 <= 50 → blocked.
        var f = NewFixture();
        var parent = new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "Z-1", Name = "Zone", TerritoryLevel = "zone", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Nodes.Items.Add(parent);

        var response = await f.Create.Handle(NewNode(f.Model.Id, "R-1", "region", parent.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Unpublished_Level_Set_Is_Blocked()
    {
        var f = NewFixture();
        f.References.MissingSets.Add(TerritoryReferenceSets.TerritoryLevel);

        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(f.Nodes.Items);
    }

    [Fact]
    public async Task Create_With_Invalid_Level_Is_Blocked()
    {
        var f = NewFixture();
        var response = await f.Create.Handle(NewNode(f.Model.Id, "X-1", "not-a-level"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Duplicate_TerritoryCode_In_Model_Is_Blocked()
    {
        var f = NewFixture();
        f.Nodes.Items.Add(new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "C-TR", Name = "x", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To });

        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Same_TerritoryCode_Across_Different_Models_Is_Allowed()
    {
        var f = NewFixture();
        var otherModel = new TerritoryModel { TenantId = TenantA, ModelCode = "TM-OTHER", Name = "Other", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Models.Items.Add(otherModel);
        f.Nodes.Items.Add(new TerritoryNode { TenantId = TenantA, ModelId = otherModel.Id, TerritoryCode = "C-TR", Name = "x", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To });

        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country"), default);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Create_With_Parent_From_Another_Model_Returns_404()
    {
        var f = NewFixture();
        var otherModel = new TerritoryModel { TenantId = TenantA, ModelCode = "TM-OTHER", Name = "Other", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Models.Items.Add(otherModel);
        var foreignParent = new TerritoryNode { TenantId = TenantA, ModelId = otherModel.Id, TerritoryCode = "C-TR", Name = "x", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Nodes.Items.Add(foreignParent);

        var response = await f.Create.Handle(NewNode(f.Model.Id, "Z-1", "zone", foreignParent.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Update_Cycle_Is_Blocked()
    {
        var f = NewFixture();
        var a = new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "A", Name = "A", TerritoryLevel = "country", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        var b = new TerritoryNode { TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "B", Name = "B", TerritoryLevel = "region", Status = "draft", EffectiveFrom = From, EffectiveTo = To };
        f.Nodes.Items.Add(a);
        f.Nodes.Items.Add(b);
        f.Nodes.CycleAnswer = true;

        var cmd = new UpdateTerritoryNodeCommand(f.Model.Id, a.Id, b.Id, "A", "A", "country",
            null, null, null, null, null, null, From, To, 0, null, null);
        var response = await f.Update.Handle(cmd, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Node_Date_Outside_Model_Is_Blocked()
    {
        var f = NewFixture();
        var outside = new DateTimeOffset(2028, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country", from: From, to: outside), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Child_Date_Outside_Parent_Is_Blocked()
    {
        var f = NewFixture();
        var parent = new TerritoryNode
        {
            TenantId = TenantA, ModelId = f.Model.Id, TerritoryCode = "C-TR", Name = "Country", TerritoryLevel = "country",
            Status = "draft", EffectiveFrom = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero), EffectiveTo = new DateTimeOffset(2027, 9, 1, 0, 0, 0, TimeSpan.Zero)
        };
        f.Nodes.Items.Add(parent);

        // Child starts before the parent's EffectiveFrom → outside parent range.
        var response = await f.Create.Handle(NewNode(f.Model.Id, "Z-1", "zone", parent.Id, from: From, to: To), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task MicroZoneProfile_Allowed_For_MicroZone()
    {
        var f = NewFixture();
        var profile = new MicroZoneProfileInput(Guid.NewGuid(), "central hospital cluster", "hospital");
        var response = await f.Create.Handle(NewNode(f.Model.Id, "MZ-1", "microzone", profile: profile), default);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(f.Nodes.Items.Single().MicroZoneProfile);
    }

    [Fact]
    public async Task MicroZoneProfile_Blocked_For_NonMicroZone()
    {
        var f = NewFixture();
        var profile = new MicroZoneProfileInput(Guid.NewGuid(), "x", null);
        var response = await f.Create.Handle(NewNode(f.Model.Id, "Z-1", "zone", profile: profile), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task NonDraft_Model_Node_Mutation_Is_Blocked()
    {
        var f = NewFixture(modelStatus: "active");
        var response = await f.Create.Handle(NewNode(f.Model.Id, "C-TR", "country"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }
}
