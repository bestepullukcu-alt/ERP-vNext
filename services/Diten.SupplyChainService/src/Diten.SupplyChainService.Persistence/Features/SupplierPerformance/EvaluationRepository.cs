using Diten.BuildingBlocks.CentralAudit;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;

/// <summary>
/// MOD-0147 DL-1 storage. Every query is scoped by Tenant + LE and hides soft-deleted rows. Create (§0.5 stage 8) writes the
/// evaluation, its receipt and the central-audit event in one transaction; a unique-index refusal is reported as a Clash so the
/// handler re-runs stage 5, and a failure after the commit was attempted is Unknown — never written a second time here.
/// </summary>
public sealed class EvaluationRepository(IMongoDatabase db, ISupplierPerformanceCommitProbe probe) : IEvaluationRepository
{
    public const string CreateOperation = "createSupplierEvaluation";
    public const string CreateTarget = "create";
    private IMongoCollection<SupplierEvaluation> Evaluations => db.GetCollection<SupplierEvaluation>(SupplierPerformanceSchema.Evaluations);
    private IMongoCollection<BsonDocument> Receipts => db.GetCollection<BsonDocument>(SupplierPerformanceSchema.Receipts).WithReadConcern(ReadConcern.Majority);
    private static BsonDocument Scope(SupplierPerformanceScope s) { s.EnsureTrusted(); return new() { { "TenantId", s.TenantId.ToString() }, { "LegalEntityId", s.LegalEntityId.ToString() } }; }
    private static BsonDocument Visible(SupplierPerformanceScope s) { var f = Scope(s); f.Add("IsDeleted", false); return f; }
    private static BsonDocument Identity(SupplierPerformanceScope s, string key)
    { var f = Scope(s); f.Add("Operation", CreateOperation); f.Add("TargetId", CreateTarget); f.Add("IdempotencyKey", key); return f; }
    private static SupplierPerformanceFailure Unavailable() => new(503, "PERSISTENCE_UNAVAILABLE");

    public async Task<EvaluationReceipt?> FindCreateReceiptAsync(SupplierPerformanceScope scope, string key, CancellationToken ct, bool afterClash = false)
    {
        try
        {
            var receipts = afterClash ? Receipts.WithReadConcern(ReadConcern.Local) : Receipts;
            var r = await receipts.Find(Identity(scope, key), new FindOptions { Collation = Collation.Simple }).FirstOrDefaultAsync(ct);
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

    public async Task<EvaluationCommitOutcome> CreateAsync(SupplierPerformanceScope scope, string key, string fingerprint, SupplierEvaluation evaluation, string responseBody, CancellationToken ct)
    {
        var receipt = Identity(scope, key);
        receipt.Add("_id", Guid.NewGuid().ToString()); receipt.Add("Fingerprint", fingerprint); receipt.Add("StatusCode", 201); receipt.Add("Body", responseBody);
        receipt.Add("ContractVersion", "v1"); receipt.Add("EvaluationId", evaluation.Id.ToString()); receipt.Add("CorrelationId", evaluation.CorrelationId.ToString());
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
                    await Evaluations.InsertOneAsync(session, evaluation, cancellationToken: ct); await probe.AtAsync("entity", ct);
                    await Receipts.InsertOneAsync(session, receipt, cancellationToken: ct); await probe.AtAsync("receipt", ct);
                    await CentralAuditTransaction.EnlistAsync(session, evaluation.Id, ct); await probe.AtAsync("beforeCommit", ct);
                    commitAttempted = true;
                    await probe.AtAsync("commit", ct);
                    for (var commit = 0; ; commit++)
                    {
                        try { await session.CommitTransactionAsync(ct); break; }
                        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && commit < 2) { }
                    }
                    await probe.AtAsync("afterCommit", ct);
                    return EvaluationCommitOutcome.Committed;
                }
                catch { if (!commitAttempted && session.IsInTransaction) try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { } throw; }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            // A receipt tuple or an active period already committed by another request.
            catch (MongoWriteException ex) when (!commitAttempted && ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { return EvaluationCommitOutcome.Clash; }
            // The other request has not committed yet: try the same write again (Procurement is not asked again).
            catch (MongoException ex) when (!commitAttempted && ex.HasErrorLabel("TransientTransactionError")) { await Task.Delay(10 * (attempt + 1), ct); }
            catch (Exception) when (commitAttempted) { return EvaluationCommitOutcome.Unknown; }
            catch (Exception ex) when (ex is MongoException or TimeoutException) { throw Unavailable(); }
        }
        return EvaluationCommitOutcome.Unknown;
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
