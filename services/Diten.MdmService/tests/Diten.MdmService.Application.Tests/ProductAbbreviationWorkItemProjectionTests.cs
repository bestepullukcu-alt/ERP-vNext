using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationWorkItemProjectionTests
{
    [Fact]
    public void Optional_absent_containers_are_omitted_and_required_shape_remains_exact()
    {
        var item = new ProductAbbreviationWorkItemProjection(
            "workItem", Guid.NewGuid().ToString("D"), "approval", "approval", "notApplicable",
            "notApplicable", "Pending", "notApplicable", "notApplicable", "notApplicable", "fresh", "inline",
            ProductAbbreviationWorkItemLabel.Display("ABC · GP-1"),
            new("REQUESTED", ProductAbbreviationWorkItemLabel.Resource("WorkAggregation_NativeStatus_WaitingApproval")),
            new("mdm-product-abbreviations", "1.0", "productAbbreviationAllocationRequest", Guid.NewGuid().ToString("D"), "/MDM/ProductAbbreviationRegister"),
            "mdm-product-abbreviations", [], [], new("version", "0"),
            Requester: new(Guid.NewGuid().ToString("D"), IsCurrentUser: true));

        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("0", root.GetProperty("concurrency").GetProperty("token").GetString());
        Assert.False(root.TryGetProperty("waitingContext", out _));
        Assert.False(root.TryGetProperty("checklist", out _));
        Assert.False(root.TryGetProperty("priority", out _));
        Assert.Equal(0, root.GetProperty("workItemCapabilities").GetArrayLength());
    }

    [Fact]
    public async Task Requester_gets_cancel_while_distinct_actor_gets_approve_and_reject_with_one_batch_join()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid().ToString("D");
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(),
            CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product"
        };
        var entry = Entry(product.Id, requesterId);
        var batchCalls = 0;
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync)
                => Paged([entry], args),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Batch(),
            _ => throw new InvalidOperationException(method.Name)
        });

        async Task<IReadOnlyList<GlobalProduct>> Batch()
        {
            batchCalls++;
            await Task.Yield();
            return [product];
        }

        var requesterResult = await Handler(register, products, tenantId, requesterId).Handle(
            new GetProductAbbreviationWorkItemsQuery("self"),
            CancellationToken.None);
        var requesterItem = Assert.Single(requesterResult.Data!.Items);
        Assert.Equal(["cancel"], requesterItem.Actions.Select(x => x.Code).ToArray());
        Assert.Equal("cancel", requesterItem.PrimaryActionCode);
        Assert.Equal("0", requesterItem.Concurrency.Token);

        var approverResult = await Handler(register, products, tenantId, Guid.NewGuid().ToString("D")).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"),
            CancellationToken.None);
        var approverItem = Assert.Single(approverResult.Data!.Items);
        Assert.Equal(["approve", "reject"], approverItem.Actions.Select(x => x.Code).ToArray());
        Assert.Equal("approve", approverItem.PrimaryActionCode);
        Assert.Equal(["reject"], approverItem.OverflowActionCodes);
        Assert.Equal(2, batchCalls);
    }

    [Fact]
    public async Task More_pending_work_than_one_page_is_projected_up_to_the_limit_instead_of_failing_everybody()
    {
        // FIX1 S8 — 101 pending items used to be a 503 for the whole tenant. They are read page by page now.
        var tenantId = Guid.NewGuid();
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var entries = Enumerable.Range(0, 150).Select(_ => Entry(product.Id, Guid.NewGuid().ToString("D"))).ToArray();
        var pageCalls = 0;
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync)
                => Counted(Paged(entries, args)),
            _ => throw new InvalidOperationException(method.Name)
        });
        Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> Counted(Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> page)
        {
            pageCalls++;
            return page;
        }
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, Guid.NewGuid().ToString("D")).Handle(
            new GetProductAbbreviationWorkItemsQuery("self"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ProductAbbreviationWorkItemContract.MaximumItems, result.Data!.Items.Count);
        Assert.Equal(entries.Take(100).Select(x => x.Id.ToString("D")), result.Data.Items.Select(x => x.Id));
        Assert.Equal(1, pageCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Projection_distinguishes_correction_and_retirement_and_does_not_invent_retirement_cancel(bool correctedRetiringProduct)
    {
        var tenantId = Guid.NewGuid();
        var actor = "retirement-maker";
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var former = Entry(product.Id, "former-maker");
        former.LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE;
        var correction = Entry(product.Id, "correction-maker");
        correction.ReplacesEntryId = former.Id;
        var retirement = Entry(product.Id, "original-maker");
        retirement.LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE;
        retirement.RetirementRequestId = "retirement-request";
        retirement.RetirementRequestedByCanonicalSubjectId = actor;
        retirement.RetirementRequestedAtUtc = DateTimeOffset.UtcNow;
        if (correctedRetiringProduct) retirement.ReplacesEntryId = Guid.NewGuid();
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync)
                => Paged([correction, retirement], args),
            nameof(IProductAbbreviationRegisterRepository.GetByIdsAsync)
                => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>(
                    ((IReadOnlyCollection<Guid>)args![0]!).Contains(former.Id) ? [former] : []),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync)
                => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, actor).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Items.Count);
        var correctionItem = Assert.Single(result.Data.Items, x => x.Source.ObjectType == ProductAbbreviationWorkItemContract.CorrectionObjectType);
        Assert.Equal(["approve", "reject"], correctionItem.Actions.Select(x => x.Code).ToArray());
        var retirementItem = Assert.Single(result.Data.Items, x => x.Source.ObjectType == ProductAbbreviationWorkItemContract.RetirementObjectType);
        Assert.Empty(retirementItem.Actions);
        Assert.Null(retirementItem.PrimaryActionCode);
    }

    [Fact]
    public async Task A_visible_item_on_the_second_page_is_found_when_the_first_page_holds_none()
    {
        // FIX1 S8 — the pages are read until the reader's answer is filled or the work runs out.
        var tenantId = Guid.NewGuid();
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var broken = Enumerable.Range(0, GetProductAbbreviationWorkItemsHandler.PageSize).Select(_ =>
        {
            var entry = Entry(product.Id, "maker");
            entry.NormalizedAbbreviation = "";
            return entry;
        }).ToList();
        var healthy = Entry(product.Id, "maker");
        broken.Add(healthy);
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync) => Paged(broken, args),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, "checker").Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.Equal(healthy.Id.ToString("D"), Assert.Single(result.Data!.Items).Id);
    }

    [Fact]
    public async Task A_page_costs_the_same_reads_however_many_corrections_and_products_it_holds()
    {
        // FIX2 A3 — the codes corrections replace and the products are read once per page; nothing is read per item.
        var tenantId = Guid.NewGuid();
        var counts = new Dictionary<string, int>();
        void Count(string name) => counts[name] = counts.GetValueOrDefault(name) + 1;
        var products = Enumerable.Range(0, 60).Select(i => new GlobalProduct
            { Id = Guid.NewGuid(), CanonicalCode = $"GP-{i}", GlobalProductName = "P" }).ToList();
        var formers = products.Select(p => { var e = Entry(p.Id, "maker"); e.LifecycleStatus = ProductAbbreviationLifecycleStatus.ACTIVE; return e; }).ToList();
        var corrections = formers.Select(f => { var e = Entry(f.GlobalProductId, "maker"); e.ReplacesEntryId = f.Id; return e; }).ToList();
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) =>
        {
            Count("register." + method.Name);
            return method.Name switch
            {
                nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync) => Paged(corrections, args),
                nameof(IProductAbbreviationRegisterRepository.GetByIdsAsync)
                    => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>(formers),
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        var productRepository = Stub<IGlobalProductRepository>((method, _) =>
        {
            Count("products." + method.Name);
            return method.Name == nameof(IGlobalProductRepository.GetByIdsAsync)
                ? Task.FromResult<IReadOnlyList<GlobalProduct>>(products)
                : throw new InvalidOperationException(method.Name);
        });

        var result = await Handler(register, productRepository, tenantId, "checker").Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.Equal(60, result.Data!.Items.Count);
        Assert.Equal(1, counts["register." + nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync)]);
        Assert.Equal(1, counts["register." + nameof(IProductAbbreviationRegisterRepository.GetByIdsAsync)]);
        Assert.Equal(1, counts["products." + nameof(IGlobalProductRepository.GetByIdsAsync)]);
        Assert.False(counts.ContainsKey("register." + nameof(IProductAbbreviationRegisterRepository.GetByIdAsync)));
    }

    [Fact]
    public async Task Stopping_at_the_limit_with_work_left_is_logged_and_the_oldest_items_are_the_ones_shown()
    {
        var tenantId = Guid.NewGuid();
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var entries = Enumerable.Range(0, 150).Select(i =>
        {
            var entry = Entry(product.Id, "maker");
            entry.PendingSinceUtcTicks = 1_000 + i;
            return entry;
        }).ToArray();
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync) => Paged(entries, args),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });
        var logger = new CapturingLogger();

        var result = await Handler(register, products, tenantId, "checker", logger).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.Equal(entries.Take(100).Select(x => x.Id.ToString("D")), result.Data!.Items.Select(x => x.Id));
        Assert.Contains(logger.Lines, line => line.Contains("later pending items are not on this answer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_orphaned_correction_is_offered_reject_but_never_approve()
    {
        // FIX1 B3(c) + Proj:76 — the code this correction would replace is RETIRED (same product): approving is
        // impossible, rejecting is the cleanup. The answer still succeeds for the whole tenant.
        var tenantId = Guid.NewGuid();
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var former = Entry(product.Id, "former-maker");
        former.LifecycleStatus = ProductAbbreviationLifecycleStatus.RETIRED;
        var orphan = Entry(product.Id, "correction-maker");
        orphan.ReplacesEntryId = former.Id;
        var healthy = Entry(product.Id, "other-maker");
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync) => Paged([orphan, healthy], args),
            nameof(IProductAbbreviationRegisterRepository.GetByIdsAsync)
                => Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>(
                    ((IReadOnlyCollection<Guid>)args![0]!).Contains(former.Id) ? [former] : []),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, "checker").Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var orphanItem = Assert.Single(result.Data!.Items, x => x.Id == orphan.Id.ToString("D"));
        Assert.Equal(["reject"], orphanItem.Actions.Select(x => x.Code).ToArray());
        var healthyItem = Assert.Single(result.Data.Items, x => x.Id == healthy.Id.ToString("D"));
        Assert.Equal(["approve", "reject"], healthyItem.Actions.Select(x => x.Code).ToArray());
    }

    [Fact]
    public async Task An_inconsistent_row_and_a_row_without_its_product_are_left_out_and_the_others_are_projected()
    {
        // FIX1 B3(b) — one bad row is one row: it used to be a 503 for every user of the tenant.
        var tenantId = Guid.NewGuid();
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-1", GlobalProductName = "Product" };
        var broken = Entry(product.Id, "maker");
        broken.NormalizedAbbreviation = "";
        var productless = Entry(Guid.NewGuid(), "maker");
        var healthy = Entry(product.Id, "maker");
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync)
                => Paged([broken, productless, healthy], args),
            _ => throw new InvalidOperationException(method.Name)
        });
        var products = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<GlobalProduct>>([product]),
            _ => throw new InvalidOperationException(method.Name)
        });

        var result = await Handler(register, products, tenantId, "checker").Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(healthy.Id.ToString("D"), Assert.Single(result.Data!.Items).Id);
    }

    [Fact]
    public async Task In_an_enforced_rollout_the_pages_policies_are_read_once_and_decide_who_sees_what()
    {
        // FIX3 item 7 — the enforced branch (PRJ: rollout Enforced → one batch read of the page's policies, evaluated by
        // the unchanged guard): a product scoped to the reader's company is shown, one scoped elsewhere is not, and no
        // policy is read product by product.
        var (tenant, reader, elsewhere) = (new TenantContext(), Guid.NewGuid(), Guid.NewGuid());
        tenant.SetTenant(Guid.NewGuid());
        var shown = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-SHOWN", GlobalProductName = "Shown" };
        var hidden = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-HIDDEN", GlobalProductName = "Hidden" };
        var reads = new Dictionary<string, int>();
        var policies = Stub<IProductLegalEntityScopePolicyRepository>((method, args) =>
        {
            reads[method.Name] = reads.GetValueOrDefault(method.Name) + 1;
            return method.Name switch
            {
                nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdsAsync)
                    => Task.FromResult<IReadOnlyList<ProductLegalEntityScopePolicy>>(
                        [Policy(tenant.TenantId, shown.Id, reader), Policy(tenant.TenantId, hidden.Id, elsewhere)]),
                _ => throw new InvalidOperationException(method.Name)
            };
        });
        var entries = new[] { Entry(shown.Id, "maker"), Entry(hidden.Id, "maker") };

        var result = await EnforcedHandler(entries, [shown, hidden], tenant, reader, policies).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful, result.ReasonCode);
        Assert.Equal(entries[0].Id.ToString("D"), Assert.Single(result.Data!.Items).Id);
        Assert.Equal(1, reads[nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdsAsync)]);
        Assert.False(reads.ContainsKey(nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdAsync)));
    }

    [Fact]
    public async Task One_unreadable_policy_hides_its_own_product_and_the_rest_of_the_page_is_still_shown()
    {
        // FIX3 item 7 — before, the batch read sat outside the guard's per-product isolation: one policy document that
        // could not be read failed the whole read, and the tenant's entire ABB work disappeared from the Task Center.
        var (tenant, reader) = (new TenantContext(), Guid.NewGuid());
        tenant.SetTenant(Guid.NewGuid());
        var readable = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-OK", GlobalProductName = "Readable" };
        var broken = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-BAD", GlobalProductName = "Broken" };
        var policies = Stub<IProductLegalEntityScopePolicyRepository>((method, args) => method.Name switch
        {
            nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdsAsync)
                => Task.FromException<IReadOnlyList<ProductLegalEntityScopePolicy>>(new FormatException("bad policy document")),
            nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdAsync) when (Guid)args![0]! == broken.Id
                => Task.FromException<ProductLegalEntityScopePolicy?>(new FormatException("bad policy document")),
            nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdAsync)
                => Task.FromResult<ProductLegalEntityScopePolicy?>(Policy(tenant.TenantId, readable.Id, reader)),
            _ => throw new InvalidOperationException(method.Name)
        });
        var entries = new[] { Entry(broken.Id, "maker"), Entry(readable.Id, "maker") };
        var logger = new CapturingLogger();

        var result = await EnforcedHandler(entries, [readable, broken], tenant, reader, policies, logger).Handle(
            new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None);

        Assert.True(result.IsSuccessful, result.ReasonCode);
        Assert.Equal(entries[1].Id.ToString("D"), Assert.Single(result.Data!.Items).Id);
        Assert.Contains(logger.Lines, line => line.Contains("work_item_policy_unreadable") && line.Contains(broken.Id.ToString()));
    }

    [Fact]
    public async Task A_database_failure_on_the_batch_policy_read_goes_up_instead_of_an_empty_board()
    {
        // FIX4 item 2 — the policy collection being down is not "every product's policy is unreadable": hiding every
        // product would answer a successful EMPTY board. It goes up, and the Task Center says the source did not answer.
        var (tenant, reader) = (new TenantContext(), Guid.NewGuid());
        tenant.SetTenant(Guid.NewGuid());
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-DOWN", GlobalProductName = "Down" };
        var policies = Stub<IProductLegalEntityScopePolicyRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdsAsync)
                => Task.FromException<IReadOnlyList<ProductLegalEntityScopePolicy>>(new TimeoutException("policies down")),
            _ => Task.FromResult<ProductLegalEntityScopePolicy?>(Policy(tenant.TenantId, product.Id, reader))
        });

        await Assert.ThrowsAsync<TimeoutException>(() => EnforcedHandler([Entry(product.Id, "maker")], [product], tenant,
            reader, policies).Handle(new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None));
    }

    [Fact]
    public async Task A_database_failure_on_one_products_policy_read_goes_up_and_is_not_hidden_as_bad_data()
    {
        // FIX4 item 2 — the product-by-product fallback (after a DATA failure of the batch) hides a product only for its
        // own unreadable document; the database failing while it reads goes up as well.
        var (tenant, reader) = (new TenantContext(), Guid.NewGuid());
        tenant.SetTenant(Guid.NewGuid());
        var product = new GlobalProduct { Id = Guid.NewGuid(), CanonicalCode = "GP-ONE", GlobalProductName = "One" };
        var policies = Stub<IProductLegalEntityScopePolicyRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopePolicyRepository.GetByGlobalProductIdsAsync)
                => Task.FromException<IReadOnlyList<ProductLegalEntityScopePolicy>>(new FormatException("bad policy document")),
            _ => Task.FromException<ProductLegalEntityScopePolicy?>(new TimeoutException("policies down"))
        });

        await Assert.ThrowsAsync<TimeoutException>(() => EnforcedHandler([Entry(product.Id, "maker")], [product], tenant,
            reader, policies).Handle(new GetProductAbbreviationWorkItemsQuery("team"), CancellationToken.None));
    }

    private static ProductLegalEntityScopePolicy Policy(Guid tenantId, Guid productId, Guid legalEntityId)
        => ProductLegalEntityScopePolicy.Create(tenantId, productId, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
            [legalEntityId], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));

    private static GetProductAbbreviationWorkItemsHandler EnforcedHandler(
        IReadOnlyList<ProductAbbreviationRegisterEntry> entries,
        IReadOnlyList<GlobalProduct> products,
        TenantContext tenant,
        Guid readerLegalEntity,
        IProductLegalEntityScopePolicyRepository policies,
        Microsoft.Extensions.Logging.ILogger<GetProductAbbreviationWorkItemsHandler>? logger = null)
    {
        var register = Stub<IProductAbbreviationRegisterRepository>((method, args) => method.Name switch
        {
            nameof(IProductAbbreviationRegisterRepository.GetPendingWorkItemsPageAsync) => Paged(entries, args),
            _ => throw new InvalidOperationException(method.Name)
        });
        var productRepository = Stub<IGlobalProductRepository>((method, _) => method.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdsAsync) => Task.FromResult(products),
            _ => throw new InvalidOperationException(method.Name)
        });
        var access = ProductLegalEntityScopeTestFixture.Enforced(tenant, [readerLegalEntity], policies);
        return new(register, productRepository, new Actor(tenant.TenantId, Guid.NewGuid().ToString("D")),
            access.Rollouts, access.Policies, access.Candidates, tenant, logger);
    }

    private static GetProductAbbreviationWorkItemsHandler Handler(
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository products,
        Guid tenantId,
        string subject,
        Microsoft.Extensions.Logging.ILogger<GetProductAbbreviationWorkItemsHandler>? logger = null)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var actor = new Actor(tenantId, subject);
        var rollout = Stub<IProductLegalEntityScopeRolloutStateRepository>((method, _) => method.Name switch
        {
            nameof(IProductLegalEntityScopeRolloutStateRepository.GetAsync)
                => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null),
            _ => throw new InvalidOperationException(method.Name)
        });
        var candidates = new ProductLegalEntityScopeCandidateFacade(
            Stub<ITrustedLegalEntityScopeProvider>((method, _) => throw new InvalidOperationException(method.Name)),
            Stub<ILegalEntityRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            tenant,
            actor);
        return new(
            register,
            products,
            actor,
            rollout,
            Stub<IProductLegalEntityScopePolicyRepository>((method, _) => throw new InvalidOperationException(method.Name)),
            candidates,
            tenant,
            logger);
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<GetProductAbbreviationWorkItemsHandler>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }

    /// <summary>The repository's paging contract over a list: the entries after <c>args[0]</c>, at most <c>args[1]</c>.</summary>
    internal static Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> Paged(
        IReadOnlyList<ProductAbbreviationRegisterEntry> entries,
        object?[]? args)
    {
        var after = (ProductAbbreviationPendingCursor?)args![0];
        var limit = (int)args[1]!;
        var start = after is null ? 0 : entries.Select(x => x.Id).ToList().IndexOf(after.Id) + 1;
        return Task.FromResult<IReadOnlyList<ProductAbbreviationRegisterEntry>>(entries.Skip(start).Take(limit).ToArray());
    }

    private static ProductAbbreviationRegisterEntry Entry(Guid productId, string requester)
        => new()
        {
            Id = Guid.NewGuid(),
            GlobalProductId = productId,
            NormalizedAbbreviation = "ABC",
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestedByCanonicalSubjectId = requester,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            Version = 0
        };

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> implementation) where T : class
    {
        var value = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)value).Implementation = implementation;
        return value;
    }

    private class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Implementation { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Implementation(targetMethod!, args);
    }

    private sealed record Actor(Guid Tenant, string Subject)
        : IProductAbbreviationActorContext, IProductIdentityActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions { get; } = new HashSet<string>(StringComparer.Ordinal)
        {
            ProductAbbreviationPermissions.Read,
            ProductAbbreviationPermissions.Approve,
            ProductAbbreviationPermissions.Reject,
            ProductAbbreviationPermissions.Cancel
        };
        public string CorrelationId => "wc-projection-test";
        public string ActorId => Subject;
    }
}
