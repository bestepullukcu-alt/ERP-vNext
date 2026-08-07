using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Eventing;

internal enum GateIAuditAcceptanceResult
{
    Accepted,
    Duplicate
}

internal sealed class GateIAuditConflictException()
    : EventTransportTerminalExceptionBase(
        EventOutboxTerminalFailureKind.Contract,
        "Gate I EventId was reused with different immutable bytes or trusted metadata.",
        "gate-i.inbox.conflict");

internal sealed class GateIAuditInboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ConsumerName { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string PayloadSha256 { get; init; } = string.Empty;
    public string TrustedMetadataSha256 { get; init; } = string.Empty;
    public string AuditOutboxIdempotencyKey { get; init; } = string.Empty;
    public DateTimeOffset AcceptedAtUtc { get; init; }
}

internal interface IGateIAuditAcceptanceRepository
{
    Task<GateIAuditAcceptanceResult> AcceptAsync(
        EventTransportMessage message,
        GateIAuditIntent intent,
        GateIAuditMapping mapping,
        CancellationToken cancellationToken);
}

internal sealed class GateIAuditAcceptanceRepository : IGateIAuditAcceptanceRepository
{
    internal const string ConsumerName = "GateIAuditIntentConsumer";
    internal const string InboxCollectionName = "gate_i_audit_inbox";
    private const string AuditOutboxCollectionName = "audit_outbox";
    private const string InboxUniqueIndexName = "ux_gate_i_audit_inbox_consumer_event";
    private const string AuditOutboxUniqueIndexName = "ux_audit_outbox_idempotency_key";

    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly TimeProvider _timeProvider;

    public GateIAuditAcceptanceRepository(
        IMongoClient client,
        IMongoDatabase database,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _database = database;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default) =>
        _database.GetCollection<GateIAuditInboxMessage>(InboxCollectionName).Indexes.CreateOneAsync(
            new CreateIndexModel<GateIAuditInboxMessage>(
                Builders<GateIAuditInboxMessage>.IndexKeys
                    .Ascending(item => item.ConsumerName)
                    .Ascending(item => item.EventId),
                new CreateIndexOptions { Unique = true, Name = InboxUniqueIndexName }),
            cancellationToken: cancellationToken);

    public async Task<GateIAuditAcceptanceResult> AcceptAsync(
        EventTransportMessage message,
        GateIAuditIntent intent,
        GateIAuditMapping mapping,
        CancellationToken cancellationToken)
    {
        var tenantId = message.TenantId!.Value;
        var inbox = _database.GetCollection<GateIAuditInboxMessage>(InboxCollectionName);
        var auditOutbox = _database.GetCollection<AuditOutboxMessage>(AuditOutboxCollectionName);
        var metadataDigest = TrustedMetadataDigest(message, mapping, intent.VerifiedSigningIdentity);
        var idempotencyKey = $"gate-i.audit-intent:{ConsumerName}:{message.EventId:D}";

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var existing = await inbox.Find(session, IdentityFilter(message.EventId))
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                EnsureIdentical(existing, intent.PayloadSha256, metadataDigest);
                await session.AbortTransactionAsync(CancellationToken.None);
                return GateIAuditAcceptanceResult.Duplicate;
            }

