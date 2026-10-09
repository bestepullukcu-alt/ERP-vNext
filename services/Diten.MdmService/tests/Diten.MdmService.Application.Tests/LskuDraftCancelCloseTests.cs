using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;
using Proxy = Diten.MdmService.Application.Tests.LskuIdentityWorkflowProcessorTests.RepositoryProxy;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX5 items 2, K3, K7 — the draft cancel's closing of a parked submit
/// (<see cref="LskuIdentityWorkflowProcessor.CloseForDraftCancelAsync"/>) on the paths HTTP cannot reach: a fake store
/// that keeps the lease (owner + generation) the way the real one does, so "the lease is given back" is measured.
/// </summary>
public sealed class LskuDraftCancelCloseTests
{
    // Item 2 (a): after the claim the re-read finds the operation gone, moved on, or in its withdrawal — the cancel
    // writes nothing and gives the lease back (409 LSKU_SUBMIT_IN_FLIGHT).
    [Theory]
    [InlineData("gone")]
    [InlineData("moved")]
    [InlineData("withdrawal")]
    public async Task Item02a_A_reread_that_does_not_match_gives_the_lease_back_and_writes_nothing(string reread)
    {
        var store = new LeaseStore(Parked(sentTicks: 0));
        store.Reread = op => reread switch
        {
            "gone" => null,
            "moved" => Copy(op, x => x.Checkpoint = LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown),
            _ => Copy(op, x =>
            {
                x.WithdrawalCommandId = Guid.NewGuid();
                x.WithdrawalFingerprint = new string('b', 64);
            })
        };

        var result = await Processor(store).CloseForDraftCancelAsync(store.Current, Canceller, "draft-cancel",
            TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal((false, "LSKU_SUBMIT_IN_FLIGHT", 409), result);
        Assert.Null(store.Current.LeaseOwner);
        Assert.Equal(0, store.Advances);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, store.Current.Checkpoint);
    }

