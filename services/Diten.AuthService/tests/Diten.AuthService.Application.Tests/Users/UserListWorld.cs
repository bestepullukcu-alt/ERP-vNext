using System.Runtime.CompilerServices;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — the disposable world of the Users-list tests: ONE fresh tenant (a new Guid, inside the
/// isolated EphemeralMongo host — never DefaultTenant, never the shared 27017) holding a known set of users, plus a
/// second fresh tenant that reuses the same names and e-mail addresses. What each user IS (status, kind, roles, login
/// time) is written down HERE, independent of the code under test; the tests compare the API against these facts.
/// </summary>
internal sealed class UserListWorld
{
    /// <summary>The facts of one subject. <see cref="Status"/> is the EXPECTED lifecycle status, stated by hand.</summary>
    internal sealed record Subject(
        string Key, string Email, string First, string Last, AccountKind Kind, string Status,
        DateTime? LastLoginAt, string[] Roles, Guid Id, int CreatedOrder);

    private static readonly ConditionalWeakTable<AccountKindAcceptance.AuthTestHost, Task<UserListWorld>> Worlds = new();

    public static Task<UserListWorld> ForAsync(AccountKindAcceptance.AuthTestHost host)
        => Worlds.GetValue(host, BuildAsync);

    public Guid TenantId { get; private init; }
    public Guid ForeignTenantId { get; private init; }
    public string Token { get; private init; } = string.Empty;
    public string ForeignToken { get; private init; } = string.Empty;
    public string NoPermissionToken { get; private init; } = string.Empty;
    public Guid AuditorsRoleId { get; private init; }
    public Guid ApproversRoleId { get; private init; }
    public Guid ForeignAuditorsRoleId { get; private init; }
    public IReadOnlyList<Subject> Live { get; private init; } = [];
    public IReadOnlyList<Subject> ForeignLive { get; private init; } = [];
    public Guid DeletedUserId { get; private init; }
    public Subject By(string key) => Live.Single(s => s.Key == key);

    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Invited = "Invited";

    private sealed record Spec(
        string Key, string Email, string First, string Last, AccountKind Kind, string Status,
        bool Confirmed, bool MustChange, bool Deactivate, DateTime? LastLoginAt, string[] Roles);

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    // Last names, first names and e-mails start with DIFFERENT base letters on purpose: the expected order is then
    // the same under any sane alphabetical rule, so the tests measure the query, not a collation table.
    private static readonly Spec[] Specs =
    [
        new("ada",    "ada@t.test",     "Ada",     "Yılmaz", AccountKind.Human,   Active,   true,  false, false, Utc(2026, 3, 1),  ["Auditors"]),
        new("bora",   "bora@t.test",    "Bora",    "Çelik",  AccountKind.Service, Active,   true,  false, false, Utc(2026, 2, 1),  ["Auditors", "Approvers"]),
        new("cem",    "cem@t.test",     "Cem",     "Demir",  AccountKind.Human,   Inactive, true,  false, true,  Utc(2026, 1, 15), []),
        new("deniz",  "deniz@t.test",   "Deniz",   "Ak",     AccountKind.Unknown, Active,   true,  false, false, null,             ["Retired"]), // its only role is soft-deleted
        new("ece",    "ece@t.test",     "Ece",     "Bal",    AccountKind.Unknown, Invited,  false, true,  false, null,             []),
        // was invited, then signed in once → the lifecycle says Active (LastLoginAt breaks the Invited rule)
        new("fatih",  "fatih@t.test",   "Fatih",   "Fidan",  AccountKind.Human,   Active,   false, true,  false, Utc(2026, 4, 1),  ["Approvers"]),
        // an administrator reset a redeemed account: MustChangePassword but confirmed → Active
        new("gul",    "gul@t.test",     "Gül",     "Aydın",  AccountKind.Human,   Active,   true,  true,  false, null,             []),
        new("hakan",  "hakan@t.test",   "Hakan",   "Öz",     AccountKind.Human,   Invited,  false, true,  false, null,             ["Approvers"]),
        new("legacy", "legacy@t.test",  "Legacy",  "Old",    AccountKind.Unknown, Active,   true,  false, false, Utc(2026, 2, 20), []), // its AccountKind field is removed below
        new("dot",    "o'neil+x@t.test","Dot.Com", "Star*",  AccountKind.Human,   Active,   true,  false, false, Utc(2026, 2, 15), []), // regex metacharacters
        new("actor",  "actor@t.test",   "Actor",   "Lister", AccountKind.Human,   Active,   true,  false, false, Utc(2026, 5, 1),  []), // the caller is a user too
        // switched off AND never signed in: Invited wins over IsActive
        new("ilker",  "ilker@t.test",   "Ilker",   "Tan",    AccountKind.Unknown, Invited,  false, true,  true,  null,             [])
    ];

    private static async Task<UserListWorld> BuildAsync(AccountKindAcceptance.AuthTestHost host)
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();

        using var scope = host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var tokens = sp.GetRequiredService<ITokenService>();
        var rawUsers = host.Database.GetCollection<BsonDocument>("users");
        var rawRoles = host.Database.GetCollection<BsonDocument>("roles");

        static BsonBinaryData Bin(Guid id) => new(id, GuidRepresentation.Standard);

        async Task<Role> NewRole(string name, Guid tenant, bool deleted = false)
        {
            var role = await roles.CreateAsync(new Role(name, name, "users-list fixture", tenant), CancellationToken.None);
            if (deleted)
            {
                await rawRoles.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Bin(role.Id)),
                    Builders<BsonDocument>.Update.Set("IsDeleted", true));
            }

            return role;
        }

        async Task<User> NewUser(Spec spec, Guid tenant)
        {
            var user = new User(spec.Email, "hash:x", spec.First, spec.Last, tenant);
            user.SetAccountKind(spec.Kind);
            if (spec.Confirmed) user.ConfirmEmail();
            if (spec.MustChange) user.RequirePasswordChange(null);
            if (spec.Deactivate) user.Deactivate();
            var created = await users.CreateAsync(user, CancellationToken.None);
            if (spec.LastLoginAt is { } login)
            {
                await rawUsers.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Bin(created.Id)),
                    Builders<BsonDocument>.Update.Set("LastLoginAt", new BsonDateTime(login)));
            }

            await Task.Delay(3); // distinct CreatedAt: creation order IS the createdAt order
            return created;
        }

        var auditors = await NewRole("Auditors", tenantId);
        var approvers = await NewRole("Approvers", tenantId);
        var retired = await NewRole("Retired", tenantId, deleted: true);
        var roleByName = new Dictionary<string, Role> { ["Auditors"] = auditors, ["Approvers"] = approvers, ["Retired"] = retired };

        var live = new List<Subject>();
        var order = 0;
        foreach (var spec in Specs)
        {
            var created = await NewUser(spec, tenantId);
            foreach (var role in spec.Roles)
            {
                await userRoles.AssignAsync(new UserRole(created.Id, roleByName[role].Id, tenantId, "users-list-fixture"), CancellationToken.None);
            }

            if (spec.Key == "legacy")
            {
                // A document from before AccountKind existed: the field is simply absent (reads as Unknown).
                await rawUsers.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Bin(created.Id)),
                    Builders<BsonDocument>.Update.Unset("AccountKind"));
            }

            live.Add(new Subject(spec.Key, spec.Email, spec.First, spec.Last, spec.Kind, spec.Status, spec.LastLoginAt,
                spec.Roles.Where(r => r != "Retired").ToArray(), created.Id, order++));
        }

        // A soft-deleted user whose name matches a live search — and who still holds a live role.
        var deleted = await NewUser(new Spec("ada-deleted", "ada.deleted@t.test", "Ada", "Deleted", AccountKind.Human, Active, true, false, false, null, []), tenantId);
        await userRoles.AssignAsync(new UserRole(deleted.Id, auditors.Id, tenantId, "users-list-fixture"), CancellationToken.None);
        await rawUsers.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Bin(deleted.Id)), Builders<BsonDocument>.Update.Set("IsDeleted", true));

        // The second tenant reuses the very same names / addresses and one of the role names.
        var foreignAuditors = await NewRole("Auditors", foreignTenantId);
        var foreignLive = new List<Subject>();
        foreach (var spec in Specs.Where(s => s.Key is "ada" or "bora" or "ece" or "actor"))
        {
            var created = await NewUser(spec, foreignTenantId);
            await userRoles.AssignAsync(new UserRole(created.Id, foreignAuditors.Id, foreignTenantId, "users-list-fixture"), CancellationToken.None);
            foreignLive.Add(new Subject(spec.Key, spec.Email, spec.First, spec.Last, spec.Kind, spec.Status, spec.LastLoginAt, ["Auditors"], created.Id, 0));
        }

        async Task<string> TokenFor(Guid tenant, string key, params string[] permissions)
        {
            var email = Specs.Single(s => s.Key == key).Email;
            var actor = await users.GetByEmailAndTenantAsync(email, tenant, CancellationToken.None)
                ?? throw new InvalidOperationException("fixture actor vanished");
            return tokens.GenerateAccessToken(actor, ["users-list-reader"], permissions, expiresInMinutes: 60);
        }

        return new UserListWorld
        {
            TenantId = tenantId,
            ForeignTenantId = foreignTenantId,
            Token = await TokenFor(tenantId, "actor", "auth.users.read"),
            ForeignToken = await TokenFor(foreignTenantId, "actor", "auth.users.read"),
            NoPermissionToken = await TokenFor(tenantId, "actor", "auth.users.create"),
            AuditorsRoleId = auditors.Id,
            ApproversRoleId = approvers.Id,
            ForeignAuditorsRoleId = foreignAuditors.Id,
            Live = live,
            ForeignLive = foreignLive,
            DeletedUserId = deleted.Id
        };
    }
}
