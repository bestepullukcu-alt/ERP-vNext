using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;

public sealed class PpmMongoContext
{
    private readonly AsyncLocal<IClientSessionHandle?> _ambientSession = new();

    public PpmMongoContext(IMongoClient client, IMongoDatabase database)
    {
        Client = client;
        Database = database;
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IClientSessionHandle? CurrentSession => _ambientSession.Value;

    public IMongoCollection<Portfolio> Portfolios =>
        Database.GetCollection<Portfolio>(PpmCollectionNames.Portfolios);

    public IMongoCollection<Initiative> Initiatives =>
        Database.GetCollection<Initiative>(PpmCollectionNames.Initiatives);

    public IMongoCollection<Program> Programs =>
        Database.GetCollection<Program>(PpmCollectionNames.Programs);

    public IMongoCollection<Project> Projects =>
        Database.GetCollection<Project>(PpmCollectionNames.Projects);

    public IMongoCollection<InvestmentCase> InvestmentCases =>
        Database.GetCollection<InvestmentCase>(PpmCollectionNames.InvestmentCases);

    public IMongoCollection<BenefitCommitment> BenefitCommitments =>
        Database.GetCollection<BenefitCommitment>(PpmCollectionNames.BenefitCommitments);

    public IMongoCollection<AuditIntentDocument> AuditIntents =>
        Database.GetCollection<AuditIntentDocument>(PpmCollectionNames.AuditIntents);

    public IMongoCollection<PpmEventOutboxDocument> EventOutbox =>
        Database.GetCollection<PpmEventOutboxDocument>(PpmCollectionNames.EventOutbox);

    public IMongoCollection<GateIMutationReceiptDocument> GateIMutationReceipts =>
        Database.GetCollection<GateIMutationReceiptDocument>(PpmCollectionNames.GateIMutationReceipts);

    public IDisposable EnterSession(IClientSessionHandle session)
    {
        if (_ambientSession.Value is not null)
            throw new InvalidOperationException("Nested PPM Mongo transactions are not supported.");

        _ambientSession.Value = session;
        return new SessionScope(_ambientSession);
    }

    public IClientSessionHandle RequireTransaction()
    {
        var session = CurrentSession;
        if (session is null || !session.IsInTransaction)
            throw new TransactionUnavailableException(
                "PPM mutations and audit intents require an active Mongo replica-set transaction.");

        return session;
    }

    private sealed class SessionScope(AsyncLocal<IClientSessionHandle?> ambientSession) : IDisposable
    {
        public void Dispose() => ambientSession.Value = null;
    }
}

public static class PpmCollectionNames
{
    public const string Portfolios = "ppm_portfolios";
    public const string Initiatives = "ppm_initiatives";
    public const string Programs = "ppm_programs";
    public const string Projects = "ppm_projects";
    public const string InvestmentCases = "ppm_investment_cases";
    public const string BenefitCommitments = "ppm_benefit_commitments";
    public const string AuditIntents = "ppm_audit_intents";
    public const string EventOutbox = "ppm_event_outbox";
    public const string GateIMutationReceipts = "ppm_gate_i_mutation_receipts";
}

public sealed class GateIMutationReceiptDocument
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string OperationId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RequestHash { get; init; } = string.Empty;
    public string ProvenanceHash { get; init; } = string.Empty;
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; }
    public int StatusCode { get; init; }
    public string StableCode { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AuditIntentDocument
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ActorId { get; init; }
    public Guid? CorrelationId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Mutation { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? OutboxEnqueuedAtUtc { get; init; }
    public string? DispatchSignatureScheme { get; init; }
    public string? DispatchKeyId { get; init; }
    public string? DispatchSignature { get; init; }
    public string? DispatchFailureCode { get; init; }
    public DateTime? DispatchUpdatedAtUtc { get; init; }
}

public sealed class PpmEventOutboxDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public int EventVersion { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public Guid? TenantId { get; init; }
    public string Producer { get; init; } = string.Empty;
    public long OccurredAtUtcTicks { get; init; }
    public byte[] CanonicalPayloadUtf8 { get; init; } = [];
    public Dictionary<string, string> TransportHeaders { get; init; } =
        new(StringComparer.Ordinal);
    public EventOutboxDeliveryStatus Status { get; init; } = EventOutboxDeliveryStatus.Pending;
    public int AttemptCount { get; init; }
    public DateTime? NextAttemptAtUtc { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