            var outbox = BuildAuditOutbox(message, intent, mapping, tenantId, idempotencyKey);
            outbox.ValidateForInsert();
            await inbox.InsertOneAsync(session, new GateIAuditInboxMessage
            {
                ConsumerName = ConsumerName,
                EventId = message.EventId.ToString("D"),
                TenantId = tenantId,
                PayloadSha256 = intent.PayloadSha256,
                TrustedMetadataSha256 = metadataDigest,
                AuditOutboxIdempotencyKey = idempotencyKey,
                AcceptedAtUtc = _timeProvider.GetUtcNow()
            }, cancellationToken: cancellationToken);
            await auditOutbox.InsertOneAsync(session, outbox, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return GateIAuditAcceptanceResult.Accepted;
        }
        catch (MongoWriteException exception) when (IsRecoverableDuplicate(exception))
        {
            await AbortIfNeededAsync(session);
            var existing = await FindAsync(message.EventId, CancellationToken.None);
            if (existing is null)
            {
                throw;
            }

            EnsureIdentical(existing, intent.PayloadSha256, metadataDigest);
            return GateIAuditAcceptanceResult.Duplicate;
        }
        catch
        {
            await AbortIfNeededAsync(session);
            throw;
        }
    }

    private FilterDefinition<GateIAuditInboxMessage> IdentityFilter(Guid eventId) =>
        Builders<GateIAuditInboxMessage>.Filter.And(
            Builders<GateIAuditInboxMessage>.Filter.Eq(item => item.ConsumerName, ConsumerName),
            Builders<GateIAuditInboxMessage>.Filter.Eq(item => item.EventId, eventId.ToString("D")));

    private async Task<GateIAuditInboxMessage?> FindAsync(Guid eventId, CancellationToken cancellationToken) =>
        await _database.GetCollection<GateIAuditInboxMessage>(InboxCollectionName)
            .Find(IdentityFilter(eventId), new FindOptions { Hint = InboxUniqueIndexName })
            .FirstOrDefaultAsync(cancellationToken);

    private static void EnsureIdentical(GateIAuditInboxMessage existing, string payloadDigest, string metadataDigest)
    {
        if (!string.Equals(existing.PayloadSha256, payloadDigest, StringComparison.Ordinal)
            || !string.Equals(existing.TrustedMetadataSha256, metadataDigest, StringComparison.Ordinal))
        {
            throw new GateIAuditConflictException();
        }
    }

    private static async Task AbortIfNeededAsync(IClientSessionHandle session)
    {
        if (session.IsInTransaction)
        {
            await session.AbortTransactionAsync(CancellationToken.None);
        }
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

    private static string TrustedMetadataDigest(
        EventTransportMessage message,
        GateIAuditMapping mapping,
        string verifiedSigningIdentity)
    {
        var headers = message.TransportMetadata.Headers;
        var canonical = string.Join('\n',
            ConsumerName,
            message.EventId.ToString("D"),
            message.EventName,
            message.EventVersion.ToString(CultureInfo.InvariantCulture),
            message.TenantId!.Value.ToString("D"),
            message.CorrelationId.ToString("D"),
            message.CausationId?.ToString("D") ?? "-",
            message.Producer,
            message.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            mapping.ModuleCode,
            verifiedSigningIdentity,
            headers[TrustedTransportMetadata.SignatureSchemeHeader],
            headers[TrustedTransportMetadata.KeyIdHeader],
            headers[TrustedTransportMetadata.SignatureHeader]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static AuditOutboxMessage BuildAuditOutbox(
        EventTransportMessage message,
        GateIAuditIntent intent,
        GateIAuditMapping mapping,
        Guid tenantId,
        string idempotencyKey) => new()
    {
        TenantId = tenantId,
        CorrelationId = message.CorrelationId,
        IdempotencyKey = idempotencyKey,
        RequestType = mapping.EventName,
        Operation = mapping.Operation,
        EntityType = mapping.AuditEntityType,
        EntityId = intent.EntityId,
        Payload = new Dictionary<string, object?>
        {
            ["TenantId"] = tenantId,
            ["CorrelationId"] = message.CorrelationId,
            ["RequestType"] = mapping.EventName,
            ["ActorType"] = AuditActorType.Service.ToString(),
            ["ActorId"] = intent.ActorId,
            ["TargetTenantId"] = null,
            ["Category"] = mapping.Category.ToString(),
            ["EntityType"] = mapping.AuditEntityType,
            ["EntityId"] = intent.EntityId,
            ["Operation"] = mapping.Operation.ToString(),
            ["Outcome"] = mapping.Outcome.ToString(),
            ["BeforeState"] = null,
            ["AfterState"] = null,
            ["Metadata"] = new Dictionary<string, object?>
            {
                ["AuditIntentId"] = intent.AuditIntentId,
                ["EventVersion"] = message.EventVersion,
                ["ExecutingService"] = mapping.Producer
            },
            ["OccurredAtUtc"] = intent.OccurredAtUtc,
            ["SourceService"] = mapping.Producer,
            ["SourceModule"] = mapping.ModuleCode,
            ["IsMetaAudit"] = false,
            ["RedactionStatus"] = AuditRedactionStatus.None.ToString()
        }
    };
}
