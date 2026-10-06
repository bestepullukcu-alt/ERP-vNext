using System.Runtime.CompilerServices;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-452 — the disposable world of the Users EXPORT tests, inside the same isolated EphemeralMongo host as the list
/// tests (fresh Guids, never DefaultTenant, never the shared 27017). Its size is chosen so the page and the file differ:
///
/// <list type="bullet">
/// <item>Tenant A — 50 users: <b>37 Active</b> (the actor included), 8 Invited, 5 Inactive. Every last name carries
/// "Alpha"; every third Active user holds the role "Auditors".</item>
/// <item>Tenant B — 11 Active users whose last names carry "Foreign"; nothing in B says "Alpha".</item>
/// </list>
/// A status=Active export of A must therefore hold exactly 37 rows whatever page size the screen shows, and a B token
/// searching "Alpha" must get a file with no row at all.
/// </summary>
internal sealed class UserExportWorld
{
    private static readonly ConditionalWeakTable<AccountKindAcceptance.AuthTestHost, Task<UserExportWorld>> Worlds = new();

    public static Task<UserExportWorld> ForAsync(AccountKindAcceptance.AuthTestHost host) => Worlds.GetValue(host, BuildAsync);

    public const int ActiveCount = 37;
    public const int InvitedCount = 8;
    public const int InactiveCount = 5;

    public Guid TenantId { get; private init; }
    public Guid ForeignTenantId { get; private init; }
    public string Token { get; private init; } = string.Empty;
    public string ForeignToken { get; private init; } = string.Empty;
    public string NoPermissionToken { get; private init; } = string.Empty;
    /// <summary>BL-452 package 3 — reads the list, may not export it: the export answers 403.</summary>
    public string ReadOnlyToken { get; private init; } = string.Empty;

    private static async Task<UserExportWorld> BuildAsync(AccountKindAcceptance.AuthTestHost host)
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

        var auditors = await roles.CreateAsync(new Role("Auditors", "Auditors", "export fixture", tenantId), CancellationToken.None);

        async Task<User> NewUser(Guid tenant, string email, string first, string last, string status)
        {
            var user = new User(email, "hash:x", first, last, tenant);
            user.SetAccountKind(AccountKind.Human);
            switch (status)
            {
                case "Active":
                    user.ConfirmEmail();
                    break;
                case "Inactive":
                    user.ConfirmEmail();
                    user.Deactivate();
                    break;
                case "Invited":
                    user.RequirePasswordChange(null);
                    break;
            }

            return await users.CreateAsync(user, CancellationToken.None);
        }

        var actor = await NewUser(tenantId, "actor@export.test", "Actor", "Alpha Lister", "Active");
        // Names in a deliberately shuffled order: the file's order must come from orderBy, not from insertion.
        var n = 0;
        for (var i = 0; i < ActiveCount - 1; i++, n++)
        {
            var key = (i * 17) % 97;
            var created = await NewUser(tenantId, $"a{key:00}.{i:00}@export.test", $"First{key:00}", $"Alpha{(char)('a' + i % 26)}{i:00}", "Active");
            if (i % 3 == 0)
            {
                await userRoles.AssignAsync(new UserRole(created.Id, auditors.Id, tenantId, "export-fixture"), CancellationToken.None);
            }
        }

        for (var i = 0; i < InvitedCount; i++) await NewUser(tenantId, $"inv{i:00}@export.test", $"Invitee{i}", $"Alpha Invited{i:00}", "Invited");
        for (var i = 0; i < InactiveCount; i++) await NewUser(tenantId, $"off{i:00}@export.test", $"Off{i}", $"Alpha Off{i:00}", "Inactive");

        var foreignActor = await NewUser(foreignTenantId, "actor@export.test", "Actor", "Foreign Lister", "Active");
        for (var i = 0; i < 10; i++) await NewUser(foreignTenantId, $"f{i:00}@export.test", $"Foreign{i}", $"Foreign{i:00}", "Active");

        return new UserExportWorld
        {
            TenantId = tenantId,
            ForeignTenantId = foreignTenantId,
            Token = tokens.GenerateAccessToken(actor, ["export-reader"], ["auth.users.read", "auth.users.export"], expiresInMinutes: 60),
            ForeignToken = tokens.GenerateAccessToken(foreignActor, ["export-reader"], ["auth.users.read", "auth.users.export"], expiresInMinutes: 60),
            NoPermissionToken = tokens.GenerateAccessToken(actor, ["export-reader"], ["auth.users.create"], expiresInMinutes: 60),
            ReadOnlyToken = tokens.GenerateAccessToken(actor, ["export-reader"], ["auth.users.read"], expiresInMinutes: 60)
        };
    }
}
