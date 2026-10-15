using System.Net;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 (B17) over HTTP on the throwaway mongod: the register sorts on the server by the
/// whitelisted keys (pack quantity as a NUMBER — it is stored as a decimal string, so a plain sort would put 10 before
/// 2), refuses an unknown key with 400, and the Global Product picker pages with `hasMore`. Each test is its own tenant.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    [Fact]
    public async Task The_register_sorts_by_pack_quantity_as_a_number_in_both_directions()
    {
        // One Global Product per GSKU: each create is a FIRST GSKU (its own revision), as a maker creates them.
        var two = await CreateWithQuantityAsync(2m);
        var ten = await CreateWithQuantityAsync(10m);
        var three = await CreateWithQuantityAsync(3m);

        var descending = await ListIdsAsync("orderBy=packQuantity&orderDir=desc");
        var ascending = await ListIdsAsync("orderBy=packQuantity&orderDir=asc");

        Assert.Equal([ten, three, two], descending.Ids);
        Assert.Equal([two, three, ten], ascending.Ids);
        Assert.Equal(3, descending.Total);
    }

    [Fact]
    public async Task The_register_pages_a_sorted_read_without_repeating_or_losing_a_row()
    {
        var created = new List<Guid>();
        foreach (var quantity in new[] { 5m, 1m, 4m })
            created.Add(await CreateWithQuantityAsync(quantity));

        var first = await ListIdsAsync("orderBy=packQuantity&orderDir=desc&pageNumber=1&pageSize=2");
        var second = await ListIdsAsync("orderBy=packQuantity&orderDir=desc&pageNumber=2&pageSize=2");

        Assert.Equal([created[0], created[2]], first.Ids);
        Assert.Equal([created[1]], second.Ids);
        Assert.Equal(3, second.Total);
    }

    [Theory]
    [InlineData("orderBy=globalProductName&orderDir=asc")]
    [InlineData("orderBy=TenantId")]
    [InlineData("orderBy=packQuantity&orderDir=sideways")]
    public async Task An_unknown_sort_key_or_direction_is_a_400(string query)
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_product_picker_pages_the_referenceable_products_and_says_when_more_exist()
    {
        for (var i = 0; i < 3; i++)
            await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        await SeedProductAsync(ProductIdentityLifecycleStatus.Retired);   // never referenceable, never counted

        var first = await CreateOptionsAsync("pageNumber=1&pageSize=2");
        var second = await CreateOptionsAsync("pageNumber=2&pageSize=2");

        Assert.Equal(2, first.Products);
        Assert.True(first.HasMore);
        Assert.Equal(1, second.Products);
        Assert.False(second.HasMore);
    }

    private async Task<Guid> CreateWithQuantityAsync(decimal quantity)
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var response = await PostAsync("/api/gskus/drafts", new { globalProductId = product, packQuantity = quantity, packUomCode = "C62" });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("gskuId").GetGuid();
    }

    private async Task<(Guid[] Ids, long Total)> ListIdsAsync(string query)
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray(),
            data.GetProperty("totalCount").GetInt64());
    }

    private async Task<(int Products, bool HasMore)> CreateOptionsAsync(string query)
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus/create-options?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return (data.GetProperty("globalProducts").GetArrayLength(), data.GetProperty("hasMore").GetBoolean());
    }
}
