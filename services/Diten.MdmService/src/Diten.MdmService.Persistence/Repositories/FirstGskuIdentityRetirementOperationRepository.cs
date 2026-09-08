using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FirstGskuIdentityRetirementOperationRepository
    : IFirstGskuIdentityRetirementOperationRepository
{
    public const string CollectionName = "mdm_first_gsku_identity_retirement_operations";
    private readonly IMongoCollection<FirstGskuIdentityRetirementOperation> _operations;
    private readonly Guid _tenantId;

    public FirstGskuIdentityRetirementOperationRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _operations = database.GetCollection<FirstGskuIdentityRetirementOperation>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(
        FirstGskuIdentityRetirementOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Valid(operation)) return new(false, false, null, "FIRST_GSKU_RETIREMENT_OPERATION_INVALID");
        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = FirstGskuIdentityRetirementCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = FirstGskuIdentityRetirementOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1 : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;
        try
        {
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(ActiveFilter & Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.OperationId, operation.OperationId),
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.GskuId, operation.GskuId)))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && ExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, existing, "FIRST_GSKU_RETIREMENT_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveFilter & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(
            x => x.OperationId, operationId)).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(
        FirstGskuIdentityRetirementClaimRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty || !Exact(request.OperationFingerprint, 256)
            || request.EligibleCheckpoints.Count == 0 || !Exact(request.LeaseOwner, 128)
            || request.NowUtcTicks <= 0 || request.LeaseUntilUtcTicks <= request.NowUtcTicks) return null;
        var filter = ActiveFilter
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(
                x => x.TemporalStorageVersion,
                FirstGskuIdentityRetirementOperation.CurrentTemporalStorageVersion)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.OperationId, request.OperationId)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.OperationFingerprint, request.OperationFingerprint)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.In(x => x.Checkpoint, request.EligibleCheckpoints)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var updated = await _operations.FindOneAndUpdateAsync(filter,
            Builders<FirstGskuIdentityRetirementOperation>.Update.Set(x => x.LeaseOwner, request.LeaseOwner)
                .Set(x => x.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
                .Set(x => x.UpdatedAtUtcTicksV1, request.NowUtcTicks)
                .Set(x => x.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
                .Inc(x => x.LeaseGeneration, 1).Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<FirstGskuIdentityRetirementOperation> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is null ? null : new(_tenantId, updated.OperationId, updated.OperationFingerprint,
            request.LeaseOwner, updated.LeaseGeneration, updated.Checkpoint, request.LeaseUntilUtcTicks);
    }

    public async Task<bool> AdvanceAsync(
        FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId || mutation.UpdatedAtUtcTicks <= 0
            || !Allowed(claim.Checkpoint, mutation)) return false;
        var filter = ActiveFilter
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.OperationId, claim.OperationId)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.Checkpoint, claim.Checkpoint)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.LeaseOwner, claim.LeaseOwner)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Gt(x => x.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<FirstGskuIdentityRetirementOperation>.Update
            .Set(x => x.Checkpoint, mutation.NextCheckpoint)
            .Set(x => x.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(x => x.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(x => x.LastFailureCode, mutation.LastFailureCode)
            .Set(x => x.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(x => x.UpdatedAt, new DateTimeOffset(mutation.UpdatedAtUtcTicks, TimeSpan.Zero))
            .Inc(x => x.Version, 1);
        if (mutation.ReleaseLease) update = update.Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null);
        return (await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public async Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = ActiveFilter
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(
                x => x.TemporalStorageVersion,
                FirstGskuIdentityRetirementOperation.CurrentTemporalStorageVersion)
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Nin(x => x.Checkpoint,
                [FirstGskuIdentityRetirementCheckpoint.Completed, FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired])
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
            & Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<FirstGskuIdentityRetirementOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.Gt(x => x.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.And(
                        Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<FirstGskuIdentityRetirementOperation>.Filter.Gt(x => x.OperationId, after.OperationId)))
                : Builders<FirstGskuIdentityRetirementOperation>.Filter.Or(
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.Ne(x => x.NextAttemptAtUtcTicksV1, null),
                    Builders<FirstGskuIdentityRetirementOperation>.Filter.And(
                        Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                        Builders<FirstGskuIdentityRetirementOperation>.Filter.Gt(x => x.OperationId, after.OperationId)));
        var rows = await _operations.Find(filter).SortBy(x => x.NextAttemptAtUtcTicksV1)
            .ThenBy(x => x.OperationId).Limit(limit).ToListAsync(cancellationToken);
        return new(rows, rows.Count == limit ? new(rows[^1].NextAttemptAtUtcTicksV1, rows[^1].OperationId) : null);
    }

    private FilterDefinition<FirstGskuIdentityRetirementOperation> ActiveFilter =>
        Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.TenantId, _tenantId)
        & Builders<FirstGskuIdentityRetirementOperation>.Filter.Eq(x => x.IsDeleted, false);

    private bool Valid(FirstGskuIdentityRetirementOperation x) => _tenantId != Guid.Empty
        && x.OperationId != Guid.Empty && x.ProductDefinitionRevisionId != Guid.Empty && x.GskuId != Guid.Empty
        && x.ExpectedRevisionVersion >= 0 && x.ExpectedGskuVersion >= 0 && x.ActorSubjectId != Guid.Empty
        && Exact(x.OperationFingerprint, 256) && Exact(x.ReasonCode, 128)
        && x.TemporalStorageVersion is 0 or FirstGskuIdentityRetirementOperation.CurrentTemporalStorageVersion;

    private static bool ExactReplay(FirstGskuIdentityRetirementOperation a, FirstGskuIdentityRetirementOperation b) =>
        a.OperationId == b.OperationId && a.OperationFingerprint == b.OperationFingerprint
        && a.ProductDefinitionRevisionId == b.ProductDefinitionRevisionId && a.GskuId == b.GskuId
        && a.ExpectedRevisionVersion == b.ExpectedRevisionVersion && a.ExpectedGskuVersion == b.ExpectedGskuVersion
        && a.ActorSubjectId == b.ActorSubjectId && a.ReasonCode == b.ReasonCode;

    private static bool Allowed(FirstGskuIdentityRetirementCheckpoint current, FirstGskuIdentityRetirementMutation mutation)
    {
        if (current is FirstGskuIdentityRetirementCheckpoint.Completed
            or FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired)
        {
            return false;
        }

        if (current == mutation.NextCheckpoint)
            return mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                   && mutation.NextAttemptAtUtcTicksV1 is > 0 && !string.IsNullOrWhiteSpace(mutation.LastFailureCode)
                   && mutation.ReleaseLease;
        return (current, mutation.NextCheckpoint) switch
        {
            (FirstGskuIdentityRetirementCheckpoint.Prepared, FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed) => true,
            (FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed, FirstGskuIdentityRetirementCheckpoint.ChildrenVerified) => true,
            (FirstGskuIdentityRetirementCheckpoint.ChildrenVerified, FirstGskuIdentityRetirementCheckpoint.GskuRetired) => true,
            (FirstGskuIdentityRetirementCheckpoint.GskuRetired, FirstGskuIdentityRetirementCheckpoint.RevisionRetired) => true,
            (FirstGskuIdentityRetirementCheckpoint.RevisionRetired, FirstGskuIdentityRetirementCheckpoint.Completed) => true,
            (_, FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };
    }

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value == value.Trim() && !value.Any(char.IsControl);

    private void EnsureIndexes() => _operations.Indexes.CreateMany([
        new CreateIndexModel<FirstGskuIdentityRetirementOperation>(
            Builders<FirstGskuIdentityRetirementOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_first_gsku_retirement_tenant_operation" }),
        new CreateIndexModel<FirstGskuIdentityRetirementOperation>(
            Builders<FirstGskuIdentityRetirementOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GskuId),
            new CreateIndexOptions { Unique = true, Name = "ux_mdm_first_gsku_retirement_tenant_gsku" }),
        new CreateIndexModel<FirstGskuIdentityRetirementOperation>(
            Builders<FirstGskuIdentityRetirementOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.NextAttemptAtUtcTicksV1).Ascending(x => x.OperationId),
            new CreateIndexOptions { Name = "ix_mdm_first_gsku_retirement_tenant_recovery" }),
        new CreateIndexModel<FirstGskuIdentityRetirementOperation>(
            Builders<FirstGskuIdentityRetirementOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Checkpoint).Ascending(x => x.LeaseUntilUtcTicksV1),
            new CreateIndexOptions { Name = "ix_mdm_first_gsku_retirement_tenant_checkpoint_lease" })
    ]);
}
