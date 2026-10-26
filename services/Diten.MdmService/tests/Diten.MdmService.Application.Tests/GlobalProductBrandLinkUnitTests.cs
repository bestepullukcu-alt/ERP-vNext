using System.Text.Json;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU05 slice 1 at handler level: T-4 (the field's absence, null, a value — from the serializer through the
/// handler to the repository call), the edit's evidence, one brand read per page (Karar b, pack §8) and the brand options
/// (T-1). The HTTP round trip on a real mongod is <c>GlobalProductBrandLinkHttpTests</c>.
/// </summary>
public sealed class GlobalProductBrandLinkUnitTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset From2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_serializer_tells_a_missing_brand_field_from_a_null_one_and_from_a_value()
    {
        var brand = Guid.NewGuid();

        var missing = JsonSerializer.Deserialize<ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest>(
            """{"globalProductName":"A","expectedVersion":0}""", Web)!;
        var cleared = JsonSerializer.Deserialize<ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest>(
            """{"globalProductName":"A","expectedVersion":0,"brandId":null}""", Web)!;
        var linked = JsonSerializer.Deserialize<ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest>(
            $$"""{"globalProductName":"A","expectedVersion":0,"brandId":"{{brand:D}}"}""", Web)!;

        Assert.False(missing.BrandIdSpecified);
        Assert.True(cleared.BrandIdSpecified);
        Assert.Null(cleared.BrandId);
        Assert.True(linked.BrandIdSpecified);
        Assert.Equal(brand, linked.BrandId);
        Assert.Null(missing.UnmappedFields);
    }

    [Fact]
    public async Task Without_the_field_the_edit_leaves_the_brand_and_reads_no_brand_for_the_link()
    {
        var current = Guid.NewGuid();
        var (handler, products, brands) = UpdateHandler(current, Brand(current, "CUR"));

        var response = await handler.Handle(new(LifecycleTestData.ProductId,
            new() { GlobalProductName = "Renamed", ExpectedVersion = 0 }, Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(products.LastBrandSpecified);
        Assert.Equal(current, response.Data!.BrandId);
        Assert.Equal("CUR", response.Data.BrandCode);
        Assert.Equal(1, brands.BatchReads); // the answer's code and name only
    }

    [Fact]
    public async Task A_null_brand_removes_it_without_reading_a_brand_to_link()
    {
        var current = Guid.NewGuid();
        var (handler, products, brands) = UpdateHandler(current, Brand(current, "CUR"));

        var response = await handler.Handle(new(LifecycleTestData.ProductId,
            Request("""{"globalProductName":"Same","expectedVersion":0,"brandId":null}"""), Guid.NewGuid()), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(products.LastBrandSpecified);
        Assert.Null(products.LastBrandId);
        Assert.Null(response.Data!.BrandId);
        Assert.Equal(0, brands.BatchReads);
    }

    [Fact]
    public async Task A_new_brand_is_checked_against_the_link_rule_before_the_write()
    {
        var archived = Brand(Guid.NewGuid(), "ARC");
        archived.IsArchived = true;
        var (handler, products, _) = UpdateHandler(null, archived);

        var response = await handler.Handle(new(LifecycleTestData.ProductId,
            Request($$"""{"globalProductName":"Same","expectedVersion":0,"brandId":"{{archived.Id:D}}"}"""), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.StartsWith("brand_archived:", response.Errors![0]);
        Assert.Equal(0, products.UpdateCalls);
    }

    [Fact]
    public async Task A_brand_write_without_a_brand_store_fails_closed()
    {
        var products = new BrandLinkTestProducts(LifecycleTestData.Product(ProductIdentityLifecycleStatus.Draft, 0));
        var handler = new UpdateGlobalProductDraftHandler(products,
            new LifecycleTestActor(LifecycleTestData.Maker, UpdateGlobalProductDraftHandler.Permission), TimeProvider.System,
            new ScopeRolloutRepository(null), new ScopePolicyRepository(), null!,
            new LifecycleScopeTenantContext(LifecycleTestData.TenantId));

        var response = await handler.Handle(new(LifecycleTestData.ProductId,
            Request($$"""{"globalProductName":"Same","expectedVersion":0,"brandId":"{{Guid.NewGuid():D}}"}"""), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(503, response.StatusCode);
        Assert.StartsWith(GlobalProductBrands.BrandStoreUnavailable, response.Errors![0]);
        Assert.Equal(0, products.UpdateCalls);
    }

    [Fact]
    public async Task The_brand_asked_for_is_a_fact_of_the_edit_and_an_edit_without_it_keeps_its_old_evidence()
    {
        var one = Brand(Guid.NewGuid(), "ONE");
        var two = Brand(Guid.NewGuid(), "TWO");
        var operation = Guid.NewGuid();

        async Task<string> EvidenceOf(string body)
        {
            var (handler, products, _) = UpdateHandler(null, one, two);
            Assert.True((await handler.Handle(new(LifecycleTestData.ProductId, Request(body), operation), CancellationToken.None))
                .IsSuccessful);
            return products.LastAuditIntent!.EvidenceHash;
        }

        var withOne = await EvidenceOf($$"""{"globalProductName":"N","expectedVersion":0,"brandId":"{{one.Id:D}}"}""");
        var withOneAgain = await EvidenceOf($$"""{"globalProductName":"N","expectedVersion":0,"brandId":"{{one.Id:D}}"}""");
        var withTwo = await EvidenceOf($$"""{"globalProductName":"N","expectedVersion":0,"brandId":"{{two.Id:D}}"}""");
        var cleared = await EvidenceOf("""{"globalProductName":"N","expectedVersion":0,"brandId":null}""");
        var without = await EvidenceOf("""{"globalProductName":"N","expectedVersion":0}""");

        Assert.Equal(withOne, withOneAgain);
        Assert.Equal(4, new[] { withOne, withTwo, cleared, without }.Distinct().Count());
        // The pre-FU05 evidence: the six facts, nothing appended.
        Assert.Equal(PreBrandEvidence(operation, "N"), without);
    }

    [Fact]
    public async Task A_list_page_reads_its_brands_once()
    {
        var a = Brand(Guid.NewGuid(), "A");
        var b = Brand(Guid.NewGuid(), "B");
        var missing = Guid.NewGuid();
        var page = new[] { a.Id, a.Id, b.Id, (Guid?)null, missing }
            .Select((brandId, index) => new GlobalProduct
            {
                Id = Guid.NewGuid(), TenantId = LifecycleTestData.TenantId, CanonicalCode = $"GP-{index}",
                GlobalProductName = $"P{index}", BrandId = brandId
            }).ToList();
        var brands = new CountingBrands(a, b);
        var handler = new GetGlobalProductsHandler(new BrandLinkTestProducts(null) { Page = page },
            new ScopeRolloutRepository(null), new ScopePolicyRepository(), null!,
            new LifecycleScopeTenantContext(LifecycleTestData.TenantId), brands);

        var response = await handler.Handle(new GetGlobalProductsQuery(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, brands.BatchReads);
        Assert.Equal(0, brands.SingleReads);
        var items = response.Data!.Items;
        Assert.Equal(["A", "A", "B", null, null], items.Select(x => x.BrandCode));
        Assert.Equal(missing, items[4].BrandId);
        Assert.Null(items[4].BrandName);
        Assert.Null(items[3].BrandId);
    }

    [Fact]
    public async Task Brand_options_are_the_linkable_brands_by_code_paged()
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var c = Brand(Guid.NewGuid(), "C");
        var a = Brand(Guid.NewGuid(), "A");
        var b = Brand(Guid.NewGuid(), "B", to: today);
        var draft = Brand(Guid.NewGuid(), "D", status: "draft");
        var inactive = Brand(Guid.NewGuid(), "E", status: "inactive");
        var archived = Brand(Guid.NewGuid(), "F");
        archived.IsArchived = true;
        var expired = Brand(Guid.NewGuid(), "G", to: today.AddDays(-1));
        var future = Brand(Guid.NewGuid(), "H", from: today.AddDays(1));
        var handler = new GetGlobalProductBrandOptionsHandler(
            new CountingBrands(c, a, b, draft, inactive, archived, expired, future),
            new LifecycleScopeTenantContext(LifecycleTestData.TenantId));

        var first = await handler.Handle(new() { PageSize = 2 }, CancellationToken.None);
        var second = await handler.Handle(new() { PageSize = 2, PageNumber = 2 }, CancellationToken.None);
        var searched = await handler.Handle(new() { Search = "brand c" }, CancellationToken.None);

        Assert.Equal(["A", "B"], first.Data!.Items.Select(x => x.BrandCode));
        Assert.Equal(["C"], second.Data!.Items.Select(x => x.BrandCode));
        Assert.Equal(3, first.Data.TotalCount);
        Assert.Equal([c.Id], searched.Data!.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task Brand_options_without_a_tenant_are_400()
    {
        var handler = new GetGlobalProductBrandOptionsHandler(new CountingBrands(), new LifecycleScopeTenantContext(Guid.Empty));

        var response = await handler.Handle(new(), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
    }

    private static (UpdateGlobalProductDraftHandler Handler, BrandLinkTestProducts Products, CountingBrands Brands) UpdateHandler(
        Guid? currentBrand, params Brand[] brands)
    {
        var product = LifecycleTestData.Product(ProductIdentityLifecycleStatus.Draft, 0);
        product.BrandId = currentBrand;
        var products = new BrandLinkTestProducts(product);
        var store = new CountingBrands(brands);
        var handler = new UpdateGlobalProductDraftHandler(products,
            new LifecycleTestActor(LifecycleTestData.Maker, UpdateGlobalProductDraftHandler.Permission), TimeProvider.System,
            new ScopeRolloutRepository(null), new ScopePolicyRepository(), null!,
            new LifecycleScopeTenantContext(LifecycleTestData.TenantId), store);
        return (handler, products, store);
    }

    private static ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest Request(string json) =>
        JsonSerializer.Deserialize<ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest>(json, Web)!;

    private static Brand Brand(Guid id, string code, string status = "active", DateTimeOffset? from = null, DateTimeOffset? to = null) => new()
    {
        Id = id, TenantId = LifecycleTestData.TenantId, BrandCode = code, BrandName = $"Brand {code}", BrandStatus = status,
        EffectiveFrom = from ?? From2025, EffectiveTo = to
    };

    // The draft edit's evidence before MOD-0290-FU05 (length-prefixed UTF-8 facts, SHA-256, hex) — written out here so a
    // change to the no-brand evidence shows as a red test, not as a silently different hash.
    private static string PreBrandEvidence(Guid operation, string name)
    {
        _ = operation;
        var facts = new[]
        {
            LifecycleTestData.TenantId.ToString("D"), LifecycleTestData.ProductId.ToString("D"),
            LifecycleTestData.Maker.ToString("D"), "0", name,
            GlobalProductNameRules.NormalizeDuplicateKey(name)
        };
        using var stream = new MemoryStream();
        foreach (var fact in facts)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(fact);
            var length = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
    }
}

internal sealed class BrandLinkTestProducts(GlobalProduct? product) : IGlobalProductRepository
{
    public IReadOnlyList<GlobalProduct> Page { get; init; } = [];
    public int UpdateCalls { get; private set; }
    public bool LastBrandSpecified { get; private set; }
    public Guid? LastBrandId { get; private set; }
    public LocalAuditIntent? LastAuditIntent { get; private set; }

    public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(product?.Id == id ? product : null);

    public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<GlobalProduct?>(null);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductPage(Page, Page.Count));

    public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductCreateResult(false, null, "NOT_USED"));

    public Task<GlobalProductLifecycleWriteResult> UpdateDraftAsync(Guid id, string globalProductName, string normalizedName,
        bool brandSpecified, Guid? brandId, int expectedVersion, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        LastBrandSpecified = brandSpecified;
        LastBrandId = brandId;
        LastAuditIntent = auditIntent;
        var updated = new GlobalProduct
        {
            Id = id, TenantId = product!.TenantId, CanonicalCode = product.CanonicalCode, GlobalProductName = globalProductName,
            GlobalProductNameNormalized = normalizedName, LifecycleStatus = product.LifecycleStatus, Version = expectedVersion + 1,
            BrandId = brandSpecified ? brandId : product.BrandId
        };
        return Task.FromResult(new GlobalProductLifecycleWriteResult(true, updated));
    }
}

internal sealed class CountingBrands(params Brand[] brands) : IBrandRepository
{
    public int BatchReads { get; private set; }
    public int SingleReads { get; private set; }

    public Task<IReadOnlyList<Brand>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        BatchReads++;
        return Task.FromResult<IReadOnlyList<Brand>>(brands.Where(brand => ids.Contains(brand.Id) && !brand.IsDeleted).ToList());
    }

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SingleReads++;
        return Task.FromResult(brands.SingleOrDefault(brand => brand.Id == id && !brand.IsDeleted));
    }

    public Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Brand>>(brands.Where(brand => !brand.IsDeleted).ToList());

    public Task<Brand> CreateAsync(Brand entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> UpdateAsync(Brand entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<BrandWriteOutcome> CreateWithHistoryAsync(Brand brand, BrandHistoryEntry history, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<BrandWriteOutcome> UpdateEditableFieldsWithHistoryAsync(Brand proposed, int expectedVersion, BrandHistoryEntry history,
        IReadOnlyList<BrandProductExternalReference> added, IReadOnlyList<BrandProductExternalReference> removed,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<BrandWriteOutcome> ArchiveWithHistoryAsync(Brand archived, int expectedVersion, BrandHistoryEntry history,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<BrandHistoryEntry>> GetHistoryAsync(Guid brandId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<Brand?> FindByExternalReferenceAsync(string sourceSystem, string externalId, Guid? excludeId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
