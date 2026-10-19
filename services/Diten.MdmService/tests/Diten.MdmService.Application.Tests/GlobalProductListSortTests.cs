using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-GP-LIST-01: the Global Product register sorts only by whitelisted stored fields — anything else is a 400 from
/// the production validator, never a silent default — and no order is the order the register had before sorting existed
/// (name ascending). The order itself is measured against a real mongod in Diten.MdmService.Api.Tests
/// (GlobalProductListSortHttpTests).
/// </summary>
public sealed class GlobalProductListSortTests
{
    [Theory]
    [InlineData("globalProductName", "asc", GlobalProductListSortField.GlobalProductName, false)]
    [InlineData("canonicalCode", "desc", GlobalProductListSortField.CanonicalCode, true)]
    [InlineData("lifecycleStatus", "DESC", GlobalProductListSortField.LifecycleStatus, true)]
    [InlineData("CanonicalCode", null, GlobalProductListSortField.CanonicalCode, false)]
    public void A_whitelisted_order_is_valid_and_resolves_to_its_stored_field(
        string orderBy, string? orderDir, GlobalProductListSortField field, bool descending)
    {
        var query = new GetGlobalProductsQuery { OrderBy = orderBy, OrderDir = orderDir };

        Assert.True(new GetGlobalProductsValidator().Validate(query).IsValid);
        Assert.Equal(new GlobalProductListSort(field, descending), query.ResolveSort());
    }

    [Theory]
    [InlineData("id")]
    [InlineData("TenantId")]
    [InlineData("globalProductNameNormalized")]
    [InlineData("createdAt")]
    [InlineData("canonicalCode;drop")]
    public void An_unknown_sort_key_is_refused(string orderBy)
    {
        var result = new GetGlobalProductsValidator().Validate(new GetGlobalProductsQuery { OrderBy = orderBy, OrderDir = "asc" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "GLOBAL_PRODUCT_LIST_ORDER_BY_INVALID");
    }

    [Theory]
    [InlineData("up")]
    [InlineData("ascending")]
    [InlineData("-1")]
    public void An_unknown_sort_direction_is_refused(string orderDir)
    {
        var result = new GetGlobalProductsValidator().Validate(new GetGlobalProductsQuery { OrderBy = "canonicalCode", OrderDir = orderDir });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "GLOBAL_PRODUCT_LIST_ORDER_DIR_INVALID");
    }

    [Fact]
    public void No_order_is_the_registers_order_before_sorting_existed_name_ascending()
    {
        var query = new GetGlobalProductsQuery();

        Assert.True(new GetGlobalProductsValidator().Validate(query).IsValid);
        Assert.Equal(GlobalProductListSort.Default, query.ResolveSort());
        Assert.Equal(new GlobalProductListSort(GlobalProductListSortField.GlobalProductName, false), GlobalProductListSort.Default);
    }
}
