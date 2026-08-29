using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// FIX-TASKS-MODULE-NAME — carries the <c>tasks</c> module's catalog <c>DisplayName</c> from the old
/// "Görevler / Tasks" to "Görev Tanımları / Task Settings".
///
/// <para><b>Why a migration is needed at all.</b> Measured in
/// <c>RegisterModuleManifestCommandHandler</c>: <c>DisplayName</c> is a <b>SOFT</b> (seed-once, operator-owned)
/// catalog field — a manifest re-push refreshes only the HARD fields and NEVER overwrites it. Editing the
/// manifest string alone would therefore change nothing on any environment where the module has already
/// registered once: the catalog row would keep printing the old name forever. Only a migration moves it.</para>
///
/// <para><b>Why it does not clobber the operator.</b> Renaming a module in the catalog admin is a legitimate
/// operator act, and SOFT means exactly that this rename belongs to them. So the row is rewritten ONLY when it
/// still holds the value the code originally seeded. Anything else — including a rename an operator typed by
/// hand — is left untouched and logged, so the change is visible rather than silent.</para>
///
/// <para>NOT marker-gated, following the <see cref="ModuleCatalogDomainCanonicalizationMigration"/> precedent:
/// the "only rewrite the exact old seed" rule makes it self-idempotent, so a second run plans zero writes. That
/// also heals an environment that registers the module fresh from an older build.</para>
///
/// <para><b>What the tenant actually sees is not this string.</b> The sidebar localizes the module by its stable
/// code (<c>Nav.Module.TASKS</c>, all seven tenant languages) and falls back to this name only when that key is
/// missing. This migration keeps the catalog — the operator's view, and the fallback — in step with the resx.</para>
/// </summary>
public static class TaskModuleDisplayNameRenameMigration
{
    private const string Actor = "system:tasks-module-display-name-rename";
    private const string ModuleCode = "tasks";

    /// <summary>The value the manifest seeded before the rename. Only this exact string is rewritten.</summary>
    internal const string OldDisplayName = "Görevler / Tasks";

    /// <summary>The value TaskManifestProvider now declares. Kept in sync by TaskModuleDisplayNameRenameMigrationTests.</summary>
    internal const string NewDisplayName = "Görev Tanımları / Task Settings";

    /// <summary>One rename decision. Pure output of <see cref="Plan"/> (no IO).</summary>
    public sealed record Rename(Guid ItemId, string OldDisplayName);

    /// <summary>
    /// Pure, unit-testable core. Emits a rename ONLY for a <c>tasks</c> row still holding the old seeded value —
    /// so a re-run over an already-renamed (or operator-renamed) catalog yields an empty list. That IS the
    /// idempotency and the do-not-clobber guarantee, in one rule.
    /// </summary>
    public static IReadOnlyList<Rename> Plan(IEnumerable<ModuleCatalogItem> liveItems, out IReadOnlyList<string> skipped)
    {
        var renames = new List<Rename>();
        var untouched = new List<string>();

        foreach (var item in liveItems)
        {
            if (!string.Equals(item.ModuleCode, ModuleCode, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var current = item.DisplayName ?? string.Empty;
            if (string.Equals(current.Trim(), OldDisplayName, StringComparison.Ordinal))
            {
                renames.Add(new Rename(item.Id, current));
            }
            else if (!string.Equals(current.Trim(), NewDisplayName, StringComparison.Ordinal))
            {
                // An operator rename (or an unrecognised value). SOFT means it is theirs — leave it, but say so.
                untouched.Add(current);
            }
        }

        skipped = untouched;
        return renames;
    }

    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var catalog = database.GetCollection<ModuleCatalogItem>(PlatformCollections.ModuleCatalog);
        var items = await catalog
            .Find(Builders<ModuleCatalogItem>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        var renames = Plan(items, out var skipped);

        foreach (var value in skipped)
        {
            Console.Error.WriteLine(
                $"[TaskModuleDisplayNameRenameMigration] Module 'tasks' DisplayName is '{value}', which is neither " +
                $"the old seed nor the new name — left as-is (DisplayName is operator-owned). Rename it in the " +
                $"module catalog if it should read '{NewDisplayName}'.");
        }

        foreach (var rename in renames)
        {
            await catalog.UpdateOneAsync(
                Builders<ModuleCatalogItem>.Filter.Eq(x => x.Id, rename.ItemId),
                Builders<ModuleCatalogItem>.Update
                    .Set(x => x.DisplayName, NewDisplayName)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, Actor),
                cancellationToken: ct);

            Console.Error.WriteLine(
                $"[TaskModuleDisplayNameRenameMigration] Module 'tasks': " +
                $"DisplayName '{rename.OldDisplayName}' → '{NewDisplayName}'.");
        }
    }
}
