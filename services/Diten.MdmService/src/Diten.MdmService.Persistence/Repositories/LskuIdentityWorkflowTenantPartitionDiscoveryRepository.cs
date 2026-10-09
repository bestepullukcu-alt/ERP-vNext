using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LskuIdentityWorkflowTenantPartitionDiscoveryRepository
    : ILskuIdentityWorkflowTenantPartitionDiscovery
{
    private readonly IMongoCollection<LskuIdentityWorkflowOperation> _operations;

    public LskuIdentityWorkflowTenantPartitionDiscoveryRepository(IMongoDatabase database) =>
        _operations = database.GetCollection<LskuIdentityWorkflowOperation>(
            LskuIdentityWorkflowOperationRepository.CollectionName);

    public async Task<LskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || nowUtcTicks <= 0 || limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            LskuIdentityWorkflowCheckpoint.Completed,
            LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
        };
        var filter = Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterTenantId.HasValue)
        {
            filter &= Builders<LskuIdentityWorkflowOperation>.Filter.Gt(
                item => item.TenantId, afterTenantId.Value);
        }

        // Stream a bounded number of due rows in tenant order instead of grouping the entire
        // cross-tenant collection. A page may contain fewer tenants than requested when one tenant
        // owns many due operations; the tenant cursor still advances monotonically and the tenant-
        // scoped repository processes every operation for that partition.
        var candidates = await _operations.Find(filter)
            .SortBy(item => item.TenantId)
            .ThenBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Project(item => item.TenantId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var page = candidates.Distinct().ToArray();
        return new(page, candidates.Count == limit ? candidates[^1] : null);
    }

    private static FilterDefinition<LskuIdentityWorkflowOperation> Stuck =>
        Builders<LskuIdentityWorkflowOperation>.Filter.Ne(item => item.IsDeleted, true)
        & ((Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint,
                LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.RecoveryDisposition,
                ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired))
           | Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint,
               LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay)
           // FIX3 item 2: a wait at its limit (Retryable, RETRY_LIMIT_REACHED) — as the retirement gauge counts it.
           | (Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.RecoveryDisposition,
                  ProductIdentityWorkflowRecoveryDisposition.Retryable)
              & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.LastFailureCode,
                  Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.LskuIdentityWorkflowProcessor.RetryLimitReached)));

    // FIX3 item 2 (R3): a withdrawal whose cancel was left unanswered and that only its requester can send again —
    // parked for the maker (AMR) or waiting on its outcome (WOU) — older than this needs a person to chase it.
    public static readonly TimeSpan WithdrawalUnansweredThreshold = TimeSpan.FromHours(1);

    private static FilterDefinition<LskuIdentityWorkflowOperation> UnansweredWithdrawal(DateTimeOffset now)
    {
        var cutoff = (now - WithdrawalUnansweredThreshold).UtcTicks;
        return Builders<LskuIdentityWorkflowOperation>.Filter.Ne(item => item.IsDeleted, true)
            & Builders<LskuIdentityWorkflowOperation>.Filter.Ne(item => item.WithdrawalCommandId, null)
            & Builders<LskuIdentityWorkflowOperation>.Filter.In(item => item.Checkpoint, new[]
            {
                LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
            })
            & (Builders<LskuIdentityWorkflowOperation>.Filter.Lt(item => item.WithdrawalUnansweredSinceUtcTicksV1, cutoff)
               // A document without the field ages from its last update (legacy rule: never Eq on a missing field).
               | (Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.WithdrawalUnansweredSinceUtcTicksV1, null)
                  & Builders<LskuIdentityWorkflowOperation>.Filter.Lt(item => item.UpdatedAtUtcTicksV1, cutoff)));
    }

    public Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(Stuck, cancellationToken: cancellationToken);

    public Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(
            (Builders<LskuIdentityWorkflowOperation>.Filter.Ne(item => item.IsDeleted, true)
             & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.Checkpoint,
                 LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
             & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.RecoveryDisposition,
                 ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
             & Builders<LskuIdentityWorkflowOperation>.Filter.Nin(item => item.LastFailureCode,
                 Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.LskuLifecycleStuckKinds
                     .IdentitySettledByTheRequestersRepeat))
            | UnansweredWithdrawal(DateTimeOffset.UtcNow),
            cancellationToken: cancellationToken);
}
