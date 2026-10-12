using Diten.InventoryService.Domain.Expiry.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// MOD-0176 indexes (pack §8.8). The unique ones are load-bearing:
/// <list type="bullet">
/// <item><c>ux_expiry_policy_company</c> — one policy row per Tenant + LegalEntity (two concurrent first saves: one wins);</item>
/// <item><c>ux_expiry_exception_active</c> — one ACTIVE case per company + dedup key (manual / sweeper / hook); the key is cleared
/// when the case reaches a terminal status, so the partial index frees it;</item>
/// <item><c>ux_expiry_exception_create_key</c> — one case per (company, opener, idempotency key);</item>
/// <item><c>ux_expiry_exception_signature</c> — a MOD-0022 signature id closes ONE case;</item>
/// <item><c>ux_expiry_event_key</c> — one event per (case, actor, idempotency key); <c>ux_expiry_event_once</c> — once-only events
/// (<c>SlaBreached</c>) happen once per case;</item>
/// <item><c>ux_expiry_exception_number</c> — the case number per tenant.</item>
/// </list>
/// </summary>
public static class ExpiryIndexConfiguration
{
    public const string PolicyCompanyIndex = "ux_expiry_policy_company";
    public const string ExceptionNumberIndex = "ux_expiry_exception_number";
    public const string ExceptionActiveIndex = "ux_expiry_exception_active";
    public const string ExceptionCreateKeyIndex = "ux_expiry_exception_create_key";
    public const string ExceptionSignatureIndex = "ux_expiry_exception_signature";
    public const string EventKeyIndex = "ux_expiry_event_key";
    public const string EventOnceIndex = "ux_expiry_event_once";

    public static readonly IReadOnlyList<string> UniqueIndexes =
    [
        PolicyCompanyIndex, ExceptionNumberIndex, ExceptionActiveIndex, ExceptionCreateKeyIndex, ExceptionSignatureIndex, EventKeyIndex, EventOnceIndex
    ];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<ExpiryPolicy>(ExpiryCollections.Policies).Indexes.CreateOneAsync(
            new CreateIndexModel<ExpiryPolicy>(
                Builders<ExpiryPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = PolicyCompanyIndex, Unique = true }),
            cancellationToken: ct);

        await database.GetCollection<ExpiryPolicyHistoryEntry>(ExpiryCollections.PolicyHistory).Indexes.CreateOneAsync(
            new CreateIndexModel<ExpiryPolicyHistoryEntry>(
                Builders<ExpiryPolicyHistoryEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Descending(x => x.ChangedAtUtc),
                new CreateIndexOptions { Name = "ix_expiry_policy_history" }),
            cancellationToken: ct);

        var cases = Builders<FefoExceptionCase>.IndexKeys;
        await database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions).Indexes.CreateManyAsync(
        [
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.Number), new CreateIndexOptions { Name = ExceptionNumberIndex, Unique = true }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.DedupKey), new CreateIndexOptions<FefoExceptionCase>
                { Name = ExceptionActiveIndex, Unique = true, PartialFilterExpression = IsString(nameof(FefoExceptionCase.DedupKey)) }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.OpenedBy).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<FefoExceptionCase>
                    { Name = ExceptionCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(FefoExceptionCase.CreateIdempotencyKey)) }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.ResolutionSignatureId), new CreateIndexOptions<FefoExceptionCase>
                { Name = ExceptionSignatureIndex, Unique = true, PartialFilterExpression = IsString(nameof(FefoExceptionCase.ResolutionSignatureId)) }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.Type).Ascending(x => x.OpenedAtUtc),
                new CreateIndexOptions { Name = "ix_expiry_exception_list" }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.DueAtUtc),
                new CreateIndexOptions { Name = "ix_expiry_exception_due" })
        ], ct);

        var events = Builders<FefoExceptionEvent>.IndexKeys;
        await database.GetCollection<FefoExceptionEvent>(ExpiryCollections.ExceptionEvents).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.ActorUserId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<FefoExceptionEvent>
                    { Name = EventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(FefoExceptionEvent.IdempotencyKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.OnceKey), new CreateIndexOptions<FefoExceptionEvent>
                { Name = EventOnceIndex, Unique = true, PartialFilterExpression = IsString(nameof(FefoExceptionEvent.OnceKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.Sequence), new CreateIndexOptions { Name = "ix_expiry_events" })
        ], ct);
    }
}

/// <summary>
/// Startup gate of the MOD-0176 indexes (the MOD-0173 <c>InventoryIndexInitializerHostedService</c> rule: the unique indexes carry
/// idempotency and "one active case", so the host refuses to start without them — fail-closed). Its own hosted service, so the
/// shared MOD-0173 index file stays untouched.
/// </summary>
public sealed class ExpiryIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiryIndexInitializerHostedService> _logger;

    public ExpiryIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<ExpiryIndexInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        try
        {
            await ExpiryIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0176 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0176 index gate failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
