using System.Net.Http.Headers;
using System.Text;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Infrastructure;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// AC15 (second half): the REAL typed HttpClients of this service — wired by the production
/// <c>AddInfrastructure</c>, with the production header-forwarding handler — against Prism mocks of the consumed contracts.
/// Plus AC16 on the real clients: an unreachable master fails closed.
/// <para>WP-0173-PM1: the PRODUCT-MASTER mock serves the v1.1 DRAFT (`product-master-bundle.openapi.yaml` 1.1.0-draft.8). Its first
/// (default) example of every operation is the v1.0 one ("v1.0-mock-only"); the v1.1 answers are named examples, chosen with
/// Prism's <c>Prefer: example=…</c> header, which only these smokes add (the production client sends none). The frozen v1.0
/// copy runs as a third mock for the backward-compatibility smoke. Every v1.1 answer is also validated against the v1.1
/// schema with the open-enum rule (CT-PM1-3).</para>
/// </summary>
public sealed class PrismConsumedContractSmokeTests : IClassFixture<PrismMockFixture>
{
    // The identifiers of the contract examples (Prism answers with them).
    private static readonly Guid ExampleItem = Guid.Parse("b1f2c3d4-0000-0000-0000-000000000001");
    private static readonly Guid ExampleSku = Guid.Parse("c3d4e5f6-0000-0000-0000-000000000002");
    private static readonly Guid ExampleMaterial = Guid.Parse("d4e5f6a7-0000-0000-0000-000000000003");
    private static readonly Guid ExampleGsku = Guid.Parse("e5f6a7b8-0000-0000-0000-000000000004");

    private readonly PrismMockFixture _prism;

    public PrismConsumedContractSmokeTests(PrismMockFixture prism) => _prism = prism;

    [Fact]
    public async Task Product_master_client_reads_every_consumed_operation_of_the_v1_1_contract()
    {
        using var scope = Services(_prism.ProductMasterBaseUrl, _prism.LocationMasterBaseUrl, preferV11: true).CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IProductMasterClient>();

        var refs = await client.ValidateRefsAsync([new ProductRefQuery(null, ExampleMaterial, SkuLevel.Item)]);
        var row = Assert.Single(refs, r => r.Exists);
        Assert.Equal("Active", row.LifecycleStatus);
        Assert.Equal("Item", row.ItemKind);

        var lsku = await client.GetSkuAsync(ExampleSku, SkuLevel.Lsku);
        Assert.Equal((true, true, "Active"), (lsku!.LotControlled, lsku.SerialControlled, lsku.LifecycleStatus));
        var gsku = await client.GetSkuAsync(ExampleGsku, SkuLevel.Gsku);
        Assert.Equal((true, (bool?)null), (gsku!.LotControlled, gsku.SerialControlled)); // OD-10: not applicable at GSKU
        var item = await client.GetSkuAsync(ExampleMaterial, SkuLevel.Item);
        Assert.Equal(ExampleMaterial.ToString(), item!.ItemId); // skuId == itemId
        Assert.Equal((true, false), (item.LotControlled, item.SerialControlled));

        var product = await client.GetProductAsync(ExampleItem);
        Assert.Equal("Active", product!.LifecycleStatus);
        Assert.Null(product.LotControlled); // the v1.1 Global Product example carries no stock field (O-1): unknown, not false

        var uom = await client.GetSkuUomAsync(ExampleMaterial);
        Assert.Equal("KGM", uom!.BaseUomId);
        Assert.True(UomConverter.TryConvertToBase(500m, "GRM", uom.BaseUomId, uom.Conversions, DateTimeOffset.UtcNow, out var kilograms));
        Assert.Equal(0.5m, kilograms);

        // The rule the ledger applies to those answers (no I/O): Lsku lot + serial, Gsku refused unless the product is
        // explicitly serial-free, Item lot only.
        Assert.Equal(new StockBehaviour(true, true, null), ProductMasterStockRules.Behaviour(SkuLevel.Lsku, lsku.LotControlled, lsku.SerialControlled, null));
        Assert.Equal(StockBehaviourRefusal.LevelNotStockable, ProductMasterStockRules.Behaviour(SkuLevel.Gsku, gsku.LotControlled, gsku.SerialControlled, product.SerialControlled).Refusal);
        Assert.Equal(new StockBehaviour(true, false, null), ProductMasterStockRules.Behaviour(SkuLevel.Item, item.LotControlled, item.SerialControlled, null));
    }

