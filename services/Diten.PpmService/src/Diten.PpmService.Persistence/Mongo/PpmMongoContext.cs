using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
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

    public IMongoCollection<AuditIntentDocument> AuditIntents =>
        Database.GetCollection<AuditIntentDocument>(PpmCollectionNames.AuditIntents);

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
    public const string AuditIntents = "ppm_audit_intents";
}

public sealed class AuditIntentDocument
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ActorId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Mutation { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
}
