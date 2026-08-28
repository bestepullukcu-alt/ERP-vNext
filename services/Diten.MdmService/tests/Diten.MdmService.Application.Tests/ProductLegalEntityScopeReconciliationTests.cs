using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeReconciliationTests
{
    [Fact]
    public async Task Ambiguous_create_and_update_return_only_202_reconciliation_required()
    {
        var createFixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        createFixture.Policies.AmbiguousCreate = true;
        var create = await createFixture.Create.Handle(new CreateProductLegalEntityScopePolicyCommand(
            createFixture.Product.Id, Guid.NewGuid(), new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        Assert.Equal(202, create.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_RECONCILIATION_REQUIRED", Assert.Single(create.Errors));

        var updateFixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        await updateFixture.Create.Handle(new(updateFixture.Product.Id, Guid.NewGuid(), new()
        {
            Mode = ProductLegalEntityScopeMode.GroupWide
        }), default);
        updateFixture.Policies.AmbiguousUpdate = true;
        var replace = await updateFixture.Replace.Handle(new(updateFixture.Product.Id, Guid.NewGuid(), new()
        {
            ExpectedVersion = 0, Mode = ProductLegalEntityScopeMode.Scoped,
            LegalEntityIds = [updateFixture.LegalEntity.Id]
        }), default);
        Assert.Equal(202, replace.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_RECONCILIATION_REQUIRED", Assert.Single(replace.Errors));

        var endFixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        await endFixture.Create.Handle(new(endFixture.Product.Id, Guid.NewGuid(), new()
        {
            Mode = ProductLegalEntityScopeMode.GroupWide
        }), default);
        endFixture.Policies.AmbiguousUpdate = true;
        var end = await endFixture.End.Handle(new(endFixture.Product.Id, Guid.NewGuid(), new()
        {
            ExpectedVersion = 0
        }), default);
        Assert.Equal(202, end.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_RECONCILIATION_REQUIRED", Assert.Single(end.Errors));
    }

    [Fact]
    public async Task Definite_version_conflict_is_409_not_202()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        await fixture.Create.Handle(new(fixture.Product.Id, Guid.NewGuid(), new()
        {
            Mode = ProductLegalEntityScopeMode.GroupWide
        }), default);
        fixture.Policies.FailUpdate = true;
        var result = await fixture.Replace.Handle(new(fixture.Product.Id, Guid.NewGuid(), new()
        {
            ExpectedVersion = 0, Mode = ProductLegalEntityScopeMode.Scoped,
            LegalEntityIds = [fixture.LegalEntity.Id]
        }), default);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_CONFLICT", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Bson_budget_failures_are_deterministic_409_not_500()
    {
        var createFixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        createFixture.Policies.ThrowBsonCreate = true;
        var create = await createFixture.Create.Handle(new(
            createFixture.Product.Id, Guid.NewGuid(),
            new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        Assert.Equal(409, create.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED", Assert.Single(create.Errors));

        var updateFixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        await updateFixture.Create.Handle(new(
            updateFixture.Product.Id, Guid.NewGuid(),
            new() { Mode = ProductLegalEntityScopeMode.GroupWide }), default);
        updateFixture.Policies.ThrowBsonUpdate = true;
        var end = await updateFixture.End.Handle(new(
            updateFixture.Product.Id, Guid.NewGuid(),
            new() { ExpectedVersion = 0 }), default);
        Assert.Equal(409, end.StatusCode);
        Assert.Equal("PRODUCT_SCOPE_BSON_LIMIT_EXCEEDED", Assert.Single(end.Errors));
    }

    [Fact]
    public async Task Completeness_counts_more_than_200_products_without_materializing_all_ids()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        fixture.Products.InventoryOverride = new(201, 200, [Guid.NewGuid()]);
        var handler = new GetProductLegalEntityScopeCompletenessHandler(
            fixture.Products, fixture.Policies, fixture.Rollout);

        var result = await handler.Handle(new GetProductLegalEntityScopeCompletenessQuery(), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.Data!.EligibleGlobalProductCount);
        Assert.Equal(200, result.Data.ConfiguredGlobalProductCount);
        Assert.Single(result.Data.MissingGlobalProductIds);
        Assert.False(result.Data.IsComplete);
        Assert.Equal(0, fixture.Policies.ConfiguredReadCount);
    }

    [Fact]
    public async Task Completeness_reports_only_bounded_missing_ids_and_never_changes_rollout()
    {
        var fixture = ScopeCommandFixture.New(ProductLegalEntityScopeRolloutMode.Preparation);
        var missingId = Guid.NewGuid();
        fixture.Products.InventoryOverride = new(2, 1, [missingId]);
        var handler = new GetProductLegalEntityScopeCompletenessHandler(
            fixture.Products, fixture.Policies, fixture.Rollout);

        var result = await handler.Handle(new GetProductLegalEntityScopeCompletenessQuery(), default);

        Assert.True(result.IsSuccessful);
        Assert.False(result.Data!.IsComplete);
        Assert.Equal([missingId], result.Data.MissingGlobalProductIds);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Preparation, fixture.Rollout.State!.Mode);
        Assert.Equal(0, fixture.Policies.ConfiguredReadCount);
    }

    [Fact]
    public async Task Real_mongo_completeness_counts_all_products_but_returns_only_bounded_missing_sample()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var products = scope.Database.GetCollection<GlobalProduct>("mdm_global_products");
        var now = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(1, 201).Select(index => new GlobalProduct
        {
            Id = Guid.NewGuid(),
            TenantId = scope.TenantId,
            CanonicalCode = $"GP-C-{index:D3}",
            GlobalProductName = $"Completeness {index:D3}",
            GlobalProductNameNormalized = $"COMPLETENESS {index:D3}",
            CodeReservationId = Guid.NewGuid(),
            LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
            CreatedAt = now.AddMinutes(-10),
            Version = 0
        }).ToArray();
        await products.InsertManyAsync(rows);
        try
        {
            var current = ProductLegalEntityScopePolicy.Create(
                scope.TenantId, rows[0].Id, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), now.AddMinutes(-5));
            current.Id = Guid.NewGuid();
            var ended = ProductLegalEntityScopePolicy.Create(
                scope.TenantId, rows[1].Id, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), now.AddMinutes(-5));
            ended.Id = Guid.NewGuid();
            ended.EndCurrent(0, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-1));
            await scope.PolicyCollection.InsertManyAsync([current, ended]);

            var repository = new GlobalProductRepository(
                scope.Database,
                new ProductScopeMongoScope.TenantContext(scope.TenantId));
            var result = await repository.GetProductLegalEntityScopeCompletenessInventoryAsync(now, 200);

            Assert.Equal(201, result.EligibleGlobalProductCount);
            Assert.Equal(1, result.ConfiguredGlobalProductCount);
            Assert.Equal(200, result.MissingGlobalProductIds.Count);
            Assert.DoesNotContain(rows[0].Id, result.MissingGlobalProductIds);
            Assert.Contains(rows[1].Id, result.MissingGlobalProductIds);
        }
        finally
        {
            await products.DeleteManyAsync(item => item.TenantId == scope.TenantId);
        }
    }
}