    [Fact]
    public async Task The_default_v1_0_shaped_answer_leaves_the_sku_flags_unknown_never_false()
    {
        using var scope = Services(_prism.ProductMasterBaseUrl, _prism.LocationMasterBaseUrl, preferV11: false).CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IProductMasterClient>();

        var sku = await client.GetSkuAsync(ExampleSku, SkuLevel.Lsku);
        Assert.Null(sku!.LotControlled);
        Assert.Null(sku.SerialControlled);
        Assert.Equal(StockBehaviourRefusal.DataIncomplete, ProductMasterStockRules.Behaviour(SkuLevel.Lsku, sku.LotControlled, sku.SerialControlled, null).Refusal);
        var product = await client.GetProductAsync(ExampleItem);
        Assert.True(product!.LotControlled); // the deprecated product flag is read (true) but no rule uses it
    }

    [Theory]
    [InlineData("skus/c3d4e5f6-0000-0000-0000-000000000002?level=Lsku", "lsku", "SkuRef")]
    [InlineData("skus/e5f6a7b8-0000-0000-0000-000000000004?level=Gsku", "gsku", "SkuRef")]
    [InlineData("skus/d4e5f6a7-0000-0000-0000-000000000003?level=Item", "item", "SkuRef")]
    [InlineData("skus/f6a7b8c9-0000-0000-0000-000000000005?level=FinishedGood", "fg", "SkuRef")]
    [InlineData("products/b1f2c3d4-0000-0000-0000-000000000001", "global-product", "ProductRef")]
    [InlineData("products/d4e5f6a7-0000-0000-0000-000000000003", "item", "ProductRef")]
    [InlineData("skus/d4e5f6a7-0000-0000-0000-000000000003/uom", "item-uom", "UomInfo")]
    [InlineData("stockable-items?status=Active&q=amoks", "page", "StockableItemPage")]
    public async Task Every_v1_1_answer_validates_against_the_v1_1_contract(string path, string example, string schema)
    {
        var body = await RawGetAsync(_prism.ProductMasterBaseUrl, path, example);
        Assert.Empty(ContractSchemas.ValidateProductMaster(schema, body));
    }

    [Fact]
    public void The_v1_1_validator_keeps_types_closed_and_enums_open()
    {
        const string sku = """{"skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Pallet","itemId":"b1f2c3d4-0000-0000-0000-000000000001","lifecycleStatus":"Frozen","lotControlled":true,"serialControlled":null}""";
        Assert.Empty(ContractSchemas.ValidateProductMaster("SkuRef", sku)); // unknown enum values are not errors (CT-PM1-3)
        Assert.NotEmpty(ContractSchemas.ValidateProductMaster("SkuRef", sku.Replace("\"lotControlled\":true", "\"lotControlled\":\"yes\"")));
        Assert.NotEmpty(ContractSchemas.ValidateProductMaster("SkuRef", sku.Replace("}", ",\"invented\":1}")));
    }

    [Fact]
    public async Task The_frozen_v1_0_contract_still_parses_and_gives_no_sku_flags()
    {
        using var scope = Services(_prism.ProductMasterV10BaseUrl, _prism.LocationMasterBaseUrl, preferV11: false).CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IProductMasterClient>();

        var refs = await client.ValidateRefsAsync([new ProductRefQuery(ExampleItem, null, null), new ProductRefQuery(null, ExampleSku, SkuLevel.Lsku)]);
        Assert.Contains(refs, r => r.Exists && r.ItemId == ExampleItem.ToString() && r.LifecycleStatus is null);
        var sku = await client.GetSkuAsync(ExampleSku, SkuLevel.Lsku);
        Assert.Equal(ExampleItem.ToString(), sku!.ItemId);
        Assert.Null(sku.LotControlled); // a v1.0 server cannot answer the v1.1 question → every movement refuses (fail-closed)
        var uom = await client.GetSkuUomAsync(ExampleSku);
        Assert.True(UomConverter.TryConvertToBase(100m, "BOX", uom!.BaseUomId, uom.Conversions, DateTimeOffset.UtcNow, out var baseQuantity));
        Assert.Equal(1000m, baseQuantity);
        Assert.Empty(ContractSchemas.ValidateProductMasterV10("SkuRef", await RawGetAsync(_prism.ProductMasterV10BaseUrl, "skus/c3d4e5f6-0000-0000-0000-000000000002?level=Lsku", null)));
    }

