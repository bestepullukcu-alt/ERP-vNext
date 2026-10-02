using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — a disposable tenant with one person who administers roles, for the HTTP round trips of the
/// Roles work package. Everything is written into the test-owned mongod, under a tenant id minted for the class.
/// </summary>
internal sealed class RoleEndpointWorld
{
    public static readonly string[] RoleAdminKeys =
    [
        "auth.roles.read", "auth.roles.create", "auth.roles.update", "auth.roles.delete", "auth.roles.assign-permission",
        "auth.users.assign-role"
    ];

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid ActorId { get; private set; }
    public string Token { get; private set; } = string.Empty;

    private RoleEndpointWorld(AccountKindAcceptance.AuthTestHost host) => _host = host;

    public static RoleEndpointWorld Create(AccountKindAcceptance.AuthTestHost host, string firstName, string lastName, params string[] tokenKeys)
    {
        var world = new RoleEndpointWorld(host);
        world.SeedActorAsync(firstName, lastName, tokenKeys.Length > 0 ? tokenKeys : RoleAdminKeys).GetAwaiter().GetResult();
        return world;
    }

    public HttpClient Client() => _host.Client(Token, TenantId);

    public async Task<Role> NewRoleAsync(string prefix, bool system = false)
    {
        using var scope = Scope();
        var name = $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(40, prefix.Length + 33)];
        var role = new Role(name, name, "roles-close fixture", TenantId);
        if (system) role.MarkAsSystem();
        return await scope.ServiceProvider.GetRequiredService<IRoleRepository>().CreateAsync(role, CancellationToken.None);
    }

    public async Task<Guid> NewUserAsync()
    {
        using var scope = Scope();
        var user = new User($"u.{Guid.NewGuid():N}@roles.test", "hash:x", "Sub", "Ject", TenantId);
        user.ConfirmEmail();
        return (await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None)).Id;
    }

    public async Task<Permission> PermissionAsync(string key)
        => await _host.Database.GetCollection<Permission>("permissions").Find(p => p.Key == key && p.IsDeleted == false).SingleAsync();

    public async Task<List<Permission>> AllPermissionsAsync()
        => await _host.Database.GetCollection<Permission>("permissions").Find(p => p.IsDeleted == false).ToListAsync();

    public Task InsertGrantAsync(RolePermission grant)
        => _host.Database.GetCollection<RolePermission>("rolePermissions").InsertOneAsync(grant);

    public async Task<List<RolePermission>> GrantsAsync(Guid roleId)
        => await _host.Database.GetCollection<RolePermission>("rolePermissions").Find(g => g.RoleId == roleId && g.TenantId == TenantId).ToListAsync();

    public async Task<List<AuthAuditLog>> LocalRowsAsync(string eventName, Guid roleId)
    {
        var rows = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == eventName && r.TenantId == TenantId).ToListAsync();
        return rows.Where(r => r.Metadata.Contains(roleId.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private IServiceScope Scope()
    {
        var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(TenantId);
        return scope;
    }

    private async Task SeedActorAsync(string firstName, string lastName, string[] tokenKeys)
    {
        using var scope = Scope();
        var sp = scope.ServiceProvider;
        var actor = new User($"actor.{Guid.NewGuid():N}@roles.test", "hash:x", firstName, lastName, TenantId);
        actor.ConfirmEmail();
        actor = await sp.GetRequiredService<IUserRepository>().CreateAsync(actor, CancellationToken.None);
        ActorId = actor.Id;
        Token = sp.GetRequiredService<ITokenService>().GenerateAccessToken(actor, ["RoleStewards"], tokenKeys, expiresInMinutes: 60);
    }
}
