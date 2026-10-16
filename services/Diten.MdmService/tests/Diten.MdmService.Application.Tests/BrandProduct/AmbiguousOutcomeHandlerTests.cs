using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.Product.Commands;
using Diten.MdmService.Application.Features.Product.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using Xunit;

namespace Diten.MdmService.Application.Tests.BrandProduct;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX4 (F2) — each of the six write handlers (brand and product create / update / archive) answers a
/// store's Ambiguous outcome (commit result unknown) with the named 503 write_outcome_unknown — never a 500, never a "saved".
/// One test per arm, so a removed arm turns its own test red.
/// </summary>
public sealed class AmbiguousOutcomeHandlerTests
{
    [Fact]
    public async Task Brand_create_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var brands = new InMemoryBrandRepository(Guid.NewGuid(), []) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new CreateBrandHandler(brands).Handle(
            new CreateBrandCommand(BrandProductTestData.BrandRequest(code: "BR-AMB")), CancellationToken.None));
    }

    [Fact]
    public async Task Brand_update_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, code: "BR-AMB");
        var brands = new InMemoryBrandRepository(tenantId, [brand]) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new UpdateBrandHandler(brands).Handle(
            new UpdateBrandCommand(brand.Id, BrandProductTestData.BrandRequest(code: "BR-AMB", name: "Renamed")), CancellationToken.None));
    }

    [Fact]
    public async Task Brand_archive_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, code: "BR-AMB");
        var brands = new InMemoryBrandRepository(tenantId, [brand]) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new ArchiveBrandHandler(brands).Handle(new ArchiveBrandCommand(brand.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Product_create_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var tenantId = Guid.NewGuid();
        var products = new InMemoryProductRepository(tenantId, []) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new CreateProductHandler(products, new InMemoryBrandRepository(tenantId, [])).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(code: "PR-AMB")), CancellationToken.None));
    }

    [Fact]
    public async Task Product_update_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var tenantId = Guid.NewGuid();
        var product = BrandProductTestData.Product(tenantId, code: "PR-AMB");
        var products = new InMemoryProductRepository(tenantId, [product]) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new UpdateProductHandler(products, new InMemoryBrandRepository(tenantId, [])).Handle(
            new UpdateProductCommand(product.Id, BrandProductTestData.ProductRequest(code: "PR-AMB", name: "Renamed")), CancellationToken.None));
    }

    [Fact]
    public async Task Product_archive_answers_an_unknown_commit_with_503_write_outcome_unknown()
    {
        var tenantId = Guid.NewGuid();
        var product = BrandProductTestData.Product(tenantId, code: "PR-AMB");
        var products = new InMemoryProductRepository(tenantId, [product]) { ForcedOutcome = BrandWriteOutcome.Ambiguous };
        AssertOutcomeUnknown(await new ArchiveProductHandler(products).Handle(new ArchiveProductCommand(product.Id), CancellationToken.None));
    }

    private static void AssertOutcomeUnknown<T>(Response<T> response)
    {
        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors!, BrandProductReasonCodes.WriteOutcomeUnknown),
            string.Join(" | ", response.Errors ?? []));
    }
}
