using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

[assembly: InternalsVisibleTo("Diten.MdmService.Application.Tests")]

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductLegalEntityScopeGuardedWriteSession
    : IProductLegalEntityScopeGuardedWriteSession
{
    private const int AuditLifecycleHeadroomBytes = 64 * 1024;
    private const int MaximumPreDeliveryBsonBytes =
        ProductLegalEntityScopePolicy.MaximumSerializedBsonBytes - AuditLifecycleHeadroomBytes;

    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ProductLegalEntityScopeRolloutState> _rollouts;
    private readonly IMongoCollection<BsonDocument> _policyDocuments;
    private readonly Guid _tenantId;
    private readonly Func<IClientSessionHandle, CancellationToken, Task> _commitAsync;

    public ProductLegalEntityScopeGuardedWriteSession(
        IMongoDatabase database,
        ITenantContext tenantContext)
        : this(
            database,
            tenantContext,
            static (session, cancellationToken) =>
                session.CommitTransactionAsync(cancellationToken))
    {
    }

    internal ProductLegalEntityScopeGuardedWriteSession(
        IMongoDatabase database,
        ITenantContext tenantContext,
        Func<IClientSessionHandle, CancellationToken, Task> commitAsync)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(commitAsync);
        if (!tenantContext.IsResolved || tenantContext.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A trusted tenant is required for guarded product scope persistence.");
        }

        _database = database;
        _tenantId = tenantContext.TenantId;
        _commitAsync = commitAsync;
        _rollouts = database.GetCollection<ProductLegalEntityScopeRolloutState>(
            ProductLegalEntityScopeRolloutStateRepository.CollectionName);
        _policyDocuments = database.GetCollection<BsonDocument>(
            ProductLegalEntityScopePolicyRepository.CollectionName);
    }

    public async Task<ProductLegalEntityScopePolicyWriteResult> ReplaceAsync(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease,
        ProductLegalEntityScopePolicy requestedPolicy,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(requestedPolicy);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ValidRequest(authority, lease, requestedPolicy, expectedVersion))
        {
            return Rejected();
        }

        using var session = await _database.Client.StartSessionAsync(
            cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(
            ReadConcern.Snapshot,
            ReadPreference.Primary,
            WriteConcern.WMajority));

        var commitAttempted = false;
        try
        {
            var rollout = await _rollouts.Find(
                    session,
                    QualifiedRolloutFilter(authority, lease))
                .FirstOrDefaultAsync(cancellationToken);
            if (rollout is null)
            {
                return await AbortRejectedAsync(
                    session,
                    cancellationToken,
                    leaseRetentionRequired: true);
            }
            rollout.EnsureValid();

            var persistedDocument = await _policyDocuments.Find(
                    session,
                    ActivePolicyIdentityFilter(requestedPolicy))
                .FirstOrDefaultAsync(cancellationToken);
            if (persistedDocument is null)
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }

            var persisted = DeserializePolicy(persistedDocument);
            if (persisted.Version != expectedVersion
                || persisted.GlobalProductId != requestedPolicy.GlobalProductId
                || persisted.CreationCommandId != requestedPolicy.CreationCommandId)
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }

            IReadOnlyList<LocalAuditIntent> newAuditIntents;
            try
            {
                newAuditIntents = ProductLegalEntityScopePolicyRepository.GetNewAuditIntents(
                    persisted,
                    requestedPolicy,
                    _tenantId);
                var transition = ProductLegalEntityScopePolicyRepository.ResolvePolicyTransition(
                    requestedPolicy,
                    newAuditIntents.Single());
                if (transition.Operation
                        != ProductAuditOperation.ProductLegalEntityScopePolicyReplaced
                    || transition.CommandId != authority.CommandId
                    || transition.ActorId != authority.SubjectId
                    || !string.Equals(
                        ComputePayloadFingerprint(requestedPolicy, expectedVersion, transition.CommandId),
                        authority.PayloadFingerprint,
                        StringComparison.Ordinal))
                {
                    return await AbortRejectedAsync(session, cancellationToken);
                }
            }
            catch (ArgumentException)
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }

            var projectedDocument = ProjectPolicyDocument(
                persistedDocument,
                requestedPolicy,
                newAuditIntents);
            ProductLegalEntityScopePolicyRepository.EnsureCompleteDocumentWithinBudget(
                projectedDocument,
                reserveAuditLifecycleHeadroom: true);

            // This is the transaction's common write-conflict point. It both rechecks the
            // exact persisted lease and Preparation mode and performs a server-authored
            // write on the rollout document before the policy CAS.
            var rolloutWrite = await _rollouts.UpdateOneAsync(
                session,
                QualifiedRolloutFilter(authority, lease),
                new BsonDocumentUpdateDefinition<ProductLegalEntityScopeRolloutState>(
                    new BsonDocument("$currentDate", new BsonDocument(
                        nameof(ProductLegalEntityScopeRolloutState.UpdatedAt), true))),
                cancellationToken: cancellationToken);
            if (rolloutWrite.MatchedCount != 1)
            {
                return await AbortRejectedAsync(
                    session,
                    cancellationToken,
                    leaseRetentionRequired: true);
            }

            var updatedDocument = await _policyDocuments.FindOneAndUpdateAsync(
                session,
                ActivePolicyIdentityFilter(requestedPolicy)
                & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
                    nameof(ProductLegalEntityScopePolicy.Version), expectedVersion))
                & ProjectedDocumentFitsFilter(projectedDocument),
                BuildPolicyUpdate(requestedPolicy, newAuditIntents),
                new FindOneAndUpdateOptions<BsonDocument>
                {
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
            if (updatedDocument is null)
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }

            ProductLegalEntityScopePolicyRepository.EnsureCompleteDocumentWithinBudget(
                updatedDocument,
                reserveAuditLifecycleHeadroom: true);
            var updated = DeserializePolicy(updatedDocument);
            if (!ProductLegalEntityScopePolicyRepository.ExactBusinessState(
                    updated,
                    requestedPolicy))
            {
                return await AbortRejectedAsync(session, cancellationToken);
            }

            commitAttempted = true;
            await _commitAsync(session, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await AbortBestEffortAsync(session);
            throw;
        }
        catch (MongoException)
        {
            if (!commitAttempted && await AbortBestEffortAsync(session))
            {
                return Rejected();
            }

            return await RecoverExactOutcomeAsync(
                authority,
                lease,
                requestedPolicy,
                cancellationToken);
        }
        catch (ProductLegalEntityScopeCommitResponseLostException)
        {
            return await RecoverExactOutcomeAsync(
                authority,
                lease,
                requestedPolicy,
                cancellationToken);
        }

        return await RecoverExactOutcomeAsync(
            authority,
            lease,
            requestedPolicy,
            cancellationToken);
    }

    private bool ValidRequest(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease,
        ProductLegalEntityScopePolicy requestedPolicy,
        int expectedVersion)
    {
        try
        {
            lease.EnsureValid();
            requestedPolicy.EnsureValid(DateTimeOffset.UtcNow);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return expectedVersion >= 0
            && requestedPolicy.TenantId == _tenantId
            && requestedPolicy.Id != Guid.Empty
            && requestedPolicy.GlobalProductId != Guid.Empty
            && requestedPolicy.CreationCommandId != Guid.Empty
            && !requestedPolicy.IsDeleted
            && requestedPolicy.Version == expectedVersion + 1
            && lease.BaselineBound
            && lease.CommandId == authority.CommandId
            && lease.ActorId == authority.SubjectId
            && string.Equals(
                lease.MutationKind,
                authority.MutationKind,
                StringComparison.Ordinal)
            && string.Equals(
                lease.PayloadFingerprint,
                authority.PayloadFingerprint,
                StringComparison.Ordinal)
            && authority.MatchesForegroundReplace(
                _tenantId,
                lease.ActorId,
                lease.CommandId,
                requestedPolicy.Id,
                lease.MutationKind,
                lease.PayloadFingerprint);
    }

    private FilterDefinition<ProductLegalEntityScopeRolloutState> QualifiedRolloutFilter(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease) =>
        Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.IsDeleted, false)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            item => item.Mode,
            ProductLegalEntityScopeRolloutMode.Preparation)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            item => item.WriterLeaseGeneration,
            lease.Generation)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(item => item.ActiveFence, null)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.Token",
            lease.Token)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.Generation",
            lease.Generation)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.CommandId",
            authority.CommandId)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.ActorId",
            authority.SubjectId)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.MutationKind",
            authority.MutationKind)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.PayloadFingerprint",
            authority.PayloadFingerprint)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.Owner",
            lease.Owner)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.AcquiredAtUtc",
            lease.AcquiredAtUtc)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.ExpiresAtUtc",
            lease.ExpiresAtUtc)
        & Builders<ProductLegalEntityScopeRolloutState>.Filter.Eq(
            "ActiveWriterLease.PreWriteStateHash",
            lease.PreWriteStateHash)
        & ProductLegalEntityScopeRolloutStateRepository
            .ActiveWriterLeaseUnexpiredAtServerTimeFilter;

    private FilterDefinition<BsonDocument> ActivePolicyIdentityFilter(
        ProductLegalEntityScopePolicy policy) =>
        new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument
        {
            { nameof(ProductLegalEntityScopePolicy.TenantId), GuidBson(_tenantId) },
            { nameof(ProductLegalEntityScopePolicy.IsDeleted), false },
            { "_id", GuidBson(policy.Id) },
            { nameof(ProductLegalEntityScopePolicy.GlobalProductId), GuidBson(policy.GlobalProductId) },
            { nameof(ProductLegalEntityScopePolicy.CreationCommandId), GuidBson(policy.CreationCommandId) }
        });

    private static UpdateDefinition<BsonDocument> BuildPolicyUpdate(
        ProductLegalEntityScopePolicy requested,
        IReadOnlyCollection<LocalAuditIntent> newAuditIntents)
    {
        var requestedDocument = requested.ToBsonDocument();
        return new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument
        {
            { "$set", new BsonDocument
                {
                    { nameof(ProductLegalEntityScopePolicy.ScopePeriods), requestedDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)] },
                    { nameof(ProductLegalEntityScopePolicy.UpdatedAt), requestedDocument[nameof(ProductLegalEntityScopePolicy.UpdatedAt)] },
                    { nameof(ProductLegalEntityScopePolicy.Version), requested.Version }
                }
            },
            { "$push", new BsonDocument(nameof(ProductLegalEntityScopePolicy.AuditIntents),
                new BsonDocument("$each", new BsonArray(
                    newAuditIntents.Select(intent => intent.ToBsonDocument())))) }
        });
    }

    private static BsonDocument ProjectPolicyDocument(
        BsonDocument persisted,
        ProductLegalEntityScopePolicy requested,
        IReadOnlyCollection<LocalAuditIntent> newAuditIntents)
    {
        var projected = persisted.DeepClone().AsBsonDocument;
        var requestedDocument = requested.ToBsonDocument();
        projected[nameof(ProductLegalEntityScopePolicy.ScopePeriods)] =
            requestedDocument[nameof(ProductLegalEntityScopePolicy.ScopePeriods)];
        projected[nameof(ProductLegalEntityScopePolicy.UpdatedAt)] =
            requestedDocument[nameof(ProductLegalEntityScopePolicy.UpdatedAt)];
        projected[nameof(ProductLegalEntityScopePolicy.Version)] = requested.Version;
        if (!projected.TryGetValue(
                nameof(ProductLegalEntityScopePolicy.AuditIntents),
                out var persistedIntents)
            || !persistedIntents.IsBsonArray)
        {
            throw new InvalidOperationException(
                "PRODUCT_LEGAL_ENTITY_SCOPE_AUDIT_STORAGE_INVALID");
        }
        foreach (var intent in newAuditIntents)
        {
            persistedIntents.AsBsonArray.Add(intent.ToBsonDocument());
        }

        return projected;
    }

    private static FilterDefinition<BsonDocument> ProjectedDocumentFitsFilter(
        BsonDocument projectedDocument) =>
        new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
            "$expr",
            new BsonDocument("$lte", new BsonArray
            {
                projectedDocument.ToBson().Length,
                MaximumPreDeliveryBsonBytes
            })));

    private async Task<ProductLegalEntityScopePolicyWriteResult> RecoverExactOutcomeAsync(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease,
        ProductLegalEntityScopePolicy requestedPolicy,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await _policyDocuments.Find(
                    ActivePolicyIdentityFilter(requestedPolicy))
                .FirstOrDefaultAsync(cancellationToken);
            if (document is not null)
            {
                ProductLegalEntityScopePolicyRepository.EnsureCompleteDocumentWithinBudget(
                    document,
                    reserveAuditLifecycleHeadroom: false);
                var persisted = DeserializePolicy(document);
                if (ProductLegalEntityScopePolicyRepository.ExactBusinessState(
                        persisted,
                        requestedPolicy)
                    && persisted.AuditIntents.Any(intent =>
                        intent.Operation
                            == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced
                        && intent.AggregateId == authority.AggregateId
                        && string.Equals(
                            intent.CommandId,
                            lease.CommandId.ToString("D"),
                            StringComparison.Ordinal)
                        && string.Equals(
                            intent.ActorId,
                            lease.ActorId.ToString("D"),
                            StringComparison.Ordinal)))
                {
                    return new(true, persisted);
                }
            }
        }
        catch (MongoException)
        {
            // A read failure after a commit attempt cannot establish success or safe release.
        }

        return new(
            false,
            null,
            WriteOutcomeAmbiguous: true);
    }

    private static async Task<ProductLegalEntityScopePolicyWriteResult> AbortRejectedAsync(
        IClientSessionHandle session,
        CancellationToken cancellationToken,
        bool leaseRetentionRequired = false)
    {
        try
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }
            return Rejected(leaseRetentionRequired);
        }
        catch (MongoException)
        {
            return new(
                false,
                null,
                WriteOutcomeAmbiguous: true,
                LeaseRetentionRequired: leaseRetentionRequired);
        }
    }

    private static async Task<bool> AbortBestEffortAsync(IClientSessionHandle session)
    {
        if (!session.IsInTransaction)
        {
            return false;
        }
        try
        {
            await session.AbortTransactionAsync(CancellationToken.None);
            return true;
        }
        catch (MongoException)
        {
            return false;
        }
    }

    private static ProductLegalEntityScopePolicyWriteResult Rejected(
        bool leaseRetentionRequired = false) =>
        new(
            false,
            null,
            VersionConflict: true,
            VerifiedZeroMutation: true,
            LeaseRetentionRequired: leaseRetentionRequired);

    private static string ComputePayloadFingerprint(
        ProductLegalEntityScopePolicy requested,
        int expectedVersion,
        Guid commandId)
    {
        var period = requested.ScopePeriods.Single(item => item.CommandId == commandId);
        var payload = string.Join(
            '|',
            "product-legal-entity-scope-replace/v1",
            requested.GlobalProductId.ToString("D"),
            commandId.ToString("D"),
            expectedVersion.ToString(CultureInfo.InvariantCulture),
            ((int)period.Mode).ToString(CultureInfo.InvariantCulture),
            string.Join(',', period.LegalEntityIds.Select(id => id.ToString("D"))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static ProductLegalEntityScopePolicy DeserializePolicy(BsonDocument document)
    {
        var known = BsonClassMap
            .LookupClassMap(typeof(ProductLegalEntityScopePolicy))
            .AllMemberMaps
            .Select(member => member.ElementName)
            .ToHashSet(StringComparer.Ordinal);
        var contractDocument = document.DeepClone().AsBsonDocument;
        foreach (var element in contractDocument.Elements.ToArray())
        {
            if (!known.Contains(element.Name))
            {
                contractDocument.Remove(element.Name);
            }
        }

        return BsonSerializer.Deserialize<ProductLegalEntityScopePolicy>(contractDocument);
    }

    private static BsonBinaryData GuidBson(Guid value) =>
        new(value, GuidRepresentation.Standard);
}

internal sealed class ProductLegalEntityScopeCommitResponseLostException : Exception
{
    internal ProductLegalEntityScopeCommitResponseLostException()
        : base("The guarded product scope commit response was not observed.")
    {
    }
}
