using Diten.AuthService.Domain.Authorization;
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
            new("auth", "users", "create", "Create User", "Permission to create a new user"),
            new("auth", "users", "read", "Read User", "Permission to view user lists and details"),
            new("auth", "users", "update", "Update User", "Permission to edit user information"),
            new("auth", "users", "delete", "Delete User", "Permission to delete users"),
            new("auth", "users", "assign-role", "Assign Role", "Permission to assign roles to users"),
            new("auth", "users", "lookup-validation", "Lookup Validation", "Permission to validate tenant user references"),

            new("auth", "roles", "create", "Create Role", "Permission to create a new role"),
            new("auth", "roles", "read", "Read Role", "Permission to view role lists"),
            new("auth", "roles", "update", "Update Role", "Permission to edit roles"),
            new("auth", "roles", "delete", "Delete Role", "Permission to delete roles"),
            new("auth", "roles", "assign-permission", "Assign Permission", "Permission to assign permissions to roles"),

            new("mdm", "legal-entities", "create", "Create Legal Entity", null),
            new("mdm", "legal-entities", "read", "Read Legal Entity", null),
            new("mdm", "legal-entities", "update", "Update Legal Entity", null),
            new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
            new("mdm", "legal-entities", "bulk-delete", "Bulk Delete", null),
            new("mdm", "legal-entities", "export", "Export", null),

            new("Platform", "BusinessReferenceData", "Read", "Read Business Reference Data", "Permission to view BusinessReferenceData stewardship screens and catalogs"),
            new("Platform", "BusinessReferenceData", "Create", "Create Business Reference Data", "Permission to create BusinessReferenceData sets"),
            new("Platform", "BusinessReferenceData", "Update", "Update Business Reference Data", "Permission to update BusinessReferenceData sets"),
            new("Platform", "BusinessReferenceData.Version", "Create", "Create Business Reference Data Version", "Permission to create BusinessReferenceData versions"),
            new("Platform", "BusinessReferenceData.Version", "Update", "Update Business Reference Data Version", "Permission to update BusinessReferenceData version content"),
            new("Platform", "BusinessReferenceData.Version", "Validate", "Validate Business Reference Data Version", "Permission to validate BusinessReferenceData versions"),
            new("Platform", "BusinessReferenceData.Version", "Submit", "Submit Business Reference Data Version", "Permission to submit BusinessReferenceData versions"),
            new("Platform", "BusinessReferenceData.Version", "Approve", "Approve Business Reference Data Version", "Permission to approve BusinessReferenceData versions"),
            new("Platform", "BusinessReferenceData.Version", "Publish", "Publish Business Reference Data Version", "Permission to publish BusinessReferenceData versions"),
            new("Platform", "BusinessReferenceData.Version", "PublishOverride", "Override Business Reference Data Publish", "Permission to publish BusinessReferenceData versions with governance override"),
            new("Platform", "BusinessReferenceData.Import", "Preview", "Preview Business Reference Data Import", "Permission to preview BusinessReferenceData imports"),
            new("Platform", "BusinessReferenceData.Import", "Commit", "Commit Business Reference Data Import", "Permission to commit BusinessReferenceData imports"),
            new("Platform", "BusinessReferenceData.Usage", "Register", "Register Business Reference Data Usage", "Permission to register BusinessReferenceData usage"),
            new("Platform", "BusinessReferenceData.Consumer", "Read", "Read Published Business Reference Data", "Permission to consume published BusinessReferenceData values"),

            new("goldenslim", "records", "read",   "Read Golden Slim",   "View Golden Slim records"),
            new("goldenslim", "records", "create", "Create Golden Slim", null),
            new("goldenslim", "records", "update", "Update Golden Slim", null),
            new("goldenslim", "records", "delete", "Delete Golden Slim", null)
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
        var superAdmin = await EnsureRole(roleCol, "SuperAdmin", "Super Administrator", "All system permissions");
        await AssignBaselineAsync(permCol, rpCol, superAdmin);

        // Admin
        var admin = await EnsureRole(roleCol, "Admin", "Administrator", "Auth and MDM administration");
        await AssignBaselineAsync(permCol, rpCol, admin);

        // Viewer
        var viewer = await EnsureRole(roleCol, "Viewer", "Viewer", "Read-only permissions");
        await AssignBaselineAsync(permCol, rpCol, viewer);
    }

    private static async Task SeedUsersAsync(IMongoDatabase database)
    {
        var userCol = database.GetCollection<User>("users");
        var roleCol = database.GetCollection<Role>("roles");
        var urCol = database.GetCollection<UserRole>("userRoles");

        var email = "admin@diten.com";
        var staticAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var users = await userCol.Find(u => u.Email == email).ToListAsync();
        var user = users.FirstOrDefault(u => u.TenantId == DefaultTenantId);
        
        if (user != null && user.Id != staticAdminId)
        {
            await userCol.DeleteOneAsync(u => u.Id == user.Id);
            await urCol.DeleteManyAsync(ur => ur.UserId == user.Id);
            user = null;
        }

        if (user == null)
        {
            var passwordHash = "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC"; 
            user = new User(email, passwordHash, "Diten", "Admin", DefaultTenantId)
            {
                Id = staticAdminId
            };
            user.SetUserName("admin");
            user.SetPlatformActorType("platform_admin");
            user.Activate();
            user.ConfirmEmail();
            await userCol.InsertOneAsync(user);
            Console.WriteLine("Created admin user with static Guid.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(user.NormalizedUserName))
            {
                user.SetUserName("admin");
            }

            user.SetPlatformActorType("platform_admin");
            user.Activate();
            user.ConfirmEmail();
            await userCol.ReplaceOneAsync(u => u.Id == user.Id, user);
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

    // Baseline grant wiring for the default tenant. Uses the same shared template as the runtime
    // RoleProvisioningService so the default-tenant seed and per-tenant provisioning never drift
    // (OD-FE9-03 Option B). Grants are System-sourced and idempotent (existing pairs are skipped).
    private static async Task AssignBaselineAsync(IMongoCollection<Permission> pCol, IMongoCollection<RolePermission> rpCol, Role role)
    {
        var catalog = await pCol.Find(_ => true).ToListAsync();
        var baseline = DefaultRolePermissionTemplate.SelectFor(role.Name, catalog);
        if (baseline.Count == 0) return;

        var currentRPs = await rpCol.Find(rp => rp.RoleId == role.Id).ToListAsync();

        foreach (var p in baseline)
        {
            if (!currentRPs.Any(rp => rp.PermissionId == p.Id))
            {
                await rpCol.InsertOneAsync(RolePermission.SystemGrant(role.Id, p.Id, DefaultTenantId, SystemUser));
            }
        }
    }
}