    // Item 2 (b): inside the settle window the cancel keeps the submit for its maker; a keep-write the store refuses
    // (the lease taken over) is said as a concurrency conflict — and this claim's lease is not left held.
    [Fact]
    public async Task Item02b_A_refused_keep_write_is_a_concurrency_conflict_and_the_lease_is_not_held()
    {
        var store = new LeaseStore(Parked(sentTicks: Now.UtcTicks)) { RefuseAdvance = true };

        var result = await Processor(store).CloseForDraftCancelAsync(store.Current, Canceller, "draft-cancel",
            TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal((false, "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409), result);
        Assert.Equal(1, store.Advances);
        Assert.Null(store.Current.LeaseOwner);
    }

    // K3: whatever throws after the claim gives the lease back before it goes on.
    [Fact]
    public async Task K3_A_failure_after_the_claim_gives_the_lease_back()
    {
        var store = new LeaseStore(Parked(sentTicks: 0)) { Reread = _ => throw new IOException("store went away") };

        await Assert.ThrowsAsync<IOException>(() => Processor(store).CloseForDraftCancelAsync(store.Current, Canceller,
            "draft-cancel", TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Null(store.Current.LeaseOwner);
        Assert.Equal(1, store.Releases);
    }

    // K7 (CT X5): an operation at its retry limit whose cancel keeps it for its maker keeps the limit AND its cause.
    [Fact]
    public async Task K7_A_kept_operation_at_its_limit_keeps_the_cause()
    {
        var parked = Parked(sentTicks: Now.UtcTicks);
        parked.LastFailureCode = LskuIdentityWorkflowProcessor.RetryLimitReached;
        parked.RetryLimitCauseCode = "REFERENCE_PROVIDER_FORBIDDEN";
        var store = new LeaseStore(parked);

        var result = await Processor(store).CloseForDraftCancelAsync(store.Current, Canceller, "draft-cancel",
            TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal((false, "LSKU_SUBMIT_IN_FLIGHT", 409), result);
        Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, store.Current.LastFailureCode);
        Assert.Equal("REFERENCE_PROVIDER_FORBIDDEN", store.Current.RetryLimitCauseCode);
        Assert.Null(store.Current.LeaseOwner);
    }

    // ---------------------------------------------------------------------------------------------------- helpers

    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-0000000000f5");
    private static readonly Guid Canceller = Guid.Parse("60000000-0000-0000-0000-0000000000f5");

    private static LskuIdentityWorkflowOperation Parked(long sentTicks)
    {
        var operationId = Guid.NewGuid();
        var lsku = Guid.NewGuid();
        return new LskuIdentityWorkflowOperation
        {
            Id = operationId, TenantId = Tenant, OperationId = operationId, LskuId = lsku, GskuId = Guid.NewGuid(),
            ProductDefinitionRevisionId = Guid.NewGuid(), ExpectedLskuVersion = 0, MakerSubjectId = Guid.NewGuid(),
            ObjectType = "lsku", ObjectId = lsku.ToString("D"), ObjectRef = "LS-F5", MarketCode = "TR",
            StartIdempotencyKey = $"lsku-identity:{Tenant:D}:{operationId:D}", OperationFingerprint = new string('a', 64),
            Checkpoint = LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
            LastFailureCode = "WORKFLOW_DELEGATED_USER_FORBIDDEN", LastStartSentAtUtcTicksV1 = sentTicks,
            UpdatedAtUtcTicksV1 = Now.AddHours(-1).UtcTicks,
            TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion
        };
    }

    private static LskuIdentityWorkflowOperation Copy(LskuIdentityWorkflowOperation source,
        Action<LskuIdentityWorkflowOperation> change)
    {
        var copy = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<LskuIdentityWorkflowOperation>(
            MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(source));
        change(copy);
        return copy;
    }

    private static LskuIdentityWorkflowProcessor Processor(LeaseStore store)
    {
        T Unused<T>() where T : class
        {
            var proxy = DispatchProxy.Create<T, Proxy>();
            ((Proxy)(object)proxy).Handler = (method, _) =>
                throw new InvalidOperationException($"{typeof(T).Name}.{method.Name} must not be called here.");
            return proxy;
        }
        var lskus = DispatchProxy.Create<ILskuRepository, Proxy>();
        ((Proxy)(object)lskus).Handler = (method, args) => method.Name == nameof(ILskuRepository.GetByIdAsync)
            ? Task.FromResult<Lsku?>(new Lsku { Id = (Guid)args![0]!, TenantId = Tenant, Version = 0 })
            : throw new InvalidOperationException(method.Name);
        var clock = new FixedClock();
        return new LskuIdentityWorkflowProcessor(store.Repository, lskus, Unused<IGskuRepository>(),
            Unused<IProductDefinitionRevisionRepository>(), Unused<IWorkflowVerifiedMarketReferenceResolver>(),
            Unused<IProductIdentityWorkflowClient>(),
            new(new(Guid.NewGuid(), null, [Guid.NewGuid()], "LSKU_APPROVAL", true, true, null), clock), clock);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // The real store's lease semantics: a claim takes the lease (new generation); an advance or a release only acts on
    // this claim's own lease; a refused advance that was to release it gives it back (S2 / K3).
    private sealed class LeaseStore
    {
        public LeaseStore(LskuIdentityWorkflowOperation current)
        {
            Current = current;
            Reread = op => Copy(op, _ => { });
            Repository = DispatchProxy.Create<ILskuIdentityWorkflowOperationRepository, Proxy>();
            ((Proxy)(object)Repository).Handler = (method, args) => method.Name switch
            {
                nameof(ILskuIdentityWorkflowOperationRepository.TryClaimAsync) => Claim((LskuIdentityWorkflowClaimRequest)args![0]!),
                nameof(ILskuIdentityWorkflowOperationRepository.GetByOperationIdAsync) => Task.FromResult(Reread(Current)),
                nameof(ILskuIdentityWorkflowOperationRepository.AdvanceAsync) =>
                    Advance((LskuIdentityWorkflowClaim)args![0]!, (LskuIdentityWorkflowCheckpointMutation)args[1]!),
                nameof(ILskuIdentityWorkflowOperationRepository.ReleaseLeaseAsync) => Release((LskuIdentityWorkflowClaim)args![0]!),
                _ => throw new InvalidOperationException($"The store's {method.Name} must not be called here.")
            };
        }

        public ILskuIdentityWorkflowOperationRepository Repository { get; }
        public LskuIdentityWorkflowOperation Current { get; }
        public Func<LskuIdentityWorkflowOperation, LskuIdentityWorkflowOperation?> Reread { get; set; }
        public bool RefuseAdvance { get; init; }
        public int Advances { get; private set; }
        public int Releases { get; private set; }

        private Task<LskuIdentityWorkflowClaim?> Claim(LskuIdentityWorkflowClaimRequest request)
        {
            if (!request.EligibleCheckpoints.Contains(Current.Checkpoint) || Current.LeaseOwner is not null)
                return Task.FromResult<LskuIdentityWorkflowClaim?>(null);
            Current.LeaseGeneration++;
            Current.LeaseOwner = request.LeaseOwner;
            Current.LeaseUntilUtcTicksV1 = request.LeaseUntilUtcTicks;
            Current.UpdatedAtUtcTicksV1 = Now.UtcTicks;
            return Task.FromResult<LskuIdentityWorkflowClaim?>(new(
                Current.TenantId, Current.OperationId, Current.LskuId, Current.MarketCode,
                Current.DecisionTransitionLogId, Current.DecisionTransitionSequence, Current.OperationFingerprint,
                request.LeaseOwner, Current.LeaseGeneration, Current.Checkpoint, request.LeaseUntilUtcTicks));
        }

        private bool Owns(LskuIdentityWorkflowClaim claim) =>
            Current.LeaseOwner == claim.LeaseOwner && Current.LeaseGeneration == claim.LeaseGeneration;

        private Task<bool> Advance(LskuIdentityWorkflowClaim claim, LskuIdentityWorkflowCheckpointMutation mutation)
        {
            Advances++;
            if (RefuseAdvance || !Owns(claim) || Current.Checkpoint != claim.Checkpoint)
            {
                if (mutation.ReleaseLease) Release(claim);
                return Task.FromResult(false);
            }
            Current.Checkpoint = mutation.NextCheckpoint;
            Current.RecoveryDisposition = mutation.RecoveryDisposition;
            Current.LastFailureCode = mutation.LastFailureCode;
            Current.RetryLimitCauseCode = mutation.RetryLimitCauseCode;
            Current.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            Current.UpdatedAtUtcTicksV1 = mutation.UpdatedAtUtcTicks;
            if (mutation.ReleaseLease)
            {
                Current.LeaseOwner = null;
                Current.LeaseUntilUtcTicksV1 = null;
            }
            return Task.FromResult(true);
        }

        private Task<bool> Release(LskuIdentityWorkflowClaim claim)
        {
            Releases++;
            if (!Owns(claim)) return Task.FromResult(false);
            Current.LeaseOwner = null;
            Current.LeaseUntilUtcTicksV1 = null;
            return Task.FromResult(true);
        }
    }
}
