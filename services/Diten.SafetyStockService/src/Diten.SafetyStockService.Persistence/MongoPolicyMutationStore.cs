using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.SafetyStockService.Application;
using MongoDB.Driver;

namespace Diten.SafetyStockService.Persistence;

public sealed class MongoPolicyMutationStore : IPolicyMutationStore
{
    private readonly SafetyStockMongoContext _context;
    private readonly IPolicyAuditAppender _audit;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoPolicyMutationStore(SafetyStockMongoContext context, IPolicyAuditAppender audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<PolicyMutationResult> ExecuteAsync(PolicyMutationCommand command, ActorSeriesScope scope, CancellationToken cancellationToken)
    {
        if (!IsValid(command)) return new(PolicyMutationStatus.Invalid);

        try
        {
            await EnsureIndexesAsync(cancellationToken);
            using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction(new TransactionOptions(
                readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority, readPreference: ReadPreference.Primary));

            try
            {
                var fingerprint = Fingerprint(command);
                var prior = await _context.Receipts.Find(session, ReceiptFilter(command)).FirstOrDefaultAsync(cancellationToken);
                if (prior is not null)
                {
                    var allowed = await ScopeAllowsReplayAsync(prior, command.LegalEntityId, scope, cancellationToken);
                    await session.AbortTransactionAsync(cancellationToken);
                    return allowed ? Replay(prior, fingerprint) : new(PolicyMutationStatus.NotFound);
                }

                var mutation = await MutateAsync(session, command, scope, cancellationToken);
                if (mutation.Result.Receipt is null)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return mutation.Result;
                }

                try
                {
                    await _audit.AppendAsync(session, mutation.Audit!, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(PolicyMutationStatus.Unavailable);
                }

                var receipt = mutation.Result.Receipt;
                await _context.Receipts.InsertOneAsync(session, new PolicyCommandReceiptDocument
                {
                    Id = Guid.NewGuid().ToString("D"), TenantId = command.TenantId.ToString("D"),
                    ActorId = command.ActorId.ToString("D"), IdempotencyKey = command.IdempotencyKey,
                    Fingerprint = fingerprint, PolicyId = receipt.PolicyId.ToString("D"),
                    RevisionId = receipt.RevisionId.ToString("D"), State = receipt.State,
                    Version = receipt.Version, Outcome = mutation.Result.Status.ToString()
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
            var prior = await _context.Receipts.Find(ReceiptFilter(command)).FirstOrDefaultAsync(cancellationToken);
            if (prior is null) return new(PolicyMutationStatus.Conflict);
            return await ScopeAllowsReplayAsync(prior, command.LegalEntityId, scope, cancellationToken)
                ? Replay(prior, Fingerprint(command)) : new(PolicyMutationStatus.NotFound);
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            return new(PolicyMutationStatus.Conflict);
        }
        catch (Exception)
        {
            return new(PolicyMutationStatus.Unavailable);
        }
    }

    private async Task<(PolicyMutationResult Result, PolicyAuditDocument? Audit)> MutateAsync(
        IClientSessionHandle session, PolicyMutationCommand command, ActorSeriesScope scope, CancellationToken ct)
    {
        var tenant = command.TenantId.ToString("D");
        var company = command.LegalEntityId.ToString("D");
        var actor = command.ActorId.ToString("D");
        var now = DateTime.UtcNow;

        if (command.Operation == PolicyOperation.Create)
        {
            var seriesFilter = Builders<PolicyDocument>.Filter.Where(p => p.TenantId == tenant
                && p.LegalEntityId == company && p.SkuId == command.SkuId.ToString("D")
                && p.WarehouseId == command.WarehouseId.ToString("D") && !p.IsDeleted);
            if (await _context.Policies.Find(session, seriesFilter).AnyAsync(ct))
                return (new(PolicyMutationStatus.Conflict), null);

            var policyId = Guid.NewGuid();
            var revisionId = Guid.NewGuid();
            var policy = new PolicyDocument
            {
                Id = policyId.ToString("D"), TenantId = tenant, LegalEntityId = company,
                SkuId = command.SkuId.ToString("D"), WarehouseId = command.WarehouseId.ToString("D"),
                CurrentRevisionId = revisionId.ToString("D"), Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var revision = new PolicyRevisionDocument
            {
                Id = revisionId.ToString("D"), PolicyId = policy.Id, TenantId = tenant, LegalEntityId = company,
                SkuId = policy.SkuId, WarehouseId = policy.WarehouseId, CoverageDays = command.CoverageDays!.Value,
                Version = 1, PreparedBy = actor, MaterialContributors = [actor], CreatedAtUtc = now, UpdatedAtUtc = now
            };
            await _context.Policies.InsertOneAsync(session, policy, cancellationToken: ct);
            await _context.Revisions.InsertOneAsync(session, revision, cancellationToken: ct);
            var result = new PolicyMutationResult(PolicyMutationStatus.Created,
                new(policyId, revisionId, revision.State, revision.Version));
            return (result, Audit(command, result.Receipt!, string.Empty, revision.State, now));
        }

        var policyFilter = Builders<PolicyDocument>.Filter.Where(p => p.Id == command.PolicyId.ToString("D")
            && p.TenantId == tenant && p.LegalEntityId == company && !p.IsDeleted);
        var currentPolicy = await _context.Policies.Find(session, policyFilter).FirstOrDefaultAsync(ct);
        if (currentPolicy is null) return (new(PolicyMutationStatus.NotFound), null);
        if (!scope.Contains(new(Guid.Parse(currentPolicy.SkuId), Guid.Parse(currentPolicy.WarehouseId))))
            return (new(PolicyMutationStatus.NotFound), null);

        var revisionFilter = Builders<PolicyRevisionDocument>.Filter.Where(r => r.Id == currentPolicy.CurrentRevisionId
            && r.PolicyId == currentPolicy.Id && r.TenantId == tenant && r.LegalEntityId == company && !r.IsDeleted);
        var current = await _context.Revisions.Find(session, revisionFilter).FirstOrDefaultAsync(ct);
        if (current is null || current.SkuId != currentPolicy.SkuId || current.WarehouseId != currentPolicy.WarehouseId)
            return (new(PolicyMutationStatus.Unavailable), null);
        if (command.ExpectedVersion != current.Version) return (new(PolicyMutationStatus.Conflict), null);
        if (command.RevisionId.ToString("D") != current.Id)
            return (new(PolicyMutationStatus.NotFound), null);

        var originalRevisionVersion = current.Version;
        PolicyRevisionDocument next;
        if (command.Operation == PolicyOperation.Revise)
        {
            if (current.CoverageDays == command.CoverageDays) return (new(PolicyMutationStatus.Conflict), null);
            next = new PolicyRevisionDocument
            {
                Id = Guid.NewGuid().ToString("D"), PolicyId = current.PolicyId, TenantId = tenant,
                LegalEntityId = company, SkuId = current.SkuId, WarehouseId = current.WarehouseId,
                PreviousRevisionId = current.Id, CoverageDays = command.CoverageDays!.Value,
                Version = 1, PreparedBy = actor,
                MaterialContributors = current.MaterialContributors.Append(actor).Distinct(StringComparer.Ordinal).ToList(),
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
        }
        else
        {
            if (command.Operation == PolicyOperation.Submit && current.State != "Draft"
                || command.Operation is PolicyOperation.Approve or PolicyOperation.Reject && current.State != "InReview")
                return (new(PolicyMutationStatus.Conflict), null);
            if (command.Operation == PolicyOperation.Approve && current.MaterialContributors.Contains(actor))
                return (new(PolicyMutationStatus.Conflict), null);

            next = current;
            next.State = command.Operation switch
            {
                PolicyOperation.Submit => "InReview", PolicyOperation.Approve => "Approved", _ => "Rejected"
            };
            next.Version++;
            if (command.Operation is PolicyOperation.Approve or PolicyOperation.Reject)
            {
                next.DecisionBy = actor;
                next.DecidedAtUtc = now;
                next.RejectionReason = command.Operation == PolicyOperation.Reject ? command.Reason : null;
            }
        }

        var guardedPolicyFilter = policyFilter & Builders<PolicyDocument>.Filter.Eq(p => p.Version, currentPolicy.Version)
            & Builders<PolicyDocument>.Filter.Eq(p => p.CurrentRevisionId, currentPolicy.CurrentRevisionId);
        var policyUpdate = Builders<PolicyDocument>.Update.Inc(p => p.Version, 1)
            .Set(p => p.CurrentRevisionId, next.Id).Set(p => p.UpdatedAtUtc, now);
        var updatedPolicy = await _context.Policies.UpdateOneAsync(session, guardedPolicyFilter, policyUpdate, cancellationToken: ct);
        if (updatedPolicy.MatchedCount != 1) return (new(PolicyMutationStatus.Conflict), null);

        if (command.Operation == PolicyOperation.Revise)
            await _context.Revisions.InsertOneAsync(session, next, cancellationToken: ct);
        else
        {
            var guardedRevision = revisionFilter & Builders<PolicyRevisionDocument>.Filter.Eq(r => r.Version, originalRevisionVersion);
            var update = Builders<PolicyRevisionDocument>.Update.Set(r => r.State, next.State)
                .Set(r => r.Version, next.Version).Set(r => r.DecisionBy, next.DecisionBy)
                .Set(r => r.DecidedAtUtc, next.DecidedAtUtc).Set(r => r.RejectionReason, next.RejectionReason)
                .Set(r => r.UpdatedAtUtc, now);
            var updated = await _context.Revisions.UpdateOneAsync(session, guardedRevision, update, cancellationToken: ct);
            if (updated.MatchedCount != 1) return (new(PolicyMutationStatus.Conflict), null);
        }

        var receipt = new PolicyMutationReceipt(command.PolicyId, Guid.Parse(next.Id), next.State, next.Version);
        var outcome = command.Operation == PolicyOperation.Revise ? PolicyMutationStatus.Created : PolicyMutationStatus.Updated;
        return (new(outcome, receipt), Audit(command, receipt,
            command.Operation == PolicyOperation.Revise ? current.State : command.Operation == PolicyOperation.Submit ? "Draft" : "InReview",
            next.State, now));
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexesReady) return;
            await _context.Policies.Indexes.CreateOneAsync(new CreateIndexModel<PolicyDocument>(
                Builders<PolicyDocument>.IndexKeys.Ascending(p => p.TenantId).Ascending(p => p.LegalEntityId)
                    .Ascending(p => p.SkuId).Ascending(p => p.WarehouseId),
                new CreateIndexOptions<PolicyDocument> { Unique = true, PartialFilterExpression = Builders<PolicyDocument>.Filter.Eq(p => p.IsDeleted, false) }), cancellationToken: ct);
            await _context.Revisions.Indexes.CreateOneAsync(new CreateIndexModel<PolicyRevisionDocument>(
                Builders<PolicyRevisionDocument>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.LegalEntityId)
                    .Ascending(r => r.PolicyId).Ascending(r => r.Id)), cancellationToken: ct);
            await _context.Audit.Indexes.CreateOneAsync(new CreateIndexModel<PolicyAuditDocument>(
                Builders<PolicyAuditDocument>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.LegalEntityId)
                    .Ascending(a => a.PolicyId).Ascending(a => a.RevisionId)), cancellationToken: ct);
            await _context.Receipts.Indexes.CreateOneAsync(new CreateIndexModel<PolicyCommandReceiptDocument>(
                Builders<PolicyCommandReceiptDocument>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.ActorId)
                    .Ascending(r => r.IdempotencyKey), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
            _indexesReady = true;
        }
        finally { _indexLock.Release(); }
    }

    private async Task<bool> ScopeAllowsReplayAsync(PolicyCommandReceiptDocument receipt,
        Guid legalEntityId, ActorSeriesScope scope, CancellationToken ct)
    {
        var policy = await _context.Policies.Find(Builders<PolicyDocument>.Filter.Where(p =>
            p.Id == receipt.PolicyId && p.TenantId == receipt.TenantId
            && p.LegalEntityId == legalEntityId.ToString("D") && !p.IsDeleted)).FirstOrDefaultAsync(ct);
        return policy is not null && scope.Contains(new(Guid.Parse(policy.SkuId), Guid.Parse(policy.WarehouseId)));
    }

    private static bool IsValid(PolicyMutationCommand command) => command.Operation switch
    {
        PolicyOperation.Create => command.SkuId != Guid.Empty && command.WarehouseId != Guid.Empty
            && command.CoverageDays > 0 && command.PolicyId == Guid.Empty && command.RevisionId == Guid.Empty,
        PolicyOperation.Revise => command.PolicyId != Guid.Empty && command.RevisionId != Guid.Empty && command.ExpectedVersion > 0
            && command.CoverageDays > 0 && !string.IsNullOrWhiteSpace(command.Reason),
        PolicyOperation.Submit or PolicyOperation.Approve => command.PolicyId != Guid.Empty
            && command.RevisionId != Guid.Empty && command.ExpectedVersion > 0,
        PolicyOperation.Reject => command.PolicyId != Guid.Empty && command.RevisionId != Guid.Empty
            && command.ExpectedVersion > 0 && !string.IsNullOrWhiteSpace(command.Reason),
        _ => false
    };

    private static FilterDefinition<PolicyCommandReceiptDocument> ReceiptFilter(PolicyMutationCommand command)
        => Builders<PolicyCommandReceiptDocument>.Filter.Where(r => r.TenantId == command.TenantId.ToString("D")
            && r.ActorId == command.ActorId.ToString("D") && r.IdempotencyKey == command.IdempotencyKey);

    private static string Fingerprint(PolicyMutationCommand command)
    {
        var payload = JsonSerializer.Serialize(new
        {
            command.Operation, command.TenantId, command.LegalEntityId, command.ActorId,
            command.PolicyId, command.RevisionId, command.SkuId, command.WarehouseId,
            command.CoverageDays, command.ExpectedVersion, command.Reason
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static PolicyMutationResult Replay(PolicyCommandReceiptDocument prior, string fingerprint)
        => prior.Fingerprint != fingerprint
            ? new(PolicyMutationStatus.Conflict)
            : new(Enum.Parse<PolicyMutationStatus>(prior.Outcome), new(Guid.Parse(prior.PolicyId),
                Guid.Parse(prior.RevisionId), prior.State, prior.Version));

    private static PolicyAuditDocument Audit(PolicyMutationCommand command, PolicyMutationReceipt receipt,
        string before, string after, DateTime now) => new()
    {
        Id = Guid.NewGuid().ToString("D"), TenantId = command.TenantId.ToString("D"),
        LegalEntityId = command.LegalEntityId.ToString("D"), PolicyId = receipt.PolicyId.ToString("D"),
        RevisionId = receipt.RevisionId.ToString("D"), ActorId = command.ActorId.ToString("D"),
        Operation = command.Operation.ToString(), BeforeState = before, AfterState = after,
        Reason = command.Reason, OccurredAtUtc = now, CorrelationId = command.IdempotencyKey
    };
}
