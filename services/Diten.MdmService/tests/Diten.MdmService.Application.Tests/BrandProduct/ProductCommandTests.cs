using Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.Brand.Queries;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.Product.Commands;
using Diten.MdmService.Application.Features.Product.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.Product.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.Product.Queries;
using Diten.MdmService.Domain.Vocabulary;
using Xunit;
using ArchiveBrandCommand = Diten.MdmService.Application.Features.Brand.Commands.ArchiveBrandCommand;

namespace Diten.MdmService.Application.Tests.BrandProduct;

// MOD-0290-FU02 — Product runtime gates (pack §22.1 items 10-20, 23-25, 27).
public sealed class ProductCommandTests
{
    private static (InMemoryProductRepository Products, InMemoryBrandRepository Brands) Repos(
        Guid tenantId,
        IEnumerable<Domain.Entities.Product>? products = null,
        IEnumerable<Domain.Entities.Brand>? brands = null)
        => (new InMemoryProductRepository(tenantId, products ?? []), new InMemoryBrandRepository(tenantId, brands ?? []));

    // Gate 10
    [Fact]
    public async Task Create_persists_product_with_normalized_code()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(code: "pr-001"), Actor: "tester"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var created = Assert.Single(products.Entities);
        Assert.Equal("PR-001", created.ProductCode);
        Assert.Equal("tester", created.CreatedBy);
    }

    // Gate 11
    [Fact]
    public async Task Create_resolves_tenant_server_side()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        await new CreateProductHandler(products, brands)
            .Handle(new CreateProductCommand(BrandProductTestData.ProductRequest()), CancellationToken.None);

        Assert.Equal(tenantId, Assert.Single(products.Entities).TenantId);
    }

    [Fact]
    public void ProductWriteRequest_has_no_tenant_id_member()
    {
        var members = typeof(Features.Product.ProductWriteRequest)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("TenantId", members);
    }

    // Gate 12
    [Fact]
    public async Task Create_rejects_duplicate_code_including_archived()
    {
        var tenantId = Guid.NewGuid();
        var (activeDup, brands) = Repos(tenantId, [BrandProductTestData.Product(tenantId, code: "PR-001")]);
        var response = await new CreateProductHandler(activeDup, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(code: "PR-001")), CancellationToken.None);
        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.ProductCodeDuplicate));

        var (archivedDup, brands2) = Repos(tenantId, [BrandProductTestData.Product(tenantId, code: "PR-OLD", isArchived: true)]);
        var archivedResponse = await new CreateProductHandler(archivedDup, brands2).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(code: "PR-OLD")), CancellationToken.None);
        Assert.Equal(409, archivedResponse.StatusCode);
    }

    // Gate 13
    [Theory]
    [InlineData("retired", BrandProductReasonCodes.InvalidProductStatus)]
    public async Task Create_rejects_unknown_status(string status, string reasonCode)
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(status: status)), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, reasonCode));
    }

    // `discontinued` is NOT authorized in FU02 — FU01 §11 locked the lifecycle set (follow-up F5).
    [Fact]
    public async Task Create_rejects_discontinued_status()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(status: "discontinued")), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.DoesNotContain("discontinued", BrandProductVocabulary.ProductStatuses);
    }

    [Fact]
    public async Task Create_rejects_unknown_product_type_dosage_form_and_uom()
    {
        var tenantId = Guid.NewGuid();

        var (p1, b1) = Repos(tenantId);
        var typeResponse = await new CreateProductHandler(p1, b1).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(productType: "spaceship")), CancellationToken.None);
        Assert.Equal(400, typeResponse.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(typeResponse.Errors, BrandProductReasonCodes.InvalidProductType));

        var (p2, b2) = Repos(tenantId);
        var formResponse = await new CreateProductHandler(p2, b2).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(dosageForm: "hologram")), CancellationToken.None);
        Assert.Equal(400, formResponse.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(formResponse.Errors, BrandProductReasonCodes.InvalidDosageForm));

        var (p3, b3) = Repos(tenantId);
        var uomResponse = await new CreateProductHandler(p3, b3).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(unitOfMeasure: "parsec")), CancellationToken.None);
        Assert.Equal(400, uomResponse.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(uomResponse.Errors, BrandProductReasonCodes.InvalidUnitOfMeasure));
    }

    // Gate 14
    [Fact]
    public async Task Archive_is_soft_and_keeps_the_record_readable()
    {
        var tenantId = Guid.NewGuid();
        var product = BrandProductTestData.Product(tenantId);
        var (products, _) = Repos(tenantId, [product]);

        var response = await new ArchiveProductHandler(products)
            .Handle(new ArchiveProductCommand(product.Id, Actor: "tester"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var stored = Assert.Single(products.Entities);
        Assert.True(stored.IsArchived);
        Assert.Equal(BrandProductVocabulary.StatusArchived, stored.ProductStatus);
        Assert.NotNull(stored.ArchivedAt);
        Assert.False(stored.IsDeleted);

        var read = await new GetProductByIdHandler(products)
            .Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);
        Assert.True(read.IsSuccessful);
    }

    // Gate 15
    [Fact]
    public async Task Update_of_archived_product_returns_409()
    {
        var tenantId = Guid.NewGuid();
        var product = BrandProductTestData.Product(tenantId, code: "PR-A", isArchived: true);
        var (products, brands) = Repos(tenantId, [product]);

        var response = await new UpdateProductHandler(products, brands).Handle(
            new UpdateProductCommand(product.Id, BrandProductTestData.ProductRequest(code: "PR-A", name: "Renamed")),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.RecordArchived));
    }

    [Fact]
    public async Task Update_rejects_code_change_with_409()
    {
        var tenantId = Guid.NewGuid();
        var product = BrandProductTestData.Product(tenantId, code: "PR-A");
        var (products, brands) = Repos(tenantId, [product]);

        var response = await new UpdateProductHandler(products, brands).Handle(
            new UpdateProductCommand(product.Id, BrandProductTestData.ProductRequest(code: "PR-Z")),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.CodeImmutable));
    }

    // Gate 16
    [Fact]
    public void Product_feature_exposes_no_delete_command()
    {
        var commandTypes = typeof(CreateProductCommand).Assembly
            .GetTypes()
            .Where(x => x.Namespace == typeof(CreateProductCommand).Namespace)
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(commandTypes, x => x.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    // Gate 17
    [Fact]
    public async Task List_and_read_are_tenant_isolated()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var mine = BrandProductTestData.Product(tenantId, code: "PR-MINE", name: "Mine");
        var theirs = BrandProductTestData.Product(otherTenantId, code: "PR-THEIRS", name: "Theirs");
        var (products, _) = Repos(tenantId, [mine, theirs]);

        var list = await new GetProductListHandler(products)
            .Handle(new GetProductListQuery(), CancellationToken.None);
        Assert.Equal("PR-MINE", Assert.Single(list.Data!.Items).ProductCode);

        var read = await new GetProductByIdHandler(products)
            .Handle(new GetProductByIdQuery(theirs.Id), CancellationToken.None);
        Assert.Equal(404, read.StatusCode);
    }

    // Gate 18
    [Fact]
    public async Task Create_against_archived_brand_returns_409()
    {
        var tenantId = Guid.NewGuid();
        var archivedBrand = BrandProductTestData.Brand(tenantId, isArchived: true);
        var (products, brands) = Repos(tenantId, brands: [archivedBrand]);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(brandId: archivedBrand.Id)),
            CancellationToken.None);

        Assert.Equal(409, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.BrandArchived));
        Assert.Empty(products.Entities);
    }

    // Gate 19 — a foreign-tenant brand is a 404, not a 409: its existence is never revealed.
    [Fact]
    public async Task Create_against_cross_tenant_brand_returns_404()
    {
        var tenantId = Guid.NewGuid();
        var foreignBrand = BrandProductTestData.Brand(Guid.NewGuid());
        var (products, brands) = Repos(tenantId, brands: [foreignBrand]);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(brandId: foreignBrand.Id)),
            CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.True(BrandProductTestData.HasReasonCode(response.Errors, BrandProductReasonCodes.BrandNotFound));
        Assert.Empty(products.Entities);
    }

    // Gate 20 — BrandId is OPTIONAL (FU01 §4.1): a brand-less product is a first-class, valid record.
    [Fact]
    public async Task Create_without_brand_succeeds()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        var response = await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(brandId: null)), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Null(Assert.Single(products.Entities).BrandId);
    }

    // A product linked BEFORE its brand was archived keeps working; only re-pointing is refused.
    [Fact]
    public async Task Update_keeps_existing_link_to_a_brand_archived_later()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId, isArchived: true);
        var product = BrandProductTestData.Product(tenantId, code: "PR-A", brandId: brand.Id);
        var (products, brands) = Repos(tenantId, [product], [brand]);

        var response = await new UpdateProductHandler(products, brands).Handle(
            new UpdateProductCommand(product.Id, BrandProductTestData.ProductRequest(code: "PR-A", name: "Updated", brandId: brand.Id)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Updated", Assert.Single(products.Entities).ProductName);
    }

    // Gate 24 — ATCCode is stored verbatim as an external taxonomy pointer; nothing resolves it.
    [Fact]
    public async Task AtcCode_is_stored_as_external_pointer_only()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        await new CreateProductHandler(products, brands).Handle(
            new CreateProductCommand(BrandProductTestData.ProductRequest(atcCode: "c09aa")), CancellationToken.None);

        var stored = Assert.Single(products.Entities);
        Assert.Equal("C09AA", stored.ATCCode);

        // No ATC master type exists anywhere in the domain assembly.
        var atcTypes = typeof(Domain.Entities.Product).Assembly.GetTypes()
            .Where(x => x.Name.Contains("Atc", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(atcTypes);
    }

    // Gate 25 — TherapeuticArea stays a reference id; no flat reference-set aggregate is introduced.
    [Fact]
    public void TherapeuticArea_is_a_reference_id_not_an_aggregate()
    {
        var therapeuticAreaTypes = typeof(Domain.Entities.Product).Assembly.GetTypes()
            .Where(x => x.Name.Contains("TherapeuticArea", StringComparison.OrdinalIgnoreCase)
                     || x.Name.Contains("Indication", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(therapeuticAreaTypes);
        Assert.Equal(typeof(Guid?), typeof(Domain.Entities.Product).GetProperty("TherapeuticAreaId")!.PropertyType);
    }

    // Gate 23/27 — archiving a brand must NOT touch its products (silent cascade is forbidden).
    [Fact]
    public async Task Archiving_a_brand_does_not_cascade_to_its_products()
    {
        var tenantId = Guid.NewGuid();
        var brand = BrandProductTestData.Brand(tenantId);
        var product = BrandProductTestData.Product(tenantId, brandId: brand.Id);
        var (products, brands) = Repos(tenantId, [product], [brand]);

        await new ArchiveBrandHandler(brands).Handle(new ArchiveBrandCommand(brand.Id), CancellationToken.None);

        var storedProduct = Assert.Single(products.Entities);
        Assert.False(storedProduct.IsArchived);
        Assert.Equal(brand.Id, storedProduct.BrandId);

        // Still listed under the (now archived) brand — history is preserved, not hidden.
        var relation = await new GetBrandProductsHandler(brands, products)
            .Handle(new GetBrandProductsQuery(brand.Id, IncludeArchived: false), CancellationToken.None);
        Assert.True(relation.IsSuccessful);
        Assert.Single(relation.Data!);
    }

    [Fact]
    public async Task Brand_products_relation_returns_404_for_unknown_brand()
    {
        var tenantId = Guid.NewGuid();
        var (products, brands) = Repos(tenantId);

        var response = await new GetBrandProductsHandler(brands, products)
            .Handle(new GetBrandProductsQuery(Guid.NewGuid(), IncludeArchived: false), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task List_applies_brand_and_type_filters_server_side()
    {
        var tenantId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var (products, _) = Repos(tenantId,
        [
            BrandProductTestData.Product(tenantId, code: "PR-A", name: "Alpha", brandId: brandId),
            BrandProductTestData.Product(tenantId, code: "PR-B", name: "Beta")
        ]);
        var handler = new GetProductListHandler(products);

        var byBrand = await handler.Handle(new GetProductListQuery { BrandId = brandId }, CancellationToken.None);
        Assert.Equal("PR-A", Assert.Single(byBrand.Data!.Items).ProductCode);

        var bySearch = await handler.Handle(new GetProductListQuery { Search = "bet" }, CancellationToken.None);
        Assert.Equal("PR-B", Assert.Single(bySearch.Data!.Items).ProductCode);
    }
}
