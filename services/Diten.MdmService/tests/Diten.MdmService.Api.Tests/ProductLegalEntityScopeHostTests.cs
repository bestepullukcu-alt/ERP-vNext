using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX4 K4 — the activation debt as the SHIPPED host resolves it: Program.cs as built, its last
/// registration wins. A host that registered an empty debt (or none) would open activation; this reads the real container.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class ProductLegalEntityScopeHostTests(MdmHttpHost host)
{
    [Fact]
    public void The_api_host_resolves_a_debt_that_keeps_activation_shut()
    {
        var debt = host.Factory.Services.GetRequiredService<IProductLegalEntityScopeActivationDebt>();

        Assert.Same(ProductLegalEntityScopeActivationDebt.Instance, debt);
        Assert.True(ProductLegalEntityScopeActivationDebt.BlocksActivation(debt));
        Assert.Single(host.Factory.Services.GetServices<IProductLegalEntityScopeActivationDebt>());
    }
}
