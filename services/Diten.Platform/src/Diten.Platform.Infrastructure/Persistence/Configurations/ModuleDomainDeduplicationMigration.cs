using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Catalog;
using Diten.Platform.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// FIX-DOMAIN-DEDUP — collapses duplicate live rows in <c>platform_module_domains</c> that share a normalized
/// key (Codes differing only by separators/case, e.g. "MASTER-DATA-MANAGEMENT" vs "MASTERDATAMANAGEMENT" — the
/// result of three creation paths with inconsistent Code conventions) and backfills the persisted
/// <c>CodeKey</c> on every live row, so the unique partial index <c>ux_platform_module_domains_code_key</c> can
/// be created cleanly. For each normalized-key group it keeps ONE canonical survivor — merging the
/// operator-meaningful values (active if any row is active; the lowest meaningful SortOrder; a human
/// DisplayName; the first Description) — rewrites its Code to the canonical UPPERCASE-no-separator form, and
/// soft-deletes the rest. Idempotent + re-runnable: once collapsed no key has &gt;1 live row and every survivor
/// is already canonical, so a re-run writes nothing. Runs BEFORE index creation in DI startup.
/// </summary>
public static class ModuleDomainDeduplicationMigration
{
    private const string Actor = "system:module-domain-dedup";

    /// <summary>The merge decision for one normalized-key group. Pure output of <see cref="Plan"/> (no IO).</summary>
    public sealed record DomainMergePlan(
        Guid SurvivorId,
        string CanonicalCode,
        bool IsActive,
        int SortOrder,
        string DisplayName,
        string? Description,
        IReadOnlyList<Guid> RedundantIds,
        bool SurvivorChanged,
        IReadOnlyList<int> ConflictingSortOrders);

    /// <summary>
    /// Pure, unit-testable core: given the live domain rows, produce one merge plan per normalized key. A plan is
    /// only emitted when it does real work (survivor fields change OR there are redundant rows to delete), so a
    /// re-run over an already-canonical single-row set yields an empty list.
    /// </summary>
    public static IReadOnlyList<DomainMergePlan> Plan(IEnumerable<ModuleDomain> liveRows)
    {
        var plans = new List<DomainMergePlan>();

        var groups = liveRows
            .Where(x => ModuleTaxonomyCanonicalizer.NormalizeKey(x.Code).Length > 0)
            .GroupBy(x => ModuleTaxonomyCanonicalizer.NormalizeKey(x.Code), StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var key = group.Key; // already UPPERCASE-no-separator → the canonical Code

            // One deterministic order drives BOTH survivor identity and value preference: active first, then a
            // meaningful (non-zero) SortOrder ascending, then oldest, then Id. Merged field values below don't
            // depend on which row is the survivor — this only fixes WHICH row's Id/CreatedAt is preserved.
            var rows = group
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.SortOrder == 0 ? int.MaxValue : r.SortOrder)
                .ThenBy(r => r.CreatedAt)
                .ThenBy(r => r.Id)
                .ToList();
            var survivor = rows[0];

            var mergedIsActive = rows.Any(r => r.IsActive);

            // "Non-default" SortOrder = non-zero (0 is the entity default / unset). Keep the lowest meaningful one;
            // surface a collision when several rows carry a meaningful order.
            var meaningfulSorts = rows.Where(r => r.SortOrder != 0).Select(r => r.SortOrder).Distinct().OrderBy(s => s).ToList();
            var mergedSort = meaningfulSorts.Count > 0 ? meaningfulSorts[0] : 0;

            // Prefer a human-friendly DisplayName — one that carries formatting (separators/mixed case) rather than
            // being the raw canonical code itself, e.g. keep "Master Data Management" over "MASTERDATAMANAGEMENT".
            // Fall back to the survivor's DisplayName, then to the code.
            var mergedDisplay = rows
                .Select(r => r.DisplayName?.Trim() ?? string.Empty)
                .FirstOrDefault(d => d.Length > 0 && !string.Equals(d, ModuleTaxonomyCanonicalizer.NormalizeKey(d), StringComparison.Ordinal));
            if (string.IsNullOrEmpty(mergedDisplay))
            {
                mergedDisplay = string.IsNullOrWhiteSpace(survivor.DisplayName) ? key : survivor.DisplayName.Trim();
            }

            var mergedDescription = rows
                .Select(r => r.Description?.Trim())
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

            var redundantIds = rows.Where(r => r.Id != survivor.Id).Select(r => r.Id).ToList();

            var survivorChanged =
                !string.Equals(survivor.Code, key, StringComparison.Ordinal)
                || !string.Equals(survivor.CodeKey, key, StringComparison.Ordinal)
                || survivor.IsActive != mergedIsActive
                || survivor.SortOrder != mergedSort
                || !string.Equals((survivor.DisplayName ?? string.Empty).Trim(), mergedDisplay, StringComparison.Ordinal)
                || !string.Equals(survivor.Description?.Trim(), mergedDescription, StringComparison.Ordinal);

            if (!survivorChanged && redundantIds.Count == 0)
            {
                continue; // already canonical single row → nothing to do (idempotent no-op)
            }

            plans.Add(new DomainMergePlan(
                survivor.Id, key, mergedIsActive, mergedSort, mergedDisplay, mergedDescription,
                redundantIds, survivorChanged, meaningfulSorts.Count > 1 ? meaningfulSorts : Array.Empty<int>()));
        }

