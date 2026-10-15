using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 (B17): the register sorts only by whitelisted stored fields — anything else is a 400 from
/// the production validator, never a silent default — and the Global Product picker learns from MDM whether another
/// page exists. The order itself is measured against a real mongod in Diten.MdmService.Api.Tests (GskuListSortHttpTests).
/// </summary>
public sealed class GskuListSortAndPickerPagingTests
{
    [Theory]
    [InlineData("canonicalCode", "asc", GskuListSortField.CanonicalCode, false)]
    [InlineData("packQuantity", "desc", GskuListSortField.PackQuantity, true)]
    [InlineData("lifecycleStatus", "DESC", GskuListSortField.LifecycleStatus, true)]
    [InlineData("PackQuantity", null, GskuListSortField.PackQuantity, false)]
    public void A_whitelisted_order_is_valid_and_resolves_to_its_stored_field(
        string orderBy, string? orderDir, GskuListSortField field, bool descending)
    {
        var query = new GetGskusQuery { OrderBy = orderBy, OrderDir = orderDir };

        Assert.True(new GetGskusValidator().Validate(query).IsValid);
        Assert.Equal(new GskuListSort(field, descending), query.ResolveSort());
    }

    [Theory]
    [InlineData("globalProductName")]
    [InlineData("revisionIdentifier")]
    [InlineData("id")]
    [InlineData("TenantId")]
    [InlineData("packQuantity;drop")]
    public void An_unknown_sort_key_is_refused(string orderBy)
    {
        var result = new GetGskusValidator().Validate(new GetGskusQuery { OrderBy = orderBy, OrderDir = "asc" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "GSKU_LIST_ORDER_BY_INVALID");
    }

    [Theory]
    [InlineData("up")]
    [InlineData("ascending")]
    [InlineData("-1")]
    public void An_unknown_sort_direction_is_refused(string orderDir)
    {
        var result = new GetGskusValidator().Validate(new GetGskusQuery { OrderBy = "canonicalCode", OrderDir = orderDir });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "GSKU_LIST_ORDER_DIR_INVALID");
    }

    [Fact]
    public void No_order_is_the_registers_default_canonical_code_ascending()
    {
        var query = new GetGskusQuery();

        Assert.True(new GetGskusValidator().Validate(query).IsValid);
        Assert.Equal(GskuListSort.Default, query.ResolveSort());
    }

    [Theory]
    [InlineData(1, 20, 21, true)]
    [InlineData(1, 20, 20, false)]
    [InlineData(2, 20, 41, true)]
    [InlineData(2, 20, 40, false)]
    [InlineData(1, 20, 0, false)]
    public async Task The_picker_is_told_whether_another_page_of_referenceable_products_exists(
        int pageNumber, int pageSize, long total, bool hasMore)
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product",
            GlobalProductNameNormalized = "PRODUCT", LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var products = new PagedProducts(product, total);

        var response = await GskuCreateOptionsFacade.GetAsync(
            products,
            new Resolver(VerifiedGskuUomEnumerationResult.Success([new("C62", "One", 10, 0)])),
            pageNumber, pageSize, null, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(hasMore, response.Data!.HasMore);
        // The page asked for is the page read: paging reaches the referenceable-only read unchanged.
        Assert.Equal((pageNumber, pageSize), products.LastPage);
    }

    private sealed class Resolver(VerifiedGskuUomEnumerationResult result) : IVerifiedGskuReferenceResolver
    {
        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(string pack, string uom, CancellationToken ct = default) =>
            Task.FromResult(VerifiedGskuReferenceResolveResult.Fail(503, "NOT_USED"));
        public Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class PagedProducts(GlobalProduct product, long total) : IGlobalProductRepository
    {
        public (int Page, int Size) LastPage { get; private set; }
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GlobalProduct?>(product.Id == id ? product : null);
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GlobalProduct?>(null);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<GlobalProductPage> GetPageAsync(int page, int size, string? search, ProductIdentityLifecycleStatus? status, CancellationToken ct = default) =>
            throw new NotSupportedException("The picker reads referenceable products only.");
        public Task<GlobalProductPage> GetReferenceablePageAsync(int page, int size, string? search, CancellationToken ct = default)
        {
            LastPage = (page, size);
            return Task.FromResult(new GlobalProductPage(total == 0 ? [] : [product], total));
        }
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct value, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
