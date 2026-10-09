using System.Reflection;
using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Authorization;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductApiMongoTests
{
    [Theory]
    [InlineData(null, "GLOBAL_PRODUCT_NAME_REQUIRED")]
    [InlineData("", "GLOBAL_PRODUCT_NAME_REQUIRED")]
    [InlineData("   ", "GLOBAL_PRODUCT_NAME_REQUIRED")]
    public async Task Invalid_name_is_rejected_before_reservation_or_product_write(string? name, string errorCode)
    {
        await using var scope = await MongoScope.CreateAsync();
        var request = ValidCreateRequest(name);
        var result = new CreateGlobalProductDraftValidator().Validate(new CreateGlobalProductDraftCommand(request));

        Assert.Contains(result.Errors, x => x.ErrorMessage == errorCode);
        Assert.Equal(0, await scope.Database.GetCollection<CodeReservation>("mdm_code_reservations").CountDocumentsAsync(FilterDefinition<CodeReservation>.Empty));
        Assert.Equal(0, await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").CountDocumentsAsync(FilterDefinition<GlobalProduct>.Empty));
    }

    [Fact]
    public async Task Name_longer_than_200_unicode_scalars_is_rejected_without_write()
    {
        await using var scope = await MongoScope.CreateAsync();
        var request = ValidCreateRequest(new string('x', 201));
        var result = new CreateGlobalProductDraftValidator().Validate(new CreateGlobalProductDraftCommand(request));

        Assert.Contains(result.Errors, x => x.ErrorMessage == "GLOBAL_PRODUCT_NAME_LENGTH_INVALID");
        Assert.Equal(0, await scope.Database.GetCollection<CodeReservation>("mdm_code_reservations").CountDocumentsAsync(FilterDefinition<CodeReservation>.Empty));
        Assert.Equal(0, await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").CountDocumentsAsync(FilterDefinition<GlobalProduct>.Empty));
    }

    [Fact]
    public void Reservation_contract_validates_name_before_allocation_and_rejects_entity_type_override()
    {
        var missingName = new ReserveCanonicalCodeCommand(new()
        {
            GlobalProductName = " ",
            IdempotencyKey = "reserve"
        });
        var entityTypeOverride = JsonSerializer.Deserialize<ProductItemSkuMasterModels.ReserveGlobalProductCodeRequest>(
            "{\"globalProductName\":\"Product\",\"idempotencyKey\":\"reserve\",\"entityType\":\"Gsku\"}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var validator = new ReserveCanonicalCodeValidator();

        Assert.Contains(validator.Validate(missingName).Errors, x => x.ErrorMessage == "GLOBAL_PRODUCT_NAME_REQUIRED");
        Assert.Contains(
            validator.Validate(new ReserveCanonicalCodeCommand(entityTypeOverride)).Errors,
            x => x.ErrorMessage == "ENTITY_TYPE_CLIENT_INPUT_FORBIDDEN");
    }

    [Fact]
    public async Task Unicode_visible_name_is_trimmed_but_otherwise_preserved()
    {
        await using var scope = await MongoScope.CreateAsync();
        var response = await CreateAsync(scope, scope.TenantA, "  İlaç 核心 Продукт  ", "unicode");
        var stored = await scope.Products(scope.TenantA).GetByIdAsync(response.Data!.GlobalProductId);

        Assert.True(response.IsSuccessful);
        Assert.Equal("İlaç 核心 Продукт", response.Data.GlobalProductName);
        Assert.Equal("İlaç 核心 Продукт", stored!.GlobalProductName);
        Assert.Equal(GlobalProductNameRules.NormalizeDuplicateKey("İlaç 核心 Продукт"), stored.GlobalProductNameNormalized);
    }

    [Fact]
    public async Task Normalized_duplicate_is_rejected_before_second_reservation_is_consumed()
    {
        await using var scope = await MongoScope.CreateAsync();
        var composed = "Caf\u00e9 Product";
        var decomposedUpper = "CAFE\u0301 PRODUCT";
        Assert.True((await CreateAsync(scope, scope.TenantA, composed, "first")).IsSuccessful);
        var reservations = scope.Reservations(scope.TenantA);
        var secondReservation = await reservations.ReserveAsync(
            CodeBearingEntityType.GlobalProduct, "second-reserve", "actor", "corr");
        var second = await CreateWithReservationAsync(
            scope, scope.TenantA, decomposedUpper, "second-create", secondReservation);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains("GLOBAL_PRODUCT_NAME_DUPLICATE", second.Errors);
        var unchanged = await reservations.GetByIdAsync(secondReservation.Id);
        Assert.Equal(CodeReservationState.Reserved, unchanged!.ReservationState);
        Assert.Equal(0, unchanged.Version);
    }

    [Fact]
    public async Task Soft_deleted_name_is_not_reusable_but_same_name_is_allowed_in_another_tenant()
    {
        await using var scope = await MongoScope.CreateAsync();
        var first = await CreateAsync(scope, scope.TenantA, "Cystolerin", "tenant-a");
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            Builders<GlobalProduct>.Filter.Eq(x => x.Id, first.Data!.GlobalProductId),
            Builders<GlobalProduct>.Update
                .Set(x => x.IsDeleted, true)
                .Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

        var reused = await CreateAsync(scope, scope.TenantA, "cYSTOLERIN", "tenant-a-reuse");
        var otherTenant = await CreateAsync(scope, scope.TenantB, "Cystolerin", "tenant-b");

        Assert.False(reused.IsSuccessful);
        Assert.Contains("GLOBAL_PRODUCT_NAME_DUPLICATE", reused.Errors);
        Assert.True(otherTenant.IsSuccessful);
    }

    [Fact]
    public async Task Concurrent_duplicate_is_stabilized_and_real_unique_index_exists()
    {
        await using var scope = await MongoScope.CreateAsync();
        var reservations = scope.Reservations(scope.TenantA);
        var firstReservation = await reservations.ReserveAsync(CodeBearingEntityType.GlobalProduct, "race-r1", "actor", "corr");
        var secondReservation = await reservations.ReserveAsync(CodeBearingEntityType.GlobalProduct, "race-r2", "actor", "corr");

        var attempts = await Task.WhenAll(
            CreateWithReservationAsync(scope, scope.TenantA, "Race Product", "race-c1", firstReservation),
            CreateWithReservationAsync(scope, scope.TenantA, "RACE PRODUCT", "race-c2", secondReservation));

        Assert.Single(attempts, x => x.IsSuccessful);
        Assert.Single(attempts, x => !x.IsSuccessful && x.Errors.Contains("GLOBAL_PRODUCT_NAME_DUPLICATE"));
        using var cursor = await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var index = Assert.Single(indexes, x => x["name"] == "ux_mdm_global_products_tenant_normalized_name");
        Assert.True(index["unique"].AsBoolean);
        Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name);
        Assert.Equal("GlobalProductNameNormalized", index["key"].AsBsonDocument.GetElement(1).Name);
    }

    [Theory]
    [InlineData("tenantId", "TENANT_ID_CLIENT_INPUT_FORBIDDEN")]
    [InlineData("canonicalCode", "CANONICAL_CODE_ASSIGNMENT_FORBIDDEN")]
    [InlineData("globalProductNameNormalized", "NORMALIZED_NAME_CLIENT_INPUT_FORBIDDEN")]
    [InlineData("entityType", "ENTITY_TYPE_CLIENT_INPUT_FORBIDDEN")]
    [InlineData("surprise", "UNKNOWN_WRITE_FIELD_FORBIDDEN")]
    public void Create_contract_rejects_client_owned_and_unknown_fields(string field, string errorCode)
    {
        var json = $$"""
            {"globalProductName":"Product","reservationId":"{{Guid.NewGuid()}}","expectedReservationVersion":0,"idempotencyKey":"cmd","{{field}}":"override"}
            """;
        var request = JsonSerializer.Deserialize<ProductItemSkuMasterModels.CreateGlobalProductDraftRequest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = new CreateGlobalProductDraftValidator().Validate(new CreateGlobalProductDraftCommand(request));

        Assert.Contains(result.Errors, x => x.ErrorMessage == errorCode);
    }

    [Fact]
    public async Task List_detail_and_selector_are_tenant_scoped_paged_searchable_and_soft_delete_aware()
    {
        await using var scope = await MongoScope.CreateAsync();
        var alpha = await CreateAsync(scope, scope.TenantA, "Alpha", "alpha");
        var cystolerin = await CreateAsync(scope, scope.TenantA, "Cystolerin", "cystolerin");
        var deleted = await CreateAsync(scope, scope.TenantA, "Deleted", "deleted");
        await CreateAsync(scope, scope.TenantB, "Tenant B Product", "tenant-b-product");
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            Builders<GlobalProduct>.Filter.Eq(x => x.Id, deleted.Data!.GlobalProductId),
            Builders<GlobalProduct>.Update.Set(x => x.IsDeleted, true));

        var list = await scope.ListHandler(scope.TenantA).Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 1 }, CancellationToken.None);
        var search = await scope.ListHandler(scope.TenantA).Handle(
            new GetGlobalProductsQuery { Search = "cysto", PageNumber = 1, PageSize = 20 }, CancellationToken.None);
        var suffixSearch = await scope.ListHandler(scope.TenantA).Handle(
            new GetGlobalProductsQuery { Search = "stolerin", PageNumber = 1, PageSize = 20 }, CancellationToken.None);
        var codeSearch = await scope.ListHandler(scope.TenantA).Handle(
            new GetGlobalProductsQuery
            {
                Search = cystolerin.Data!.CanonicalCode,
                PageNumber = 1,
                PageSize = 20
            },
            CancellationToken.None);
        var detail = await scope.DetailHandler(scope.TenantA).Handle(
            new GetGlobalProductByIdQuery(cystolerin.Data!.GlobalProductId), CancellationToken.None);
        var selector = await scope.SelectorHandler(scope.TenantA).Handle(
            new GetGlobalProductSelectorQuery { PageNumber = 1, PageSize = 20 }, CancellationToken.None);

        Assert.Equal(2, list.Data!.TotalCount);
        Assert.Single(list.Data.Items);
        Assert.Equal(alpha.Data!.GlobalProductId, list.Data.Items[0].Id);
        Assert.Single(search.Data!.Items);
        Assert.Equal(cystolerin.Data.GlobalProductId, search.Data.Items[0].Id);
        Assert.Empty(suffixSearch.Data!.Items);
        Assert.Equal(0, suffixSearch.Data.TotalCount);
        Assert.Single(codeSearch.Data!.Items);
        Assert.Equal(cystolerin.Data.GlobalProductId, codeSearch.Data.Items[0].Id);
        Assert.Equal("Cystolerin", detail.Data!.GlobalProductName);
        Assert.Equal(2, selector.Data!.TotalCount);
        Assert.Equal(3, typeof(ProductItemSkuMasterModels.GlobalProductSelectorDto).GetProperties().Length);
        Assert.All(selector.Data.Items, x => Assert.DoesNotContain("Tenant B", x.GlobalProductName));
    }

    [Fact]
    public async Task Missing_deleted_and_cross_tenant_detail_share_non_disclosing_404()
    {
        await using var scope = await MongoScope.CreateAsync();
        var active = await CreateAsync(scope, scope.TenantA, "Hidden Product", "hidden");
        var handlerA = scope.DetailHandler(scope.TenantA);
        var handlerB = scope.DetailHandler(scope.TenantB);
        var missing = await handlerA.Handle(new GetGlobalProductByIdQuery(Guid.NewGuid()), CancellationToken.None);
        var crossTenant = await handlerB.Handle(new GetGlobalProductByIdQuery(active.Data!.GlobalProductId), CancellationToken.None);
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            Builders<GlobalProduct>.Filter.Eq(x => x.Id, active.Data.GlobalProductId),
            Builders<GlobalProduct>.Update.Set(x => x.IsDeleted, true));
        var deleted = await handlerA.Handle(new GetGlobalProductByIdQuery(active.Data.GlobalProductId), CancellationToken.None);

        foreach (var response in new[] { missing, crossTenant, deleted })
        {
            Assert.Equal(404, response.StatusCode);
            Assert.Equal(new[] { "GLOBAL_PRODUCT_NOT_FOUND" }, response.Errors);
        }
    }

    [Fact]
    public async Task Enforced_scope_paging_filters_before_skip_and_count_and_rejects_malformed_policy()
    {
        await using var scope = await MongoScope.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var legalEntityA = Guid.NewGuid();
        var legalEntityB = Guid.NewGuid();
        var products = new[]
        {
            Product(scope.TenantA, "Alpha"),
            Product(scope.TenantA, "Beta"),
            Product(scope.TenantA, "Gamma"),
            Product(scope.TenantA, "Historical"),
            Product(scope.TenantA, "Malformed"),
            Product(scope.TenantA, "Overlapping"),
            Product(scope.TenantA, "Wrong Types")
        };
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertManyAsync(products);

        var policies = new[]
        {
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[0].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [legalEntityA], Guid.NewGuid(), now),
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[1].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [legalEntityB], Guid.NewGuid(), now),
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[2].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.GroupWide,
                [], Guid.NewGuid(), now),
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[3].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.GroupWide,
                [], Guid.NewGuid(), now.AddMinutes(-2)),
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[4].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [legalEntityA], Guid.NewGuid(), now),
            ProductLegalEntityScopePolicy.Create(
                scope.TenantA, products[5].Id, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [legalEntityA], Guid.NewGuid(), now)
        };
        policies[3].ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            now.AddMinutes(-1));
        policies[3].ScopePeriods[0].ActorId = Guid.Empty;
        policies[4].ScopePeriods[0].LegalEntityIds.Add(legalEntityA);
        policies[5].ScopePeriods.Add(new ProductLegalEntityScopePeriod
        {
            PeriodId = Guid.NewGuid(),
            Mode = ProductLegalEntityScopeMode.Scoped,
            LegalEntityIds = [legalEntityA],
            EffectiveFromUtc = now,
            CommandId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            CreatedAtUtc = now
        });
        await scope.Database.GetCollection<ProductLegalEntityScopePolicy>(
                "mdm_product_legal_entity_scope_policies")
            .InsertManyAsync(policies);
        var wrongTypePolicy = ProductLegalEntityScopePolicy.Create(
            scope.TenantA,
            products[6].Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            now);
        var wrongTypeDocument = wrongTypePolicy.ToBsonDocument();
        wrongTypeDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)]
            .AsBsonArray[0]
            .AsBsonDocument[nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc)] = "not-a-date";
        await scope.Database.GetCollection<BsonDocument>(
                "mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(wrongTypeDocument);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantA,
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await scope.Database.GetCollection<ProductLegalEntityScopeRolloutState>(
                "mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);

        var context = scope.Context(scope.TenantA);
        var provider = new EchoTrustedScopeProvider([legalEntityA]);
        var candidates = new ProductLegalEntityScopeCandidateFacade(
            provider,
            new InMemoryLegalEntityRepository(scope.TenantA,
            [
                new LegalEntity
                {
                    Id = legalEntityA,
                    TenantId = scope.TenantA,
                    Code = "LE-A",
                    LegalName = "Legal Entity A",
                    OperationalStatus = LegalEntityOperationalStatus.Active
                }
            ]),
            context,
            new ScopeActorContext());
        var rolloutRepository = new ProductLegalEntityScopeRolloutStateRepository(scope.Database, context);
        var policyRepository = new ProductLegalEntityScopePolicyRepository(scope.Database, context);
        var handler = new GetGlobalProductsHandler(
            scope.Products(scope.TenantA),
            rolloutRepository,
            policyRepository,
            candidates,
            context);
        var detailHandler = new GetGlobalProductByIdHandler(
            scope.Products(scope.TenantA),
            rolloutRepository,
            policyRepository,
            candidates,
            context);

        var firstPage = await handler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 1 },
            CancellationToken.None);
        var secondPage = await handler.Handle(
            new GetGlobalProductsQuery { PageNumber = 2, PageSize = 1 },
            CancellationToken.None);
        var prefixSearch = await handler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 20, Search = "alp" },
            CancellationToken.None);
        var suffixSearch = await handler.Handle(
            new GetGlobalProductsQuery { PageNumber = 1, PageSize = 20, Search = "pha" },
            CancellationToken.None);
        var wrongTypeDetail = await detailHandler.Handle(
            new GetGlobalProductByIdQuery(products[6].Id),
            CancellationToken.None);

        Assert.True(firstPage.IsSuccessful);
        Assert.True(secondPage.IsSuccessful);
        Assert.Equal(2, firstPage.Data!.TotalCount);
        Assert.Equal(2, secondPage.Data!.TotalCount);
        Assert.Equal(products[0].Id, Assert.Single(firstPage.Data.Items).Id);
        Assert.Equal(products[2].Id, Assert.Single(secondPage.Data.Items).Id);
        Assert.Equal(products[0].Id, Assert.Single(prefixSearch.Data!.Items).Id);
        Assert.Empty(suffixSearch.Data!.Items);
        Assert.False(wrongTypeDetail.IsSuccessful);
        Assert.Equal(404, wrongTypeDetail.StatusCode);
        Assert.Equal(new[] { "GLOBAL_PRODUCT_NOT_FOUND" }, wrongTypeDetail.Errors);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Completeness_inventory_counts_only_full_valid_current_policies_with_canonical_bson_dates()
    {
        await using var scope = await MongoScope.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var products = new[]
        {
            Product(scope.TenantA, "Configured"),
            Product(scope.TenantA, "Future"),
            Product(scope.TenantA, "Malformed Date"),
            Product(scope.TenantA, "Missing")
        };
        await scope.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertManyAsync(products);

        var configured = ProductLegalEntityScopePolicy.Create(
            scope.TenantA,
            products[0].Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            now);
        var future = ProductLegalEntityScopePolicy.Create(
            scope.TenantA,
            products[1].Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            now);
        future.ScopePeriods[0].EffectiveFromUtc = now.AddHours(1);
        future.ScopePeriods[0].CreatedAtUtc = now.AddHours(1);
        var malformed = ProductLegalEntityScopePolicy.Create(
            scope.TenantA,
            products[2].Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide,
            [],
            Guid.NewGuid(),
            now);
        var malformedDocument = malformed.ToBsonDocument();
        malformedDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)]
            .AsBsonArray[0]
            .AsBsonDocument[nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc)] = "not-a-date";

        await scope.Database.GetCollection<ProductLegalEntityScopePolicy>(
                "mdm_product_legal_entity_scope_policies")
            .InsertManyAsync([configured, future]);
        await scope.Database.GetCollection<BsonDocument>(
                "mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(malformedDocument);

        var result = await scope.Products(scope.TenantA)
            .GetProductLegalEntityScopeCompletenessInventoryAsync(now, 20);

        Assert.Equal(4, result.EligibleGlobalProductCount);
        Assert.Equal(1, result.ConfiguredGlobalProductCount);
        Assert.Equal(3, result.MissingGlobalProductIds.Count);
        Assert.All(products.Skip(1), product => Assert.Contains(product.Id, result.MissingGlobalProductIds));
    }

    [Theory]
    [InlineData(nameof(GlobalProductsController.GetAll), "mdm.global-products.read")]
    [InlineData(nameof(GlobalProductsController.GetSelector), "mdm.global-products.read")]
    [InlineData(nameof(GlobalProductsController.GetById), "mdm.global-products.read")]
    [InlineData(nameof(GlobalProductsController.ReserveCode), "mdm.global-products.create")]
    [InlineData(nameof(GlobalProductsController.CreateDraft), "mdm.global-products.create")]
    [InlineData(nameof(GlobalProductsController.UpdateDraft), "mdm.global-products.update")]
    [InlineData(nameof(GlobalProductsController.SubmitIdentity), "mdm.global-products.submit")]
    [InlineData(nameof(GlobalProductsController.WithdrawIdentityApproval), "mdm.global-products.withdraw")]
    [InlineData(nameof(GlobalProductsController.RetireIdentity), "mdm.global-products.retire")]
    [InlineData(nameof(GlobalProductsController.RequestCorrection), "mdm.global-products.request-correction")]
    [InlineData(nameof(GlobalProductsController.RequestRetirement), "mdm.global-products.request-retirement")]
    public void Endpoints_fail_closed_on_named_permissions(string methodName, string permission)
    {
        var attribute = typeof(GlobalProductsController).GetMethod(methodName)!.GetCustomAttribute<HasPermissionAttribute>();
        Assert.Equal($"Permission:{permission}", attribute!.Policy);
    }

    [Fact]
    public void Lifecycle_surface_exposes_only_server_bound_global_product_routes()
    {
        var update = typeof(GlobalProductsController).GetMethod(nameof(GlobalProductsController.UpdateDraft))!;
        var updateRoute = Assert.IsType<HttpPutAttribute>(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.Equal("{id:guid}", updateRoute.Template);

        var lifecycleRoutes = typeof(GlobalProductsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => new
            {
                method.Name,
                Http = method.GetCustomAttributes<HttpMethodAttribute>().SingleOrDefault()
            })
            .Where(item => (item.Http?.Template ?? string.Empty)
                .Contains("{id:guid}/", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(5, lifecycleRoutes.Length);
        Assert.Contains(lifecycleRoutes, item => item.Name == nameof(GlobalProductsController.SubmitIdentity)
            && item.Http!.HttpMethods.SequenceEqual(["POST"])
            && item.Http.Template == "{id:guid}/submit");
        Assert.Contains(lifecycleRoutes, item => item.Name == nameof(GlobalProductsController.WithdrawIdentityApproval)
            && item.Http!.HttpMethods.SequenceEqual(["POST"])
            && item.Http.Template == "{id:guid}/withdraw");
        Assert.Contains(lifecycleRoutes, item => item.Name == nameof(GlobalProductsController.RetireIdentity)
            && item.Http!.HttpMethods.SequenceEqual(["POST"])
            && item.Http.Template == "{id:guid}/retire");
        Assert.Contains(lifecycleRoutes, item => item.Name == nameof(GlobalProductsController.RequestCorrection)
            && item.Http!.HttpMethods.SequenceEqual(["POST"])
            && item.Http.Template == "{id:guid}/correction-requests");
        Assert.Contains(lifecycleRoutes, item => item.Name == nameof(GlobalProductsController.RequestRetirement)
            && item.Http!.HttpMethods.SequenceEqual(["POST"])
            && item.Http.Template == "{id:guid}/retirement-requests");
        Assert.DoesNotContain(lifecycleRoutes, item =>
            (item.Http!.Template ?? string.Empty).Contains("approve", StringComparison.OrdinalIgnoreCase)
            || (item.Http.Template ?? string.Empty).Contains("reject", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Paging_is_bounded()
    {
        Assert.False(new GetGlobalProductsValidator().Validate(new GetGlobalProductsQuery { PageNumber = 0, PageSize = 101 }).IsValid);
        Assert.False(new GetGlobalProductSelectorValidator().Validate(new GetGlobalProductSelectorQuery { PageNumber = 0, PageSize = 101 }).IsValid);
    }

    private static ProductItemSkuMasterModels.CreateGlobalProductDraftRequest ValidCreateRequest(string? name)
        => new()
        {
            GlobalProductName = name,
            ReservationId = Guid.NewGuid(),
            ExpectedReservationVersion = 0,
            IdempotencyKey = "create"
        };

    private static GlobalProduct Product(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        GlobalProductName = name,
        GlobalProductNameNormalized = GlobalProductNameRules.NormalizeDuplicateKey(name),
        CodeReservationId = Guid.NewGuid(),
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
        IsDeleted = false
    };

    private static async Task<Diten.Shared.Core.Response<ProductItemSkuMasterModels.GlobalProductDraftDto>> CreateAsync(
        MongoScope scope,
        Guid tenantId,
        string name,
        string key)
    {
        var reservation = await scope.Reservations(tenantId).ReserveAsync(
            CodeBearingEntityType.GlobalProduct, key + "-reserve", "actor", key);
        return await CreateWithReservationAsync(scope, tenantId, name, key + "-create", reservation);
    }

    private static Task<Diten.Shared.Core.Response<ProductItemSkuMasterModels.GlobalProductDraftDto>> CreateWithReservationAsync(
        MongoScope scope,
        Guid tenantId,
        string name,
        string key,
        CodeReservation reservation)
    {
        var handler = new CreateGlobalProductDraftHandler(
            scope.Reservations(tenantId),
            scope.Products(tenantId),
            scope.Context(tenantId),
            new ActorContext());
        return handler.Handle(new CreateGlobalProductDraftCommand(new()
        {
            GlobalProductName = name,
            ReservationId = reservation.Id,
            ExpectedReservationVersion = reservation.Version,
            IdempotencyKey = key
        }), CancellationToken.None);
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId => "test-actor";
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;
        private readonly string _databaseName;

        private MongoScope(IMongoClient client, IMongoDatabase database, string databaseName)
        {
            _client = client;
            Database = database;
            _databaseName = databaseName;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }

        public static async Task<MongoScope> CreateAsync()
        {
            var settings = MongoClientSettings.FromConnectionString(
                Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017");
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "DitenERP_GlobalProductApi_Test_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName);
        }

        public TenantContext Context(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }

        public CodeReservationRepository Reservations(Guid tenantId) => new(Database, Context(tenantId));
        public GlobalProductRepository Products(Guid tenantId) => new(Database, Context(tenantId));

        public GetGlobalProductsHandler ListHandler(Guid tenantId)
        {
            var dependencies = ScopeDependencies(tenantId);
            return new(
                Products(tenantId),
                dependencies.Rollout,
                dependencies.Policies,
                dependencies.Candidates,
                dependencies.Context);
        }

        public GetGlobalProductByIdHandler DetailHandler(Guid tenantId)
        {
            var dependencies = ScopeDependencies(tenantId);
            return new(
                Products(tenantId),
                dependencies.Rollout,
                dependencies.Policies,
                dependencies.Candidates,
                dependencies.Context);
        }

        public GetGlobalProductSelectorHandler SelectorHandler(Guid tenantId)
        {
            var dependencies = ScopeDependencies(tenantId);
            return new(
                Products(tenantId),
                dependencies.Rollout,
                dependencies.Policies,
                dependencies.Candidates,
                dependencies.Context);
        }

        private ScopeDependencySet ScopeDependencies(Guid tenantId)
        {
            var context = Context(tenantId);
            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
                tenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);
            var provider = new NoCallTrustedScopeProvider();
            var candidates = new ProductLegalEntityScopeCandidateFacade(
                provider,
                new InMemoryLegalEntityRepository(tenantId, []),
                context,
                new ScopeActorContext());
            return new(
                context,
                new FixedRolloutRepository(rollout),
                new EmptyPolicyRepository(),
                candidates);
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);
    }

    private sealed record ScopeDependencySet(
        TenantContext Context,
        IProductLegalEntityScopeRolloutStateRepository Rollout,
        IProductLegalEntityScopePolicyRepository Policies,
        ProductLegalEntityScopeCandidateFacade Candidates);

    private sealed class ScopeActorContext : IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }

    private sealed class EchoTrustedScopeProvider(IReadOnlyList<Guid> legalEntityIds)
        : ITrustedLegalEntityScopeProvider
    {
        public int CallCount { get; private set; }

        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
                expectedTenantId,
                expectedSubjectId,
                moduleCode,
                permissionKey,
                DateTimeOffset.UtcNow,
                legalEntityIds));
        }
    }

    private sealed class NoCallTrustedScopeProvider : ITrustedLegalEntityScopeProvider
    {
        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preparation must not call the trusted scope provider.");
    }

    private sealed class FixedRolloutRepository(ProductLegalEntityScopeRolloutState state)
        : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopeRolloutState?>(state);

        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopeRolloutState?>(null);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
            ProductLegalEntityScopeRolloutState requested,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
            ProductLegalEntityScopeRolloutState requested,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class EmptyPolicyRepository : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
            Guid globalProductId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopePolicy?>(null);

        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProductLegalEntityScopePolicy?>(null);

        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
            ProductLegalEntityScopePolicy policy,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
            ProductLegalEntityScopePolicy policy,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
            IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