    [Fact]
    public async Task Location_client_reads_every_consumed_operation_of_the_frozen_contract()
    {
        using var scope = Services(_prism.ProductMasterBaseUrl, _prism.LocationMasterBaseUrl, preferV11: false).CreateScope();
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
        using var scope = Services(dead, dead, preferV11: false).CreateScope();

        await Assert.ThrowsAsync<ProductMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IProductMasterClient>().GetProductAsync(ExampleItem));
        await Assert.ThrowsAsync<ProductMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IProductMasterClient>().GetSkuAsync(ExampleSku, SkuLevel.Item));
        await Assert.ThrowsAsync<LocationMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<ILocationMasterClient>().GetLocationAsync("loc-A-12-3"));
    }

    [Fact]
    public async Task A_master_answering_outside_its_contract_is_treated_as_unavailable()
    {
        // Pointed at the LOCATION mock, POST validate answers 200 with location-shaped results
        // ({ locationId, exists }) — outside the PRODUCT-MASTER ValidateResponse shape → unavailable, never a pass.
        using var scope = Services(_prism.LocationMasterBaseUrl, _prism.ProductMasterBaseUrl, preferV11: false).CreateScope();

        await Assert.ThrowsAsync<ProductMasterUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<IProductMasterClient>().ValidateRefsAsync([new ProductRefQuery(ExampleItem, null, null)]));
    }

    private static async Task<string> RawGetAsync(Uri baseUrl, string path, string? example)
    {
        using var http = new HttpClient { BaseAddress = baseUrl };
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "smoke-test");
        if (example is not null) request.Headers.TryAddWithoutValidation("Prefer", "example=" + example);
        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path} ({example}): {(int)response.StatusCode} {body}");
        return body;
    }

    private static ServiceProvider Services(Uri productMaster, Uri locationMaster, bool preferV11)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Inventory:Upstream:ProductMasterBaseUrl"] = productMaster.ToString(),
            ["Inventory:Upstream:LocationMasterBaseUrl"] = locationMaster.ToString(),
            // W-0173-02 client: not exercised by these Prism smokes, but the registration requires an absolute URL.
            ["Inventory:Upstream:LegalEntityMasterBaseUrl"] = "http://127.0.0.1:9/",
            // MOD-0173-FU02 org-unit client: not exercised here either; the registration requires an absolute URL.
            ["Inventory:Upstream:OrgUnitDirectoryBaseUrl"] = "http://127.0.0.1:9/",
            ["Inventory:Upstream:TimeoutSeconds"] = "5"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, new HostingEnvironment { EnvironmentName = "Testing" });
        if (preferV11)
        {
            // Test-only: picks the v1.1 named example of each operation (Prism `Prefer`); production sends no such header.
            services.AddHttpClient<IProductMasterClient, ProductMasterHttpClient>().AddHttpMessageHandler(() => new PreferV11ExampleHandler());
        }

        // The production handler forwards the CALLER's bearer (Prism requires one) and correlation id.
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer smoke-test";
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        var provider = services.BuildServiceProvider();
        context.RequestServices = provider;
        provider.GetRequiredService<CorrelationContext>().Set(Guid.NewGuid());
        return provider;
    }

    /// <summary>Maps each request to the v1.1 example of the contract that answers it.</summary>
    private sealed class PreferV11ExampleHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            var example =
                path.EndsWith("/validate", StringComparison.Ordinal) ? "item"
                : path.EndsWith("/uom", StringComparison.Ordinal) ? (path.Contains(ExampleMaterial.ToString(), StringComparison.Ordinal) ? "item-uom" : "fg-uom")
                : path.Contains("/products/", StringComparison.Ordinal) ? (path.Contains(ExampleMaterial.ToString(), StringComparison.Ordinal) ? "item" : "global-product")
                : query.Contains("level=Lsku", StringComparison.Ordinal) ? "lsku"
                : query.Contains("level=Gsku", StringComparison.Ordinal) ? "gsku"
                : query.Contains("level=FinishedGood", StringComparison.Ordinal) ? "fg"
                : "item";
            request.Headers.TryAddWithoutValidation("Prefer", "example=" + example);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
