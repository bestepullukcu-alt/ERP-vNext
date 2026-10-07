using System.Reflection;
using System.Text.RegularExpressions;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Mode = Diten.MdmService.Application.Features.ProductLegalEntityScopes.ProductIdentityBackgroundScope.TenantMode;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-INT-01 FIX1 — the review round of the scope integration:
/// <list type="bullet">
/// <item>item 1 (CT decision a): the operator's admission repair follows the background scope rule;</item>
/// <item>M1: its request shape is refused before the fence (no lease); M8: every newly fenced command, sent through the
/// REAL registration, is refused by name while a fence is up, never reaches its handler and leaves no lease;</item>
/// <item>M5: the FG / LSKU draft cancel scope tests get their positive control; M6: every product handler answers the
/// read guard's own status (the BL-567 503 reaches the caller).</item>
/// </list>
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeIntFix1Tests : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private readonly AuditIntentTemporalMongoFixture _fixture;
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeIntFix1Tests(AuditIntentTemporalMongoFixture fixture)
    {
        _fixture = fixture;
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() => Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    // ---------------------------------------------------------------------------------------------- item 1

    [Fact]
    public async Task In_an_enforced_tenant_the_repair_releases_only_in_scope_orphans_and_never_names_the_others()
    {
        var world = new RepairWorld(ProductLegalEntityScopeRolloutMode.Enforced);
        var inScope = world.Orphan(classified: true);
        var outOfScope = world.Orphan(classified: false);

        var response = await world.Handler().Handle(new RepairLskuChildAdmissionsCommand(), default);

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
        Assert.Equal((1, 1, 1), (response.Data!.Found, response.Data.Released, response.Data.OutOfScope));
        Assert.Equal([inScope.GskuId], response.Data.Items.Select(item => item.GskuId));
        // The out-of-scope orphan: its code is not burned, its admission not released.
        Assert.Equal([inScope.ReservationToBurnId!.Value], world.Burned);
        Assert.Equal([inScope.GskuId], world.Released);
        Assert.DoesNotContain(outOfScope.ReservationToBurnId!.Value, world.Burned);
    }

    [Theory]
    [InlineData(ProductLegalEntityScopeRolloutMode.FailClosedSuspended)]
    public async Task A_suspended_tenant_refuses_the_repair_by_name_before_any_orphan_is_read(ProductLegalEntityScopeRolloutMode mode)
    {
        var world = new RepairWorld(mode);
        world.Orphan(classified: true);

        var response = await world.Handler().Handle(new RepairLskuChildAdmissionsCommand(), default);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(RepairLskuChildAdmissionsHandler.ScopeClosed, response.Errors!);
        Assert.False(world.OrphansRead);
        Assert.Empty(world.Burned);
    }

    [Fact]
    public async Task In_a_preparation_tenant_the_repair_releases_every_orphan_as_before()
    {
        var world = new RepairWorld(ProductLegalEntityScopeRolloutMode.Preparation);
        world.Orphan(classified: true);
        world.Orphan(classified: false);

        var response = await world.Handler().Handle(new RepairLskuChildAdmissionsCommand(), default);

        Assert.Equal((2, 2, 0), (response.Data!.Found, response.Data.Released, response.Data.OutOfScope));
    }

    // ---------------------------------------------------------------------------------------------- FIX2 item 1 / m6

    [Fact]
    public async Task Out_of_scope_orphans_in_front_no_longer_hide_the_in_scope_orphan_behind_them()
    {
        // FIX2 item 1: Limit out-of-scope orphans first (lowest GSKU ids, never repaired, so they stay in front), one
        // in-scope orphan behind them. Before, every run read the same first Limit orphans and never reached it.
        var world = new RepairWorld(ProductLegalEntityScopeRolloutMode.Enforced) { PageSize = 1 };
        world.Orphan(classified: false);
        world.Orphan(classified: false);
        var behind = world.Orphan(classified: true);

        var response = await world.Handler().Handle(new RepairLskuChildAdmissionsCommand(Limit: 2), default);

        Assert.True(response.IsSuccessful, string.Join(",", response.Errors ?? []));
        Assert.Equal([behind.GskuId], world.Released);
        Assert.Equal((1, 1, 2, false), (response.Data!.Found, response.Data.Released, response.Data.OutOfScope, response.Data.Truncated));
        Assert.Equal([behind.GskuId], response.Data.Items.Select(item => item.GskuId));
    }

    [Fact]
    public async Task The_walk_stops_at_its_page_bound_and_says_it_was_truncated()
    {
        var world = new RepairWorld(ProductLegalEntityScopeRolloutMode.Enforced) { PageSize = 1 };
        for (var i = 0; i < GskuOrphanAdmissionPage.MaximumPages + 1; i++) world.Orphan(classified: false);
        world.Orphan(classified: true);

        var response = await world.Handler().Handle(new RepairLskuChildAdmissionsCommand(Limit: 1), default);

        Assert.True(response.Data!.Truncated);
        Assert.Equal(GskuOrphanAdmissionPage.MaximumPages, world.PagesRead);
        Assert.Equal(GskuOrphanAdmissionPage.MaximumPages, response.Data.OutOfScope);
        Assert.Empty(world.Released);
    }

    [Fact]
    public async Task A_finished_good_orphan_is_repaired_by_the_same_scope_rule()
    {
        // FIX2 m6: the FG kind of the same repair (POST api/finished-goods/operator/child-admission-repairs).
        var world = new RepairWorld(ProductLegalEntityScopeRolloutMode.Enforced);
        var inScope = world.Orphan(classified: true, GskuChildIdentityKind.FinishedGood);
        world.Orphan(classified: false, GskuChildIdentityKind.FinishedGood);
        world.Orphan(classified: true, GskuChildIdentityKind.Lsku);

        var response = await world.Handler().Handle(
            new RepairLskuChildAdmissionsCommand(ChildKind: GskuChildIdentityKind.FinishedGood), default);

        Assert.Equal([inScope.GskuId], world.Released);
        Assert.Equal((1, 1), (response.Data!.Released, response.Data.OutOfScope));
    }

    // ---------------------------------------------------------------------------------------------- FIX2 m4

    [Fact]
    public async Task A_product_write_cancelled_by_its_own_caller_is_not_answered_as_an_unreachable_store()
    {
        var tenant = Tenant(Guid.NewGuid());
        var rollouts = Proxy<IProductLegalEntityScopeRolloutStateRepository>((method, _) =>
            throw new MongoDB.Driver.MongoException("operation cancelled"));
        var readiness = Proxy<IProductLegalEntityScopeOperationalReadinessRepository>((method, _) =>
            throw new NotSupportedException(method.Name));
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(rollouts, readiness, new Actor(), tenant, TimeProvider.System);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<MongoDB.Driver.MongoException>(() => coordinator.EnterAsync(
            new CancelLskuDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"), cancelled.Token));
        // Not cancelled: the same failure is still the coded 503 (unchanged).
        var answered = await coordinator.EnterAsync(
            new CancelLskuDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"), default);
        Assert.Equal((false, 503), (answered.IsAllowed, answered.StatusCode));
    }

    [Fact]
    public async Task A_policy_read_cancelled_by_its_own_caller_is_not_answered_as_an_unreachable_store()
    {
        var rollouts = Proxy<IProductLegalEntityScopeRolloutStateRepository>((method, _) =>
            throw new MongoDB.Driver.MongoException("operation cancelled"));
        var handler = new Diten.MdmService.Application.Features.ProductLegalEntityScopes.Handlers.QueryHandlers
            .GetProductLegalEntityScopeCompletenessHandler(
                Proxy<IGlobalProductRepository>((method, _) => throw new NotSupportedException(method.Name)),
                Proxy<IProductLegalEntityScopePolicyRepository>((method, _) => throw new NotSupportedException(method.Name)),
                rollouts);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<MongoDB.Driver.MongoException>(() => handler.Handle(
            new Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries.GetProductLegalEntityScopeCompletenessQuery(),
            cancelled.Token));
        Assert.Equal(503, (await handler.Handle(
            new Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries.GetProductLegalEntityScopeCompletenessQuery(),
            default)).StatusCode);
    }

    // ---------------------------------------------------------------------------------------------- M1 / M2 / M8

    [Fact]
    public async Task Through_the_real_registration_an_invalid_repair_is_400_before_the_fence_and_takes_no_lease()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var rollouts = await PreparationAsync(scope);
        await using var provider = RealPipeline(scope, out var probe);

        var response = await SendAsync(provider, new RepairLskuChildAdmissionsCommand(MinimumAgeMinutes: 1));

        Assert.Equal(400, response.Status);
        Assert.Equal(RepairLskuChildAdmissionsHandler.RequestInvalid, response.Code);
        Assert.Empty(probe.Reached);
        Assert.Empty((await rollouts.GetAsync())!.ProductWriterLeases);
    }

    public static TheoryData<string> NewlyFencedWriters => new()
    {
        nameof(CancelLskuDraftCommand), nameof(CancelFinishedGoodDraftCommand), nameof(RepairLskuChildAdmissionsCommand)
    };

    [Theory]
    [MemberData(nameof(NewlyFencedWriters))]
    public async Task Through_the_real_registration_a_fenced_writer_is_refused_by_name_never_reaches_its_handler_and_takes_no_lease(
        string writer)
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var rollouts = await PreparationAsync(scope);
        var rollout = (await rollouts.GetAsync())!;
        Assert.True((await rollouts.AcquireFenceAsync(Fence(), rollout.Id, rollout.Version,
            ProductLegalEntityScopeRolloutMode.Preparation)).Acquired);
        await using var provider = RealPipeline(scope, out var probe);

        var response = await SendAsync(provider, Command(writer));

        Assert.Equal((409, "PRODUCT_SCOPE_WRITE_FENCE_ACTIVE"), (response.Status, response.Code));
        Assert.Empty(probe.Reached);
        Assert.Empty((await rollouts.GetAsync())!.ProductWriterLeases);
    }

    [Fact]
    public void Two_repair_calls_with_the_same_parameters_are_two_lease_identities()
    {
        // M2: before, the lease identity was the payload's hash — two overlapping runs shared one lease.
        var first = ProductLegalEntityScopeMutationIdentity.Create(new RepairLskuChildAdmissionsCommand());
        var second = ProductLegalEntityScopeMutationIdentity.Create(new RepairLskuChildAdmissionsCommand());

        Assert.NotEqual(first.CommandId, second.CommandId);
        Assert.NotEqual(first.ObjectKeys, second.ObjectKeys);
    }

    // ---------------------------------------------------------------------------------------------- M5

    [Theory]
    [InlineData(true, 409, "FINISHED_GOOD_NOT_A_DRAFT")]
    [InlineData(false, 404, "FINISHED_GOOD_NOT_FOUND")]
    public async Task In_an_enforced_tenant_a_finished_good_cancel_passes_the_scope_only_for_the_makers_legal_entity(
        bool policyForMaker, int status, string code)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var (revision, product, gsku) = Chain(tenant.TenantId);
        var finishedGood = new FinishedGood { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id };
        var makerEntity = Guid.NewGuid();
        var policies = new OnePolicy(Policy(tenant.TenantId, product.Id, policyForMaker ? makerEntity : Guid.NewGuid()));
        var scope = ProductLegalEntityScopeTestFixture.Enforced(tenant, [makerEntity], policies);
        var finishedGoods = Proxy<IFinishedGoodRepository>((method, args) => method.Name switch
        {
            "GetByIdAsync" => Task.FromResult<FinishedGood?>((Guid)args![0]! == finishedGood.Id ? finishedGood : null),
            // Past the scope: the record's own named state answers (no write in this test).
            "CancelDraftAsync" => Task.FromResult(new FinishedGoodDraftCancelResult(false, finishedGood, "FINISHED_GOOD_NOT_A_DRAFT")),
            _ => throw new NotSupportedException(method.Name)
        });
        var handler = new CancelFinishedGoodDraftHandler(tenant, new Maker(FinishedGoodDraftCancellationPermissions.CancelDraft),
            finishedGoods, ReadOnly<IGskuRepository>(gsku), ReadOnly<IProductDefinitionRevisionRepository>(revision),
            ReadOnly<IGlobalProductRepository>(product), ReadOnly<ICodeReservationRepository>(finishedGood),
            scope.Rollouts, scope.Policies, scope.Candidates, TimeProvider.System);

        var response = await handler.Handle(new(finishedGood.Id, 0, Guid.NewGuid(), "CREATED_BY_MISTAKE"), default);

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, response.Errors!);
    }

    [Theory]
    [InlineData(true, 409, "LSKU_NOT_A_DRAFT")]
    [InlineData(false, 404, "LSKU_NOT_FOUND")]
    public async Task In_an_enforced_tenant_an_lsku_cancel_passes_the_scope_only_for_the_makers_legal_entity(
        bool policyForMaker, int status, string code)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        var (revision, product, gsku) = Chain(tenant.TenantId);
        // Identity-approved: past the scope the handler's own state check answers before any write.
        var lsku = new Lsku { Id = Guid.NewGuid(), TenantId = tenant.TenantId, GskuId = gsku.Id,
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var makerEntity = Guid.NewGuid();
        var policies = new OnePolicy(Policy(tenant.TenantId, product.Id, policyForMaker ? makerEntity : Guid.NewGuid()));
        var scope = ProductLegalEntityScopeTestFixture.Enforced(tenant, [makerEntity], policies);
        var handler = new CancelLskuDraftHandler(tenant, new Maker(LskuIdentityLifecyclePermissions.CancelDraft),
            ReadOnly<ILskuRepository>(lsku), ReadOnly<IGskuRepository>(gsku),
            ReadOnly<IProductDefinitionRevisionRepository>(revision), ReadOnly<IGlobalProductRepository>(product),
            ReadOnly<ILskuIdentityWorkflowOperationRepository>(lsku), ReadOnly<ICodeReservationRepository>(lsku), null!,
            new LskuIdentityWorkflowExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            scope.Rollouts, scope.Policies, scope.Candidates, TimeProvider.System);

        var response = await handler.Handle(new(lsku.Id, 0, Guid.NewGuid(), "CREATED_BY_MISTAKE"), default);

        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, response.Errors!);
    }

    // ---------------------------------------------------------------------------------------------- M6

    [Fact]
    public void Every_product_handler_answers_the_read_guards_own_status_never_its_own()
    {
        // BL-567 / M6: the guard answers a coded 503 when the rollout cannot be read; a handler that turned that into its
        // own status (a 403, a 500) would hide it. Every caller of the consumer guard, in every product feature, must
        // hand back the guard's StatusCode.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var features = Path.Combine(root!.FullName, "services", "Diten.MdmService", "src", "Diten.MdmService.Application", "Features");
        var callers = 0;
        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(features, "*.cs", SearchOption.AllDirectories))
        {
            var code = ProductLegalEntityScopeWriteAdmissionContractTests.WithoutComments(File.ReadAllText(path));
            foreach (Match call in Regex.Matches(code, @"var\s+(\w+)\s*=\s*await\s+[\w.]+\.ResolveContextAsync\("))
            {
                callers++;
                var name = call.Groups[1].Value;
                var after = code.Substring(call.Index, Math.Min(600, code.Length - call.Index));
                if (!Regex.IsMatch(after, $@"\b{name}\.StatusCode\b"))
                    offenders.Add($"{Path.GetFileName(path)}:{name}");
            }
        }
        Assert.True(callers >= 30, $"the scan found only {callers} guard callers — it is blind");
        Assert.True(offenders.Count == 0, "Handlers that do not answer the guard's status: " + string.Join(", ", offenders));
    }

    // ---------------------------------------------------------------------------------------------- plumbing

    private static async Task<ProductLegalEntityScopeRolloutStateRepository> PreparationAsync(ProductScopeMongoScope scope)
    {
        var rollouts = scope.RolloutRepository();
        Assert.True((await rollouts.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow))).Succeeded);
        return rollouts;
    }

    private static ProductLegalEntityScopeActivationFence Fence() => new()
    {
        Token = new string('F', 64), State = ProductLegalEntityScopeAdmissionState.Closing, Action = "ActivateEnforced",
        CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(), ReasonCode = "OWNER_APPROVED", AcquiredAtUtc = DateTimeOffset.UtcNow
    };

    private static object Command(string writer) => writer switch
    {
        nameof(CancelLskuDraftCommand) => new CancelLskuDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
        nameof(CancelFinishedGoodDraftCommand) => new CancelFinishedGoodDraftCommand(Guid.NewGuid(), 1, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
        _ => new RepairLskuChildAdmissionsCommand()
    };

    /// <summary>The real MediatR registration (every behavior in its real order) over the test-owned replica set; only
    /// the three handlers are probes, registered last so they win over the scanned ones.</summary>
    private ServiceProvider RealPipeline(ProductScopeMongoScope scope, out Probe probe)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = _fixture.ReplicaConnectionString,
            ["Mongo:DatabaseName"] = ProductLegalEntityScopeMongoCollection.DatabaseName
        }).Build();
        services.AddPersistence(configuration, runStartupMaintenance: false);
        Diten.MdmService.Application.DependencyInjection.AddApplication(services);
        services.AddScoped<ITenantContext>(_ => Tenant(scope.TenantId));
        services.AddScoped<IProductIdentityActorContext, Actor>();
        services.AddSingleton<IPlatformAuditForwarder, NoAudit>();
        var shared = new Probe();
        probe = shared;
        services.AddTransient<IRequestHandler<CancelLskuDraftCommand, Response<LskuDraftCancellationResult>>>(_ =>
            new ProbeHandler<CancelLskuDraftCommand, LskuDraftCancellationResult>(shared,
                () => new LskuDraftCancellationResult(Guid.NewGuid(), 2, true, false)));
        services.AddTransient<IRequestHandler<CancelFinishedGoodDraftCommand, Response<FinishedGoodDraftCancellationResult>>>(_ =>
            new ProbeHandler<CancelFinishedGoodDraftCommand, FinishedGoodDraftCancellationResult>(shared,
                () => new FinishedGoodDraftCancellationResult(Guid.NewGuid(), 2, default, false)));
        services.AddTransient<IRequestHandler<RepairLskuChildAdmissionsCommand, Response<LskuChildAdmissionRepairResult>>>(_ =>
            new ProbeHandler<RepairLskuChildAdmissionsCommand, LskuChildAdmissionRepairResult>(shared,
                () => new LskuChildAdmissionRepairResult(0, 0, [])));
        return services.BuildServiceProvider();
    }

    private static async Task<(int Status, string? Code)> SendAsync(IServiceProvider provider, object command)
    {
        await using var requestScope = provider.CreateAsyncScope();
        var response = await requestScope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        var type = response!.GetType();
        var errors = (IEnumerable<string>?)type.GetProperty("Errors")!.GetValue(response);
        return ((int)type.GetProperty("StatusCode")!.GetValue(response)!, errors?.FirstOrDefault());
    }

    private static TenantContext Tenant(Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        return tenant;
    }

    private static (ProductDefinitionRevision, GlobalProduct, Gsku) Chain(Guid tenantId)
    {
        var revision = new ProductDefinitionRevision { Id = Guid.NewGuid(), TenantId = tenantId, GlobalProductId = Guid.NewGuid() };
        var product = new GlobalProduct { Id = revision.GlobalProductId, TenantId = tenantId };
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = tenantId, ProductDefinitionRevisionId = revision.Id };
        return (revision, product, gsku);
    }

    private static ProductLegalEntityScopePolicy Policy(Guid tenantId, Guid productId, Guid legalEntityId) =>
        ProductLegalEntityScopePolicy.Create(tenantId, productId, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
            [legalEntityId], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy>();
        ((FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static T ReadOnly<T>(object value) where T : class => Proxy<T>((method, args) =>
    {
        if (!method.Name.StartsWith("GetById", StringComparison.Ordinal)) throw new NotSupportedException(method.Name);
        var found = (Guid)args![0]! == ((EntityBase)value).Id ? value : null;
        return typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(method.ReturnType.GetGenericArguments()[0]).Invoke(null, [found]);
    });

    /// <summary>Item 1: a tenant with orphans on GSKUs whose products are classified (a policy in force) or not.</summary>
    private sealed class RepairWorld
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly ProductLegalEntityScopeRolloutMode _mode;
        private readonly List<GskuOrphanChildAdmission> _orphans = [];
        private readonly Dictionary<Guid, Guid> _productOfGsku = [];
        private readonly HashSet<Guid> _classified = [];

        public RepairWorld(ProductLegalEntityScopeRolloutMode mode) => _mode = mode;

        public List<Guid> Burned { get; } = [];
        public List<Guid> Released { get; } = [];
        public bool OrphansRead { get; private set; }

        /// <summary>FIX2: GSKUs per page of the paged walk (the store's own page is 100).</summary>
        public int PageSize { get; init; } = 100;

        public int PagesRead { get; private set; }

        private int _sequence;

        public GskuOrphanChildAdmission Orphan(bool classified, GskuChildIdentityKind kind = GskuChildIdentityKind.Lsku)
        {
            // Ascending ids: the walk's GSKU id order is the order the orphans are created in.
            var gskuId = new Guid(++_sequence, 0, 0, new byte[8]);
            var productId = Guid.NewGuid();
            _productOfGsku[gskuId] = productId;
            if (classified) _classified.Add(productId);
            var orphan = new GskuOrphanChildAdmission(gskuId, 3, kind, "create-" + gskuId.ToString("N"),
                new string('a', 64), DateTimeOffset.UtcNow.AddHours(-1), Guid.NewGuid(), Guid.NewGuid(), 2);
            _orphans.Add(orphan);
            return orphan;
        }

        public RepairLskuChildAdmissionsHandler Handler()
        {
            var tenant = Tenant(_tenantId);
            var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenantId, Guid.NewGuid(), Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-1));
            rollout.Mode = _mode;
            var rollouts = Proxy<IProductLegalEntityScopeRolloutStateRepository>((method, _) => method.Name == "GetAsync"
                ? Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout)
                : throw new NotSupportedException(method.Name));
            var policies = Proxy<IProductLegalEntityScopePolicyRepository>((method, args) => method.Name == "GetByGlobalProductIdAsync"
                ? Task.FromResult(_classified.Contains((Guid)args![0]!)
                    ? ProductLegalEntityScopePolicy.Create(_tenantId, (Guid)args[0]!, Guid.NewGuid(),
                        ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1))
                    : null)
                : throw new NotSupportedException(method.Name));
            var gskus = Proxy<IGskuRepository>((method, args) =>
            {
                switch (method.Name)
                {
                    case "FindOrphanChildCreationAdmissionsAsync":
                        OrphansRead = true;
                        return Task.FromResult<IReadOnlyList<GskuOrphanChildAdmission>>(_orphans);
                    case "FindOrphanChildCreationAdmissionPageAsync":
                    {
                        OrphansRead = true;
                        PagesRead++;
                        var kind = (GskuChildIdentityKind)args![0]!;
                        var after = (Guid?)args[2];
                        var behind = _orphans.Where(item => item.ChildKind == kind && (after is null || item.GskuId.CompareTo(after.Value) > 0))
                            .OrderBy(item => item.GskuId).ToList();
                        var gskus = behind.Select(item => item.GskuId).Distinct().Take(PageSize).ToHashSet();
                        var page = behind.Where(item => gskus.Contains(item.GskuId)).ToList();
                        var more = behind.Count > page.Count;
                        return Task.FromResult(new GskuOrphanAdmissionPage(page, more ? page[^1].GskuId : null));
                    }
                    case "GetByIdAsync":
                        var id = (Guid)args![0]!;
                        return Task.FromResult<Gsku?>(_productOfGsku.ContainsKey(id)
                            ? new Gsku { Id = id, TenantId = _tenantId, ProductDefinitionRevisionId = id } : null);
                    case "RepairOrphanChildCreationAdmissionAsync":
                        var orphan = (GskuOrphanChildAdmission)args![0]!;
                        Released.Add(orphan.GskuId);
                        return Task.FromResult(new GskuChildCreationAdmissionResult(true, false,
                            new Gsku { Id = orphan.GskuId, Version = orphan.GskuVersion + 1 }, null));
                    default:
                        throw new NotSupportedException(method.Name);
                }
            });
            // The revision of a GSKU here carries the GSKU's id; its product is the GSKU's product.
            var revisions = Proxy<IProductDefinitionRevisionRepository>((method, args) => method.Name == "GetByIdAsync"
                ? Task.FromResult<ProductDefinitionRevision?>(_productOfGsku.TryGetValue((Guid)args![0]!, out var product)
                    ? new ProductDefinitionRevision { Id = (Guid)args[0]!, TenantId = _tenantId, GlobalProductId = product }
                    : null)
                : throw new NotSupportedException(method.Name));
            var reservations = Proxy<ICodeReservationRepository>((method, args) =>
            {
                if (method.Name != "BurnIdentityBindingAsync") throw new NotSupportedException(method.Name);
                Burned.Add((Guid)args![0]!);
                return Task.FromResult(new ReservationOperationResult(true, null));
            });
            var lskus = Proxy<ILskuRepository>((method, _) => throw new NotSupportedException(method.Name));
            var background = new ProductIdentityBackgroundScope(rollouts, policies, tenant, TimeProvider.System);
            return new RepairLskuChildAdmissionsHandler(gskus, reservations, tenant, new Actor(), TimeProvider.System,
                new LskuBackgroundScope(background, lskus, gskus, revisions));
        }
    }

    public sealed class Probe
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Reached { get; } = new();
    }

    private sealed class ProbeHandler<TCommand, TResult>(Probe probe, Func<TResult> result)
        : IRequestHandler<TCommand, Response<TResult>> where TCommand : IRequest<Response<TResult>>
    {
        public Task<Response<TResult>> Handle(TCommand request, CancellationToken cancellationToken)
        {
            probe.Reached.Enqueue(typeof(TCommand).Name);
            return Task.FromResult(Response<TResult>.Success(result()));
        }
    }

    private sealed class NoAudit : IPlatformAuditForwarder
    {
        public Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class OnePolicy(ProductLegalEntityScopePolicy policy) : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(Guid globalProductId, CancellationToken cancellationToken = default) =>
            Task.FromResult(globalProductId == policy.GlobalProductId ? policy : null);
        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopePolicy?>(null);
        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(ProductLegalEntityScopePolicy item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(ProductLegalEntityScopePolicy item, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(IReadOnlyCollection<Guid> globalProductIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(globalProductIds.Where(id => id == policy.GlobalProductId).ToList());
    }
}
