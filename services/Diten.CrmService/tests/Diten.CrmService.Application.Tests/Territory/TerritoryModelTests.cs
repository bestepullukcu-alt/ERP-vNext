using System.Reflection;
using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Models;
using Diten.CrmService.Application.Features.Territory.Models.Handlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryModelTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset From = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2027, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private static CreateTerritoryModelCommand NewCreate(string code = "TM-2027") =>
        new(code, "TR Commercial Plan 2027", "tr", null, From, To, null, null, null);

    [Fact]
    public async Task Create_Draft_Model_Succeeds()
    {
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var response = await handler.Handle(NewCreate(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var stored = repo.Items.Single();
        Assert.Equal("draft", stored.Status);
        Assert.Equal(1, stored.VersionNumber);
        Assert.Equal(TenantA, stored.TenantId);
    }

    [Fact]
    public async Task Create_Duplicate_ModelCode_Returns_409()
    {
        var repo = new FakeTerritoryModelRepo();
        repo.Items.Add(new TerritoryModel { TenantId = TenantA, ModelCode = "TM-2027", Name = "Existing", Status = "draft" });
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var response = await handler.Handle(NewCreate("TM-2027"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Unpublished_Status_Set_Returns_400()
    {
        var references = new FakeTerritoryReferenceValidator();
        references.MissingSets.Add(TerritoryReferenceSets.TerritoryModelStatus);
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, references);

        var response = await handler.Handle(NewCreate(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(repo.Items); // fail-closed: nothing persisted
    }

    [Fact]
    public void Create_Command_Has_No_TenantId_Field()
    {
        // TenantId is server-resolved; it can never arrive from the request payload.
        var props = typeof(CreateTerritoryModelCommand).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToList();
        Assert.DoesNotContain("TenantId", props);
    }

    [Fact]
    public void Invalid_Date_Range_Is_Rejected_By_Validator()
    {
        var validator = new CreateTerritoryModelCommandValidator();
        var bad = NewCreate() with { EffectiveFrom = To, EffectiveTo = From };
        Assert.False(validator.Validate(bad).IsValid);
    }

    [Fact]
    public async Task Update_Draft_Model_Succeeds()
    {
        var repo = new FakeTerritoryModelRepo();
        var model = new TerritoryModel { TenantId = TenantA, ModelCode = "TM-2027", Name = "Old", Status = "draft", EffectiveFrom = From };
        repo.Items.Add(model);
        var handler = new UpdateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var response = await handler.Handle(new UpdateTerritoryModelCommand(model.Id, "New Name", "tr", null, From, To, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("New Name", model.Name);
    }

    [Fact]
    public async Task Update_NonDraft_Model_Returns_409()
    {
        var repo = new FakeTerritoryModelRepo();
        var model = new TerritoryModel { TenantId = TenantA, ModelCode = "TM-2027", Name = "Live", Status = "active", EffectiveFrom = From };
        repo.Items.Add(model);
        var handler = new UpdateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var response = await handler.Handle(new UpdateTerritoryModelCommand(model.Id, "New", null, null, From, To, null, null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task GetById_CrossTenant_Returns_404()
    {
        var repo = new FakeTerritoryModelRepo();
        var owned = new TerritoryModel { TenantId = TenantB, ModelCode = "TM-B", Name = "OtherTenant", Status = "draft" };
        repo.Items.Add(owned);
        var handler = new GetTerritoryModelByIdHandler(TenantFactory.Tenant(TenantA), repo);

        var response = await handler.Handle(new GetTerritoryModelByIdQuery(owned.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    // ── FU02A: Business Unit scopes ──────────────────────────────────────────

    private static TerritoryBusinessScopeInput Bu(string code) => new("business-unit", code);

    [Fact]
    public async Task Create_With_BusinessUnit_Scopes_Persists_Only_BusinessUnit_Type()
    {
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var cmd = NewCreate() with { BusinessScopes = new[] { Bu("alpha"), Bu("beta"), Bu("gamma") } };
        var response = await handler.Handle(cmd, default);

        Assert.True(response.IsSuccessful);
        var stored = repo.Items.Single();
        Assert.Equal(3, stored.BusinessScopes.Count);
        Assert.All(stored.BusinessScopes, s => Assert.Equal("business-unit", s.ScopeType));
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, stored.BusinessScopes.Select(s => s.ScopeCode));
    }

    [Fact]
    public async Task Create_With_Duplicate_BusinessUnit_Collapses()
    {
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var cmd = NewCreate() with { BusinessScopes = new[] { Bu("alpha"), Bu("Alpha"), Bu("alpha") } };
        var response = await handler.Handle(cmd, default);

        Assert.True(response.IsSuccessful);
        Assert.Single(repo.Items.Single().BusinessScopes);
    }

    [Fact]
    public async Task Create_With_NonBusinessUnit_ScopeType_Rejected_400()
    {
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        // brand-group (e.g. almiba/tutukon) must NOT be accepted as a business unit in FU02A.
        var cmd = NewCreate() with { BusinessScopes = new[] { new TerritoryBusinessScopeInput("brand-group", "almiba") } };
        var response = await handler.Handle(cmd, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Create_With_Unpublished_BusinessUnit_Set_FailsClosed_400()
    {
        var references = new FakeTerritoryReferenceValidator();
        references.MissingSets.Add(TerritoryReferenceSets.BusinessUnitValueSet);
        var repo = new FakeTerritoryModelRepo();
        var handler = new CreateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, references);

        var cmd = NewCreate() with { BusinessScopes = new[] { Bu("alpha") } };
        var response = await handler.Handle(cmd, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Update_Replaces_BusinessUnit_Scopes()
    {
        var repo = new FakeTerritoryModelRepo();
        var model = new TerritoryModel
        {
            TenantId = TenantA, ModelCode = "TM-2027", Name = "Old", Status = "draft", EffectiveFrom = From,
            BusinessScopes = { new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" } }
        };
        repo.Items.Add(model);
        var handler = new UpdateTerritoryModelHandler(TenantFactory.Tenant(TenantA), repo, new FakeTerritoryReferenceValidator());

        var cmd = new UpdateTerritoryModelCommand(model.Id, "New", "tr", null, From, To, null, null,
            new[] { Bu("beta"), Bu("gamma") });
        var response = await handler.Handle(cmd, default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(new[] { "beta", "gamma" }, model.BusinessScopes.Select(s => s.ScopeCode));
    }

    [Fact]
    public void Validator_Rejects_NonBusinessUnit_ScopeType()
    {
        var validator = new CreateTerritoryModelCommandValidator();
        var bad = NewCreate() with { BusinessScopes = new[] { new TerritoryBusinessScopeInput("product-portfolio", "x") } };
        Assert.False(validator.Validate(bad).IsValid);
    }

    [Fact]
    public void Detail_Mapper_Projects_BusinessScopes()
    {
        var model = new TerritoryModel
        {
            TenantId = TenantA, ModelCode = "TM-2027", Name = "N", Status = "draft",
            BusinessScopes = { new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" } }
        };
        var dto = TerritoryModelMapper.ToDetail(model);
        Assert.Single(dto.BusinessScopes);
        Assert.Equal("alpha", dto.BusinessScopes[0].ScopeCode);
    }
}
