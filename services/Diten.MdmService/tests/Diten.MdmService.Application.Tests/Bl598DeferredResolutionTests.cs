using System.Text.Json.Nodes;
using Diten.MdmService.Application;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Features.ProductMaster.Handlers;
using Diten.MdmService.Application.Features.ProductMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-BL598-01 (CT decision b) — no store-backed service is built while a request is being RESOLVED: the write fence's
/// coordinator only for a mutation, the product-master stores inside the handler's own failure handling. Only "the store
/// cannot be reached" (timeout, connection, not primary) becomes a coded 503; a defect stays a 500; a mutation whose fence
/// cannot be built never reaches its handler.
/// </summary>
public sealed class Bl598DeferredResolutionTests
{
    // Measured: the container resolves Lazy<T> through the open-generic registration in AddApplication, and builds
    // nothing until .Value is read.
    [Fact]
    public void Bl598_The_container_resolves_Lazy_of_a_service_and_builds_it_only_on_first_use()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        Counted.Built = 0;
        services.AddScoped<Counted>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var lazy = scope.ServiceProvider.GetRequiredService<Lazy<Counted>>();

        Assert.Equal(0, Counted.Built);
        Assert.Same(scope.ServiceProvider.GetRequiredService<Counted>(), lazy.Value);
        Assert.Equal(1, Counted.Built);
    }

    [Fact]
    public async Task Bl598_A_query_never_builds_the_write_fence_coordinator()
    {
        var built = 0;
        var behavior = new ProductLegalEntityScopeWriteFenceBehavior<GetProductMasterItemQuery, Response<JsonObject>>(
            new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(() =>
            {
                built++;
                throw new TimeoutException("store away");
            }));

        var response = await behavior.Handle(new GetProductMasterItemQuery("x"),
            () => Task.FromResult(Response<JsonObject>.Success(new JsonObject())), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, built);
    }

    [Fact]
    public async Task Bl598_A_mutation_whose_fence_cannot_reach_the_store_is_refused_503_and_its_handler_is_not_called()
    {
        var reached = false;
        var behavior = Behavior(() => throw new TimeoutException("A timeout occurred after 400ms selecting a server"));

        var response = await behavior.Handle(Mutation(), () =>
        {
            reached = true;
            return Task.FromResult(Response<ProductItemSkuMasterModels.CodeReservationDto>.Fail("NEVER", 500));
        }, default);

        Assert.False(reached);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(ProductLegalEntityScopeWriteFenceCoordinator.RolloutStoreUnavailableCode, response.Errors.Single());
    }

    [Fact]
    public async Task Bl598_A_defect_while_building_the_fence_is_not_masked_as_503()
    {
        var reached = false;
        var behavior = Behavior(() => throw new InvalidOperationException("A trusted tenant is required."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(Mutation(), () =>
        {
            reached = true;
            return Task.FromResult(Response<ProductItemSkuMasterModels.CodeReservationDto>.Fail("NEVER", 500));
        }, default));
        Assert.False(reached);
    }

    [Fact]
    public async Task Bl598_The_product_master_handler_answers_the_contract_503_when_a_store_cannot_be_reached()
    {
        var handler = Handler(() => throw new TimeoutException("A timeout occurred after 400ms selecting a server"));

        var response = await handler.Handle(new GetProductMasterItemQuery(Guid.NewGuid().ToString("D")), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(ProductMasterContract.Unavailable, response.Errors.Single());
    }

    [Fact]
    public async Task Bl598_A_defect_while_building_the_product_master_stores_is_not_masked_as_503()
    {
        var handler = Handler(() => throw new InvalidOperationException("A trusted tenant is required."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new GetProductMasterItemQuery(Guid.NewGuid().ToString("D")), default));
    }

    [Theory]
    [InlineData(typeof(TimeoutException), true)]
    [InlineData(typeof(InvalidOperationException), false)]
    [InlineData(typeof(ArgumentException), false)]
    [InlineData(typeof(NullReferenceException), false)]
    public void Bl598_Only_a_store_connection_failure_counts(Type exception, bool counts)
    {
        Assert.Equal(counts, StoreConnectionFailure.Is((Exception)Activator.CreateInstance(exception, "x")!));
    }

    [Fact]
    public void Bl598_The_drivers_connection_and_not_primary_failures_count_by_name_and_its_other_failures_do_not()
    {
        var endPoint = new System.Net.DnsEndPoint("127.0.0.1", 1);
        var connection = new MongoDB.Driver.Core.Connections.ConnectionId(
            new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(), endPoint));
        Assert.True(StoreConnectionFailure.Is(new MongoDB.Driver.MongoConnectionException(connection, "down")));
        Assert.True(StoreConnectionFailure.Is(
            new MongoDB.Driver.MongoNotPrimaryException(connection, new MongoDB.Bson.BsonDocument("find", "x"),
                new MongoDB.Bson.BsonDocument("ok", 0))));
        Assert.False(StoreConnectionFailure.Is(new MongoDB.Driver.MongoClientException("a client defect")));
    }

    // BL-598 FIX1 (minor): measured — the driver's authentication failure IS a connection failure by type; a wrong
    // credential is a configuration defect, so it is not counted as "the store cannot be reached" (stays a 500).
    [Fact]
    public void Bl598_Fix1_A_wrong_credential_is_not_an_outage()
    {
        var endPoint = new System.Net.DnsEndPoint("127.0.0.1", 1);
        var connection = new MongoDB.Driver.Core.Connections.ConnectionId(
            new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(), endPoint));
        var authentication = new MongoDB.Driver.MongoAuthenticationException(connection, "bad credentials");

        Assert.True(authentication is MongoDB.Driver.MongoConnectionException);
        Assert.False(StoreConnectionFailure.Is(authentication));
    }

    // BL-598 FIX1 (minor): Replace builds its store-backed collaborators lazily — an unreachable store is its coded 503,
    // a defect is not masked.
    [Fact]
    public async Task Bl598_Fix1_Replace_answers_503_when_its_stores_cannot_be_reached()
    {
        var handler = new Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers
            .ReplaceProductLegalEntityScopePolicyHandler(
                null!,
                new Lazy<IProductLegalEntityScopeRolloutStateRepository>(() => throw new TimeoutException("store away")),
                new Lazy<ILegalEntityRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<ProductLegalEntityScopeCandidateFacade>(() => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<Diten.MdmService.Application.Contracts.Authorization.IProductLegalEntityScopeWriterAuthorityProvider>(
                    () => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(() => throw new Xunit.Sdk.XunitException("not reached")));

        var response = await handler.Handle(new(Guid.NewGuid(), Guid.NewGuid(), new()), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(ProductLegalEntityScopeWriteFenceCoordinator.RolloutStoreUnavailableCode, response.Errors.Single());
    }

    // BL-598 FIX2 (weak test made real): Replace with a CANCELLED request, and with a defect while building its stores —
    // neither is masked as the store's 503.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bl598_Fix2_Replace_never_turns_a_cancellation_or_a_defect_into_the_stores_503(bool cancelledWithTimeout)
    {
        using var cancelled = new CancellationTokenSource();
        if (cancelledWithTimeout) cancelled.Cancel();
        Exception failure = cancelledWithTimeout ? new TimeoutException("store away") : new InvalidOperationException("a defect");
        var handler = new Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.CommandHandlers
            .ReplaceProductLegalEntityScopePolicyHandler(
                null!,
                new Lazy<IProductLegalEntityScopeRolloutStateRepository>(() => throw failure),
                new Lazy<ILegalEntityRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<ProductLegalEntityScopeCandidateFacade>(() => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<Diten.MdmService.Application.Contracts.Authorization.IProductLegalEntityScopeWriterAuthorityProvider>(
                    () => throw new Xunit.Sdk.XunitException("not reached")),
                new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(() => throw new Xunit.Sdk.XunitException("not reached")));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(new(Guid.NewGuid(), Guid.NewGuid(), new()),
            cancelled.Token));

        Assert.Same(failure, thrown);
    }

    // D4: a cancelled request is a cancellation, never the store's 503 (fence and product master).
    [Fact]
    public async Task Bl598_Fix1_A_cancelled_request_is_never_turned_into_the_stores_503()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var behavior = Behavior(() => throw new TimeoutException("store away"));
        await Assert.ThrowsAsync<TimeoutException>(() => behavior.Handle(Mutation(),
            () => Task.FromResult(Response<ProductItemSkuMasterModels.CodeReservationDto>.Fail("NEVER", 500)), cancelled.Token));

        var handler = Handler(() => throw new TimeoutException("store away"));
        await Assert.ThrowsAsync<TimeoutException>(() =>
            handler.Handle(new GetProductMasterItemQuery(Guid.NewGuid().ToString("D")), cancelled.Token));
    }

    private static ProductLegalEntityScopeWriteFenceBehavior<ReserveCanonicalCodeCommand,
        Response<ProductItemSkuMasterModels.CodeReservationDto>> Behavior(
        Func<ProductLegalEntityScopeWriteFenceCoordinator> build) =>
        new(new Lazy<ProductLegalEntityScopeWriteFenceCoordinator>(build));

    private static ReserveCanonicalCodeCommand Mutation() => new(new ProductItemSkuMasterModels.ReserveGlobalProductCodeRequest());

    private static ProductMasterQueryHandler Handler(Func<IItemRepository> items)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        return new ProductMasterQueryHandler(
            new Lazy<IItemRepository>(items),
            new Lazy<IGlobalProductRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<IProductDefinitionRevisionRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<IGskuRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<ILskuRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<IFinishedGoodRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            null!,
            new Lazy<IProductLegalEntityScopeRolloutStateRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<IProductLegalEntityScopePolicyRepository>(() => throw new Xunit.Sdk.XunitException("not reached")),
            new Lazy<ProductLegalEntityScopeCandidateFacade>(() => throw new Xunit.Sdk.XunitException("not reached")),
            tenant);
    }

    public sealed class Counted
    {
        public static int Built;
        public Counted() => Built++;
    }
}
