using Diten.BuildingBlocks.CentralAudit;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;

/// <summary>
/// MOD-0147 storage. Every query is scoped by Tenant + LE and hides soft-deleted rows. Create and submit (§0.5 stage 8) write their
/// state, receipt and central-audit event in one transaction, submit also its Pending event; a unique-index refusal is reported as a
/// Clash so the handler re-runs stage 5. After the commit was attempted nothing is written again: the receipt is read back once, and only a
/// commit that cannot be proven either way is Unknown.
/// </summary>
public sealed class EvaluationRepository(IMongoDatabase db, ISupplierPerformanceCommitProbe probe) : IEvaluationRepository
{
    public const string CreateOperation = "createSupplierEvaluation";
    public const string CreateTarget = "create";
    public const string SubmitOperation = "submitSupplierEvaluation";
    private IMongoCollection<SupplierEvaluation> Evaluations => db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations);
    private IMongoCollection<BsonDocument> Outbox => db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Outbox);
    private IMongoCollection<BsonDocument> Receipts => db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Receipts).WithReadConcern(ReadConcern.Majority);
    private static BsonDocument Scope(SupplierPerformanceScope s) { s.EnsureTrusted(); return new() { { "TenantId", s.TenantId.ToString() }, { "LegalEntityId", s.LegalEntityId.ToString() } }; }
    private static BsonDocument Visible(SupplierPerformanceScope s) { var f = Scope(s); f.Add("IsDeleted", false); return f; }
    private static BsonDocument Identity(SupplierPerformanceScope s, string operation, string target, string key)
    { var f = Scope(s); f.Add("Operation", operation); f.Add("TargetId", target); f.Add("IdempotencyKey", key); return f; }
    private static SupplierPerformanceFailure Unavailable() => new(503, "PERSISTENCE_UNAVAILABLE");

    public Task<EvaluationReceipt?> FindCreateReceiptAsync(SupplierPerformanceScope scope, string key, CancellationToken ct, bool afterClash = false) =>
        FindReceiptAsync(Identity(scope, CreateOperation, CreateTarget, key), afterClash, ct);

    public Task<EvaluationReceipt?> FindSubmitReceiptAsync(SupplierPerformanceScope scope, Guid evaluationId, string key, CancellationToken ct, bool afterClash = false) =>
        FindReceiptAsync(Identity(scope, SubmitOperation, evaluationId.ToString(), key), afterClash, ct);

    private async Task<EvaluationReceipt?> FindReceiptAsync(BsonDocument identity, bool afterClash, CancellationToken ct)
    {
        try
        {
            var receipts = afterClash ? Receipts.WithReadConcern(ReadConcern.Local) : Receipts;
            var r = await receipts.Find(identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
            return r is null ? null : new(r["Fingerprint"].AsString, r["StatusCode"].AsInt32, r["Body"].AsString);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { throw Unavailable(); }
    }

    public async Task<bool> ActivePeriodExistsAsync(SupplierPerformanceScope scope, string supplierId, string periodStart, string periodEnd, CancellationToken ct)
    {
        var f = Visible(scope); f.Add("SupplierId", supplierId); f.Add("PeriodStart", periodStart); f.Add("PeriodEnd", periodEnd); f.Add("IsActivePeriod", true);
        try { return await Evaluations.Find(f, new FindOptions { Collation = Collation.Simple }).AnyAsync(ct); }
        catch (Exception ex) when (ex is MongoException or TimeoutException) { throw Unavailable(); }
    }

    public Task<EvaluationCommitOutcome> CreateAsync(SupplierPerformanceScope scope, string key, string fingerprint, SupplierEvaluation evaluation, string responseBody, CancellationToken ct) =>
        CommitAsync(scope, Identity(scope, CreateOperation, CreateTarget, key), 201, fingerprint, responseBody, evaluation.Id, evaluation.Id.ToString(), evaluation.CorrelationId, async session =>
        {
            await Evaluations.InsertOneAsync(session, evaluation, cancellationToken: ct); await probe.AtAsync("entity", ct);
            return true;
        }, ct);

    public Task<EvaluationCommitOutcome> SubmitAsync(SupplierPerformanceScope scope, string key, string fingerprint, EvaluationSubmission s, string responseBody, CancellationToken ct) =>
        CommitAsync(scope, Identity(scope, SubmitOperation, s.EvaluationId.ToString(), key), 200, fingerprint, responseBody, s.EvaluationId, s.ScorecardId.ToString(), s.CorrelationId, async session =>
        {
            var draft = Visible(scope); draft.Add("_id", s.EvaluationId.ToString()); draft.Add("Status", nameof(EvaluationStatus.DRAFT)); draft.Add("Version", s.ExpectedVersion);
            var update = Builders<SupplierEvaluation>.Update.Set(x => x.Status, EvaluationStatus.PUBLISHED).Set(x => x.Version, s.ExpectedVersion + 1)
                .Set(x => x.ScorecardId, s.ScorecardId).Set(x => x.PublishedAt, s.PublishedAt).Set(x => x.PublishedBy, scope.ActorId).Set(x => x.PublishCorrelationId, s.CorrelationId)
                .Set(x => x.SubmittedAt, s.SubmittedAt).Set(x => x.SubmitComment, s.Comment).Set(x => x.UpdatedBy, scope.ActorId).Set(x => x.UpdatedAt, s.PublishedAt);
            var changed = await Evaluations.UpdateOneAsync(session, draft, update, cancellationToken: ct);
            if (changed.ModifiedCount != 1) return false;
            await probe.AtAsync("entity", ct);
            var pending = Scope(scope); pending.Add("_id", s.EventId.ToString()); pending.Add("EvaluationId", s.EvaluationId.ToString()); pending.Add("EventName", SupplierPerformanceOutboxStore.EventName);
            pending.Add("Status", "Pending"); pending.Add("AttemptCount", 0); pending.Add("OccurredAt", s.PublishedAt.UtcDateTime); pending.Add("NextAttemptAt", s.PublishedAt.UtcDateTime);
            pending.Add("CorrelationId", s.CorrelationId.ToString()); pending.Add("CausationId", s.EvaluationId.ToString()); pending.Add("PayloadJson", s.EventPayload);
            await Outbox.InsertOneAsync(session, pending, cancellationToken: ct); await probe.AtAsync("outbox", ct);
            return true;
        }, ct);

    // The one transaction of §0.5 stage 8: the command's writes + receipt + central audit. commitId is unique to this request, so
    // only this request's own receipt can prove a commit whose answer was lost. Writes answering false abort it as a Conflict.
    private async Task<EvaluationCommitOutcome> CommitAsync(SupplierPerformanceScope scope, BsonDocument identity, int status, string fingerprint, string responseBody,
        Guid evaluationId, string commitId, Guid correlationId, Func<IClientSessionHandle, Task<bool>> writes, CancellationToken ct)
    {
        var receipt = identity.DeepClone().AsBsonDocument;
        receipt.Add("_id", Guid.NewGuid().ToString()); receipt.Add("Fingerprint", fingerprint); receipt.Add("StatusCode", status); receipt.Add("Body", responseBody);
        receipt.Add("ContractVersion", "v1"); receipt.Add("EvaluationId", evaluationId.ToString()); receipt.Add("CommitId", commitId); receipt.Add("CorrelationId", correlationId.ToString());
        receipt.Add("ActorId", scope.ActorId.ToString()); receipt.Add("CommittedAt", DateTime.UtcNow);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var commitAttempted = false;
            try
            {
                using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority, maxCommitTime: TimeSpan.FromSeconds(5)));
                try
                {
                    if (!await writes(session)) { await session.AbortTransactionAsync(CancellationToken.None); return EvaluationCommitOutcome.Conflict; }
                    await Receipts.InsertOneAsync(session, receipt, cancellationToken: ct); await probe.AtAsync("receipt", ct);
                    await CentralAuditTransaction.EnlistAsync(session, evaluationId, ct); await probe.AtAsync("beforeCommit", ct);
                    commitAttempted = true;
                    await probe.AtAsync("commit", ct);
                    for (var commit = 0; ; commit++)
                    {
                        // Not ct: a client that hangs up must not leave a committed evaluation whose central record is abandoned.
                        try { await session.CommitTransactionAsync(CancellationToken.None); break; }
                        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }
                    }
                    await probe.AtAsync("afterCommit", CancellationToken.None);
                    return EvaluationCommitOutcome.Committed;
                }
                catch { if (!commitAttempted && session.IsInTransaction) try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { } throw; }
            }
            catch (OperationCanceledException) when (!commitAttempted && ct.IsCancellationRequested) { throw; }
            // A receipt tuple or an active period already committed by another request.
            catch (MongoWriteException ex) when (!commitAttempted && ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return EvaluationCommitOutcome.Clash; }
            // The other request has not committed yet: try the same write again (Procurement is not asked again).
            catch (MongoException ex) when (!commitAttempted && ex.HasErrorLabel("TransientTransactionError")) { await Task.Delay(10 * (attempt + 1), ct); }
            catch (Exception) when (commitAttempted) { return await ResolveCommitAsync(identity, commitId); }
            catch (Exception ex) when (ex is MongoException or TimeoutException) { throw Unavailable(); }
        }
        return EvaluationCommitOutcome.Unknown;
    }

    // A commit whose answer was lost: this request's own receipt proves it committed, so the request succeeds and the shared
    // forwarder confirms its enlisted row. Bounded and independent of the caller's token; any failure here stays Unknown (503).
    private async Task<EvaluationCommitOutcome> ResolveCommitAsync(BsonDocument identity, string commitId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await probe.AtAsync("resolve", timeout.Token);
            var r = await Receipts.WithReadConcern(ReadConcern.Local).Find(identity, new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(timeout.Token);
            return r is not null && r["CommitId"].AsString == commitId ? EvaluationCommitOutcome.Committed : EvaluationCommitOutcome.Unknown;
        }
        catch (Exception) { return EvaluationCommitOutcome.Unknown; }
    }

    public async Task<SupplierEvaluation?> GetAsync(SupplierPerformanceScope scope, Guid evaluationId, CancellationToken ct)
    { var f = Visible(scope); f.Add("_id", evaluationId.ToString()); return await Evaluations.Find(f).FirstOrDefaultAsync(ct); }

    public async Task<(IReadOnlyList<SupplierEvaluation> Items, long Total)> ListAsync(SupplierPerformanceScope scope, EvaluationFilter filter, int page, int pageSize, CancellationToken ct)
    {
        var f = Visible(scope);
        if (filter.SupplierId is not null) f.Add("SupplierId", filter.SupplierId);
        if (filter.Status is not null) f.Add("Status", filter.Status.ToString());
        Period(f, filter.PeriodFrom, filter.PeriodTo);
        var options = new FindOptions { Collation = Collation.Simple };
        var total = await Evaluations.CountDocumentsAsync(f, new CountOptions { Collation = Collation.Simple }, ct);
        var skip = (long)(page - 1) * pageSize;
        if (skip >= total) return ([], total);
        var items = await Evaluations.Find(f, options).Sort(Order).Skip((int)skip).Limit(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<SupplierEvaluation?> GetPublishedAsync(SupplierPerformanceScope scope, Guid scorecardId, CancellationToken ct)
    { var f = Visible(scope); f.Add("ScorecardId", scorecardId.ToString()); f.Add("Status", nameof(EvaluationStatus.PUBLISHED)); return await Evaluations.Find(f).FirstOrDefaultAsync(ct); }

    public async Task<IReadOnlyList<SupplierEvaluation>> ListPublishedAsync(SupplierPerformanceScope scope, ScorecardFilter filter, CancellationToken ct)
    {
        var f = Visible(scope); f.Add("Status", nameof(EvaluationStatus.PUBLISHED));
        if (filter.SupplierId is not null) f.Add("SupplierId", filter.SupplierId);
        if (filter.RiskLevel is not null) f.Add("RiskLevel", filter.RiskLevel);
        Period(f, filter.PeriodFrom, filter.PeriodTo);
        return await Evaluations.Find(f, new FindOptions { Collation = Collation.Simple }).Sort(Order).ToListAsync(ct);
    }

    private static readonly SortDefinition<SupplierEvaluation> Order = new BsonDocument { { "PeriodStart", 1 }, { "SupplierId", 1 }, { "_id", 1 } };

    // ISO dates compare as text: an evaluation is inside [periodFrom, periodTo] when its whole period is.
    private static void Period(BsonDocument f, string? from, string? to)
    {
        if (from is not null) f.Add("PeriodStart", new BsonDocument("$gte", from));
        if (to is not null) f.Add("PeriodEnd", new BsonDocument("$lte", to));
    }
}
