using System.Net;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GP-LIST-01 over HTTP on the throwaway mongod: the Global Product register sorts on the server by the
/// whitelisted keys, keeps its order before sorting existed when none is asked for (name ascending), pages a sorted read
/// without repeating or losing a row, and refuses an unknown key with 400. Each test is its own tenant.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    [Fact]
    public async Task The_global_product_register_without_an_order_keeps_its_name_order()
    {
        var bravo = await SeedNamedProductAsync("GP-0001", "Bravo", ProductIdentityLifecycleStatus.Draft);
        var alpha = await SeedNamedProductAsync("GP-0003", "alpha", ProductIdentityLifecycleStatus.Retired);
        var charlie = await SeedNamedProductAsync("GP-0002", "Charlie", ProductIdentityLifecycleStatus.IdentityApproved);

        var page = await ListProductIdsAsync("");

        Assert.Equal([alpha, bravo, charlie], page.Ids);
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task The_global_product_register_sorts_by_each_whitelisted_key_in_both_directions()
    {
        var bravo = await SeedNamedProductAsync("GP-0001", "Bravo", ProductIdentityLifecycleStatus.Draft);
        var alpha = await SeedNamedProductAsync("GP-0003", "Alpha", ProductIdentityLifecycleStatus.Retired);
        var charlie = await SeedNamedProductAsync("GP-0002", "Charlie", ProductIdentityLifecycleStatus.IdentityApproved);

        Assert.Equal([alpha, charlie, bravo], (await ListProductIdsAsync("orderBy=canonicalCode&orderDir=desc")).Ids);
        Assert.Equal([bravo, charlie, alpha], (await ListProductIdsAsync("orderBy=canonicalCode&orderDir=asc")).Ids);
        Assert.Equal([charlie, bravo, alpha], (await ListProductIdsAsync("orderBy=globalProductName&orderDir=desc")).Ids);
        // Draft (1) < IdentityApproved (3) < Retired (4) — the lifecycle order, not the alphabet of its names.
        Assert.Equal([bravo, charlie, alpha], (await ListProductIdsAsync("orderBy=lifecycleStatus&orderDir=asc")).Ids);
        Assert.Equal([alpha, charlie, bravo], (await ListProductIdsAsync("orderBy=lifecycleStatus&orderDir=desc")).Ids);
    }

    [Fact]
    public async Task The_global_product_register_pages_a_sorted_read_without_repeating_or_losing_a_row()
    {
        var created = new List<Guid>();
        foreach (var code in new[] { "GP-0005", "GP-0001", "GP-0004" })
            created.Add(await SeedNamedProductAsync(code, $"Same name {code}", ProductIdentityLifecycleStatus.Draft));

        var first = await ListProductIdsAsync("orderBy=canonicalCode&orderDir=desc&pageNumber=1&pageSize=2");
        var second = await ListProductIdsAsync("orderBy=canonicalCode&orderDir=desc&pageNumber=2&pageSize=2");

        Assert.Equal([created[0], created[2]], first.Ids);
        Assert.Equal([created[1]], second.Ids);
        Assert.Equal(3, second.Total);
    }

    [Theory]
    [InlineData("orderBy=id")]
    [InlineData("orderBy=TenantId&orderDir=asc")]
    [InlineData("orderBy=canonicalCode&orderDir=sideways")]
    public async Task An_unknown_global_product_sort_key_or_direction_is_a_400(string query)
    {
        var response = await host.Client(ProductReaderToken()).GetAsync($"/api/global-products?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string ProductReaderToken() => host.Token(_tenant, _maker, "mdm.global-products.read");

    private async Task<Guid> SeedNamedProductAsync(string code, string name, ProductIdentityLifecycleStatus status)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = _tenant, CanonicalCode = code, GlobalProductName = name,
            GlobalProductNameNormalized = name.ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            LifecycleStatus = status, CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private async Task<(Guid[] Ids, long Total)> ListProductIdsAsync(string query)
    {
        var response = await host.Client(ProductReaderToken()).GetAsync($"/api/global-products?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return (data.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray(),
            data.GetProperty("totalCount").GetInt64());
    }
}
