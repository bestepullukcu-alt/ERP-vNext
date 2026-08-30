using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.GateI;


public sealed class GateIRelationshipMutationPersistence(
    PpmMongoContext context,
    IInvestmentCaseRepository investmentCases,
    IBenefitCommitmentRepository benefitCommitments,
    IGateIRelationshipMutationFaultProbe faultProbe,
    IGateIRelationshipTransportMetadataProvider transportMetadataProvider)
    : IGateIRelationshipMutationPersistence
{
    private const int MaximumCommitAttempts = 3;

    public async Task<GateIReceiptResult> ReconcileAsync(
        GateIMutationScope scope,
        CancellationToken cancellationToken)
    {
        ValidateScope(scope, allowMissingProvenance: true);
        try
        {
            var receipt = await context.GateIMutationReceipts
                .WithReadConcern(ReadConcern.Majority)
                .Find(ReceiptFilter(scope))
                .SingleOrDefaultAsync(cancellationToken);
            if (receipt is null) return new GateIReceiptResult(GateIReceiptDisposition.Missing);
            if (!string.Equals(receipt.RequestHash, scope.RequestHash, StringComparison.Ordinal))
                return new GateIReceiptResult(GateIReceiptDisposition.Conflict);
            if (!string.IsNullOrEmpty(scope.ProvenanceHash)
                && !string.Equals(receipt.ProvenanceHash, scope.ProvenanceHash, StringComparison.Ordinal))
                return new GateIReceiptResult(GateIReceiptDisposition.Conflict);
            return new GateIReceiptResult(
                GateIReceiptDisposition.Matching,
                new GateIRelationshipMutationResult(
                    receipt.AggregateId,
                    receipt.AggregateVersion,
                    receipt.StableCode,
                    true));
        }
        catch (MongoException exception)
        {
            throw new GateIRelationshipUnavailableException("Receipt reconciliation is unavailable.", exception);
        }
    }

    public Task<GateIRelationshipMutationResult> ExecuteInvestmentCaseAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<InvestmentCase> mutation,
        string mutationName,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            scope,
            aggregateId,
            expectedVersion,
            mutationName,
            async token =>
            {
                var entity = await investmentCases.GetByIdAsync(scope.TenantId, aggregateId, token)
                    ?? throw new GateIRelationshipNotFoundException();
                mutation(entity);
                await investmentCases.ReplaceAsync(entity, expectedVersion, token);
                return (entity.Version, nameof(InvestmentCase));
            },
            cancellationToken);

    public Task<GateIRelationshipMutationResult> ExecuteBenefitCommitmentAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        Action<BenefitCommitment> mutation,
        string mutationName,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            scope,
            aggregateId,
            expectedVersion,
            mutationName,
            async token =>
            {
                var entity = await benefitCommitments.GetByIdAsync(scope.TenantId, aggregateId, token)
                    ?? throw new GateIRelationshipNotFoundException();
                mutation(entity);
                await benefitCommitments.ReplaceAsync(entity, expectedVersion, token);
                return (entity.Version, nameof(BenefitCommitment));
            },
            cancellationToken);

    private async Task<GateIRelationshipMutationResult> ExecuteAsync(
        GateIMutationScope scope,
        Guid aggregateId,
        int expectedVersion,
        string mutationName,
        Func<CancellationToken, Task<(int Version, string EntityType)>> body,
        CancellationToken cancellationToken)
    {
        ValidateScope(scope, allowMissingProvenance: false);
        if (aggregateId == Guid.Empty || expectedVersion < 1 || string.IsNullOrWhiteSpace(mutationName))
            throw new GateIRelationshipConflictException("Gate I mutation identity is invalid.");

        var auditIntentId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;
        var auditPrototype = new AuditIntentDocument
        {
            Id = auditIntentId,
            TenantId = scope.TenantId,
            ActorId = scope.ActorId,
            CorrelationId = scope.CorrelationId,
            EntityType = "GateIRelationship",
            EntityId = aggregateId,
            Mutation = mutationName,
            OccurredAtUtc = occurredAtUtc
        };
        var payload = CanonicalAuditPayload(auditPrototype);
        var eventMetadata = new EventMetadata(
            auditIntentId,
            "ppm.audit-intent.submitted.v1",
            1,
            scope.CorrelationId,
            null,
            scope.TenantId,
            "Diten.PpmService",
            new DateTimeOffset(occurredAtUtc));
        TrustedTransportMetadata transportMetadata;
        try
        {
            transportMetadata = await transportMetadataProvider.CreateAsync(
                eventMetadata, payload, cancellationToken);
            ValidateTransportMetadata(transportMetadata);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is EventValidationException or GateIRelationshipUnavailableException)
        {
            throw new GateIRelationshipUnavailableException(
                "Gate I signed transport metadata is unavailable.", exception);
        }
        IClientSessionHandle? session = null;
        try
        {
            session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction(new TransactionOptions(
                ReadConcern.Snapshot,
                ReadPreference.Primary,
                WriteConcern.WMajority));
            using var ambient = context.EnterSession(session);

            var existing = await context.GateIMutationReceipts.Find(
                session, ReceiptFilter(scope)).SingleOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                if (!string.Equals(existing.RequestHash, scope.RequestHash, StringComparison.Ordinal))
                    throw new GateIRelationshipConflictException("Idempotency key payload conflict.");
                if (!string.Equals(existing.ProvenanceHash, scope.ProvenanceHash, StringComparison.Ordinal))
                    throw new GateIRelationshipConflictException("Idempotency key provenance conflict.");
                return new GateIRelationshipMutationResult(
                    existing.AggregateId, existing.AggregateVersion, existing.StableCode, true);
            }

            (int Version, string EntityType) mutated;
            try { mutated = await body(cancellationToken); }
            catch (OptimisticConcurrencyException exception)
            { throw new GateIRelationshipConflictException(exception.Message); }
            catch (InvalidOperationException exception)
            { throw new GateIRelationshipConflictException(exception.Message); }
            await faultProbe.AfterParticipantAsync("relationship", cancellationToken);

            var result = new GateIRelationshipMutationResult(
                aggregateId, mutated.Version, "ppm_gate_i_relationship_mutated", false);
            var receipt = new GateIMutationReceiptDocument
            {
                Id = Guid.NewGuid(),
                TenantId = scope.TenantId,
                OperationId = scope.OperationId,
                IdempotencyKey = scope.IdempotencyKey,
                RequestHash = scope.RequestHash,
                ProvenanceHash = scope.ProvenanceHash,
                AggregateId = aggregateId,
                AggregateVersion = mutated.Version,
                StatusCode = 200,
                StableCode = result.StableCode,
                CreatedAtUtc = occurredAtUtc
            };
            await context.GateIMutationReceipts.InsertOneAsync(
                session, receipt, cancellationToken: cancellationToken);
            await faultProbe.AfterParticipantAsync("receipt", cancellationToken);

            var audit = new AuditIntentDocument
            {
                Id = auditIntentId,
                TenantId = scope.TenantId,
                ActorId = scope.ActorId,
                CorrelationId = scope.CorrelationId,
                EntityType = mutated.EntityType,
                EntityId = aggregateId,
                Mutation = mutationName,
                OccurredAtUtc = occurredAtUtc,
                OutboxEnqueuedAtUtc = occurredAtUtc,
                DispatchSignatureScheme = transportMetadata.Headers[TrustedTransportMetadata.SignatureSchemeHeader],
                DispatchKeyId = transportMetadata.Headers[TrustedTransportMetadata.KeyIdHeader],
                DispatchSignature = transportMetadata.Headers[TrustedTransportMetadata.SignatureHeader],
                DispatchUpdatedAtUtc = occurredAtUtc
            };
            await context.AuditIntents.InsertOneAsync(
                session, audit, cancellationToken: cancellationToken);
            await faultProbe.AfterParticipantAsync("audit-intent", cancellationToken);

            await context.EventOutbox.InsertOneAsync(
                session,
                new PpmEventOutboxDocument
                {
                    Id = Guid.NewGuid(),
                    EventId = auditIntentId,
                    EventName = "ppm.audit-intent.submitted.v1",
                    EventVersion = 1,
                    CorrelationId = scope.CorrelationId,
                    TenantId = scope.TenantId,
                    Producer = "Diten.PpmService",
                    OccurredAtUtcTicks = occurredAtUtc.Ticks,
                    CanonicalPayloadUtf8 = payload,
                    TransportHeaders = new Dictionary<string, string>(
                        transportMetadata.Headers, StringComparer.Ordinal),
                    Status = EventOutboxDeliveryStatus.Pending,
                    CreatedAtUtc = occurredAtUtc,
                    UpdatedAtUtc = occurredAtUtc
                },
                cancellationToken: cancellationToken);
            await faultProbe.AfterParticipantAsync("event-outbox", cancellationToken);

            await CommitOnlyAsync(session, cancellationToken);
            return result;
        }
        catch (GateIRelationshipNotFoundException) { await Abort(session); throw; }
        catch (GateIRelationshipConflictException) { await Abort(session); throw; }
        catch (OperationCanceledException) { await Abort(session); throw; }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await Abort(session);
            var reconciled = await ReconcileAsync(scope, cancellationToken);
            if (reconciled.Disposition == GateIReceiptDisposition.Conflict)
                throw new GateIRelationshipConflictException("Idempotency key payload conflict.");
            if (reconciled.StoredResult is not null) return reconciled.StoredResult;
            throw new GateIRelationshipConflictException("A concurrent Gate I mutation changed the aggregate.");
        }
        catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            var reconciled = await ReconcileAsync(scope, cancellationToken);
            if (reconciled.Disposition == GateIReceiptDisposition.Conflict)
                throw new GateIRelationshipConflictException("Idempotency key payload conflict.");
            if (reconciled.StoredResult is not null) return reconciled.StoredResult;
            throw new GateIRelationshipUnavailableException("Commit outcome is indeterminate.", exception);
        }
        catch (MongoException exception)
        {
            await Abort(session);
            throw new GateIRelationshipUnavailableException("Gate I transaction is unavailable.", exception);
        }
        finally { session?.Dispose(); }
    }

    private static async Task CommitOnlyAsync(
        IClientSessionHandle session,
        CancellationToken cancellationToken)
    {
        MongoException? last = null;
        for (var attempt = 1; attempt <= MaximumCommitAttempts; attempt++)
        {
            try { await session.CommitTransactionAsync(cancellationToken); return; }
            catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
            {
                last = exception;
            }
        }
        throw last ?? new MongoClientException("Commit-only retry exhausted.");
    }

    private static FilterDefinition<GateIMutationReceiptDocument> ReceiptFilter(GateIMutationScope scope) =>
        Builders<GateIMutationReceiptDocument>.Filter.Eq(item => item.TenantId, scope.TenantId)
        & Builders<GateIMutationReceiptDocument>.Filter.Eq(item => item.OperationId, scope.OperationId)
        & Builders<GateIMutationReceiptDocument>.Filter.Eq(item => item.IdempotencyKey, scope.IdempotencyKey);

    private static byte[] CanonicalAuditPayload(AuditIntentDocument audit)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("auditIntentId", audit.Id.ToString("D"));
        writer.WriteString("actorId", audit.ActorId.ToString("D"));
        writer.WriteString("entityType", audit.EntityType);
        writer.WriteString("entityId", audit.EntityId.ToString("D"));
        writer.WriteString("mutation", audit.Mutation);
        writer.WriteString("occurredAtUtc", audit.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void ValidateScope(GateIMutationScope scope, bool allowMissingProvenance)
    {
        if (scope.TenantId == Guid.Empty || scope.ActorId == Guid.Empty || scope.CorrelationId == Guid.Empty
            || string.IsNullOrWhiteSpace(scope.OperationId) || string.IsNullOrWhiteSpace(scope.IdempotencyKey)
            || !LowerHex64(scope.RequestHash)
            || (!allowMissingProvenance && !LowerHex64(scope.ProvenanceHash)))
            throw new GateIRelationshipConflictException("Gate I mutation scope is invalid.");
    }

    private static bool LowerHex64(string value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateTransportMetadata(TrustedTransportMetadata metadata)
    {
        if (metadata.Headers.Count != 3
            || !metadata.Headers.ContainsKey(TrustedTransportMetadata.SignatureSchemeHeader)
            || !metadata.Headers.ContainsKey(TrustedTransportMetadata.KeyIdHeader)
            || !metadata.Headers.ContainsKey(TrustedTransportMetadata.SignatureHeader))
            throw new EventValidationException("Gate I outbox requires complete signed transport metadata.");
    }

    private static async Task Abort(IClientSessionHandle? session)
    {
        if (session?.IsInTransaction == true)
            await session.AbortTransactionAsync(CancellationToken.None);
    }
}