        return plans;
    }

    /// <summary>
    /// Pure decision for the CodeKey backfill: given a row's <paramref name="code"/> and its CURRENTLY-STORED
    /// <paramref name="storedCodeKey"/> (null/absent on rows written before the field existed), return the canonical
    /// CodeKey and whether a write is needed. CRUCIAL: an ALREADY-CANONICAL Code with a null stored CodeKey still
    /// needs a write — that is the exact gap the first cut missed (it only set CodeKey when it REWROTE Code, so
    /// canonical rows kept a null CodeKey and broke the unique-index build). <c>NormalizesToEmpty</c> flags a Code
    /// that reduces to "" (all separators) — such a row cannot get a usable key and is surfaced loudly.
    /// </summary>
    public static (string NewCodeKey, bool NeedsWrite, bool NormalizesToEmpty) DecideCodeKeyBackfill(string? code, string? storedCodeKey)
    {
        var key = ModuleTaxonomyCanonicalizer.NormalizeKey(code);
        var needsWrite = !string.Equals(storedCodeKey, key, StringComparison.Ordinal);
        return (key, needsWrite, key.Length == 0);
    }

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<ModuleDomain>(PlatformCollections.ModuleDomains);

        var live = await collection
            .Find(Builders<ModuleDomain>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        foreach (var plan in Plan(live))
        {
            if (plan.ConflictingSortOrders.Count > 1)
            {
                Console.Error.WriteLine(
                    $"[ModuleDomainDeduplicationMigration] Domain key '{plan.CanonicalCode}' had conflicting SortOrders " +
                    $"[{string.Join(", ", plan.ConflictingSortOrders)}]; kept {plan.SortOrder}.");
            }

            if (plan.SurvivorChanged)
            {
                var update = Builders<ModuleDomain>.Update
                    .Set(x => x.Code, plan.CanonicalCode)     // canonical UPPERCASE-no-separator
                    .Set(x => x.CodeKey, plan.CanonicalCode)  // == NormalizeKey(Code); set explicitly for the backfill
                    .Set(x => x.IsActive, plan.IsActive)
                    .Set(x => x.SortOrder, plan.SortOrder)
                    .Set(x => x.DisplayName, plan.DisplayName)
                    .Set(x => x.Description, plan.Description)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, Actor);

                await collection.UpdateOneAsync(
                    Builders<ModuleDomain>.Filter.Eq(x => x.Id, plan.SurvivorId), update, cancellationToken: ct);
            }

            if (plan.RedundantIds.Count > 0)
            {
                await collection.UpdateManyAsync(
                    Builders<ModuleDomain>.Filter.In(x => x.Id, plan.RedundantIds),
                    Builders<ModuleDomain>.Update
                        .Set(x => x.IsDeleted, true)
                        .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                        .Set(x => x.UpdatedBy, Actor),
                    cancellationToken: ct);

                Console.Error.WriteLine(
                    $"[ModuleDomainDeduplicationMigration] Merged {plan.RedundantIds.Count + 1} rows for domain key " +
                    $"'{plan.CanonicalCode}' into survivor {plan.SurvivorId}; soft-deleted {plan.RedundantIds.Count}.");
            }
        }

        await BackfillCodeKeysAsync(database, ct);
    }

    /// <summary>
    /// Backfills <c>CodeKey</c> on EVERY live row via a direct <c>$set</c>, reading the RAW stored document so the
    /// entity's Code setter (which recomputes CodeKey in memory on deserialization) cannot mask a null persisted
    /// value. Idempotent — only rows whose stored CodeKey differs from <c>NormalizeKey(Code)</c> are written. After
    /// it runs it asserts no live row has a null/empty CodeKey; if any remain (a Code that normalizes to ""), it
    /// logs LOUDLY, because the unique partial index on CodeKey will then refuse to build and the collection would
    /// silently lose all uniqueness protection.
    /// </summary>
    private static async Task BackfillCodeKeysAsync(IMongoDatabase database, CancellationToken ct)
    {
        var bson = database.GetCollection<BsonDocument>(PlatformCollections.ModuleDomains);
        var liveFilter = Builders<BsonDocument>.Filter.Ne("IsDeleted", true); // absent or false → live
        var missingKeyFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("CodeKey", false),
            Builders<BsonDocument>.Filter.Eq("CodeKey", BsonNull.Value),
            Builders<BsonDocument>.Filter.Eq("CodeKey", string.Empty));

        var toBackfill = await bson
            .Find(Builders<BsonDocument>.Filter.And(liveFilter, missingKeyFilter))
            .ToListAsync(ct);

        // Distinct Code → canonical CodeKey among rows lacking a usable key. We update by CODE (a string) rather
        // than by _id, because the _id is a Guid whose binary representation depends on the client's
        // GuidRepresentation config — matching on it is fragile, whereas the string Code always compares cleanly.
        var byCode = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalizesToEmpty = new List<string>();
        foreach (var doc in toBackfill)
        {
            var code = doc.TryGetValue("Code", out var c) && c.IsString ? c.AsString : string.Empty;
            var (newCodeKey, _, isEmpty) = DecideCodeKeyBackfill(code, storedCodeKey: null); // filter already proved it's missing
            if (isEmpty)
            {
                normalizesToEmpty.Add(code);
                continue; // cannot produce a usable key — surfaced loudly below
            }
            byCode[code] = newCodeKey;
        }

        var modified = 0L;
        foreach (var (code, newCodeKey) in byCode)
        {
            var result = await bson.UpdateManyAsync(
                Builders<BsonDocument>.Filter.And(
                    liveFilter, missingKeyFilter, Builders<BsonDocument>.Filter.Eq("Code", code)),
                Builders<BsonDocument>.Update
                    .Set("CodeKey", newCodeKey)
                    .Set("UpdatedAt", DateTimeOffset.UtcNow)
                    .Set("UpdatedBy", Actor),
                cancellationToken: ct);
            modified += result.ModifiedCount;
        }

        if (modified > 0)
        {
            Console.Error.WriteLine(
                $"[ModuleDomainDeduplicationMigration] Backfilled CodeKey on {modified} live domain row(s).");
        }

        // Post-condition: no live row may have a null/empty CodeKey, else the unique index on CodeKey cannot build.
        var remaining = await bson.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.And(liveFilter, missingKeyFilter), cancellationToken: ct);

        if (remaining > 0)
        {
            Console.Error.WriteLine(
                $"[ModuleDomainDeduplicationMigration] ERROR: {remaining} live domain row(s) still have a null/empty " +
                $"CodeKey after backfill (Code(s) that normalize to empty: [{string.Join(", ", normalizesToEmpty)}]). " +
                "The unique index ux_platform_module_domains_code_key will NOT build — platform_module_domains has NO " +
                "uniqueness protection until these Codes are fixed.");
        }
    }
}
