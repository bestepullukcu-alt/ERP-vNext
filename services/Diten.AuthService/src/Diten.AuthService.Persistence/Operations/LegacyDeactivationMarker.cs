using System.Text.Json;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Operations;

/// <summary>
/// BL-529 FIX8 item 1 (a) — THE ONE-TIME POST-DEPLOY STEP for accounts an administrator deactivated BEFORE BL-529. Then a
/// deactivation set IsActive = false and nothing else: no mark (the field did not exist) and the pending set-password link
/// stayed. This marks every such account and ends its link — what a deactivation has done since BL-529.
/// <para><b>Which accounts:</b> not deleted, inactive, not marked, and NOT a pending invitation — the code's own rule,
/// <see cref="User.IsInvitationPending"/> (must change password, e-mail not confirmed, never signed in). A pending invitation
/// is the documented exception: it is inactive because it was never activated, and mechanically it cannot be told apart
/// from a deactivated one. All tenants, the platform tenant included: it is an operator's data step.</para>
/// <para>FIX9 — two steps, <see cref="FindAsync"/> then <see cref="MarkAsync"/>: the list an operator reads is the list the
/// mark is given, and every write re-checks the WHOLE rule on the stored document (still not deleted, still inactive, still
/// unmarked, still not a pending invitation, same tenant) — an activation, a mark, a deletion or a re-invitation that
/// landed after the list was read is never overwritten ("active AND marked" is never written). The read loads only the
/// fields the rule needs: never a password hash.</para>
/// </summary>
public static class LegacyDeactivationMarker
{
    public const string AuditEventName = "auth.legacy_deactivation_marker.run";

    public sealed record Account(Guid TenantId, Guid UserId);

    /// <param name="SkippedPending">Inactive, unmarked accounts left alone because they read as pending invitations, with the
    /// expiry of their link (ids only) — the exception an operator should see, not just trust.</param>
    public sealed record Findings(IReadOnlyList<Account> Found, IReadOnlyList<(Account Account, DateTime? LinkExpiresAt)> SkippedPending);

    public sealed record MarkResult(IReadOnlyList<Account> Marked, IReadOnlyList<Account> NotMarked);

    /// <summary>A failure part-way through <see cref="MarkAsync"/>: what was written before it, and what was not.</summary>
    public sealed class PartialMarkException(IReadOnlyList<Account> marked, IReadOnlyList<Account> remaining, Exception inner)
        : Exception("The marker stopped part-way.", inner)
    {
        public IReadOnlyList<Account> Marked { get; } = marked;
        public IReadOnlyList<Account> Remaining { get; } = remaining;
    }

