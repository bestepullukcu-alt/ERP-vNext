using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

public static class DataSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SystemUser = "system";

    public static async Task SeedAsync(IMongoDatabase database)
    {
        try 
        {
            Console.WriteLine("Seeding permissions...");
            await SeedPermissionsAsync(database);
            
            Console.WriteLine("Seeding roles...");
            await SeedRolesAsync(database);
            
            Console.WriteLine("Seeding users...");
            await SeedUsersAsync(database);
            
            Console.WriteLine("Seeding completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical Seeding Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }

    private static async Task SeedPermissionsAsync(IMongoDatabase database)
    {
        var col = database.GetCollection<Permission>("permissions");
        var permissions = new List<Permission>
        {
            new("auth", "users", "create", "Kullanıcı Oluştur", "Yeni kullanıcı ekleme yetkisi"),
            new("auth", "users", "read", "Kullanıcı Oku", "Kullanıcı listesi ve detay görme yetkisi"),
            new("auth", "users", "update", "Kullanıcı Güncelle", "Kullanıcı bilgilerini düzenleme yetkisi"),
            new("auth", "users", "delete", "Kullanıcı Sil", "Kullanıcıyı silme yetkisi"),
            new("auth", "users", "assign-role", "Rol Ata", "Kullanıcıya rol atama yetkisi"),
            
            new("auth", "roles", "create", "Rol Oluştur", "Yeni rol ekleme yetkisi"),
            new("auth", "roles", "read", "Rol Oku", "Rol listesi görme yetkisi"),
            new("auth", "roles", "update", "Rol Güncelle", "Rol düzenleme yetkisi"),
            new("auth", "roles", "delete", "Rol Sil", "Rol silme yetkisi"),
            new("auth", "roles", "assign-permission", "Yetki Ata", "Role yetki atama yetkisi"),

            new("mdm", "legal-entities", "create", "Tüzel Kişi Oluştur", null),
            new("mdm", "legal-entities", "read", "Tüzel Kişi Oku", null),
            new("mdm", "legal-entities", "update", "Tüzel Kişi Güncelle", null),
            new("mdm", "legal-entities", "delete", "Tüzel Kişi Sil", null),
            new("mdm", "legal-entities", "bulk-delete", "Toplu Sil", null),
            new("mdm", "legal-entities", "export", "Dışa Aktar", null)
        };

        foreach (var p in permissions)
        {
            var filter = Builders<Permission>.Filter.Eq(x => x.Key, p.Key);
            var exists = await col.Find(filter).AnyAsync();
            if (!exists) await col.InsertOneAsync(p);
        }
    }

    private static async Task SeedRolesAsync(IMongoDatabase database)
    {
        var roleCol = database.GetCollection<Role>("roles");
        var permCol = database.GetCollection<Permission>("permissions");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");

        // SuperAdmin
        var superAdmin = await EnsureRole(roleCol, "SuperAdmin", "Süper Yönetici", "Tüm sistem yetkileri");
        await AssignAllPermissions(permCol, rpCol, superAdmin.Id);

        // Admin
        var admin = await EnsureRole(roleCol, "Admin", "Yönetici", "Auth ve MDM yönetimi");
        await AssignPermissions(permCol, rpCol, admin.Id, "auth", "mdm");

        // Viewer
        var viewer = await EnsureRole(roleCol, "Viewer", "İzleyici", "Sadece okuma yetkisi");
        await AssignReadPermissions(permCol, rpCol, viewer.Id);
    }

    private static async Task SeedUsersAsync(IMongoDatabase database)
    {
        var userCol = database.GetCollection<User>("users");
        var roleCol = database.GetCollection<Role>("roles");
        var urCol = database.GetCollection<UserRole>("userRoles");

        var email = "admin@diten.com";
        var users = await userCol.Find(u => u.Email == email).ToListAsync();
        var user = users.FirstOrDefault(u => u.TenantId == DefaultTenantId);
        
        if (user == null)
        {
            var passwordHash = "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC"; 
            user = new User(email, passwordHash, "Diten", "Admin", DefaultTenantId);
            await userCol.InsertOneAsync(user);
            Console.WriteLine("Created admin user.");
        }

        var roles = await roleCol.Find(r => r.Name == "SuperAdmin").ToListAsync();
        var superAdminRole = roles.FirstOrDefault(r => r.TenantId == DefaultTenantId);
        
        if (superAdminRole != null)
        {
            var urPairs = await urCol.Find(ur => ur.UserId == user.Id).ToListAsync();
            var exists = urPairs.Any(ur => ur.RoleId == superAdminRole.Id);
            
            if (!exists)
            {
                await urCol.InsertOneAsync(new UserRole(user.Id, superAdminRole.Id, DefaultTenantId, SystemUser));
                Console.WriteLine("Assigned SuperAdmin role to admin user.");
            }
        }
    }

    private static async Task<Role> EnsureRole(IMongoCollection<Role> col, string name, string display, string desc)
    {
        var roles = await col.Find(r => r.Name == name).ToListAsync();
        var role = roles.FirstOrDefault(r => r.TenantId == DefaultTenantId);
        
        if (role == null)
        {
            role = new Role(name, display, desc, DefaultTenantId);
            role.MarkAsSystem();
            await col.InsertOneAsync(role);
            Console.WriteLine($"Created role {name}");
        }
        return role;
    }

    private static async Task AssignAllPermissions(IMongoCollection<Permission> pCol, IMongoCollection<RolePermission> rpCol, Guid roleId)
    {
        var perms = await pCol.Find(_ => true).ToListAsync();
        var currentRPs = await rpCol.Find(rp => rp.RoleId == roleId).ToListAsync();
        
        foreach (var p in perms)
        {
            if (!currentRPs.Any(rp => rp.PermissionId == p.Id))
            {
                await rpCol.InsertOneAsync(new RolePermission(roleId, p.Id, DefaultTenantId, SystemUser));
            }
        }
    }

    private static async Task AssignPermissions(IMongoCollection<Permission> pCol, IMongoCollection<RolePermission> rpCol, Guid roleId, params string[] modules)
    {
        var perms = await pCol.Find(p => modules.Contains(p.Module)).ToListAsync();
        var currentRPs = await rpCol.Find(rp => rp.RoleId == roleId).ToListAsync();

        foreach (var p in perms)
        {
            if (!currentRPs.Any(rp => rp.PermissionId == p.Id))
            {
                await rpCol.InsertOneAsync(new RolePermission(roleId, p.Id, DefaultTenantId, SystemUser));
            }
        }
    }

    private static async Task AssignReadPermissions(IMongoCollection<Permission> pCol, IMongoCollection<RolePermission> rpCol, Guid roleId)
    {
        var perms = await pCol.Find(p => p.Action == "read").ToListAsync();
        var currentRPs = await rpCol.Find(rp => rp.RoleId == roleId).ToListAsync();

        foreach (var p in perms)
        {
            if (!currentRPs.Any(rp => rp.PermissionId == p.Id))
            {
                await rpCol.InsertOneAsync(new RolePermission(roleId, p.Id, DefaultTenantId, SystemUser));
            }
        }
    }
}
