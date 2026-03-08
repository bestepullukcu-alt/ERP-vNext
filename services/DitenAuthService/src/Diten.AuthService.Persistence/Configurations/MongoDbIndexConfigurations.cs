using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        // Users
        var usersCol = database.GetCollection<User>("users");
        await usersCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Email).Ascending(u => u.TenantId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.TenantId)),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.IsActive).Ascending(u => u.TenantId))
        });

        // Roles
        var rolesCol = database.GetCollection<Role>("roles");
        await rolesCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Role>(Builders<Role>.IndexKeys.Ascending(r => r.Name).Ascending(r => r.TenantId), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Role>(Builders<Role>.IndexKeys.Ascending(r => r.TenantId))
        });

        // Permissions
        var permissionsCol = database.GetCollection<Permission>("permissions");
        await permissionsCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Permission>(Builders<Permission>.IndexKeys.Ascending(p => p.Key), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Permission>(Builders<Permission>.IndexKeys.Ascending("Module").Ascending("Resource").Ascending("Action"), new CreateIndexOptions { Unique = true })
        });

        // UserRoles
        var userRolesCol = database.GetCollection<UserRole>("userRoles");
        await userRolesCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending("UserId").Ascending("RoleId").Ascending("TenantId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending("UserId").Ascending("TenantId"))
        });

        // RolePermissions
        var rolePermissionsCol = database.GetCollection<RolePermission>("rolePermissions");
        await rolePermissionsCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RolePermission>(Builders<RolePermission>.IndexKeys.Ascending("RoleId").Ascending("PermissionId").Ascending("TenantId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RolePermission>(Builders<RolePermission>.IndexKeys.Ascending("RoleId").Ascending("TenantId"))
        });

        // RefreshTokens
        var refreshTokensCol = database.GetCollection<RefreshToken>("refreshTokens");
        await refreshTokensCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending("UserId").Ascending("TenantId")),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        });
    }
}
