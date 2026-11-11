using System.Net;
using System.Text;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Diten.InventoryService.Infrastructure.Upstream;
using Xunit;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-1 — the MOD-0173 seam of pack §5 (F-5): PRODUCT-MASTER v1.1 <c>getSku.storageConditions</c> read by the REAL
/// <see cref="ProductMasterHttpClient"/> into <c>SkuReference.StorageConditions</c>: a list of codes as sent (an open enum — a code this service
/// does not know yet is kept), an empty list = "no conditions", and anything else — absent, <c>null</c>, not an array, an entry that is not a
/// code — = null = UNKNOWN. And the consumer side: unknown is never read as "no conditions" (pack §8.12 →
/// <c>WMS_STORAGE_CONDITIONS_UNKNOWN</c>; the fit rule DL-2 applies).
/// </summary>
public sealed class WarehouseStorageConditionsSeamTests
{
    private sealed class Answer(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static async Task<IReadOnlyList<string>?> ReadAsync(string member)
    {
        var sku = Guid.NewGuid();
        var json = $$"""{ "skuId": "{{sku}}", "skuLevel": "LSKU", "itemId": "{{Guid.NewGuid()}}", "lifecycleStatus": "Active" {{member}} }""";
        var read = await new ProductMasterHttpClient(new HttpClient(new Answer(json)) { BaseAddress = new Uri("http://product-master.test/") })
            .GetSkuAsync(sku, SkuLevel.Lsku);
        Assert.NotNull(read);
        return read!.StorageConditions;
    }

    [Fact]
    public async Task A_list_of_codes_is_read_as_sent_and_an_empty_list_means_no_conditions()
    {
        Assert.Equal(["COOL_2_8", "PROTECT_FROM_LIGHT"], await ReadAsync(""", "storageConditions": ["COOL_2_8", "PROTECT_FROM_LIGHT"]"""));
        Assert.Equal(["ULTRA_COLD_NEW"], await ReadAsync(""", "storageConditions": ["ULTRA_COLD_NEW"]""")); // open enum: kept, decided later
        var none = await ReadAsync(""", "storageConditions": []""");
        Assert.NotNull(none);
        Assert.Empty(none!);
    }

    [Theory]
    [InlineData("")]
    [InlineData(""", "storageConditions": null""")]
    [InlineData(""", "storageConditions": "COOL_2_8" """)]
    [InlineData(""", "storageConditions": ["COOL_2_8", 7]""")]
    [InlineData(""", "storageConditions": ["COOL_2_8", ""]""")]
    [InlineData(""", "storageConditions": { "code": "COOL_2_8" }""")]
    public async Task Anything_else_is_unknown_never_an_empty_list(string member)
        => Assert.Null(await ReadAsync(member));

    /// <summary>K3 sabotage (k) at the seam: read an absent field as [] → the unknown product would fit an unlabelled bin → red.</summary>
    [Fact]
    public async Task An_absent_field_reaches_the_fit_rule_as_unknown_not_as_no_conditions()
    {
        var conditions = await ReadAsync("");
        Assert.Equal(StorageConditionFit.Unknown, LocationRoleRules.Fit(conditions, null));
    }
}
