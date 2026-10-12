using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-BL598-01 FIX1 item 3 — the handlers' own mapping of a policy store whose unique indexes are not in place: Create
/// and End answer 503 PRODUCT_SCOPE_POLICY_STORE_NOT_READY (nothing written), never another code or status.
/// </summary>
public sealed class Bl598Fix1HandlerTests
{
    [Fact]
    public async Task Bl598_Fix1_A_create_on_a_store_not_ready_is_503_by_name()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        fixture.Policies.IndexesNotReady = true;

        var create = await fixture.Create.Handle(new CreateProductLegalEntityScopePolicyCommand(
            fixture.Product.Id, Guid.NewGuid(), new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);

        Assert.Equal(503, create.StatusCode);
        Assert.Equal(CreateProductLegalEntityScopePolicyHandler.StoreNotReadyCode, Assert.Single(create.Errors));
        Assert.Equal("PRODUCT_SCOPE_POLICY_STORE_NOT_READY", CreateProductLegalEntityScopePolicyHandler.StoreNotReadyCode);
        Assert.Null(fixture.Policies.Current);
    }

    [Fact]
    public async Task Bl598_Fix1_An_end_on_a_store_not_ready_is_503_by_name()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        await fixture.Create.Handle(new(fixture.Product.Id, Guid.NewGuid(), new() { Mode = ProductLegalEntityScopeMode.GroupWide }),
            default);
        fixture.Policies.IndexesNotReady = true;

        var end = await fixture.End.Handle(new(fixture.Product.Id, Guid.NewGuid(), new() { ExpectedVersion = 0 }), default);

        Assert.Equal(503, end.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_POLICY_STORE_NOT_READY", Assert.Single(end.Errors));
    }
}
