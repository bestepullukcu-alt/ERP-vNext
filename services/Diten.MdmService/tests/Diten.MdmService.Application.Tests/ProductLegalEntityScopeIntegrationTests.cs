using System.Reflection;
using Diten.MdmService.Application.Behaviors;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MediatR;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-INT-01 — the scope rollout after the GP / LSKU / FG rounds merged into takeover.
/// <list type="bullet">
/// <item>(3) CT decision: the three writers the merge brought (LSKU / FG draft cancel, the operator's admission repair)
/// are born FENCED — while a fence measures the inventory twice, no writer moves product state (the repair included:
/// it changes a GSKU's admissions). Measured through the real pipeline behavior and the real coordinator.</item>
/// <item>BL-567: a product read whose rollout read fails answers a coded 503 — never a 500.</item>
/// </list>
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeIntegrationTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeIntegrationTests(AuditIntentTemporalMongoFixture fixture)
    {
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    public static TheoryData<string> NewlyFencedWriters => new()
    {
        nameof(CancelLskuDraftCommand), nameof(CancelFinishedGoodDraftCommand), nameof(RepairLskuChildAdmissionsCommand)
    };

    [Theory]
    [MemberData(nameof(NewlyFencedWriters))]
    public async Task A_newly_fenced_writer_is_refused_by_name_while_a_fence_is_up_and_never_reaches_its_handler(string writer)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var rollouts = await PreparationAsync(scope);
        var rollout = (await rollouts.GetAsync())!;
        Assert.True((await rollouts.AcquireFenceAsync(Fence(), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation)).Acquired);

        var (status, code, reachedHandler) = await RunThroughFenceAsync(scope, rollouts, writer);

        Assert.Equal((409, "PRODUCT_SCOPE_WRITE_FENCE_ACTIVE"), (status, code));
        Assert.False(reachedHandler);
    }

    [Theory]
    [MemberData(nameof(NewlyFencedWriters))]
    public async Task A_newly_fenced_writer_goes_through_when_no_fence_is_up_and_frees_its_lease(string writer)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var rollouts = await PreparationAsync(scope);

        var (status, _, reachedHandler) = await RunThroughFenceAsync(scope, rollouts, writer);

        Assert.Equal(200, status);
        Assert.True(reachedHandler);
        Assert.Empty((await rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public async Task In_an_enforced_tenant_a_finished_good_draft_cancel_outside_the_makers_scope_is_not_found()
    {
        // (3) The FG twin of LSKU's accepted LskuFix1ScopeTests (CT sabotage L10): the cancel's own scope rule. The
        // operator's admission repair is tenant-level (no product to evaluate) — measured, reported, no scope rule.
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GlobalProductId = Guid.NewGuid() };
        var product = new GlobalProduct { Id = revision.GlobalProductId, TenantId = tenant.TenantId };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, ProductDefinitionRevisionId = revision.Id };
        var finishedGood = new FinishedGood { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id };
        var scope = ProductLegalEntityScopeTestFixture.Enforced(tenant, [Guid.NewGuid()], new NoPolicies());
        var handler = new CancelFinishedGoodDraftHandler(tenant, new Maker(FinishedGoodDraftCancellationPermissions.CancelDraft),
            ReadOnly<IFinishedGoodRepository>(finishedGood), ReadOnly<IGskuRepository>(gsku),
            ReadOnly<IProductDefinitionRevisionRepository>(revision), ReadOnly<IGlobalProductRepository>(product),
            ReadOnly<ICodeReservationRepository>(finishedGood),
            scope.Rollouts, scope.Policies, scope.Candidates, TimeProvider.System);

        var response = await handler.Handle(new(finishedGood.Id, 0, Guid.NewGuid(), "CREATED_BY_MISTAKE"), default);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("FINISHED_GOOD_NOT_FOUND", response.Errors);
    }

    [Theory]
    [InlineData("invalid-record", "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID")]
    [InlineData("format", "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID")]
    // FIX1 M6: a record the serializer refuses by its own exception type.
    [InlineData("bson", "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID")]
    [InlineData("mongo", "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE")]
    [InlineData("timeout", "PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE")]
    public async Task A_product_read_whose_rollout_read_fails_answers_a_coded_503_never_a_500(string failure, string code)
    {
        // BL-567: before, only the repository's own refusal (InvalidOperationException) was mapped in the read guard —
        // a store failure or a record the serializer cannot map was a 500 on every product read of the tenant. The
        // guard is shared by the GP / GSKU / LSKU / FG lists and details; measured here through the real GP list handler.
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        Exception thrown = failure switch
        {
            "invalid-record" => new InvalidOperationException("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_STATE_INVALID"),
            "format" => new FormatException("cannot deserialize Mode"),
            "bson" => new MongoDB.Bson.BsonSerializationException("Element 'Mode' does not match any field"),
            "mongo" => new MongoException("connection lost"),
            _ => new TimeoutException("server selection timed out")
        };
        var handler = new GetGlobalProductsHandler(
            Unreachable<IGlobalProductRepository>(), new FailingRollouts(thrown), new NoPolicies(),
            new ProductLegalEntityScopeCandidateFacade(Unreachable<Contracts.Authorization.ITrustedLegalEntityScopeProvider>(),
                new InMemoryLegalEntityRepository(tenant.TenantId, []), tenant, new Actor()),
            tenant);

        var response = await handler.Handle(new GetGlobalProductsQuery(), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains(code, response.Errors);
    }

    [Fact]
    public async Task A_rollout_written_before_the_fence_and_the_lease_slot_existed_admits_writers_the_slot_and_a_fence()
    {
        // (5) Mongo new-field rule, measured on every scope filter: the conditions on ActiveFence, ActiveWriterLease and
        // its PreWriteStateHash are Eq(null) — which matches a MISSING field too; WriterLeaseGeneration and the product-lease
        // fields use Exists(false). A record without ActiveFence / ActiveWriterLease / WriterLeaseGeneration (and without
        // the FIX1 fields) must still admit a product writer, the foreground slot, and then a fence.
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var rollouts = await PreparationAsync(scope);
        await UnsetAsync(scope, "ActiveFence", "ActiveWriterLease", "WriterLeaseGeneration",
            "ProductWriterLeases", "ProductWriterLeaseRevision", "LastFenceAbort");

        var product = await rollouts.AcquireProductWriterLeaseAsync(Lease("GlobalProductId:" + Guid.NewGuid().ToString("D")));
        Assert.True(product.Acquired, product.FailureCode);
        Assert.True(await rollouts.ReleaseProductWriterLeaseAsync(product.Lease!.Token, product.Lease.Generation));

        await UnsetAsync(scope, "ActiveFence", "ActiveWriterLease", "WriterLeaseGeneration");
        var slot = await rollouts.AcquireWriterLeaseAsync(Lease("GlobalProductId:" + Guid.NewGuid().ToString("D")));
        Assert.True(slot.Acquired, slot.FailureCode);

        await UnsetAsync(scope, "ActiveFence", "ActiveWriterLease");
        var rollout = (await rollouts.GetAsync())!;
        var fenced = await rollouts.AcquireFenceAsync(Fence(), rollout.Id, rollout.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        Assert.True(fenced.Acquired, fenced.FailureCode);
    }

    private static Task UnsetAsync(ProductScopeMongoScope scope, params string[] fields) =>
        scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_product_legal_entity_scope_rollout_states").UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("TenantId", new MongoDB.Bson.BsonBinaryData(scope.TenantId, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument(fields.Select(field => new MongoDB.Bson.BsonElement(field, "")))));

    private static ProductLegalEntityScopeWriterLease Lease(string key)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProductLegalEntityScopeWriterLease
        {
            Token = Guid.NewGuid(), Generation = 1, CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(),
            MutationKind = "LegacyWrite", PayloadFingerprint = new string('A', 64), Owner = "Diten.MDM:LegacyWrite",
            AcquiredAtUtc = now, ExpiresAtUtc = now.AddSeconds(ProductLegalEntityScopeWriterLease.DurationSeconds),
            ObjectKeys = [key]
        };
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<ProductLegalEntityScopeRolloutStateRepository> PreparationAsync(ProductScopeMongoScope scope)
    {
        var rollouts = scope.RolloutRepository();
        Assert.True((await rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow))).Succeeded);
        return rollouts;
    }

    /// <summary>The command, through the real write-fence behavior and coordinator; the handler is a probe.</summary>
    private static async Task<(int Status, string? Code, bool ReachedHandler)> RunThroughFenceAsync(
        ProductScopeMongoScope scope, IProductLegalEntityScopeRolloutStateRepository rollouts, string writer)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(scope.TenantId);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(rollouts,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(), tenant, TimeProvider.System);
        return writer switch
        {
            nameof(CancelLskuDraftCommand) => await RunAsync(coordinator,
                new CancelLskuDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                () => new LskuDraftCancellationResult(Guid.NewGuid(), 2, true, false)),
            nameof(CancelFinishedGoodDraftCommand) => await RunAsync(coordinator,
                new CancelFinishedGoodDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                () => new FinishedGoodDraftCancellationResult(Guid.NewGuid(), 2, default, false)),
            _ => await RunAsync(coordinator,
                new RepairLskuChildAdmissionsCommand(),
                () => new LskuChildAdmissionRepairResult(0, 0, []))
        };
    }

    private static async Task<(int Status, string? Code, bool ReachedHandler)> RunAsync<TCommand, TResult>(
        ProductLegalEntityScopeWriteFenceCoordinator coordinator, TCommand command, Func<TResult> result)
        where TCommand : IRequest<Response<TResult>>
    {
        var reached = false;
        var behavior = new ProductLegalEntityScopeWriteFenceBehavior<TCommand, Response<TResult>>(coordinator);
        var response = await behavior.Handle(command, () =>
        {
            reached = true;
            return Task.FromResult(Response<TResult>.Success(result()));
        }, default);
        return (response.StatusCode, response.Errors?.FirstOrDefault(), reached);
    }

    private static ProductLegalEntityScopeActivationFence Fence() => new()
    {
        Token = new string('E', 64), State = ProductLegalEntityScopeAdmissionState.Closing, Action = "ActivateEnforced",
        CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(), ReasonCode = "OWNER_APPROVED", AcquiredAtUtc = DateTimeOffset.UtcNow
    };

    private static T ReadOnly<T>(object value) where T : class
    {
        var proxy = DispatchProxy.Create<T, ReadProxy>();
        ((ReadProxy)(object)proxy).Value = value;
        return proxy;
    }

    private static T Unreachable<T>() where T : class => DispatchProxy.Create<T, UnreachableProxy>();

    public class ReadProxy : DispatchProxy
    {
        public object Value { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (!targetMethod!.Name.StartsWith("GetById", StringComparison.Ordinal)) throw new NotSupportedException(targetMethod.Name);
            var entity = (EntityBase)Value;
            var found = (Guid)args![0]! == entity.Id ? Value : null;
            var result = typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0]);
            return result.Invoke(null, [found]);
        }
    }

    public class UnreachableProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException("not reached: " + targetMethod?.Name);
    }

    private sealed class Actor : IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }

    private sealed class Maker(string key) : IProductIdentityLifecycleActorContext
    {
        private readonly Guid _subject = Guid.NewGuid();
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = _subject; return true; }
        public bool HasPermission(string permission) => permission == key;
    }

    [Fact]
    public async Task A_product_read_cancelled_by_its_own_client_is_not_answered_as_an_unreachable_store()
    {
        // FIX1 M7: the driver may surface a cancelled read as a MongoException; the caller going away is not a 503.
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var handler = new GetGlobalProductsHandler(
            Unreachable<IGlobalProductRepository>(), new FailingRollouts(new MongoException("operation cancelled")), new NoPolicies(),
            new ProductLegalEntityScopeCandidateFacade(Unreachable<Contracts.Authorization.ITrustedLegalEntityScopeProvider>(),
                new InMemoryLegalEntityRepository(tenant.TenantId, []), tenant, new Actor()),
            tenant);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<MongoException>(() => handler.Handle(new GetGlobalProductsQuery(), cancelled.Token));
    }

    private sealed class FailingRollouts(Exception failure) : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) => throw failure;
        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => throw failure;
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState state, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoPolicies : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy policy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy policy, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
