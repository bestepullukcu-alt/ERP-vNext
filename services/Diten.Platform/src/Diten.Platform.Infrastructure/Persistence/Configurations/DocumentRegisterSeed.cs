using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// WP-DM-1b — tenant-AGNOSTIC, config-driven startup seed of the Document Master Register from the GMG reference CSV.
/// Dev-only + explicitly enabled + config-tenant + marker-gated. The target tenant comes from
/// <c>DocumentRegisterSeed:TenantId</c> — NO hardcode (the legacy PositionAssignmentSeed 97c5 hardcode is not
/// followed); absent/blank/prod ⇒ skip, no leakage. Reuses the DM-1a mapping (<see cref="DocumentRegisterIngestMapping"/>)
/// and the quoted-aware parser, and does the same idempotent (TenantId, PermanentUid) upsert — here directly on the
/// collection (the established seed pattern; the runtime ingest path is the MediatR command handler). Metadata only;
/// no real files (DM-4/MOD-0262).
/// </summary>
internal static class DocumentRegisterSeed
{
    public const string MarkerKeyPrefix = "document-register-seed:";

    public static async Task EnsureSeededAsync(
        IMongoDatabase database,
        DocumentRegisterSeedOptions options,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        // Every gate must hold; any unmet ⇒ skip. Prod-safe (dev-only), never a guessed/hardcoded tenant. The pure
        // predicate is unit-tested (leakage guard) in DocumentRegisterSeedGate; here we also resolve the tenant id.
        // A relative CsvPath (the committed dev config's "Seed/document-management/…") resolves against the runtime
        // directory via Path.GetFullPath — the SAME resolution the BRD catalog loader uses. The gate's null/blank
        // guard runs first, so Path.GetFullPath is only ever handed a non-blank path (it throws on empty).
        if (!DocumentRegisterSeedGate.ShouldRun(options, environment.IsDevelopment(), p => File.Exists(Path.GetFullPath(p)))
            || !options.TryGetTenantId(out var tenantId))
        {
            return;
        }

        var markerKey = MarkerKeyPrefix + tenantId.ToString("D");
        var markerExists = await SeedMarkerStore.ExistsAsync(database, markerKey, ct);

        var collection = database.GetCollection<DocumentMasterRegisterEntry>(
            PlatformCollections.DocumentManagementMasterRegister);
        var hasLiveRecords = await collection.Find(x => x.TenantId == tenantId && !x.IsDeleted).AnyAsync(ct);

        var decision = BootstrapSeedPolicy.Decide(markerExists, hasLiveRecords);
        if (decision == SeedDecision.Skip)
        {
            return;
        }

        if (decision == SeedDecision.SeedAndMark)
        {
            try
            {
                await IngestAsync(collection, options.CsvPath!, tenantId, ct);
            }
            catch (Exception ex)
            {
                // A dev convenience seed must not crash platform startup on a CSV hiccup. Log, and DO NOT set the
                // marker, so a corrected file re-seeds on the next startup (idempotent upsert makes the retry safe).
                Console.WriteLine($"DocumentRegisterSeed skipped for tenant {tenantId}: {ex.Message}");
                return;
            }
        }

        // SeedAndMark and MarkOnly both set the marker; MarkOnly preserves operator-curated data without seeding.
        await SeedMarkerStore.SetAsync(database, markerKey, ct);
    }

    private static async Task IngestAsync(
        IMongoCollection<DocumentMasterRegisterEntry> collection, string csvPath, Guid tenantId, CancellationToken ct)
    {
        // Relative path → resolved against the runtime directory (BRD catalog-loader parity); absolute path is
        // returned unchanged by Path.GetFullPath (idempotent — existing absolute-path behaviour is unaffected).
        var csv = await File.ReadAllTextAsync(Path.GetFullPath(csvPath), ct);
        var parsed = DocumentReferenceListParser.Parse(csv, tenantId);
        if (parsed.MissingColumns.Count > 0)
        {
            Console.WriteLine(
                $"DocumentRegisterSeed: reference CSV missing columns [{string.Join(", ", parsed.MissingColumns)}] — not seeded.");
            return;
        }

        foreach (var src in parsed.Entries)
        {
            if (string.IsNullOrWhiteSpace(src.Title))
            {
                continue; // DocumentTitle is required; an empty-title row is skipped (surfaced by the runtime handler).
            }

            var existing = await collection
                .Find(x => x.TenantId == tenantId && x.PermanentUid == src.DocumentUid && !x.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing is null)
            {
                var entry = new DocumentMasterRegisterEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DocumentTitle = src.Title,
                    CreatedBy = "document-register-seed"
                };
                DocumentRegisterIngestMapping.Apply(entry, src);
                await collection.InsertOneAsync(entry, cancellationToken: ct);
            }
            else
            {
                DocumentRegisterIngestMapping.Apply(existing, src);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.UpdatedBy = "document-register-seed";
                await collection.ReplaceOneAsync(x => x.Id == existing.Id, existing, cancellationToken: ct);
            }
        }
    }
}
