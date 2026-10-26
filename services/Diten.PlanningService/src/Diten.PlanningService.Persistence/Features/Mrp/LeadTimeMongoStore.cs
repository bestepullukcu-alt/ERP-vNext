using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.Mrp;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.Mrp;

public interface ILeadTimeAuditAppender
{
    Task AppendAsync(IClientSessionHandle session, LeadTimeAuditDocument audit, CancellationToken cancellationToken);
}

public sealed class MongoLeadTimeAuditAppender(LeadTimeMongoContext context) : ILeadTimeAuditAppender
{
    public Task AppendAsync(IClientSessionHandle session, LeadTimeAuditDocument audit, CancellationToken cancellationToken) =>
        context.Audit.InsertOneAsync(session, audit, cancellationToken: cancellationToken);
}

public sealed class LeadTimeMongoStore(LeadTimeMongoContext context, ILeadTimeAuditAppender audit)
    : ILeadTimeMutationStore
{
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public async Task<LeadTimeResult> ExecuteAsync(LeadTimeCommand command, CancellationToken cancellationToken)
    {
        if (!IsValid(command)) return new(LeadTimeStatus.Invalid);
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction(new TransactionOptions(
                readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority,
                readPreference: ReadPreference.Primary));
            try
            {
                var fingerprint = Fingerprint(command);
                var prior = await context.Receipts.Find(session, ReceiptFilter(command))
                    .FirstOrDefaultAsync(cancellationToken);
                if (prior is not null)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return Replay(prior, command, fingerprint);
                }

                var mutation = await MutateAsync(session, command, cancellationToken);
                if (mutation.Result.Receipt is null)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return mutation.Result;
                }

                try
                {
                    await audit.AppendAsync(session, mutation.Audit!, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(LeadTimeStatus.Unavailable);
                }

                var receipt = mutation.Result.Receipt;
                await context.Receipts.InsertOneAsync(session, new LeadTimeReceiptDocument
                {
                    Id = Guid.NewGuid().ToString("D"), TenantId = command.TenantId.ToString("D"),
                    ActorId = command.ActorId.ToString("D"), IdempotencyKey = command.IdempotencyKey,
                    Fingerprint = fingerprint, PolicyId = receipt.PolicyId.ToString("D"),
                    RevisionId = receipt.RevisionId.ToString("D"),
                    LegalEntityId = command.LegalEntityId.ToString("D"),
                    SkuId = command.SkuId.ToString("D"), WarehouseId = command.WarehouseId.ToString("D"),
                    State = receipt.State, Version = receipt.Version,
                    Outcome = mutation.Result.Status.ToString()
                }, cancellationToken: cancellationToken);
                await session.CommitTransactionAsync(cancellationToken);
                return mutation.Result;
            }
            catch
            {
                if (session.IsInTransaction)
                    await session.AbortTransactionAsync(CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            var prior = await context.Receipts.Find(ReceiptFilter(command)).FirstOrDefaultAsync(cancellationToken);
            return prior is null ? new(LeadTimeStatus.Conflict) : Replay(prior, command, Fingerprint(command));
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            return new(LeadTimeStatus.Conflict);
        }
        catch (Exception)
        {
            return new(LeadTimeStatus.Unavailable);
        }
    }

    private async Task<(LeadTimeResult Result, LeadTimeAuditDocument? Audit)> MutateAsync(
        IClientSessionHandle session, LeadTimeCommand command, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tenant = command.TenantId.ToString("D");
        var company = command.LegalEntityId.ToString("D");
        var sku = command.SkuId.ToString("D");
        var warehouse = command.WarehouseId.ToString("D");
        var actor = command.ActorId.ToString("D");
        LeadTimePolicyDocument policy;
        LeadTimeRevisionDocument revision;
        string previousState;

        if (command.Operation == LeadTimeOperation.Create)
        {
            var existing = await context.Policies.Find(session, Builders<LeadTimePolicyDocument>.Filter.Where(p =>
                p.TenantId == tenant && p.LegalEntityId == company && p.SkuId == sku &&
                p.WarehouseId == warehouse && !p.IsDeleted)).AnyAsync(ct);
            if (existing) return (new(LeadTimeStatus.Conflict), null);

            revision = NewRevision(command, [actor], now);
            policy = new LeadTimePolicyDocument
            {
                Id = Guid.NewGuid().ToString("D"), TenantId = tenant, LegalEntityId = company,
                SkuId = sku, WarehouseId = warehouse, CurrentRevisionId = revision.Id,
                Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now, Revisions = [revision]
            };
            await context.Policies.InsertOneAsync(session, policy, cancellationToken: ct);
            previousState = "";
        }
        else
        {
            var filter = Builders<LeadTimePolicyDocument>.Filter.Where(p =>
                p.Id == command.PolicyId.ToString("D") && p.TenantId == tenant &&
                p.LegalEntityId == company && p.SkuId == sku && p.WarehouseId == warehouse && !p.IsDeleted);
            policy = await context.Policies.Find(session, filter).FirstOrDefaultAsync(ct);
            if (policy is null) return (new(LeadTimeStatus.NotFound), null);
            if (policy.Version != command.ExpectedVersion) return (new(LeadTimeStatus.Conflict), null);
            if (policy.CurrentRevisionId != command.RevisionId.ToString("D"))
                return (new(LeadTimeStatus.NotFound), null);
            var current = policy.Revisions.SingleOrDefault(r => r.Id == policy.CurrentRevisionId);
            if (current is null) return (new(LeadTimeStatus.Unavailable), null);
            previousState = current.State;

            if (command.Operation == LeadTimeOperation.Revise)
            {
                if (current.Days == command.LeadTimeDays &&
                    current.EffectiveFromUtc == ToUtc(command.EffectiveFrom!.Value) &&
                    current.EffectiveUntilUtc == ToUtc(command.EffectiveUntil))
                    return (new(LeadTimeStatus.Conflict), null);
                revision = NewRevision(command, current.Contributors.Append(actor)
                    .Distinct(StringComparer.Ordinal).ToList(), now);
                policy.Revisions.Add(revision);
                policy.CurrentRevisionId = revision.Id;
            }
            else
            {
                if (command.Operation == LeadTimeOperation.Submit && current.State != "Draft" ||
                    command.Operation is LeadTimeOperation.Approve or LeadTimeOperation.Reject &&
                    current.State != "InReview")
                    return (new(LeadTimeStatus.Conflict), null);
                if (command.Operation is LeadTimeOperation.Approve or LeadTimeOperation.Reject &&
                    current.Contributors.Contains(actor))
                    return (new(LeadTimeStatus.Forbidden), null);
                revision = current;
                revision.State = command.Operation switch
                {
                    LeadTimeOperation.Submit => "InReview",
                    LeadTimeOperation.Approve => "Approved",
                    _ => "Rejected"
                };
                if (command.Operation is LeadTimeOperation.Approve or LeadTimeOperation.Reject)
                {
                    revision.ReviewerId = actor;
                    revision.RejectionReason = command.Operation == LeadTimeOperation.Reject ? command.Reason : null;
                }
            }

            var oldVersion = policy.Version;
            policy.Version++;
            policy.UpdatedAtUtc = now;
            var guarded = filter & Builders<LeadTimePolicyDocument>.Filter.Eq(p => p.Version, oldVersion)
                & Builders<LeadTimePolicyDocument>.Filter.Eq(p => p.CurrentRevisionId, command.RevisionId.ToString("D"));
            var updated = await context.Policies.ReplaceOneAsync(session, guarded, policy, cancellationToken: ct);
            if (updated.MatchedCount != 1) return (new(LeadTimeStatus.Conflict), null);
        }

        var receipt = new LeadTimeReceipt(Guid.Parse(policy.Id), Guid.Parse(revision.Id),
            revision.State, policy.Version);
        var result = new LeadTimeResult(command.Operation is LeadTimeOperation.Create or LeadTimeOperation.Revise
            ? LeadTimeStatus.Created : LeadTimeStatus.Updated, receipt);
        var auditRecord = new LeadTimeAuditDocument
        {
            Id = Guid.NewGuid().ToString("D"), TenantId = tenant, LegalEntityId = company,
            SkuId = sku, WarehouseId = warehouse, PolicyId = policy.Id, RevisionId = revision.Id,
            ActorId = actor, Action = command.Operation.ToString(), PreviousState = previousState,
            State = revision.State, Days = revision.Days, EffectiveFromUtc = revision.EffectiveFromUtc,
            EffectiveUntilUtc = revision.EffectiveUntilUtc, Reason = command.Reason,
            Version = policy.Version, AtUtc = now
        };
        return (result, auditRecord);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexesReady) return;
            await context.Policies.Indexes.CreateOneAsync(new CreateIndexModel<LeadTimePolicyDocument>(
                Builders<LeadTimePolicyDocument>.IndexKeys.Ascending(p => p.TenantId)
                    .Ascending(p => p.LegalEntityId).Ascending(p => p.SkuId).Ascending(p => p.WarehouseId),
                new CreateIndexOptions<LeadTimePolicyDocument>
                {
                    Unique = true,
                    PartialFilterExpression = Builders<LeadTimePolicyDocument>.Filter.Eq(p => p.IsDeleted, false)
                }), cancellationToken: ct);
            await context.Receipts.Indexes.CreateOneAsync(new CreateIndexModel<LeadTimeReceiptDocument>(
                Builders<LeadTimeReceiptDocument>.IndexKeys.Ascending(r => r.TenantId)
                    .Ascending(r => r.ActorId).Ascending(r => r.IdempotencyKey),
                new CreateIndexOptions { Unique = true }), cancellationToken: ct);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static LeadTimeRevisionDocument NewRevision(LeadTimeCommand command, List<string> contributors, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid().ToString("D"), Days = command.LeadTimeDays!.Value,
            EffectiveFromUtc = ToUtc(command.EffectiveFrom!.Value),
            EffectiveUntilUtc = ToUtc(command.EffectiveUntil),
            Contributors = contributors, CreatedAtUtc = now
        };

    private static bool IsValid(LeadTimeCommand c) =>
        c.TenantId != Guid.Empty && c.LegalEntityId != Guid.Empty && c.SkuId != Guid.Empty &&
        c.WarehouseId != Guid.Empty && c.ActorId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(c.IdempotencyKey) && c.IdempotencyKey.Length <= 128 &&
        (c.Operation == LeadTimeOperation.Create
            ? c.PolicyId == Guid.Empty && c.RevisionId == Guid.Empty && c.ExpectedVersion is null
            : c.PolicyId != Guid.Empty && c.RevisionId != Guid.Empty && c.ExpectedVersion > 0) &&
        (c.Operation is LeadTimeOperation.Create or LeadTimeOperation.Revise
            ? c.LeadTimeDays is > 0 and <= 3650 && c.EffectiveFrom is not null &&
              (c.EffectiveUntil is null || c.EffectiveUntil >= c.EffectiveFrom)
            : c.LeadTimeDays is null && c.EffectiveFrom is null && c.EffectiveUntil is null) &&
        (c.Operation == LeadTimeOperation.Reject ? !string.IsNullOrWhiteSpace(c.Reason) :
            string.IsNullOrWhiteSpace(c.Reason));

    private static DateTime ToUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static DateTime? ToUtc(DateOnly? day) => day is null ? null : ToUtc(day.Value);

    private static FilterDefinition<LeadTimeReceiptDocument> ReceiptFilter(LeadTimeCommand c) =>
        Builders<LeadTimeReceiptDocument>.Filter.Where(r => r.TenantId == c.TenantId.ToString("D") &&
            r.ActorId == c.ActorId.ToString("D") && r.IdempotencyKey == c.IdempotencyKey);

    private static string Fingerprint(LeadTimeCommand c) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c))));

    private static LeadTimeResult Replay(LeadTimeReceiptDocument prior, LeadTimeCommand command, string fingerprint)
    {
        if (prior.LegalEntityId != command.LegalEntityId.ToString("D") ||
            prior.SkuId != command.SkuId.ToString("D") ||
            prior.WarehouseId != command.WarehouseId.ToString("D"))
            return new(LeadTimeStatus.NotFound);
        if (prior.Fingerprint != fingerprint) return new(LeadTimeStatus.Conflict);
        return new(Enum.Parse<LeadTimeStatus>(prior.Outcome),
            new(Guid.Parse(prior.PolicyId), Guid.Parse(prior.RevisionId), prior.State, prior.Version));
    }
}
