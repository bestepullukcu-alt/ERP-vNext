using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Persistence.Repositories;
using Diten.HumanCapitalService.Persistence.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Migrations;

/// <summary>
/// One-shot startup migration for the legal-entity scoping pilot: stamps the tenant root holding onto
/// applicant-intake rows created before scoping (LegalEntityId == empty), and reconciles the stale
/// unique index that keyed on (TenantId, Code) so the repository can (re)create the scoped
/// (TenantId, LegalEntityId, Code) index. Both steps are idempotent and safe to run on every boot.
/// </summary>
public sealed class ApplicantIntakeLegalEntityBackfillService : IHostedService
{
    // GRAND HOLDING (tenant root) — the backfill target for records that predate legal-entity scoping.
    private static readonly Guid RootHoldingLegalEntityId = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");

    private readonly IMongoClient _mongoClient;
    private readonly MongoSettings _mongoSettings;
    private readonly ILogger<ApplicantIntakeLegalEntityBackfillService> _logger;

    public ApplicantIntakeLegalEntityBackfillService(
        IMongoClient mongoClient,
        MongoSettings mongoSettings,
        ILogger<ApplicantIntakeLegalEntityBackfillService> logger)
    {
        _mongoClient = mongoClient;
        _mongoSettings = mongoSettings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collection = _mongoClient
                .GetDatabase(_mongoSettings.DatabaseName)
                .GetCollection<ApplicantIntakeReadinessMetadata>(
                    MongoApplicantIntakeReadinessMetadataRepository.CollectionName);

            await ReconcileStaleUniqueIndexAsync(collection, cancellationToken);
            await BackfillLegalEntityAsync(collection, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never block startup on the migration; the repository re-ensures indexes lazily and the
            // backfill can be re-run on the next boot.
            _logger.LogError(ex, "Applicant intake legal-entity backfill migration failed; continuing startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task BackfillLegalEntityAsync(
        IMongoCollection<ApplicantIntakeReadinessMetadata> collection,
        CancellationToken ct)
    {
        // Records created before scoping have no LegalEntityId field at all; an absent field does not
        // equal the empty-guid binary, so match both the absent-field and empty-value cases.
        var missing = Builders<ApplicantIntakeReadinessMetadata>.Filter.Or(
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Exists(x => x.LegalEntityId, false),
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, Guid.Empty));
        var update = Builders<ApplicantIntakeReadinessMetadata>.Update
            .Set(x => x.LegalEntityId, RootHoldingLegalEntityId);

        var result = await collection.UpdateManyAsync(missing, update, cancellationToken: ct);
        if (result.ModifiedCount > 0)
        {
            _logger.LogInformation(
                "Backfilled {Count} applicant intake readiness record(s) to the tenant root holding {LegalEntityId}.",
                result.ModifiedCount,
                RootHoldingLegalEntityId);
        }
    }

    private async Task ReconcileStaleUniqueIndexAsync(
        IMongoCollection<ApplicantIntakeReadinessMetadata> collection,
        CancellationToken ct)
    {
        using var cursor = await collection.Indexes.ListAsync(ct);
        var indexes = await cursor.ToListAsync(ct);

        var existing = indexes.FirstOrDefault(index =>
            index.TryGetValue("name", out var name)
            && name.AsString == MongoApplicantIntakeReadinessMetadataRepository.ActiveCodeUniqueIndexName);

        if (existing is null)
        {
            return;
        }

        // Drop the index only when its key spec no longer matches the scoped (TenantId, LegalEntityId, Code)
        // shape, so the repository's lazy EnsureIndexes can recreate it without an IndexKeySpecsConflict.
        var key = existing.GetValue("key", null)?.AsBsonDocument;
        var isScopedKey = key is not null
            && key.ElementCount == 3
            && key.Contains("TenantId")
            && key.Contains("LegalEntityId")
            && key.Contains("Code");

        if (!isScopedKey)
        {
            await collection.Indexes.DropOneAsync(
                MongoApplicantIntakeReadinessMetadataRepository.ActiveCodeUniqueIndexName,
                ct);
            _logger.LogInformation(
                "Dropped stale applicant intake unique index {IndexName} for legal-entity re-scoping.",
                MongoApplicantIntakeReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        }
    }
}
