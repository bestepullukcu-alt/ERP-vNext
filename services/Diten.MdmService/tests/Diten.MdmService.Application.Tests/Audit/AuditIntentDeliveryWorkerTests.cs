using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.Audit;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuditIntentDeliveryWorkerTests
{
    [Fact]
    public async Task Worker_is_default_disabled_and_does_not_create_a_scope()
    {
        var scopes = new CountingScopeFactory();
        var worker = new AuditIntentDeliveryWorker(
            scopes,
            Options.Create(new AuditIntentDeliveryWorkerOptions()),
            Options.Create(new AuthTrustedSourceAuditServiceIdentityProviderOptions()),
            Options.Create(new TrustedSourceAuditIntentClientOptions()));

        await worker.StartAsync(CancellationToken.None);

        Assert.False(new AuditIntentDeliveryWorkerOptions().Enabled);
        Assert.Equal(0, scopes.Count);
    }

    [Fact]
    public void Defaults_are_bounded_but_activation_requires_explicit_lease_owner()
    {
        var options = new AuditIntentDeliveryWorkerOptions();
        Assert.InRange(options.TenantPageSize, 1, 100);
        Assert.InRange(options.BatchSize, 1, 100);
        Assert.InRange(options.LeaseSeconds, 10, 900);
        Assert.InRange(options.MaximumAttempts, 1, 20);
        Assert.True(string.IsNullOrEmpty(options.LeaseOwner));
    }

    [Fact]
    public async Task Enabled_invalid_configuration_fails_before_scope_or_mutation()
    {
        var scopes = new CountingScopeFactory();
        var worker = new AuditIntentDeliveryWorker(
            scopes,
            Options.Create(new AuditIntentDeliveryWorkerOptions { Enabled = true, LeaseOwner = string.Empty }),
            Options.Create(new AuthTrustedSourceAuditServiceIdentityProviderOptions()),
            Options.Create(new TrustedSourceAuditIntentClientOptions()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => worker.StartAsync(CancellationToken.None));

        Assert.Equal("AUDIT_INTENT_DELIVERY_WORKER_CONFIGURATION_INVALID", error.Message);
        Assert.Equal(0, scopes.Count);
    }

    [Fact]
    public async Task Run_cycle_isolates_A_B_A_tenant_scopes_and_disposes_every_scope()
    {
        var tenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var tenantB = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var script = new WorkerScript([tenantA, tenantB], [tenantA]);
        using var provider = BuildProvider(script);
        var scopes = new TrackingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        var worker = CreateEnabledWorker(scopes);

        await RunCycleAsync(worker, CancellationToken.None);
        await RunCycleAsync(worker, CancellationToken.None);

        Assert.Equal([tenantA, tenantB, tenantA], script.ObservedTenants);
        Assert.Equal(5, scopes.Created);
        Assert.Equal(scopes.Created, scopes.Disposed);
    }

    [Fact]
    public async Task Run_cycle_disposes_tenant_and_discovery_scopes_on_exception_and_cancellation()
    {
        var tenant = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var failing = new WorkerScript([tenant]) { ThrowForTenant = tenant };
        using (var provider = BuildProvider(failing))
        {
            var scopes = new TrackingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunCycleAsync(CreateEnabledWorker(scopes), CancellationToken.None));
            Assert.Equal(scopes.Created, scopes.Disposed);
        }

        var blocking = new WorkerScript([tenant]) { BlockForTenant = tenant };
        using (var provider = BuildProvider(blocking))
        {
            var scopes = new TrackingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
            using var cancellation = new CancellationTokenSource();
            var run = RunCycleAsync(CreateEnabledWorker(scopes), cancellation.Token);
            await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.Equal(scopes.Created, scopes.Disposed);
        }
    }

    private static ServiceProvider BuildProvider(WorkerScript script)
    {
        var services = new ServiceCollection();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddSingleton<IAuditIntentTenantPartitionDiscovery>(script);
        services.AddScoped<IAuditIntentDeliveryRepository>(provider =>
            new ScopeRecordingRepository(provider.GetRequiredService<TenantContext>(), script));
        services.AddSingleton<ITrustedSourceAuditServiceIdentityProvider, NeverIdentityProvider>();
        services.AddSingleton<ITrustedSourceAuditIntentClient, NeverIntentClient>();
        services.AddScoped<AuditIntentDeliveryProcessor>();
        return services.BuildServiceProvider();
    }

    private static AuditIntentDeliveryWorker CreateEnabledWorker(IServiceScopeFactory scopes) => new(
        scopes,
        Options.Create(new AuditIntentDeliveryWorkerOptions
        {
            Enabled = true,
            LeaseOwner = "g4-worker-test",
            PollIntervalSeconds = 1
        }),
        Options.Create(new AuthTrustedSourceAuditServiceIdentityProviderOptions
        {
            AuthBaseUrl = "http://localhost:5056",
            ExpectedIssuer = "Diten.Auth",
            ClientId = "mdm-worker",
            ActiveClientSecret = "test-secret"
        }),
        Options.Create(new TrustedSourceAuditIntentClientOptions { PlatformBaseUrl = "http://localhost:5057" }));

    private static Task RunCycleAsync(AuditIntentDeliveryWorker worker, CancellationToken cancellationToken)
    {
        var method = typeof(AuditIntentDeliveryWorker).GetMethod(
            "RunCycleAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RunCycleAsync seam is missing.");
        return (Task)(method.Invoke(worker, [cancellationToken])
            ?? throw new InvalidOperationException("RunCycleAsync returned no task."));
    }

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        public int Count { get; private set; }
        public IServiceScope CreateScope()
        {
            Count++;
            throw new InvalidOperationException("SCOPE_NOT_EXPECTED");
        }
    }

    private sealed class TrackingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public int Created { get; private set; }
        public int Disposed { get; private set; }

        public IServiceScope CreateScope()
        {
            Created++;
            return new TrackingScope(inner.CreateScope(), () => Disposed++);
        }

        private sealed class TrackingScope(IServiceScope inner, Action disposed) : IServiceScope
        {
            public IServiceProvider ServiceProvider => inner.ServiceProvider;
            public void Dispose()
            {
                inner.Dispose();
                disposed();
            }
        }
    }

    private sealed class WorkerScript(params Guid[][] tenantPages) : IAuditIntentTenantPartitionDiscovery
    {
        private readonly Queue<Guid[]> _pages = new(tenantPages);
        public List<Guid> ObservedTenants { get; } = [];
        public Guid? ThrowForTenant { get; init; }
        public Guid? BlockForTenant { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AuditIntentTenantPartitionPage> DiscoverAsync(
            Guid? afterTenantId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            Assert.Null(afterTenantId);
            var tenants = _pages.Count == 0 ? [] : _pages.Dequeue();
            return Task.FromResult(new AuditIntentTenantPartitionPage(tenants, null));
        }

        public async Task ObserveAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            ObservedTenants.Add(tenantId);
            if (ThrowForTenant == tenantId) throw new InvalidOperationException("INJECTED_TENANT_FAILURE");
            if (BlockForTenant == tenantId)
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }

    private sealed class ScopeRecordingRepository(TenantContext tenantContext, WorkerScript script)
        : IAuditIntentDeliveryRepository
    {
        public async Task<IReadOnlyList<AuditIntentWorkItem>> DiscoverEligibleAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            await script.ObserveAsync(tenantContext.TenantId, cancellationToken);
            return [];
        }

        public Task<AuditIntentClaim?> TryClaimAsync(AuditIntentLocator locator, long expectedClaimGeneration, string leaseOwner, TimeSpan leaseDuration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditIntentClaimedPayload?> ReadClaimedPayloadAsync(AuditIntentClaim claim, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkRetryableFailureAsync(AuditIntentClaim claim, TimeSpan retryDelay, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkDeadLetterAsync(AuditIntentClaim claim, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkDeliveredAsync(AuditIntentClaim claim, AuditIntentAcknowledgement acknowledgement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AcknowledgeAndCompactAsync(AuditIntentClaim claim, AuditIntentAcknowledgement acknowledgement, string compactReceiptReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CompactDeliveredAsync(AuditIntentClaim claim, string compactReceiptReference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NeverIdentityProvider : ITrustedSourceAuditServiceIdentityProvider
    {
        public Task<TrustedSourceAuditServiceIdentity> GetAsync(Guid tenantId, string audience, bool forceRefresh, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NeverIntentClient : ITrustedSourceAuditIntentClient
    {
        public Task<TrustedSourceAuditIntentDeliveryResult> AcceptAsync(TrustedSourceAuditIntentEnvelope envelope, TrustedSourceAuditServiceIdentity identity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
