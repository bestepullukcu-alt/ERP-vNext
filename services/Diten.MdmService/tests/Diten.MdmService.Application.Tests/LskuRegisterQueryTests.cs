using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using System.Reflection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuRegisterQueryTests
{
    [Fact]
    public void Query_validators_enforce_bounded_paging_and_search()
    {
        var list = new GetLskusValidator().Validate(new GetLskusQuery
        {
            PageNumber = 0,
            PageSize = 101,
            Search = new string('X', 101),
            LifecycleStatus = (ProductIdentityLifecycleStatus)999
        });
        var options = new GetLskuCreateOptionsValidator().Validate(new GetLskuCreateOptionsQuery
        {
            PageNumber = 0,
            PageSize = 51,
            Search = new string('X', 101)
        });

        Assert.Equal(4, list.Errors.Count);
        Assert.Equal(3, options.Errors.Count);
    }

    [Fact]
    public async Task List_uses_one_lsku_page_and_one_gsku_batch_with_exact_projection()
    {
        var product = Product();
        var revision = Revision(product.Id);
        var gsku = Gsku("GS-001", revision.Id);
        var lskus = new LskuRepositoryStub(
            new Lsku
            {
                Id = Guid.NewGuid(),
                GskuId = gsku.Id,
                CanonicalCode = "LS-002",
                MarketCode = "US",
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                Version = 2,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Lsku
            {
                Id = Guid.NewGuid(),
                GskuId = gsku.Id,
                CanonicalCode = "LS-001",
                MarketCode = "TR",
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow
            });
        var gskus = new GskuRepositoryStub(gsku);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        var handler = new GetLskusHandler(
            lskus,
            gskus,
            new RevisionRepositoryStub(revision),
            new ProductRepositoryStub(product),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(new GetLskusQuery
        {
            PageNumber = 2,
            PageSize = 20,
            Search = " ls- ",
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft
        }, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, lskus.PageCalls);
        Assert.Equal((2, 20, "LS-", (ProductIdentityLifecycleStatus?)ProductIdentityLifecycleStatus.Draft), lskus.LastPageRequest);
        Assert.Equal(1, gskus.BatchCalls);
        Assert.Equal(2, response.Data!.Items.Count);
        Assert.All(response.Data.Items, x => Assert.Equal("GS-001", x.GskuCanonicalCode));
        Assert.Equal(
            ["Id", "CanonicalCode", "GskuId", "GskuCanonicalCode", "MarketCode", "LifecycleStatus", "Version", "CreatedAt", "UpdatedAt", "AvailableActions"],
            typeof(ProductItemSkuMasterModels.LskuListItemDto).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public void Detail_actions_are_server_owned_ordered_and_maker_only()
    {
        var maker = Guid.NewGuid();
        var lsku = new Lsku { LifecycleStatus = ProductIdentityLifecycleStatus.Draft };
        Assert.Equal(["DETAILS", "SUBMIT"], Actions(lsku,
            new Actor(maker, LskuIdentityLifecyclePermissions.Submit)));
        Assert.Equal(["DETAILS"], Actions(lsku, new Actor(maker)));

        lsku.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
        lsku.IdentityWorkflowBinding = new ProductIdentityWorkflowBinding { SubmitterSubjectId = maker };
        Assert.Equal(["DETAILS", "WITHDRAW_APPROVAL"], Actions(lsku,
            new Actor(maker, LskuIdentityLifecyclePermissions.Withdraw)));
        Assert.Equal(["DETAILS"], Actions(lsku,
            new Actor(Guid.NewGuid(), LskuIdentityLifecyclePermissions.Withdraw)));
        Assert.Equal(["DETAILS"], Actions(lsku,
            new Actor(null, LskuIdentityLifecyclePermissions.Withdraw)));

        lsku.LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved;
        Assert.Equal(["DETAILS", "REQUEST_RETIREMENT"], Actions(lsku, new Actor(maker,
            LskuRetirementRequestPermissions.Request)));
        lsku.ActiveLifecycleOperation = new(LskuLifecycleOperationKind.Retirement, Guid.NewGuid(), 0);
        Assert.Equal(["DETAILS"], Actions(lsku, new Actor(maker,
            LskuRetirementRequestPermissions.Request)));
    }

    private static IReadOnlyList<string> Actions(Lsku lsku, IProductIdentityLifecycleActorContext actor) =>
        Assert.IsAssignableFrom<IReadOnlyList<string>>(
            typeof(GetLskuByIdHandler)
                .GetMethod("BuildAvailableActions", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [lsku, actor]));

    private sealed class Actor(Guid? subjectId, params string[] permissions)
        : IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid resolved)
        {
            resolved = subjectId ?? Guid.Empty;
            return subjectId.HasValue;
        }

        public bool HasPermission(string permission) => permissions.Contains(permission, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Missing_detail_returns_non_disclosing_404_without_parent_lookup()
    {
        var gskus = new GskuRepositoryStub();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        var response = await new GetLskuByIdHandler(
                new LskuRepositoryStub(),
                gskus,
                new RevisionRepositoryStub(),
                new ProductRepositoryStub(),
                access.Rollouts,
                access.Policies,
                access.Candidates,
                tenantContext)
            .Handle(new GetLskuByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("LSKU_NOT_FOUND", response.Errors);
        Assert.Equal(0, gskus.GetByIdCalls);
    }

    [Fact]
    public async Task Create_options_are_bounded_batch_joined_and_expose_no_provider_evidence()
    {
        var product = Product();
        var revision = Revision(product.Id);
        var gsku = Gsku("GS-001", revision.Id);
        var gskus = new GskuRepositoryStub(gsku);
        var revisions = new RevisionRepositoryStub(revision);
        var products = new ProductRepositoryStub(product);
        var markets = new MarketResolver(VerifiedMarketEnumerationResult.Success(
        [
            new("US", "United States", 20),
            new("TR", "Türkiye", 10)
        ]));

        var response = await LskuCreateOptionsFacade.GetAsync(
            gskus,
            revisions,
            products,
            markets,
            1,
            20,
            "gs-",
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join(',', response.Errors));
        Assert.Equal((1, 20, "GS-"), gskus.LastReferenceablePageRequest);
        Assert.Equal(1, gskus.ReferenceablePageCalls);
        Assert.Equal(1, revisions.BatchCalls);
        Assert.Equal(1, products.BatchCalls);
        Assert.Equal(1, markets.EnumerationCalls);
        var option = Assert.Single(response.Data!.Gskus);
        Assert.Equal(
            (gsku.Id, "GS-001", product.CanonicalCode, product.GlobalProductName,
                revision.RevisionIdentifier, gsku.PackQuantity, gsku.PackUomCode),
            (option.Id, option.CanonicalCode, option.GlobalProductCanonicalCode, option.GlobalProductName,
                option.RevisionIdentifier, option.PackQuantity, option.PackUomCode));
        Assert.Equal(["TR", "US"], response.Data.Markets.Select(x => x.Code));

        var json = JsonSerializer.Serialize(response.Data);
        Assert.DoesNotContain("CatalogVersion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReferenceTenant", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assignment", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Publication", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(404, 404)]
    [InlineData(409, 409)]
    [InlineData(500, 503)]
    [InlineData(503, 503)]
    [InlineData(504, 504)]
    public async Task Create_options_preserve_exact_provider_failure_classes(
        int providerStatus,
        int publicStatus)
    {
        var response = await LskuCreateOptionsFacade.GetAsync(
            new GskuRepositoryStub(),
            new RevisionRepositoryStub(),
            new ProductRepositoryStub(),
            new MarketResolver(VerifiedMarketEnumerationResult.Fail(providerStatus, "REFERENCE_FAILURE")),
            1,
            20,
            null,
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(publicStatus, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_to_market_enumeration()
    {
        var resolver = new CancellingMarketResolver();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LskuCreateOptionsFacade.GetAsync(
            new GskuRepositoryStub(),
            new RevisionRepositoryStub(),
            new ProductRepositoryStub(),
            resolver,
            1,
            20,
            null,
            cancellation.Token));
        Assert.True(resolver.ObservedCancellation);
    }

    private static GlobalProduct Product() => new()
    {
        Id = Guid.NewGuid(),
        CanonicalCode = "GP-001",
        GlobalProductName = "Product",
        GlobalProductNameNormalized = "PRODUCT",
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft
    };

    private static ProductDefinitionRevision Revision(Guid productId) => new()
    {
        Id = Guid.NewGuid(),
        GlobalProductId = productId,
        RevisionIdentifier = "REV-001"
    };

    private static Gsku Gsku(string code, Guid? revisionId = null) => new()
    {
        Id = Guid.NewGuid(),
        CanonicalCode = code,
        ProductDefinitionRevisionId = revisionId ?? Guid.NewGuid(),
        PackQuantity = 1m,
        PackUomCode = "C62",
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft
    };

    private sealed class LskuRepositoryStub(params Lsku[] items) : ILskuRepository
    {
        public int PageCalls { get; private set; }
        public (int Page, int Size, string? Search, ProductIdentityLifecycleStatus? LifecycleStatus) LastPageRequest { get; private set; }

        public Task<Lsku?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.SingleOrDefault(x => x.Id == id));

        public Task<LskuPage> GetPageAsync(int page, int size, string? search, ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken ct = default)
        {
            PageCalls++;
            LastPageRequest = (page, size, search, lifecycleStatus);
            return Task.FromResult(new LskuPage(items, items.Length));
        }

        public Task<Lsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<Lsku?>(null);
        public Task<Lsku?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Lsku?>(null);
        public Task<Lsku?> GetByIdentityKeyAsync(Guid gskuId, string market, CancellationToken ct = default) =>
            Task.FromResult<Lsku?>(null);
        public Task<LskuCreateResult> CreateDraftAsync(Lsku value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class GskuRepositoryStub(params Gsku[] items) : IGskuRepository
    {
        public int GetByIdCalls { get; private set; }
        public int BatchCalls { get; private set; }
        public int ReferenceablePageCalls { get; private set; }
        public (int Page, int Size, string? Search) LastReferenceablePageRequest { get; private set; }

        public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            GetByIdCalls++;
            return Task.FromResult(items.SingleOrDefault(x => x.Id == id));
        }

        public Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.SingleOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Gsku>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<Gsku>>(items.Where(x => ids.Contains(x.Id)).ToList());
        }

        public Task<GskuPage> GetReferenceablePageAsync(
            int page,
            int size,
            string? search,
            CancellationToken ct = default)
        {
            ReferenceablePageCalls++;
            LastReferenceablePageRequest = (page, size, search);
            return Task.FromResult(new GskuPage(items, items.Length));
        }

        public Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(string search, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<Gsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<Gsku?>(null);
        public Task<GskuCreateResult> CreateDraftAsync(Gsku value, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<GskuUpdateResult> UpdateDraftAsync(Gsku value, int version, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class RevisionRepositoryStub(params ProductDefinitionRevision[] items)
        : IProductDefinitionRevisionRepository
    {
        public int BatchCalls { get; private set; }
        public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ProductDefinitionRevision>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken ct = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<ProductDefinitionRevision>>(
                items.Where(x => ids.Contains(x.Id)).ToList());
        }
        public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<ProductDefinitionRevision?>(null);
        public Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(Guid id, string command, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(ProductDefinitionRevision value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class ProductRepositoryStub(params GlobalProduct[] items) : IGlobalProductRepository
    {
        public int BatchCalls { get; private set; }
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(items.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<GlobalProduct>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken ct = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<GlobalProduct>>(items.Where(x => ids.Contains(x.Id)).ToList());
        }
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GlobalProduct?>(null);
        public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<GlobalProductPage> GetPageAsync(int page, int size, string? search, ProductIdentityLifecycleStatus? status, CancellationToken ct = default) =>
            Task.FromResult(new GlobalProductPage(items, items.Length));
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class MarketResolver(VerifiedMarketEnumerationResult enumeration)
        : IVerifiedMarketReferenceResolver
    {
        public int EnumerationCalls { get; private set; }
        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(VerifiedMarketReferenceResolveResult.Fail(503, "NOT_USED"));
        public Task<VerifiedMarketEnumerationResult> EnumerateActiveAsync(CancellationToken ct = default)
        {
            EnumerationCalls++;
            return Task.FromResult(enumeration);
        }
    }

    private sealed class CancellingMarketResolver : IVerifiedMarketReferenceResolver
    {
        public bool ObservedCancellation { get; private set; }
        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(VerifiedMarketReferenceResolveResult.Fail(503, "NOT_USED"));
        public async Task<VerifiedMarketEnumerationResult> EnumerateActiveAsync(CancellationToken ct = default)
        {
            ObservedCancellation = ct.IsCancellationRequested;
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unreachable");
        }
    }
}
