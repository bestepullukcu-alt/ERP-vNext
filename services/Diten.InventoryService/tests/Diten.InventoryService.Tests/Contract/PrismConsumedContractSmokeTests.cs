using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// AC15 (second half): the REAL typed HttpClients of this service — wired by the production
/// <c>AddInfrastructure</c>, with the production header-forwarding handler — against Prism mocks of the frozen
/// PRODUCT-MASTER and LOCATION contracts. Plus AC16 on the real clients: an unreachable master fails closed.
/// </summary>
public sealed class PrismConsumedContractSmokeTests : IClassFixture<PrismMockFixture>
{
    // The identifiers of the frozen contract examples (Prism answers with them).
    private static readonly Guid ExampleItem = Guid.Parse("b1f2c3d4-0000-0000-0000-000000000001");
    private static readonly Guid ExampleSku = Guid.Parse("c3d4e5f6-0000-0000-0000-000000000002");

    private readonly PrismMockFixture _prism;

    public PrismConsumedContractSmokeTests(PrismMockFixture prism) => _prism = prism;

    [Fact]
    public async Task Product_master_client_reads_every_consumed_operation_of_the_frozen_contract()
    {
        using var scope = Services(_prism.ProductMasterBaseUrl, _prism.LocationMasterBaseUrl).CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IProductMasterClient>();

        var refs = await client.ValidateRefsAsync([new ProductRefQuery(ExampleItem, null, null), new ProductRefQuery(null, ExampleSku, SkuLevel.Lsku)]);
        var product = await client.GetProductAsync(ExampleItem);
        var sku = await client.GetSkuAsync(ExampleSku, SkuLevel.Lsku);
        var uom = await client.GetSkuUomAsync(ExampleSku);

        Assert.Contains(refs, r => r.Exists && r.ItemId == ExampleItem.ToString());
        Assert.Contains(refs, r => r.Exists && r.SkuId == ExampleSku.ToString() && r.SkuLevel == "Lsku");
        Assert.NotNull(product);
        Assert.True(product!.LotControlled);
        Assert.Equal(ExampleItem.ToString(), sku!.ItemId);
        Assert.Equal("EA", uom!.BaseUomId);
        Assert.True(UomConverter.TryConvertToBase(100m, "BOX", uom.BaseUomId, uom.Conversions, DateTimeOffset.UtcNow, out var baseQuantity));
        Assert.Equal(1000m, baseQuantity);
    }

    [Fact]
    public async Task Location_client_reads_every_consumed_operation_of_the_frozen_contract()
    {
        using var scope = Services(_prism.ProductMasterBaseUrl, _prism.LocationMasterBaseUrl).CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ILocationMasterClient>();

        var validation = await client.ValidateLocationsAsync(["loc-A-12-3"]);
        var location = await client.GetLocationAsync("loc-A-12-3");
        var path = await client.GetLocationPathAsync("loc-A-12-3");

        Assert.Contains(validation, v => v.LocationId == "loc-A-12-3" && v.Exists);
        Assert.Equal("Active", location!.Status);
        Assert.True(location.IsStockable);
        Assert.True(path!.ContainsWarehouse("wh-01"));
    }

    [Fact]
    public async Task Unreachable_masters_fail_closed_on_the_real_clients()
    {
        var dead = new Uri("http://127.0.0.1:1/");
        using var scope = Services(dead, dead).CreateScope();

        await Assert.ThrowsAsync<ProductMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IProductMasterClient>().GetProductAsync(ExampleItem));
        await Assert.ThrowsAsync<LocationMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<ILocationMasterClient>().GetLocationAsync("loc-A-12-3"));
    }

    [Fact]
    public async Task A_master_answering_outside_its_contract_is_treated_as_unavailable()
    {
        // Pointed at the LOCATION mock, POST validate answers 200 with location-shaped results
        // ({ locationId, exists }) — outside the PRODUCT-MASTER ValidateResponse shape → unavailable, never a pass.
        using var scope = Services(_prism.LocationMasterBaseUrl, _prism.ProductMasterBaseUrl).CreateScope();

        await Assert.ThrowsAsync<ProductMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IProductMasterClient>().ValidateRefsAsync([new ProductRefQuery(ExampleItem, null, null)]));
    }

    private static ServiceProvider Services(Uri productMaster, Uri locationMaster)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Inventory:Upstream:ProductMasterBaseUrl"] = productMaster.ToString(),
            ["Inventory:Upstream:LocationMasterBaseUrl"] = locationMaster.ToString(),
            // W-0173-02 client: not exercised by these Prism smokes, but the registration requires an absolute URL.
            ["Inventory:Upstream:LegalEntityMasterBaseUrl"] = "http://127.0.0.1:9/",
            ["Inventory:Upstream:TimeoutSeconds"] = "5"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, new HostingEnvironment { EnvironmentName = "Testing" });

        // The production handler forwards the CALLER's bearer (Prism requires one) and correlation id.
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer smoke-test";
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        var provider = services.BuildServiceProvider();
        context.RequestServices = provider;
        provider.GetRequiredService<CorrelationContext>().Set(Guid.NewGuid());
        return provider;
    }
}
