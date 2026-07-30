using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Eventing;

internal enum PpmAuditAcceptanceResult
{
    Accepted,
    Duplicate
}

internal sealed class PpmAuditPayloadConflictException : PpmAuditContractException
{
    public PpmAuditPayloadConflictException() : base("PPM audit EventId was reused with different canonical payload bytes.") { }
}

internal interface IPpmAuditAcceptanceRepository
{
    Task<PpmAuditAcceptanceResult> AcceptAsync(
        EventTransportMessage message,
        PpmAuditIntent intent,
        CancellationToken cancellationToken);
}

internal sealed class PpmAuditAcceptanceRepository : IPpmAuditAcceptanceRepository
{
    internal const string ConsumerName = "PpmAuditIntentSubmittedV1Consumer";
    private const string InboxCollectionName = "ppm_audit_inbox";
    private const string AuditOutboxCollectionName = "audit_outbox";
    private const string InboxUniqueIndexName = "ux_ppm_audit_inbox_consumer_event";
    private const string AuditOutboxUniqueIndexName = "ux_audit_outbox_idempotency_key";

    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;

    public PpmAuditAcceptanceRepository(IMongoClient client, IMongoDatabase database)
    {
        _client = client;
        _database = database;
    }

    public async Task<PpmAuditAcceptanceResult> AcceptAsync(
        EventTransportMessage message,
        PpmAuditIntent intent,
        CancellationToken cancellationToken)
    {
        var tenantId = message.TenantId!.Value;
        var inbox = _database.GetCollection<PpmAuditInboxMessage>(InboxCollectionName);
        var auditOutbox = _database.GetCollection<AuditOutboxMessage>(AuditOutboxCollectionName);
        var idempotencyKey = $"ppm.audit-intent:{message.EventId:D}";

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var filter = Builders<PpmAuditInboxMessage>.Filter.And(
                Builders<PpmAuditInboxMessage>.Filter.Eq(x => x.ConsumerName, ConsumerName),
                Builders<PpmAuditInboxMessage>.Filter.Eq(x => x.EventId, message.EventId.ToString("D")));
            var existing = await inbox.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.PayloadSha256, intent.PayloadSha256, StringComparison.Ordinal))
                {
                    throw new PpmAuditPayloadConflictException();
                }

                await session.AbortTransactionAsync(cancellationToken);
                return PpmAuditAcceptanceResult.Duplicate;
            }

            var outboxMessage = BuildAuditOutbox(message, intent, tenantId, idempotencyKey);
            outboxMessage.ValidateForInsert();
            await inbox.InsertOneAsync(session, new PpmAuditInboxMessage
            {
                ConsumerName = ConsumerName,
                EventId = message.EventId.ToString("D"),
                TenantId = tenantId,
                PayloadSha256 = intent.PayloadSha256,
                AuditOutboxIdempotencyKey = idempotencyKey,
                AcceptedAtUtc = DateTimeOffset.UtcNow
            }, cancellationToken: cancellationToken);
            await auditOutbox.InsertOneAsync(session, outboxMessage, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return PpmAuditAcceptanceResult.Accepted;
        }
        catch (MongoWriteException exception) when (IsRecoverableDuplicate(exception))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            var existing = await FindExactInboxAsync(message.EventId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            if (!string.Equals(existing.PayloadSha256, intent.PayloadSha256, StringComparison.Ordinal))
            {
                throw new PpmAuditPayloadConflictException();
            }

            return PpmAuditAcceptanceResult.Duplicate;
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task<PpmAuditInboxMessage?> FindExactInboxAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        // Exact compound unique-index lookup; EventId is persisted as canonical "D" text so process-global
        // legacy Guid serializers cannot widen or alter this greenfield inbox recovery query.
        var collection = _database.GetCollection<PpmAuditInboxMessage>(InboxCollectionName);
        var filter = Builders<PpmAuditInboxMessage>.Filter.And(
            Builders<PpmAuditInboxMessage>.Filter.Eq(x => x.ConsumerName, ConsumerName),
            Builders<PpmAuditInboxMessage>.Filter.Eq(x => x.EventId, eventId.ToString("D")));
        return await collection.Find(filter, new FindOptions
            {
                Hint = InboxUniqueIndexName
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsRecoverableDuplicate(MongoWriteException exception)
    {
        if (exception.WriteError?.Category != ServerErrorCategory.DuplicateKey)
        {
            return false;
        }

        var message = exception.WriteError.Message ?? exception.Message;
        return message.Contains(InboxUniqueIndexName, StringComparison.Ordinal)
               || message.Contains(AuditOutboxUniqueIndexName, StringComparison.Ordinal);
    }

    private static AuditOutboxMessage BuildAuditOutbox(
        EventTransportMessage message,
        PpmAuditIntent intent,
        Guid tenantId,
        string idempotencyKey)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["AuditIntentId"] = intent.AuditIntentId,
            ["EventVersion"] = message.EventVersion
        };
        if (message.CausationId.HasValue)
        {
            metadata["CausationId"] = message.CausationId.Value;
        }

        var operation = MapOperation(intent.Mutation);

        return new AuditOutboxMessage
        {
            TenantId = tenantId,
            CorrelationId = message.CorrelationId,
            IdempotencyKey = idempotencyKey,
            RequestType = PpmAuditIntentParser.EventName,
            Operation = operation,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Payload = new Dictionary<string, object?>
            {
                ["TenantId"] = tenantId,
                ["CorrelationId"] = message.CorrelationId,
                ["RequestType"] = PpmAuditIntentParser.EventName,
                ["ActorType"] = AuditActorType.TenantUser.ToString(),
                ["ActorId"] = intent.ActorId,
                ["TargetTenantId"] = null,
                ["Category"] = AuditCategory.PortfolioDelivery.ToString(),
                ["EntityType"] = intent.EntityType,
                ["EntityId"] = intent.EntityId,
                ["Operation"] = operation.ToString(),
                ["Outcome"] = AuditOutcome.Succeeded.ToString(),
                ["BeforeState"] = null,
                ["AfterState"] = null,
                ["Metadata"] = metadata,
                ["OccurredAtUtc"] = intent.OccurredAtUtc,
                ["SourceService"] = PpmAuditIntentParser.Producer,
                ["SourceModule"] = "MOD-0117",
                ["IsMetaAudit"] = false,
                ["RedactionStatus"] = AuditRedactionStatus.None.ToString()
            }
        };
    }

    internal static AuditOperation MapOperation(string mutation) =>
        mutation switch
        {
            "created" => AuditOperation.Create,
            "updated" => AuditOperation.Update,
            "lifecycle-changed" => AuditOperation.LifecycleTransition,
            "soft-deleted" => AuditOperation.Delete,
            _ => throw new PpmAuditContractException("PPM audit mutation is not supported.")
        };
}
