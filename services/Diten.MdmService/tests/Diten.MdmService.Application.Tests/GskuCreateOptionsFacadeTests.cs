using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuCreateOptionsFacadeTests
{
    [Fact]
    public async Task Returns_only_bounded_product_and_uom_fields_in_provider_order()
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product",
            GlobalProductNameNormalized = "PRODUCT", LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        };
        var response = await GskuCreateOptionsFacade.GetAsync(
            new ProductRepository(product),
            new Resolver(VerifiedGskuUomEnumerationResult.Success(
            [
                new("KGM", "Kilogram", 30, 3),
                new("C62", "One", 10, 0)
            ])),
            1, 20, null, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var option = Assert.Single(response.Data!.GlobalProducts);
        Assert.Equal((product.Id, "GP-1", "Product"), (option.Id, option.CanonicalCode, option.GlobalProductName));
        Assert.Equal(["C62", "KGM"], response.Data.Uoms.Select(x => x.Code));
        Assert.Equal([0, 3], response.Data.Uoms.Select(x => x.MaximumDecimalPrecision));
    }

    [Theory]
    [InlineData(401, 503)]
    [InlineData(403, 503)]
    [InlineData(503, 503)]
    [InlineData(504, 504)]
    [InlineData(409, 409)]
    public async Task Provider_failures_are_fail_closed(int providerStatus, int publicStatus)
    {
        var response = await GskuCreateOptionsFacade.GetAsync(
            new ProductRepository(),
            new Resolver(VerifiedGskuUomEnumerationResult.Fail(providerStatus, "REFERENCE_FAILURE")),
            1, 20, null, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(publicStatus, response.StatusCode);
        Assert.Null(response.Data);
    }

    private sealed class Resolver(VerifiedGskuUomEnumerationResult result) : IVerifiedGskuReferenceResolver
    {
        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(string pack, string uom, CancellationToken ct = default) =>
            Task.FromResult(VerifiedGskuReferenceResolveResult.Fail(503, "NOT_USED"));
        public Task<VerifiedGskuUomEnumerationResult> EnumerateUomsAsync(CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class ProductRepository(params GlobalProduct[] products) : IGlobalProductRepository
    {
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(products.SingleOrDefault(x => x.Id == id));
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<GlobalProduct?>(null);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<GlobalProductPage> GetPageAsync(int page, int size, string? search, ProductIdentityLifecycleStatus? status, CancellationToken ct = default) =>
            Task.FromResult(new GlobalProductPage(products, products.Length));
        public Task<GlobalProductPage> GetReferenceablePageAsync(int page, int size, string? search, CancellationToken ct = default) =>
            Task.FromResult(new GlobalProductPage(products, products.Length));
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct value, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
