using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Repositories;

public sealed class AuditIntentRepository(PpmMongoContext context) : IAuditIntentRepository
{
    public async Task AddAsync(AuditIntent intent, CancellationToken cancellationToken)
    {
        if (intent.Id == Guid.Empty ||
            intent.TenantId == Guid.Empty ||
            intent.ActorId == Guid.Empty ||
            intent.CorrelationId == Guid.Empty ||
            intent.EntityId == Guid.Empty ||
            string.IsNullOrWhiteSpace(intent.EntityType) ||
            string.IsNullOrWhiteSpace(intent.Mutation) ||
            intent.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("Audit intent contains invalid required data.");
        }

        var document = new AuditIntentDocument
        {
            Id = intent.Id,
            TenantId = intent.TenantId,
            ActorId = intent.ActorId,
            CorrelationId = intent.CorrelationId,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Mutation = intent.Mutation,
            OccurredAtUtc = intent.OccurredAtUtc
        };

        var session = context.RequireTransaction();
        await context.AuditIntents.InsertOneAsync(
            session,
            document,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AuditIntentDispatchCandidate>> GetDispatchCandidatesAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var filter = Builders<AuditIntentDocument>.Filter.And(
            Builders<AuditIntentDocument>.Filter.Eq(
                item => item.OutboxEnqueuedAtUtc,
                null),
            Builders<AuditIntentDocument>.Filter.Eq(
                item => item.DispatchFailureCode,
                null));
        var documents = await context.AuditIntents
            .Find(filter)
            .SortBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return documents.Select(ToDispatchCandidate).ToArray();
    }

    public async Task<bool> MarkOutboxEnqueuedAsync(
        Guid intentId,
        DateTime enqueuedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateMarkerArguments(intentId, enqueuedAtUtc);
        var result = await context.AuditIntents.UpdateOneAsync(
            item => item.Id == intentId
                    && item.OutboxEnqueuedAtUtc == null
                    && item.DispatchFailureCode == null,
            Builders<AuditIntentDocument>.Update
                .Set(item => item.OutboxEnqueuedAtUtc, enqueuedAtUtc)
                .Set(item => item.DispatchUpdatedAtUtc, enqueuedAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<AuditIntentDispatchMetadata> EnsureDispatchMetadataAsync(
        Guid intentId,
        AuditIntentDispatchMetadata proposed,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateMarkerArguments(intentId, updatedAtUtc);
        ValidateDispatchMetadata(proposed);

        await context.AuditIntents.UpdateOneAsync(
            item => item.Id == intentId
                    && item.OutboxEnqueuedAtUtc == null
                    && item.DispatchFailureCode == null
                    && item.DispatchSignatureScheme == null
                    && item.DispatchKeyId == null
                    && item.DispatchSignature == null,
            Builders<AuditIntentDocument>.Update
                .Set(item => item.DispatchSignatureScheme, proposed.SignatureScheme)
                .Set(item => item.DispatchKeyId, proposed.KeyId)
                .Set(item => item.DispatchSignature, proposed.Signature)
                .Set(item => item.DispatchUpdatedAtUtc, updatedAtUtc),
            cancellationToken: cancellationToken);

        var persisted = await context.AuditIntents
            .Find(item => item.Id == intentId)
            .Project(item => new AuditIntentDispatchMetadata(
                item.DispatchSignatureScheme!,
                item.DispatchKeyId!,
                item.DispatchSignature!))
            .FirstOrDefaultAsync(cancellationToken);
        if (persisted is null)
        {
            throw new InvalidOperationException(
                "Audit intent is unavailable for dispatch metadata selection.");
        }

        ValidateDispatchMetadata(persisted);
        return persisted;
    }

    public async Task<bool> MarkDispatchQuarantinedAsync(
        Guid intentId,
        string failureCode,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateMarkerArguments(intentId, updatedAtUtc);
        if (!IsStableFailureCode(failureCode))
        {
            throw new ArgumentException(
                "Dispatch failure code must be a stable machine-readable value.",
                nameof(failureCode));
        }

        var result = await context.AuditIntents.UpdateOneAsync(
            item => item.Id == intentId
                    && item.OutboxEnqueuedAtUtc == null
                    && item.DispatchFailureCode == null,
            Builders<AuditIntentDocument>.Update
                .Set(item => item.DispatchFailureCode, failureCode)
                .Set(item => item.DispatchUpdatedAtUtc, updatedAtUtc),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private static AuditIntentDispatchCandidate ToDispatchCandidate(
        AuditIntentDocument document) =>
        new(
            document.Id,
            document.TenantId,
            document.ActorId,
            document.CorrelationId ?? Guid.Empty,
            document.EntityType,
            document.EntityId,
            document.Mutation,
            document.OccurredAtUtc,
            document.DispatchSignatureScheme is null
                && document.DispatchKeyId is null
                && document.DispatchSignature is null
                ? null
                : new AuditIntentDispatchMetadata(
                    document.DispatchSignatureScheme ?? string.Empty,
                    document.DispatchKeyId ?? string.Empty,
                    document.DispatchSignature ?? string.Empty));

    private static void ValidateMarkerArguments(Guid intentId, DateTime timestampUtc)
    {
        if (intentId == Guid.Empty)
        {
            throw new ArgumentException("Audit intent identifier must be non-empty.", nameof(intentId));
        }

        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Dispatch timestamp must be UTC.", nameof(timestampUtc));
        }
    }

    private static bool IsStableFailureCode(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character =>
            character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '.'
                or '-');

    private static void ValidateDispatchMetadata(AuditIntentDispatchMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (string.IsNullOrWhiteSpace(metadata.SignatureScheme)
            || string.IsNullOrWhiteSpace(metadata.KeyId)
            || metadata.KeyId.Length > 128
            || metadata.KeyId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
            || metadata.Signature.Length != 64
            || metadata.Signature.Any(character =>
                character is not (>= '0' and <= '9')
                    and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Audit intent dispatch metadata is invalid.");
        }
    }
}
