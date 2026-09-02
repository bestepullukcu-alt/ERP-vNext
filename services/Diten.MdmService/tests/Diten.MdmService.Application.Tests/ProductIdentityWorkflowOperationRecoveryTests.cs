using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.OrphanedOperationRecovery.Validators;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using System.Collections.Concurrent;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductIdentityWorkflowOperationRecoveryTests
{
    [Fact]
    public async Task Abandon_RequiresTwoExactTrustedNotFoundObservations_AndWritesDeterministicAudit()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);

        var response = await harness.Handler.Handle(harness.Command(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, harness.Client.LookupCalls);
        Assert.Equal(1, harness.Repository.RecoverCalls);
        var mutation = Assert.IsType<ProductIdentityWorkflowOperationRecoveryMutation>(harness.Repository.Mutation);
        Assert.Null(mutation.Successor);
        var audit = Assert.Single(mutation.AuditIntents);
        Assert.Equal(ProductAuditOperation.ProductIdentityWorkflowOperationAbandonedBeforeWorkflowStart, audit.Operation);
        Assert.Equal(audit.IntentId,
            ProductIdentityWorkflowOperationRecoveryAuditIntentFactory.Create(TenantId, mutation.ExpectedScope,
                mutation.Evidence).Single().IntentId);
    }

    [Fact]
    public async Task Supersede_DerivesStableSuccessorIdentityAndRequiresFamilySubmitPermission()
    {
        var denied = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Supersede, submit: false);
        var deniedResult = await denied.Handler.Handle(denied.Command(), default);
        Assert.False(deniedResult.IsSuccessful);
        Assert.Equal(403, deniedResult.StatusCode);
        Assert.Equal(0, denied.Client.LookupCalls);

        var allowed = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Supersede);
        var result = await allowed.Handler.Handle(allowed.Command(), default);
        Assert.True(result.IsSuccessful);
        var successor = Assert.IsType<ProductIdentityWorkflowOperationRecoverySuccessor>(
            allowed.Repository.Mutation!.Successor);
        Assert.NotEqual(OperationId, successor.OperationId);
        Assert.Equal(OperatorId, successor.MakerSubjectId);
        Assert.StartsWith($"global-product-identity:{TenantId:D}:", successor.StartIdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(64, successor.OperationFingerprint.Length);
    }

    [Fact]
    public async Task SameCommand_RevalidatesNotFound_ThenUsesPersistedEvidenceForExactReplay()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Supersede);
        var command = harness.Command();

        var first = await harness.Handler.Handle(command, default);
        var firstMutation = harness.Repository.Mutation!;
        var second = await harness.Handler.Handle(command, default);
        var replayMutation = harness.Repository.Mutation!;

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(4, harness.Client.LookupCalls);
        Assert.Equal(2, harness.Repository.RecoverCalls);
        Assert.Equal(firstMutation.Evidence, replayMutation.Evidence);
        Assert.Equal(firstMutation.Successor, replayMutation.Successor);
        Assert.Equal(firstMutation.AuditIntents.Single().IntentId,
            replayMutation.AuditIntents.Single().IntentId);
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowOperationFamily.GlobalProduct)]
    [InlineData(ProductIdentityWorkflowOperationFamily.FirstGsku)]
    [InlineData(ProductIdentityWorkflowOperationFamily.Lsku)]
    [InlineData(ProductIdentityWorkflowOperationFamily.FinishedGood)]
    public async Task TrustedLookup_UsesExactTenantObjectMakerAndStartKeyForEveryFamily(
        ProductIdentityWorkflowOperationFamily family)
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon, family: family);

        var response = await harness.Handler.Handle(harness.Command(), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, harness.Client.Requests.Count);
        var candidate = harness.Repository.InitialCandidate;
        foreach (var lookup in harness.Client.Requests)
        {
            Assert.Equal(TenantId, lookup.TenantId);
            Assert.Equal(ExpectedObjectType(family), lookup.Request.ExpectedObjectType);
            Assert.Equal(ExpectedObjectId(candidate.Scope), lookup.Request.ExpectedObjectId);
            Assert.Equal(MakerId, lookup.Request.ExpectedMakerSubjectId);
            Assert.Equal(candidate.StartIdempotencyKey, lookup.Request.IdempotencyKey);
        }
    }

    [Fact]
    public async Task TerminalCommandPayloadDrift_FailsBeforeProviderAndPersistenceReplay()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);
        var command = harness.Command();
        Assert.True((await harness.Handler.Handle(command, default)).IsSuccessful);
        var lookups = harness.Client.LookupCalls;
        var drift = command with { Request = command.Request with { ReasonCode = "DIFFERENT_REASON" } };

        var response = await harness.Handler.Handle(drift, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(lookups, harness.Client.LookupCalls);
        Assert.Equal(1, harness.Repository.RecoverCalls);
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Success, 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Incomplete, 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.NonTerminal, 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, 409)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected, 503)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, 503)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Retryable, 503)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Timeout, 504)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Invalid, 503)]
    public async Task NonAuthoritativeOrPositiveEvidence_FailsClosedWithoutMutation(
        ProductIdentityWorkflowTransportOutcome outcome, int status)
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);
        harness.Client.Outcomes.Enqueue(outcome == ProductIdentityWorkflowTransportOutcome.Success
            ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult())
            : ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(outcome, "PROVIDER_FAILURE"));

        var response = await harness.Handler.Handle(harness.Command(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(0, harness.Repository.RecoverCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonExactNotFound_FailsClosedWithoutMutation(bool carriesValue)
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);
        harness.Client.Outcomes.Enqueue(new(
            ProductIdentityWorkflowTransportOutcome.NotFound,
            carriesValue ? StartResult() : null,
            carriesValue ? "NOT_FOUND_NON_LEAKAGE" : "WRONG_NOT_FOUND_CODE"));

        var response = await harness.Handler.Handle(harness.Command(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(0, harness.Repository.RecoverCalls);
    }

    [Fact]
    public async Task ConcurrentIdenticalCommands_LoserUsesWinnerSnapshotForOneBoundedExactReplay()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Supersede,
            concurrentRace: true);
        var command = harness.Command();

        var responses = await Task.WhenAll(
            harness.Handler.Handle(command, default),
            harness.Handler.Handle(command, default));

        Assert.All(responses, response => Assert.True(response.IsSuccessful));
        Assert.Single(responses.Select(response => response.Data!.Successor!.OperationId).Distinct());
        Assert.Single(responses.Select(response => response.Data!.Successor!.TargetVersion).Distinct());
        Assert.Equal(4, harness.Client.LookupCalls);
        Assert.Equal(3, harness.Repository.RecoverCalls);
        Assert.Equal(2, harness.Repository.InitialWriteMutations.Count);
        Assert.NotEqual(harness.Repository.InitialWriteMutations[0].Evidence.RecoveredAtUtcTicksV1,
            harness.Repository.InitialWriteMutations[1].Evidence.RecoveredAtUtcTicksV1);
        var persisted = Assert.IsType<ProductIdentityWorkflowOperationPersistedRecoverySnapshot>(
            (await harness.Repository.GetCandidateAsync(OperationId))!.PersistedRecovery);
        var exactReplay = harness.Repository.Mutations.Last();
        Assert.Equal(persisted.RecoveredAtUtcTicksV1, exactReplay.Evidence.RecoveredAtUtcTicksV1);
        Assert.Equal(persisted.SuccessorOperationId, exactReplay.Successor!.OperationId);
        Assert.Equal(persisted.SuccessorOperationFingerprint, exactReplay.Successor.OperationFingerprint);
    }

    [Fact]
    public async Task ContradictorySecondObservation_FailsClosedImmediatelyBeforeMutation()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);
        harness.Client.Outcomes.Enqueue(NotFound());
        harness.Client.Outcomes.Enqueue(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>
            .Success(StartResult()));

        var response = await harness.Handler.Handle(harness.Command(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(2, harness.Client.LookupCalls);
        Assert.Equal(0, harness.Repository.RecoverCalls);
    }

    [Fact]
    public async Task CancellationFromTrustedLookup_Propagates()
    {
        var harness = Harness.Create(ProductIdentityWorkflowOperationRecoveryAction.Abandon);
        harness.Client.ThrowCancellation = true;
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Handler.Handle(harness.Command(), source.Token));
        Assert.Equal(0, harness.Repository.RecoverCalls);
    }

    [Fact]
    public void Validator_RejectsRevisionVersionForNonFirstGskuAndNonExactText()
    {
        var validator = new RecoverOrphanedProductIdentityWorkflowOperationValidator();
        var command = new RecoverOrphanedProductIdentityWorkflowOperationCommand(
            OperationId, CommandId,
            new(ProductIdentityWorkflowOperationRecoveryAction.Abandon, 0, new(0, 0), " reason ", null));
        Assert.False(validator.Validate(command).IsValid);
    }

    private sealed class Harness
    {
        private Harness(ProductIdentityWorkflowOperationRecoveryAction action, bool submit,
            ProductIdentityWorkflowOperationFamily family, bool concurrentRace)
        {
            Action = action;
            var permissions = new List<string> { ProductIdentityWorkflowOperationRecoveryPermissions.Recover };
            if (submit) permissions.Add(SubmitPermission(family));
            Actor = new(OperatorId, permissions);
            Repository = new(Candidate(family), concurrentRace);
            Client = new(concurrentRace);
            Handler = new(new Tenant(TenantId), Actor, Repository, Client,
                concurrentRace
                    ? new AdvancingTimeProvider(new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero))
                    : new FixedTimeProvider(new DateTimeOffset(2031, 2, 3, 4, 5, 6, TimeSpan.Zero)));
        }

        public ProductIdentityWorkflowOperationRecoveryAction Action { get; }
        public Actor Actor { get; }
        public Repository Repository { get; }
        public Client Client { get; }
        public RecoverOrphanedProductIdentityWorkflowOperationHandler Handler { get; }
        public static Harness Create(ProductIdentityWorkflowOperationRecoveryAction action, bool submit = true,
            ProductIdentityWorkflowOperationFamily family = ProductIdentityWorkflowOperationFamily.GlobalProduct,
            bool concurrentRace = false) => new(action, submit, family, concurrentRace);
        public RecoverOrphanedProductIdentityWorkflowOperationCommand Command() => new(
            OperationId, CommandId,
            new(Action, 0, ExpectedVersions(Repository.InitialCandidate.Scope), "OPERATOR_RECOVERY", "approved"));
    }

    private sealed class Repository : IProductIdentityWorkflowOperationRecoveryRepository
    {
        private readonly object _gate = new();
        private readonly bool _concurrentRace;
        private readonly TaskCompletionSource _freshWritesArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _freshWriteArrivals;
        public Repository(ProductIdentityWorkflowOperationRecoveryCandidate initialCandidate, bool concurrentRace)
        {
            InitialCandidate = initialCandidate;
            _concurrentRace = concurrentRace;
        }
        public ProductIdentityWorkflowOperationRecoveryCandidate InitialCandidate { get; }
        private int _recoverCalls;
        public int RecoverCalls => _recoverCalls;
        public ProductIdentityWorkflowOperationRecoveryMutation? Mutation { get; private set; }
        public List<ProductIdentityWorkflowOperationRecoveryMutation> Mutations { get; } = new();
        public List<ProductIdentityWorkflowOperationRecoveryMutation> InitialWriteMutations { get; } = new();
        private ProductIdentityWorkflowOperationRecoveryCandidate? _persisted;
        public Task<ProductIdentityWorkflowOperationRecoveryCandidate?> GetCandidateAsync(
            Guid operationId, CancellationToken cancellationToken = default)
        {
            if (operationId != OperationId)
                return Task.FromResult<ProductIdentityWorkflowOperationRecoveryCandidate?>(null);
            lock (_gate)
                return Task.FromResult<ProductIdentityWorkflowOperationRecoveryCandidate?>(_persisted ?? InitialCandidate);
        }
        public async Task<ProductIdentityWorkflowOperationRecoveryWriteResult> RecoverAsync(
            ProductIdentityWorkflowOperationRecoveryMutation mutation,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _recoverCalls);
            if (_concurrentRace && Volatile.Read(ref _persisted) is null)
            {
                lock (_gate) InitialWriteMutations.Add(mutation);
                if (Interlocked.Increment(ref _freshWriteArrivals) == 2) _freshWritesArrived.TrySetResult();
                await _freshWritesArrived.Task.WaitAsync(cancellationToken);
            }
            lock (_gate)
            {
                Mutation = mutation;
                Mutations.Add(mutation);
                if (_persisted is not null)
                {
                    if (_concurrentRace && Mutations.Count == 2)
                        return Write(ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict, mutation);
                    return Write(ProductIdentityWorkflowOperationRecoveryWriteStatus.ExactReplay, mutation);
                }
                Persist(mutation);
                return Write(ProductIdentityWorkflowOperationRecoveryWriteStatus.Applied, mutation);
            }
        }

        private void Persist(ProductIdentityWorkflowOperationRecoveryMutation mutation)
        {
            var evidence = mutation.Evidence;
            _persisted = new ProductIdentityWorkflowOperationRecoveryCandidate(
                mutation.OperationId, mutation.ExpectedOperationVersion + 1, mutation.ExpectedScope,
                mutation.ExpectedOriginalMakerSubjectId, mutation.ExpectedStartIdempotencyKey,
                mutation.ExpectedOperationFingerprint, false, false, evidence.Disposition, null, null,
                mutation.ExpectedLeaseGeneration,
                new(evidence.Disposition, evidence.CommandId, evidence.OperatorSubjectId,
                    evidence.ReasonCode, evidence.Comment, evidence.WorkflowNotFoundEvidenceId,
                    evidence.WorkflowNotFoundEvidenceFingerprint,
                    evidence.WorkflowNotFoundObservedAtUtcTicksV1, evidence.RecoveredAtUtcTicksV1,
                    mutation.Successor?.OperationId, mutation.Successor?.StartIdempotencyKey,
                    mutation.Successor?.OperationFingerprint));
        }

        private static ProductIdentityWorkflowOperationRecoveryWriteResult Write(
            ProductIdentityWorkflowOperationRecoveryWriteStatus status,
            ProductIdentityWorkflowOperationRecoveryMutation mutation) => new(
                status, mutation.OperationId, IncrementScope(mutation.ExpectedScope),
                mutation.Evidence.Disposition, mutation.ExpectedOperationVersion + 1, mutation.Successor,
                status == ProductIdentityWorkflowOperationRecoveryWriteStatus.Conflict ? "LOST_CAS" : null);
    }

    private sealed class Client : IProductIdentityWorkflowClient
    {
        private readonly bool _barrier;
        private readonly TaskCompletionSource _firstObservations = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Client(bool barrier = false) => _barrier = barrier;
        public Queue<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> Outcomes { get; } = new();
        public ConcurrentQueue<(Guid TenantId, ProductIdentityWorkflowStartResultRequest Request)> Requests { get; } = new();
        private int _lookupCalls;
        public int LookupCalls => _lookupCalls;
        public bool ThrowCancellation { get; set; }
        public async Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _lookupCalls);
            Requests.Enqueue((tenantId, request));
            if (ThrowCancellation) throw new OperationCanceledException(cancellationToken);
            if (_barrier && call <= 2)
            {
                if (call == 2) _firstObservations.TrySetResult();
                await _firstObservations.Task.WaitAsync(cancellationToken);
            }
            return Outcomes.Count == 0 ? NotFound() : Outcomes.Dequeue();
        }
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Start is forbidden.");
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Transition lookup is forbidden.");
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class Actor(Guid subjectId, IReadOnlyCollection<string> permissions)
        : IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid resolved) { resolved = subjectId; return subjectId != Guid.Empty; }
        public bool HasPermission(string permission) => permissions.Contains(permission, StringComparer.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private long _ticks = now.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Add(ref _ticks, TimeSpan.TicksPerSecond),
            TimeSpan.Zero);
    }

    private static ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> NotFound() =>
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
            ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND_NON_LEAKAGE");
    private static ProductIdentityWorkflowStartResult StartResult() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "GP", "Running", "Stage", "Step", DateTimeOffset.UtcNow, null, false, null);

    private static ProductIdentityWorkflowOperationRecoveryCandidate Candidate(
        ProductIdentityWorkflowOperationFamily family)
    {
        ProductIdentityWorkflowOperationRecoveryScope scope = family switch
        {
            ProductIdentityWorkflowOperationFamily.GlobalProduct =>
                new GlobalProductWorkflowRecoveryScope(ProductId, 0),
            ProductIdentityWorkflowOperationFamily.FirstGsku =>
                new FirstGskuWorkflowRecoveryScope(ProductId, RevisionId, GskuId, 0, 0),
            ProductIdentityWorkflowOperationFamily.Lsku =>
                new LskuWorkflowRecoveryScope(LskuId, GskuId, RevisionId, "TR", 0),
            ProductIdentityWorkflowOperationFamily.FinishedGood =>
                new FinishedGoodWorkflowRecoveryScope(FinishedGoodId, GskuId, RevisionId, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        return new(OperationId, 0, scope, MakerId,
            $"{StartKeyPrefix(family)}:{TenantId:D}:{OperationId:D}", new string('a', 64), true,
            false, ProductIdentityWorkflowRecoveryDisposition.None, null, null, 0);
    }

    private static ProductIdentityWorkflowTargetVersions ExpectedVersions(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => new(item.GlobalProductVersion),
            FirstGskuWorkflowRecoveryScope item => new(item.GskuVersion, item.ProductDefinitionRevisionVersion),
            LskuWorkflowRecoveryScope item => new(item.LskuVersion),
            FinishedGoodWorkflowRecoveryScope item => new(item.FinishedGoodVersion),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    private static ProductIdentityWorkflowOperationRecoveryScope IncrementScope(
        ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
        {
            GlobalProductWorkflowRecoveryScope item => new GlobalProductWorkflowRecoveryScope(
                item.GlobalProductId, item.GlobalProductVersion + 1),
            FirstGskuWorkflowRecoveryScope item => new FirstGskuWorkflowRecoveryScope(
                item.GlobalProductId, item.ProductDefinitionRevisionId, item.GskuId,
                item.GskuVersion + 1, item.ProductDefinitionRevisionVersion + 1),
            LskuWorkflowRecoveryScope item => new LskuWorkflowRecoveryScope(
                item.LskuId, item.GskuId, item.ProductDefinitionRevisionId, item.MarketCode,
                item.LskuVersion + 1),
            FinishedGoodWorkflowRecoveryScope item => new FinishedGoodWorkflowRecoveryScope(
                item.FinishedGoodId, item.GskuId, item.ProductDefinitionRevisionId,
                item.FinishedGoodVersion + 1),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };

    private static string ExpectedObjectType(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => ProductIdentityWorkflowStartRequestFactory.GlobalProductObjectType,
        ProductIdentityWorkflowOperationFamily.FirstGsku => FirstGskuIdentityWorkflowStartRequestFactory.ObjectType,
        ProductIdentityWorkflowOperationFamily.Lsku => LskuIdentityWorkflowStartRequestFactory.LskuObjectType,
        ProductIdentityWorkflowOperationFamily.FinishedGood => FinishedGoodIdentityWorkflowStartRequestFactory.FinishedGoodObjectType,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string ExpectedObjectId(ProductIdentityWorkflowOperationRecoveryScope scope) => scope switch
    {
        GlobalProductWorkflowRecoveryScope item => item.GlobalProductId.ToString("D"),
        FirstGskuWorkflowRecoveryScope item => item.GskuId.ToString("D"),
        LskuWorkflowRecoveryScope item => item.LskuId.ToString("D"),
        FinishedGoodWorkflowRecoveryScope item => item.FinishedGoodId.ToString("D"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static string StartKeyPrefix(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => "global-product-identity",
        ProductIdentityWorkflowOperationFamily.FirstGsku => "first-gsku-identity",
        ProductIdentityWorkflowOperationFamily.Lsku => "lsku-identity",
        ProductIdentityWorkflowOperationFamily.FinishedGood => "finished-good-identity",
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static string SubmitPermission(ProductIdentityWorkflowOperationFamily family) => family switch
    {
        ProductIdentityWorkflowOperationFamily.GlobalProduct => ProductIdentityLifecyclePermissions.GlobalProductSubmit,
        ProductIdentityWorkflowOperationFamily.FirstGsku => FirstGskuIdentityLifecyclePermissions.Submit,
        ProductIdentityWorkflowOperationFamily.Lsku => LskuIdentityLifecyclePermissions.Submit,
        ProductIdentityWorkflowOperationFamily.FinishedGood => FinishedGoodIdentityLifecyclePermissions.Submit,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid GskuId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid LskuId = Guid.Parse("20000000-0000-0000-0000-000000000004");
    private static readonly Guid FinishedGoodId = Guid.Parse("20000000-0000-0000-0000-000000000005");
    private static readonly Guid OperationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid CommandId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid MakerId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid OperatorId = Guid.Parse("60000000-0000-0000-0000-000000000001");
}