    /// <summary>The tool's database: opened HERE, in Persistence (the only place a MongoClient is made), with the
    /// serialization Auth itself runs with (Persistence/DependencyInjection.cs — Guid as Standard, extra elements ignored).</summary>
    public static IMongoDatabase OpenDatabase(string connectionString, string databaseName)
    {
        Serialization.GuidSerializerRegistration.EnsureStandard();
        MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register(
            "IgnoreExtraElementsConvention",
            new MongoDB.Bson.Serialization.Conventions.ConventionPack
            {
                new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true)
            },
            _ => true);
        var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618 // the same setting Auth's own client carries (Persistence/DependencyInjection.cs)
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        return new MongoClient(settings).GetDatabase(databaseName);
    }

    public static async Task<Findings> FindAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var users = database.GetCollection<User>("users");
        var f = Builders<User>.Filter;
        var inactiveUnmarked = f.And(
            f.Eq(u => u.IsDeleted, false),
            f.Eq(u => u.IsActive, false),
            f.Ne(u => u.DeactivatedByAdministrator, true));
        // Only what the rule reads — never the password hash.
        var projection = Builders<User>.Projection
            .Include(u => u.Id).Include(u => u.TenantId).Include(u => u.IsActive).Include(u => u.IsDeleted)
            .Include(u => u.DeactivatedByAdministrator).Include(u => u.MustChangePassword).Include(u => u.EmailConfirmed)
            .Include(u => u.LastLoginAt).Include(u => u.PasswordResetTokenExpiresAt);
        var read = await users.Find(inactiveUnmarked).Project<User>(projection).ToListAsync(ct);

        var ordered = read.OrderBy(u => u.TenantId).ThenBy(u => u.Id).ToList();
        return new Findings(
            ordered.Where(u => !u.IsInvitationPending()).Select(u => new Account(u.TenantId, u.Id)).ToList(),
            ordered.Where(u => u.IsInvitationPending())
                .Select(u => (new Account(u.TenantId, u.Id), u.PasswordResetTokenExpiresAt)).ToList());
    }

    /// <summary>Marks the accounts of a list <see cref="FindAsync"/> produced. Each write re-checks the whole rule; an
    /// account that no longer matches is skipped (<see cref="MarkResult.NotMarked"/>), never overwritten.</summary>
    public static async Task<MarkResult> MarkAsync(IMongoDatabase database, IReadOnlyList<Account> accounts, CancellationToken ct = default)
    {
        var users = database.GetCollection<User>("users");
        var f = Builders<User>.Filter;
        var marked = new List<Account>();
        var notMarked = new List<Account>();
        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            UpdateResult result;
            try
            {
                result = await users.UpdateOneAsync(
                    f.And(
                        f.Eq(u => u.Id, account.UserId),
                        f.Eq(u => u.TenantId, account.TenantId),
                        f.Eq(u => u.IsDeleted, false),
                        f.Eq(u => u.IsActive, false),
                        f.Ne(u => u.DeactivatedByAdministrator, true),
                        NotPendingInvitation),
                    Builders<User>.Update
                        .Set(u => u.DeactivatedByAdministrator, true)
                        .Set(u => u.PasswordResetTokenHash, null)
                        .Set(u => u.PasswordResetTokenExpiresAt, null)
                        .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow),
                    cancellationToken: ct);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                throw new PartialMarkException(marked, accounts.Skip(i).ToList(), failure);
            }

            (result.ModifiedCount == 1 ? marked : notMarked).Add(account);
        }

        return new MarkResult(marked, notMarked);
    }

    public const string ModeDryRun = "dry-run";
    public const string ModeApply = "apply";
    public const string ModeRefused = "refused";

    /// <summary>
    /// One audit row per run that reached the database: counts and ids, no personal data. <paramref name="mode"/> is
    /// <see cref="ModeDryRun"/>, <see cref="ModeApply"/> or <see cref="ModeRefused"/> (the count was not the expected one);
    /// a run that stopped part-way records what it wrote, what it did not reach and <c>stoppedPartWay = true</c>. A run that
    /// never reached the database (a usage error, a connection failure) writes none — there is nowhere to write it.
    /// </summary>
    public static Task RecordRunAsync(
        IMongoDatabase database,
        string mode,
        Findings findings,
        IReadOnlyList<Account> marked,
        IReadOnlyList<Account> notMarked,
        IReadOnlyList<Account> notReached,
        CancellationToken ct = default) =>
        new AuthAuditService(database).WriteAsync(AuditEventName, null, Guid.Empty, JsonSerializer.Serialize(new
        {
            mode,
            found = findings.Found.Count,
            skippedPending = findings.SkippedPending.Count,
            marked = marked.Count,
            notMarked = notMarked.Count,
            notReached = notReached.Count,
            stoppedPartWay = notReached.Count > 0,
            ids = findings.Found.Select(a => new { tenant = a.TenantId, user = a.UserId }),
            markedIds = marked.Select(a => a.UserId),
            notReachedIds = notReached.Select(a => a.UserId)
        }), ct);

    // The code's pending-invitation rule (User.IsInvitationPending), on the stored document: must change password, e-mail
    // not confirmed, never signed in. A write never touches such a document.
    private static readonly FilterDefinition<User> NotPendingInvitation = new BsonDocument("$nor", new BsonArray
    {
        new BsonDocument
        {
            { nameof(User.MustChangePassword), true },
            { nameof(User.EmailConfirmed), new BsonDocument("$ne", true) },
            { nameof(User.LastLoginAt), BsonNull.Value }
        }
    });
}
