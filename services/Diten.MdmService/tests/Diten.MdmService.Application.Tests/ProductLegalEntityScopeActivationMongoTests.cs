using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Application.Tests.Audit;
using MongoDB.Driver;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeActivationMongoTests
    : IClassFixture<AuditIntentTemporalMongoFixture>, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly string? _previousMongoConnection;

    public ProductLegalEntityScopeActivationMongoTests(AuditIntentTemporalMongoFixture fixture)
    {
        _previousMongoConnection = Environment.GetEnvironmentVariable("MDM_TEST_MONGO");
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", fixture.ReplicaConnectionString);
    }

    public void Dispose() =>
        Environment.SetEnvironmentVariable("MDM_TEST_MONGO", _previousMongoConnection);

    [Fact]
    public async Task Fence_exact_replay_is_idempotent_drift_denied_and_only_equal_stable_snapshots_quiesce()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        var created = Assert.IsType<ProductLegalEntityScopeRolloutState>((await repository.CreateAsync(state)).State);
        var fence = Fence(Guid.NewGuid(), Guid.NewGuid(), "OWNER_APPROVED");

        var first = await repository.AcquireFenceAsync(fence, created.Id, created.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        var replay = await repository.AcquireFenceAsync(Fence(fence.CommandId, fence.ActorId, fence.ReasonCode), created.Id, created.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        var drift = await repository.AcquireFenceAsync(Fence(fence.CommandId, fence.ActorId, "DRIFT"), created.Id, created.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        Assert.True(first.Acquired);
        Assert.Equal(first.Fence!.Token, replay.Fence!.Token);
        Assert.False(drift.Acquired);

        Assert.False(await repository.BindFenceSnapshotsAsync(
            first.Fence.Token, Snapshot('B', Now), Snapshot('D', Now)));
        var firstStable = Snapshot('B', Now);
        var secondStable = Snapshot('B', Now.AddTicks(1));
        Assert.True(await repository.BindFenceSnapshotsAsync(first.Fence.Token, firstStable, secondStable));
        var persisted = Assert.IsType<ProductLegalEntityScopeRolloutState>(await repository.GetAsync());
        Assert.Equal(ProductLegalEntityScopeAdmissionState.Quiesced, persisted.ActiveFence!.State);
        Assert.Equal(secondStable.SnapshotHash, persisted.LastInventorySnapshot!.SnapshotHash);
    }

    [Fact]
    public async Task Oversized_bound_snapshot_fails_before_rollout_mutation()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        var created = Assert.IsType<ProductLegalEntityScopeRolloutState>((await repository.CreateAsync(state)).State);
        var fence = Fence(Guid.NewGuid(), Guid.NewGuid(), "OWNER_APPROVED");
        var acquired = await repository.AcquireFenceAsync(fence, created.Id, created.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        var oversized = Snapshot('B');
        oversized.CanonicalPayload = new string('X', ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes) + "\n";

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => repository.BindFenceSnapshotsAsync(
            acquired.Fence!.Token, oversized, oversized));

        var persisted = Assert.IsType<ProductLegalEntityScopeRolloutState>(await repository.GetAsync());
        Assert.Equal(ProductLegalEntityScopeAdmissionState.Closing, persisted.ActiveFence!.State);
        Assert.Null(persisted.LastInventorySnapshot);
    }

    [Fact]
    public async Task Commit_before_response_ambiguity_recovers_exact_transition_and_receipt_replay_is_single_cardinality()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var actorId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        var created = Assert.IsType<ProductLegalEntityScopeRolloutState>((await repository.CreateAsync(state)).State);
        var fence = Fence(commandId, actorId, "OWNER_APPROVED");
        var acquired = await repository.AcquireFenceAsync(
            fence, created.Id, created.Version, ProductLegalEntityScopeRolloutMode.Preparation);
        var snapshot = Snapshot('A');
        Assert.True(await repository.BindFenceSnapshotsAsync(
            acquired.Fence!.Token, snapshot, snapshot));

        var requested = Assert.IsType<ProductLegalEntityScopeRolloutState>(await repository.GetAsync());
        requested.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        requested.Version = 1;
        requested.UpdatedAt = Now.AddMinutes(1);
        requested.LastTransitionCommandId = commandId;
        requested.LastTransitionActorId = actorId;
        requested.LastTransitionAction = "ActivateEnforced";
        requested.LastTransitionReasonCode = fence.ReasonCode;
        requested.LastTransitionEvidenceHash = snapshot.SnapshotHash;
        var intent = ProductLegalEntityScopeAuditIntentFactory.Create(
            requested,
            ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated,
            0,
            1,
            commandId,
            actorId,
            snapshot.SnapshotHash,
            requested.UpdatedAt.Value);
        requested.LastTransitionIntentId = intent.IntentId;
        requested.AuditIntents.Add(intent);

        var ambiguousAfterCommit = RolloutRepositoryWithUpdateExecutor(scope, async operation =>
        {
            _ = await operation();
            throw ProductLegalEntityScopeMongoTests.CreateAmbiguousException(0);
        });
        var recovered = await ambiguousAfterCommit.CommitTransitionAsync(
            acquired.Fence.Token, requested, 0);

        Assert.True(recovered.Succeeded);
        Assert.False(recovered.WriteOutcomeAmbiguous);
        var stored = Assert.IsType<ProductLegalEntityScopeRolloutState>(await repository.GetAsync());
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, stored.Mode);
        Assert.Equal(1, stored.Version);
        Assert.Null(stored.ActiveFence);
        Assert.Equal(commandId, stored.LastTransitionCommandId);
        Assert.Equal(intent.IntentId, Assert.Single(stored.AuditIntents).IntentId);
        Assert.Empty(stored.AuditIntentReceipts);

        var delivery = new AuditIntentDeliveryRepository(
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            TimeProvider.System);
        var item = Assert.Single(await delivery.DiscoverEligibleAsync(10), candidate =>
            candidate.Locator.AggregateType == AuditAggregateType.ProductLegalEntityScopeRolloutState
            && candidate.Locator.AggregateId == stored.Id
            && candidate.Locator.IntentId == intent.IntentId);
        var claim = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
            item.Locator, item.ClaimGeneration, "h1b-activation-test", TimeSpan.FromMinutes(5)));
        var acknowledgedAt = DateTimeOffset.UtcNow;
        const string contractVersion = "owner-approved-contract-test-v1";
        var acknowledgement = new AuditIntentAcknowledgement(
            "durable-outbox-accepted",
            AuditIntentContract.BuildCentralIdempotencyKey(
                claim.Locator.TenantId, claim.Locator.IntentId, contractVersion),
            contractVersion,
            acknowledgedAt);
        Assert.True(await delivery.AcknowledgeAndCompactAsync(
            claim, acknowledgement, "h1b-activation-receipt"));
        Assert.True(await delivery.AcknowledgeAndCompactAsync(
            claim, acknowledgement, "h1b-activation-receipt"));

        var afterReceiptReplay = Assert.IsType<ProductLegalEntityScopeRolloutState>(await repository.GetAsync());
        Assert.Equal(1, afterReceiptReplay.Version);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, afterReceiptReplay.Mode);
        Assert.Empty(afterReceiptReplay.AuditIntents);
        Assert.Equal(intent.IntentId, Assert.Single(afterReceiptReplay.AuditIntentReceipts).IntentId);
    }

    private static ProductLegalEntityScopeActivationFence Fence(Guid command, Guid actor, string reason) => new()
    {
        Token = new string('F', 64), State = ProductLegalEntityScopeAdmissionState.Closing,
        Action = "ActivateEnforced", CommandId = command, ActorId = actor,
        ReasonCode = reason, AcquiredAtUtc = Now
    };

    private static ProductLegalEntityScopeInventorySnapshot Snapshot(char marker, DateTimeOffset? observedAt = null)
    {
        var observed = observedAt ?? Now;
        var payload = $"schemaVersion=1\nobservedUtcTicks={observed.UtcTicks}\nmarker={marker}\n";
        var stablePayload = $"schemaVersion=1\nmarker={marker}\n";
        return new ProductLegalEntityScopeInventorySnapshot
        {
            ObservedAtUtc = observed,
            CanonicalPayload = payload,
            StableFactsHash = Sha(stablePayload),
            SnapshotHash = Sha(payload)
        };
    }

    private static string Sha(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ProductLegalEntityScopeRolloutStateRepository RolloutRepositoryWithUpdateExecutor(
        ProductScopeMongoScope scope,
        Func<Func<Task<ProductLegalEntityScopeRolloutState?>>, Task<ProductLegalEntityScopeRolloutState?>> updateExecutor)
    {
        var constructor = typeof(ProductLegalEntityScopeRolloutStateRepository)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 4);
        return (ProductLegalEntityScopeRolloutStateRepository)constructor.Invoke(
        [
            scope.Database,
            new ProductScopeMongoScope.TenantContext(scope.TenantId),
            (Func<Func<Task>, Task>)(operation => operation()),
            updateExecutor
        ]);
    }
}
