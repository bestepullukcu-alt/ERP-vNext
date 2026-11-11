using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.SafetyStockService.Application;
using Diten.SafetyStockService.Domain;
using MongoDB.Driver;

namespace Diten.SafetyStockService.Persistence;

public sealed class ManualQuantityMongoStore(SafetyStockMongoContext context, IPolicyAuditAppender audit)
    : IManualQuantityStore
{
    public async Task<ManualQuantityResult> ExecuteAsync(ManualQuantityCommand command,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct)
    {
        if (command.PolicyId == Guid.Empty || command.PolicyRevisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 ||
            command.Operation == ManualQuantityOperation.Create &&
            (command.QuantityId != Guid.Empty || command.Quantity is null or < 0 ||
             command.EffectiveFrom is null || command.EffectiveUntil is null ||
             command.EffectiveFrom > command.EffectiveUntil || string.IsNullOrWhiteSpace(command.Reason)) ||
            command.Operation != ManualQuantityOperation.Create &&
            (command.QuantityId == Guid.Empty || command.ExpectedVersion is null or <= 0))
            return new(ManualQuantityStatus.Invalid);
        if (!scope.Contains(new SeriesKey(command.SkuId, command.WarehouseId)))
            return new(ManualQuantityStatus.NotFound);

        try
        {
            await EnsureIndexesAsync(ct);
            using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
            session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority, readPreference: ReadPreference.Primary));
            try
            {
                var fingerprint = Fingerprint(command);
                var prior = await context.ManualQuantityReceipts.Find(session, ReceiptFilter(command))
                    .FirstOrDefaultAsync(ct);
                if (prior is not null)
                {
                    await session.AbortTransactionAsync(ct);
                    return prior.Fingerprint == fingerprint &&
                        prior.PolicyId == command.PolicyId.ToString("D") &&
                        prior.PolicyRevisionId == command.PolicyRevisionId.ToString("D")
                        ? new(prior.Outcome == nameof(ManualQuantityStatus.Created) ?
                            ManualQuantityStatus.Created : ManualQuantityStatus.Updated, Receipt(prior))
                        : new(ManualQuantityStatus.Conflict);
                }

                var policy = await context.Policies.Find(session, Builders<PolicyDocument>.Filter.Where(p =>
                    p.Id == command.PolicyId.ToString("D") && p.TenantId == command.TenantId.ToString("D") &&
                    p.LegalEntityId == command.LegalEntityId.ToString("D") && !p.IsDeleted))
                    .FirstOrDefaultAsync(ct);
                if (policy is null || policy.SkuId != command.SkuId.ToString("D") ||
                    policy.WarehouseId != command.WarehouseId.ToString("D"))
                    return await AbortAsync(session, ManualQuantityStatus.NotFound, ct);
                if (policy.CurrentRevisionId != command.PolicyRevisionId.ToString("D"))
                    return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                var revision = await context.Revisions.Find(session, Builders<PolicyRevisionDocument>.Filter.Where(r =>
                    r.Id == policy.CurrentRevisionId && r.PolicyId == policy.Id &&
                    r.TenantId == policy.TenantId && r.LegalEntityId == policy.LegalEntityId && !r.IsDeleted))
                    .FirstOrDefaultAsync(ct);
                if (revision is null || revision.State != "Approved" ||
                    revision.SkuId != policy.SkuId || revision.WarehouseId != policy.WarehouseId)
                    return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                if (!await HasPolicyApprovalAsync(session, revision, ct))
                    return await AbortAsync(session, ManualQuantityStatus.Unavailable, ct);

                var now = DateTime.UtcNow;
                ManualQuantityDocument quantity;
                string before;
                if (command.Operation == ManualQuantityOperation.Create)
                {
                    quantity = new ManualQuantityDocument
                    {
                        Id = Guid.NewGuid().ToString("D"), TenantId = policy.TenantId,
                        LegalEntityId = policy.LegalEntityId, PolicyId = policy.Id,
                        PolicyRevisionId = revision.Id, SkuId = policy.SkuId,
                        WarehouseId = policy.WarehouseId, Quantity = command.Quantity!.Value,
                        BaseUomId = baseUomId,
                        EffectiveFromUtc = command.EffectiveFrom!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        EffectiveUntilUtc = command.EffectiveUntil!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        Reason = command.Reason!.Trim(), PreparedBy = command.ActorId.ToString("D"),
                        CreatedAtUtc = now, UpdatedAtUtc = now
                    };
                    before = string.Empty;
                    await context.ManualQuantities.InsertOneAsync(session, quantity, cancellationToken: ct);
                }
                else
                {
                    quantity = await context.ManualQuantities.Find(session,
                        QuantityFilter(command.TenantId, command.LegalEntityId) &
                        Builders<ManualQuantityDocument>.Filter.Eq(q => q.Id, command.QuantityId.ToString("D")))
                        .FirstOrDefaultAsync(ct) ?? new ManualQuantityDocument();
                    if (quantity.Id.Length == 0 || quantity.PolicyId != policy.Id ||
                        quantity.PolicyRevisionId != revision.Id || quantity.SkuId != policy.SkuId ||
                        quantity.WarehouseId != policy.WarehouseId)
                        return await AbortAsync(session, ManualQuantityStatus.NotFound, ct);
                    if (quantity.BaseUomId != baseUomId || quantity.Version != command.ExpectedVersion)
                        return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                    if (command.Operation == ManualQuantityOperation.Submit && quantity.State != "Draft" ||
                        command.Operation == ManualQuantityOperation.Approve && quantity.State != "InReview" ||
                        command.Operation == ManualQuantityOperation.Approve &&
                        (quantity.PreparedBy == command.ActorId.ToString("D") ||
                         quantity.SubmittedBy == command.ActorId.ToString("D")))
                        return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                    if (command.Operation == ManualQuantityOperation.Approve)
                    {
                        var overlap = Builders<ManualQuantityDocument>.Filter.Where(q =>
                            q.TenantId == policy.TenantId && q.LegalEntityId == policy.LegalEntityId &&
                            q.SkuId == policy.SkuId && q.WarehouseId == policy.WarehouseId &&
                            q.Id != quantity.Id && q.State == "Approved" && !q.IsDeleted &&
                            q.EffectiveFromUtc <= quantity.EffectiveUntilUtc &&
                            q.EffectiveUntilUtc >= quantity.EffectiveFromUtc);
                        if (await context.ManualQuantities.Find(session, overlap).AnyAsync(ct))
                            return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                    }
                    before = quantity.State;
                    var previousVersion = quantity.Version;
                    quantity.State = command.Operation == ManualQuantityOperation.Submit ? "InReview" : "Approved";
                    quantity.Version++;
                    quantity.UpdatedAtUtc = now;
                    if (command.Operation == ManualQuantityOperation.Submit)
                        quantity.SubmittedBy = command.ActorId.ToString("D");
                    if (command.Operation == ManualQuantityOperation.Approve)
                        quantity.ReviewedBy = command.ActorId.ToString("D");
                    var updated = await context.ManualQuantities.UpdateOneAsync(session,
                        QuantityFilter(command.TenantId, command.LegalEntityId) &
                        Builders<ManualQuantityDocument>.Filter.Eq(q => q.Id, quantity.Id) &
                        Builders<ManualQuantityDocument>.Filter.Eq(q => q.Version, previousVersion),
                        Builders<ManualQuantityDocument>.Update.Set(q => q.State, quantity.State)
                            .Set(q => q.Version, quantity.Version).Set(q => q.UpdatedAtUtc, now)
                            .Set(q => q.SubmittedBy, quantity.SubmittedBy)
                            .Set(q => q.ReviewedBy, quantity.ReviewedBy), cancellationToken: ct);
                    if (updated.MatchedCount != 1)
                        return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);
                }

                var policyLock = await context.Policies.UpdateOneAsync(session,
                    Builders<PolicyDocument>.Filter.Where(p => p.Id == policy.Id &&
                        p.TenantId == policy.TenantId && p.LegalEntityId == policy.LegalEntityId &&
                        p.Version == policy.Version && p.CurrentRevisionId == revision.Id && !p.IsDeleted),
                    Builders<PolicyDocument>.Update.Inc(p => p.Version, 1), cancellationToken: ct);
                if (policyLock.MatchedCount != 1)
                    return await AbortAsync(session, ManualQuantityStatus.Conflict, ct);

                var operation = command.Operation switch
                {
                    ManualQuantityOperation.Create => "safety-stock.manual-quantity-created",
                    ManualQuantityOperation.Submit => "safety-stock.manual-quantity-submitted",
                    _ => "safety-stock.manual-quantity-approved"
                };
                await audit.AppendAsync(session, new PolicyAuditDocument
                {
                    Id = Guid.NewGuid().ToString("D"), TenantId = policy.TenantId,
                    LegalEntityId = policy.LegalEntityId, PolicyId = policy.Id,
                    RevisionId = quantity.Id, ActorId = command.ActorId.ToString("D"),
                    Operation = operation, BeforeState = before, AfterState = quantity.State,
                    Reason = command.Operation == ManualQuantityOperation.Create ? quantity.Reason : null,
                    Quantity = quantity.Quantity, BaseUomId = quantity.BaseUomId,
                    EffectiveFromUtc = quantity.EffectiveFromUtc,
                    EffectiveUntilUtc = quantity.EffectiveUntilUtc,
                    OccurredAtUtc = now, CorrelationId = command.IdempotencyKey
                }, ct);
                var receipt = new ManualQuantityReceiptDocument
                {
                    Id = Guid.NewGuid().ToString("D"), TenantId = policy.TenantId,
                    ActorId = command.ActorId.ToString("D"), IdempotencyKey = command.IdempotencyKey,
                    Fingerprint = fingerprint, QuantityId = quantity.Id, PolicyId = policy.Id,
                    PolicyRevisionId = revision.Id, Quantity = quantity.Quantity, BaseUomId = quantity.BaseUomId,
                    EffectiveFromUtc = quantity.EffectiveFromUtc, EffectiveUntilUtc = quantity.EffectiveUntilUtc,
                    State = quantity.State, Version = quantity.Version,
                    Outcome = command.Operation == ManualQuantityOperation.Create ?
                        nameof(ManualQuantityStatus.Created) : nameof(ManualQuantityStatus.Updated)
                };
                await context.ManualQuantityReceipts.InsertOneAsync(session, receipt, cancellationToken: ct);
                await session.CommitTransactionAsync(ct);
                return new(command.Operation == ManualQuantityOperation.Create ?
                    ManualQuantityStatus.Created : ManualQuantityStatus.Updated, Receipt(receipt));
            }
            catch
            {
                if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        { return new(ManualQuantityStatus.Conflict); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { return new(ManualQuantityStatus.Conflict); }
        catch (Exception) { return new(ManualQuantityStatus.Unavailable); }
    }

    public async Task<ManualQuantityResult> ReadEffectiveAsync(EffectiveManualQuantityQuery query,
        ActorSeriesScope scope, string baseUomId, CancellationToken ct)
    {
        if (query.AsOf == default)
            return new(ManualQuantityStatus.Invalid);
        if (!scope.Contains(new SeriesKey(query.SkuId, query.WarehouseId)))
            return new(ManualQuantityStatus.NotFound);
        try
        {
            using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
            session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot,
                readPreference: ReadPreference.Primary));
            var date = query.AsOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var filter = QuantityFilter(query.TenantId, query.LegalEntityId) &
                Builders<ManualQuantityDocument>.Filter.Where(q => q.SkuId == query.SkuId.ToString("D") &&
                    q.WarehouseId == query.WarehouseId.ToString("D") && q.State == "Approved" &&
                    q.EffectiveFromUtc <= date && q.EffectiveUntilUtc >= date);
            var matches = await context.ManualQuantities.Find(session, filter).Limit(2).ToListAsync(ct);
            if (matches.Count == 0) return new(ManualQuantityStatus.NotFound);
            if (matches.Count != 1) return new(ManualQuantityStatus.Unavailable);
            var quantity = matches[0];
            var policy = await context.Policies.Find(session, Builders<PolicyDocument>.Filter.Where(p =>
                p.Id == quantity.PolicyId && p.TenantId == quantity.TenantId &&
                p.LegalEntityId == quantity.LegalEntityId && !p.IsDeleted)).FirstOrDefaultAsync(ct);
            var revision = await context.Revisions.Find(session, Builders<PolicyRevisionDocument>.Filter.Where(r =>
                r.Id == quantity.PolicyRevisionId && r.PolicyId == quantity.PolicyId &&
                r.TenantId == quantity.TenantId && r.LegalEntityId == quantity.LegalEntityId &&
                !r.IsDeleted)).FirstOrDefaultAsync(ct);
            var records = await context.Audit.Find(session,
                Builders<PolicyAuditDocument>.Filter.Where(a => a.TenantId == quantity.TenantId &&
                    a.LegalEntityId == quantity.LegalEntityId && a.PolicyId == quantity.PolicyId &&
                    a.RevisionId == quantity.Id && a.Operation.StartsWith("safety-stock.manual-quantity-")))
                .ToListAsync(ct);
            if (policy is null || revision is null || revision.State != "Approved" ||
                policy.CurrentRevisionId != revision.Id ||
                policy.SkuId != quantity.SkuId || policy.WarehouseId != quantity.WarehouseId ||
                revision.SkuId != quantity.SkuId || revision.WarehouseId != quantity.WarehouseId ||
                quantity.BaseUomId != baseUomId || quantity.Quantity < 0 ||
                quantity.SubmittedBy is null || quantity.ReviewedBy is null ||
                quantity.ReviewedBy == quantity.PreparedBy || quantity.ReviewedBy == quantity.SubmittedBy ||
                quantity.Version != 3 || records.Count != 3 ||
                !records.Any(a => a.Operation == "safety-stock.manual-quantity-created" &&
                    a.ActorId == quantity.PreparedBy && a.AfterState == "Draft" &&
                    a.Reason == quantity.Reason && a.Quantity == quantity.Quantity &&
                    a.BaseUomId == quantity.BaseUomId &&
                    a.EffectiveFromUtc == quantity.EffectiveFromUtc &&
                    a.EffectiveUntilUtc == quantity.EffectiveUntilUtc) ||
                !records.Any(a => a.Operation == "safety-stock.manual-quantity-submitted" &&
                    a.ActorId == quantity.SubmittedBy && a.BeforeState == "Draft" && a.AfterState == "InReview") ||
                !records.Any(a => a.Operation == "safety-stock.manual-quantity-approved" &&
                    a.ActorId == quantity.ReviewedBy && a.BeforeState == "InReview" && a.AfterState == "Approved"))
                return new(ManualQuantityStatus.Unavailable);
            if (!await HasPolicyApprovalAsync(session, revision, ct))
                return new(ManualQuantityStatus.Unavailable);
            return new(ManualQuantityStatus.Found, Receipt(quantity));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(ManualQuantityStatus.Unavailable); }
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        await context.ManualQuantityReceipts.Indexes.CreateOneAsync(new CreateIndexModel<ManualQuantityReceiptDocument>(
            Builders<ManualQuantityReceiptDocument>.IndexKeys.Ascending(r => r.TenantId)
                .Ascending(r => r.ActorId).Ascending(r => r.IdempotencyKey),
            new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await context.ManualQuantities.Indexes.CreateOneAsync(new CreateIndexModel<ManualQuantityDocument>(
            Builders<ManualQuantityDocument>.IndexKeys.Ascending(q => q.TenantId)
                .Ascending(q => q.LegalEntityId).Ascending(q => q.SkuId)
                .Ascending(q => q.WarehouseId).Ascending(q => q.EffectiveFromUtc)),
            cancellationToken: ct);
    }

    private async Task<bool> HasPolicyApprovalAsync(IClientSessionHandle session,
        PolicyRevisionDocument revision, CancellationToken ct)
    {
        if (revision.DecisionBy is null || revision.MaterialContributors.Contains(revision.DecisionBy))
            return false;
        var approval = Builders<PolicyAuditDocument>.Filter.Where(a =>
            a.TenantId == revision.TenantId && a.LegalEntityId == revision.LegalEntityId &&
            a.PolicyId == revision.PolicyId && a.RevisionId == revision.Id &&
            a.Operation == "Approve" && a.AfterState == "Approved" &&
            a.ActorId == revision.DecisionBy);
        return await context.Audit.Find(session, approval).AnyAsync(ct);
    }

    private static FilterDefinition<ManualQuantityDocument> QuantityFilter(Guid tenant, Guid company) =>
        Builders<ManualQuantityDocument>.Filter.Where(q => q.TenantId == tenant.ToString("D") &&
            q.LegalEntityId == company.ToString("D") && !q.IsDeleted);

    private static FilterDefinition<ManualQuantityReceiptDocument> ReceiptFilter(ManualQuantityCommand command) =>
        Builders<ManualQuantityReceiptDocument>.Filter.Where(r => r.TenantId == command.TenantId.ToString("D") &&
            r.ActorId == command.ActorId.ToString("D") && r.IdempotencyKey == command.IdempotencyKey);

    private static string Fingerprint(ManualQuantityCommand command) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(command with { IdempotencyKey = string.Empty }))));

    private static ManualQuantityReceipt Receipt(ManualQuantityDocument value) => new(
        Guid.Parse(value.Id), Guid.Parse(value.PolicyId), Guid.Parse(value.PolicyRevisionId),
        value.Quantity, value.BaseUomId, DateOnly.FromDateTime(value.EffectiveFromUtc),
        DateOnly.FromDateTime(value.EffectiveUntilUtc), value.State, value.Version);

    private static ManualQuantityReceipt Receipt(ManualQuantityReceiptDocument value) => new(
        Guid.Parse(value.QuantityId), Guid.Parse(value.PolicyId), Guid.Parse(value.PolicyRevisionId),
        value.Quantity, value.BaseUomId, DateOnly.FromDateTime(value.EffectiveFromUtc),
        DateOnly.FromDateTime(value.EffectiveUntilUtc), value.State, value.Version);

    private static async Task<ManualQuantityResult> AbortAsync(IClientSessionHandle session,
        ManualQuantityStatus status, CancellationToken ct)
    {
        await session.AbortTransactionAsync(ct);
        return new(status);
    }
}
