using System.Diagnostics;
using System.Reflection;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;
using Proxy = Diten.MdmService.Application.Tests.FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX2 item 9 (and the "FR:93 lease / 2" rule): the correction, the retirement request and the
/// pair retirement claim through the shared bounded claim. A store that never answers is answered within HALF the lease
/// (measured, not only the code) with 503, and a claim the store may have written is given back. In FIX1 these three
/// still called TryClaimAsync with the caller's token: a silent store hung the request and kept the lease.
/// </summary>
public sealed class GskuBoundedClaimTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task A_correction_whose_claim_the_store_never_answers_is_answered_within_half_the_lease()
    {
        var operations = new SilentCorrectionStore();
        var processor = new GskuCorrectionWorkflowProcessor(operations, Unused<IGskuRepository>(),
            Unused<IProductDefinitionRevisionRepository>(), Unused<IProductIdentityWorkflowClient>(),
            Unused<Diten.MdmService.Application.Contracts.ReferenceData.IWorkflowVerifiedGskuReferenceResolver>(),
            new GskuCorrectionWorkflowStartRequestFactory(new(Guid.NewGuid(), null, [Guid.NewGuid()], "GSKU_CORRECTION",
                false, false, null, Guid.NewGuid(), null), TimeProvider.System), TimeProvider.System);

        var watch = Stopwatch.StartNew();
        var result = await processor.RecoverAsync(operations.Operation, "worker", new(Lease, TimeSpan.FromSeconds(1)), default);
        watch.Stop();

        AssertBounded(result.StatusCode, result.ErrorCode, watch.Elapsed, operations.Released);
    }

    [Fact]
    public async Task A_retirement_request_whose_claim_the_store_never_answers_is_answered_within_half_the_lease()
    {
        var operations = new SilentRetirementRequestStore();
        var processor = new GskuRetirementRequestWorkflowProcessor(operations, Unused<IGskuRepository>(),
            Unused<IProductDefinitionRevisionRepository>(), Unused<IProductIdentityWorkflowClient>(),
            new FirstGskuIdentityRetirementProcessor(new SilentPairRetirementStore(), Unused<IGskuRepository>(),
                Unused<IProductDefinitionRevisionRepository>(), TimeProvider.System),
            new GskuRetirementRequestWorkflowStartRequestFactory(new(Guid.NewGuid(), null, [Guid.NewGuid()],
                "GSKU_RETIREMENT", false, false, null, Guid.NewGuid(), null, Guid.NewGuid(), null), TimeProvider.System),
            TimeProvider.System);

        var watch = Stopwatch.StartNew();
        var result = await processor.RecoverAsync(operations.Operation, "worker", new(Lease, TimeSpan.FromSeconds(1)), default);
        watch.Stop();

        AssertBounded(result.StatusCode, result.ErrorCode, watch.Elapsed, operations.Released);
    }

    [Fact]
    public async Task A_pair_retirement_whose_claim_the_store_never_answers_is_answered_within_half_the_lease()
    {
        var operations = new SilentPairRetirementStore();
        var processor = new FirstGskuIdentityRetirementProcessor(operations, Unused<IGskuRepository>(),
            Unused<IProductDefinitionRevisionRepository>(), TimeProvider.System);

        var watch = Stopwatch.StartNew();
        var result = await processor.RecoverAsync(operations.Operation, "worker", Lease, default);
        watch.Stop();

        AssertBounded(result.StatusCode, result.ErrorCode, watch.Elapsed, operations.Released);
    }

    private static void AssertBounded(int status, string? code, TimeSpan elapsed, int released)
    {
        Assert.Equal((503, ProductIdentityWorkflowClaims.StoreUnavailable), (status, code));
        // Half the lease: not earlier (the store had its time) and well before the lease itself runs out.
        Assert.InRange(elapsed, Lease / 2 - TimeSpan.FromMilliseconds(50), Lease - TimeSpan.FromMilliseconds(150));
        Assert.Equal(1, released);
    }

    private static T Unused<T>() where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Handler = (method, _) => throw new NotSupportedException(method.Name);
        return proxy;
    }

    private static Task<TClaim?> Never<TClaim>(CancellationToken token) where TClaim : class =>
        Task.Delay(Timeout.Infinite, token).ContinueWith<TClaim?>(_ => null, CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    private sealed class SilentCorrectionStore : IGskuCorrectionWorkflowOperationRepository
    {
        public GskuCorrectionWorkflowOperation Operation { get; } = new()
        {
            OperationId = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationFingerprint = new string('a', 64),
            Checkpoint = GskuCorrectionWorkflowCheckpoint.AwaitingDecision
        };
        public int Released { get; private set; }
        public Task<GskuCorrectionWorkflowOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GskuCorrectionWorkflowOperation?>(Operation);
        public Task<GskuCorrectionWorkflowClaim?> TryClaimAsync(Guid id, string fingerprint,
            IReadOnlyCollection<GskuCorrectionWorkflowCheckpoint> checkpoints, string owner, long now, long until,
            CancellationToken ct = default) => Never<GskuCorrectionWorkflowClaim>(ct);
        public Task ReleaseIfOwnedAsync(Guid id, string owner, long until, CancellationToken ct = default)
        {
            Released++;
            return Task.CompletedTask;
        }
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? after, int limit,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuCorrectionWorkflowReserveResult> ReserveAsync(GskuCorrectionWorkflowOperation operation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AdvanceAsync(GskuCorrectionWorkflowClaim claim, GskuCorrectionWorkflowMutation mutation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuCorrectionWorkflowRecoverablePage> DiscoverRecoverableAsync(long now, int limit,
            Guid? after = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class SilentRetirementRequestStore : IGskuRetirementRequestOperationRepository
    {
        public GskuRetirementRequestOperation Operation { get; } = new()
        {
            OperationId = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationFingerprint = new string('a', 64),
            Checkpoint = GskuRetirementRequestCheckpoint.AwaitingDecision
        };
        public int Released { get; private set; }
        public Task<GskuRetirementRequestOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GskuRetirementRequestOperation?>(Operation);
        public Task<GskuRetirementRequestWorkflowClaim?> TryClaimAsync(Guid id, string fingerprint,
            IReadOnlyCollection<GskuRetirementRequestCheckpoint> checkpoints, string owner, long now, long until,
            CancellationToken ct = default) => Never<GskuRetirementRequestWorkflowClaim>(ct);
        public Task ReleaseIfOwnedAsync(Guid id, string owner, long until, CancellationToken ct = default)
        {
            Released++;
            return Task.CompletedTask;
        }
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? after, int limit,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuRetirementRequestWorkflowReserveResult> ReserveAsync(GskuRetirementRequestOperation operation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AdvanceAsync(GskuRetirementRequestWorkflowClaim claim, GskuRetirementRequestWorkflowMutation mutation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuRetirementRequestWorkflowRecoverablePage> DiscoverRecoverableAsync(long now, int limit,
            Guid? after = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class SilentPairRetirementStore : IFirstGskuIdentityRetirementOperationRepository
    {
        public FirstGskuIdentityRetirementOperation Operation { get; } = new()
        {
            OperationId = Guid.NewGuid(), TenantId = Guid.NewGuid(), OperationFingerprint = new string('a', 64),
            Checkpoint = FirstGskuIdentityRetirementCheckpoint.Prepared
        };
        public int Released { get; private set; }
        public Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<FirstGskuIdentityRetirementOperation?>(Operation);
        public Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(FirstGskuIdentityRetirementClaimRequest request,
            CancellationToken ct = default) => Never<FirstGskuIdentityRetirementClaim>(ct);
        public Task ReleaseIfOwnedAsync(Guid id, string owner, long until, CancellationToken ct = default)
        {
            Released++;
            return Task.CompletedTask;
        }
        public Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(FirstGskuIdentityRetirementOperation operation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AdvanceAsync(FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(long now, int limit,
            FirstGskuIdentityRetirementRecoveryCursor? after = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
