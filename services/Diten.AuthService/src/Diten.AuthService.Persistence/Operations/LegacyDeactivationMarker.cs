using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Operations;

/// <summary>
/// BL-529 FIX8 item 1 (a) — THE ONE-TIME POST-DEPLOY STEP for accounts an administrator deactivated BEFORE BL-529. Then a
/// deactivation set IsActive = false and nothing else: no mark (the field did not exist) and the pending set-password link
/// stayed. Such a link would still pass the door and switch the account back on. This marks every such account and ends
/// its link — what a deactivation has done since BL-529.
/// <para><b>Which accounts:</b> not deleted, inactive, not marked, and NOT a pending invitation — the code's own rule,
/// <see cref="User.IsInvitationPending"/> (must change password, e-mail not confirmed, never signed in). A pending invitation
/// is the documented exception: it is inactive because it was never activated, and mechanically it cannot be told apart
/// from a deactivated one.</para>
/// <para><b>Dry run by default:</b> counts and lists (tenant id, user id — no personal data) and writes nothing. With
/// apply, each write is targeted and conditional (still inactive, still unmarked), so a run is idempotent and never
/// overwrites an activation that landed meanwhile. All tenants: it is an operator's data step.</para>
/// </summary>
public static class LegacyDeactivationMarker
{
    public sealed record Account(Guid TenantId, Guid UserId);

    public sealed record Result(bool DryRun, IReadOnlyList<Account> Found, int Marked);

    /// <summary>The tool's entry: the connection is opened HERE, in Persistence (the only place a MongoClient is made), with
    /// the serialization Auth itself runs with (Persistence/DependencyInjection.cs).</summary>
    public static Task<Result> RunAsync(string connectionString, string databaseName, bool apply, CancellationToken ct = default)
    {
        Serialization.GuidSerializerRegistration.EnsureStandard();
        MongoDB.Bson.Serialization.Conventions.ConventionRegistry.Register(
            "IgnoreExtraElementsConvention",
            new MongoDB.Bson.Serialization.Conventions.ConventionPack
            {
                new MongoDB.Bson.Serialization.Conventions.IgnoreExtraElementsConvention(true)
            },
            _ => true);
        return RunAsync(new MongoClient(connectionString).GetDatabase(databaseName), apply, ct);
    }

    public static async Task<Result> RunAsync(IMongoDatabase database, bool apply, CancellationToken ct = default)
    {
        var users = database.GetCollection<User>("users");
        var f = Builders<User>.Filter;
        var candidates = f.And(
            f.Eq(u => u.IsDeleted, false),
            f.Eq(u => u.IsActive, false),
            f.Ne(u => u.DeactivatedByAdministrator, true));

        var found = (await users.Find(candidates).ToListAsync(ct))
            .Where(u => !u.IsInvitationPending())
            .OrderBy(u => u.TenantId).ThenBy(u => u.Id)
            .Select(u => new Account(u.TenantId, u.Id))
            .ToList();
        if (!apply)
        {
            return new Result(DryRun: true, found, Marked: 0);
        }

        var marked = 0;
        foreach (var account in found)
        {
            var result = await users.UpdateOneAsync(
                f.And(candidates, f.Eq(u => u.Id, account.UserId), f.Eq(u => u.TenantId, account.TenantId)),
                Builders<User>.Update
                    .Set(u => u.DeactivatedByAdministrator, true)
                    .Set(u => u.PasswordResetTokenHash, null)
                    .Set(u => u.PasswordResetTokenExpiresAt, null)
                    .Set(u => u.UpdatedAt, (DateTimeOffset?)DateTimeOffset.UtcNow),
                cancellationToken: ct);
            marked += (int)result.ModifiedCount;
        }

        return new Result(DryRun: false, found, marked);
    }
}
