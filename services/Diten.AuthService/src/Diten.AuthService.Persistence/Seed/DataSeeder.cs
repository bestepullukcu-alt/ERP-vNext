using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

public static class DataSeeder
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tenant97c5Id = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string SystemUser = "system";
    private const string BusinessReferenceDataConsumerReadKey = "platform.businessreferencedata.consumer.read";

    // MOD-0023 Batch 09 — workflow admin is a tenant-scoped screen. The DefaultRolePermissionTemplate
    // never grants platform.* to tenant roles (escalation boundary), so the tenant operator must be
    // granted these explicitly (same pattern as the BRD consumer grant above).
    private const string Tenant97c5WorkflowOperatorEmail = "bestepullukcu@gmail.com";
    private static readonly string[] WorkflowPermissionKeys =
    {
        "platform.workflow.definitions.view",
        "platform.workflow.definitions.manage",
        "platform.workflow.definitions.publish",
        "platform.workflow.instances.start",
        "platform.workflow.instances.view",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject",
        "platform.workflow.tasks.delegate",
        "platform.workflow.tasks.request-info",
        "platform.workflow.tasks.cancel",
        "platform.workflow.transitions.evaluate",
        "platform.workflow.escalations.manage",
        "platform.workflow.escalations.run"
    };

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

            Console.WriteLine("Seeding tenant-97c5 BRD consumer grant...");
            await SeedTenant97c5BusinessReferenceDataConsumerGrantAsync(database);

            Console.WriteLine("Seeding tenant-97c5 workflow operator grant...");
            await SeedTenant97c5WorkflowGrantAsync(database);

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

            new("platform", "document-management.contract", "view", "View Documentation Management Contract", "Permission to view the MOD-0028-FU01 foundation contract"),
            new("platform", "document-management.collection-definitions", "list", "List Documentation Collection Definitions", "Permission foundation for listing documentation collection definitions"),
            new("platform", "document-management.collection-definitions", "view", "View Documentation Collection Definitions", "Permission foundation for viewing documentation collection definitions"),
            new("platform", "document-management.baseline-releases", "list", "List Documentation Baseline Releases", "Permission foundation for listing documentation baseline releases"),
            new("platform", "document-management.corporate-root", "initialize", "Initialize Documentation Corporate Root", "Permission foundation for initializing the documentation corporate root"),
            new("platform", "document-management.collection-instances", "view", "View Documentation Collection Instances", "Permission foundation for viewing documentation collection instances"),

            new("platform", "document-management.qms-baselines", "import", "Import QMS Documentation Baseline", "Permission to dry-run and commit MOD-0028-FU02 QMS folder baseline imports"),
            new("platform", "document-management.qms-baselines", "view", "View QMS Documentation Baselines", "Permission to view MOD-0028-FU02 QMS baselines and their definitions"),
            new("platform", "document-management.qms-baselines", "publish", "Publish QMS Documentation Baseline", "Permission to publish a MOD-0028-FU02 QMS baseline into an immutable snapshot manifest"),

            // MOD-0028-FU04 — Manual Documentation Structure Builder permissions (6).
            new("platform", "document-management.qms-baselines", "create", "Create Manual QMS Baseline", "Permission to create a manual DRAFT QMS documentation baseline without Excel import"),
            new("platform", "document-management.qms-baselines", "validate", "Validate QMS Baseline Draft", "Permission to validate a DRAFT QMS baseline tree before publish"),
            new("platform", "document-management.collection-definitions", "create", "Create Collection Definition Node", "Permission to add root or child nodes to a DRAFT QMS baseline"),
            new("platform", "document-management.collection-definitions", "edit", "Edit Collection Definition Node", "Permission to edit metadata of a DRAFT QMS baseline node"),
            new("platform", "document-management.collection-definitions", "move", "Move Collection Definition Node", "Permission to move or reorder a DRAFT QMS baseline node"),
            new("platform", "document-management.collection-definitions", "delete", "Delete Collection Definition Node", "Permission to soft-delete a DRAFT QMS baseline node"),

            // MOD-0028-FU05 — Company Adoption / CollectionInstance provisioning (instantiation wizard) permissions (5).
            new("platform", "document-management.baseline-releases", "view", "View Documentation Baseline Release", "Permission to view a published baseline release and instantiation prerequisites"),
            new("platform", "document-management.baselines", "instantiate", "Instantiate Documentation Baseline", "Permission to read an instantiation operation for company adoption"),
            new("platform", "document-management.instantiations", "dry-run", "Dry-Run Documentation Instantiation", "Permission to preview (dry-run) a CollectionInstance provisioning plan"),
            new("platform", "document-management.instantiations", "execute", "Execute Documentation Instantiation", "Permission to execute a CollectionInstance provisioning plan"),
            new("platform", "document-management.collection-instances", "retry", "Retry Documentation Instantiation", "Permission to retry failed nodes of a CollectionInstance provisioning operation"),

            // MOD-0023 — Workflow Config / Approval Templates permissions (13). Keys must match the
            // constants in Diten.Platform WorkflowPermissions (platform.workflow.*). Platform-scoped, so
            // the DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            new("platform", "workflow.definitions", "view", "View Workflow Definitions", "Permission to view workflow approval templates and versions"),
            new("platform", "workflow.definitions", "manage", "Manage Workflow Definitions", "Permission to create and edit workflow approval templates"),
            new("platform", "workflow.definitions", "publish", "Publish Workflow Definition", "Permission to publish an immutable workflow template version"),
            new("platform", "workflow.instances", "start", "Start Workflow Instance", "Permission to start a workflow approval instance"),
            new("platform", "workflow.instances", "view", "View Workflow Instances", "Permission to view workflow instances and approval tasks"),
            new("platform", "workflow.tasks", "approve", "Approve Workflow Task", "Permission to approve a workflow approval task"),
            new("platform", "workflow.tasks", "reject", "Reject Workflow Task", "Permission to reject a workflow approval task"),
            new("platform", "workflow.tasks", "delegate", "Delegate Workflow Task", "Permission to delegate a workflow approval task"),
            new("platform", "workflow.tasks", "request-info", "Request Info on Workflow Task", "Permission to request additional information on a workflow approval task"),
            new("platform", "workflow.tasks", "cancel", "Cancel Workflow Task", "Permission to cancel a workflow approval task"),
            new("platform", "workflow.transitions", "evaluate", "Evaluate Workflow Transition Gate", "Permission to evaluate the workflow integration transition gate"),
            new("platform", "workflow.escalations", "manage", "Manage Workflow SLA Rules", "Permission to create and view workflow SLA escalation rules"),
            new("platform", "workflow.escalations", "run", "Run Workflow Escalations", "Permission to run the workflow escalation/timeout processor")
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

        // Seed 5 additional mock users for DefaultTenantId and Tenant97c5Id
        await SeedMockUsersForTenantAsync(userCol, DefaultTenantId);
        await SeedMockUsersForTenantAsync(userCol, Tenant97c5Id);
    }

    private static async Task SeedMockUsersForTenantAsync(IMongoCollection<User> userCol, Guid tenantId)
    {
        var count = await userCol.CountDocumentsAsync(u => u.TenantId == tenantId && !u.IsDeleted);
        // If there's only 1 (the admin) or 0 users, let's seed 5 more.
        if (count <= 1)
        {
            var suffix = tenantId == DefaultTenantId ? "def" : "t97";
            var mockUsers = new List<User>
            {
                new User($"john.doe.{suffix}@diten.com", "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC", "John", "Doe", tenantId),
                new User($"jane.smith.{suffix}@diten.com", "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC", "Jane", "Smith", tenantId),
                new User($"bob.johnson.{suffix}@diten.com", "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC", "Bob", "Johnson", tenantId),
                new User($"alice.williams.{suffix}@diten.com", "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC", "Alice", "Williams", tenantId),
                new User($"charlie.brown.{suffix}@diten.com", "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC", "Charlie", "Brown", tenantId)
            };

            for (int i = 0; i < mockUsers.Count; i++)
            {
                var mu = mockUsers[i];
                mu.SetUserName($"user-{suffix}-{i + 1}");
                mu.Activate();
                mu.ConfirmEmail();
                await userCol.InsertOneAsync(mu);
            }
            Console.WriteLine($"Seeded 5 mock users for tenant {tenantId}.");
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

    private static async Task SeedTenant97c5BusinessReferenceDataConsumerGrantAsync(IMongoDatabase database)
    {
        var roleCol = database.GetCollection<Role>("roles");
        var permCol = database.GetCollection<Permission>("permissions");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");

        var adminRole = await roleCol
            .Find(r => r.TenantId == Tenant97c5Id && r.Name == DefaultRolePermissionTemplate.AdminRole && !r.IsDeleted)
            .FirstOrDefaultAsync();
        if (adminRole is null)
        {
            Console.WriteLine("Skipped tenant-97c5 BRD consumer grant: Admin role not found.");
            return;
        }

        var permission = await permCol
            .Find(p => p.Key == BusinessReferenceDataConsumerReadKey && !p.IsDeleted)
            .FirstOrDefaultAsync();
        if (permission is null)
        {
            Console.WriteLine("Skipped tenant-97c5 BRD consumer grant: permission not found.");
            return;
        }

        var exists = await rpCol.Find(rp =>
                rp.TenantId == Tenant97c5Id
                && rp.RoleId == adminRole.Id
                && rp.PermissionId == permission.Id
                && !rp.IsDeleted)
            .AnyAsync();
        if (exists)
        {
            return;
        }

        await rpCol.InsertOneAsync(RolePermission.SystemGrant(adminRole.Id, permission.Id, Tenant97c5Id, SystemUser));
        Console.WriteLine("Granted Platform.BusinessReferenceData.Consumer.Read to tenant-97c5 Admin role.");
    }

    // MOD-0023 Batch 09 — grant the workflow admin permissions to the tenant-97c5 operator so the
    // tenant-shell Workflow menu appears and the screens work. Grants to every role the operator holds
    // in tenant-97c5, plus that tenant's Admin role as a fallback. Idempotent and GUID-safe (entity
    // constructors honor the configured GUID serialization).
    private static async Task SeedTenant97c5WorkflowGrantAsync(IMongoDatabase database)
    {
        var roleCol = database.GetCollection<Role>("roles");
        var permCol = database.GetCollection<Permission>("permissions");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        var userCol = database.GetCollection<User>("users");
        var urCol = database.GetCollection<UserRole>("userRoles");

        // Resolve the operator's tenant-97c5 roles (if the user exists yet).
        var operatorUser = await userCol
            .Find(u => u.Email == Tenant97c5WorkflowOperatorEmail && u.TenantId == Tenant97c5Id && !u.IsDeleted)
            .FirstOrDefaultAsync();

        var targetRoleIds = new HashSet<Guid>();
        if (operatorUser is not null)
        {
            var userRoles = await urCol
                .Find(ur => ur.UserId == operatorUser.Id && ur.TenantId == Tenant97c5Id && !ur.IsDeleted)
                .ToListAsync();
            foreach (var ur in userRoles)
            {
                targetRoleIds.Add(ur.RoleId);
            }
        }
        else
        {
            Console.WriteLine($"tenant-97c5 workflow grant: operator '{Tenant97c5WorkflowOperatorEmail}' not found; falling back to Admin role only.");
        }

        // Always include the tenant-97c5 Admin role as a convention-aligned fallback.
        var adminRole = await roleCol
            .Find(r => r.TenantId == Tenant97c5Id && r.Name == DefaultRolePermissionTemplate.AdminRole && !r.IsDeleted)
            .FirstOrDefaultAsync();
        if (adminRole is not null)
        {
            targetRoleIds.Add(adminRole.Id);
        }

        if (targetRoleIds.Count == 0)
        {
            Console.WriteLine("Skipped tenant-97c5 workflow grant: no target roles resolved.");
            return;
        }

        var permissions = await permCol
            .Find(p => WorkflowPermissionKeys.Contains(p.Key) && !p.IsDeleted)
            .ToListAsync();
        if (permissions.Count == 0)
        {
            Console.WriteLine("Skipped tenant-97c5 workflow grant: workflow permissions not found in catalog.");
            return;
        }

        var granted = 0;
        foreach (var roleId in targetRoleIds)
        {
            var currentRps = await rpCol
                .Find(rp => rp.RoleId == roleId && rp.TenantId == Tenant97c5Id && !rp.IsDeleted)
                .ToListAsync();
            foreach (var permission in permissions)
            {
                if (currentRps.Any(rp => rp.PermissionId == permission.Id))
                {
                    continue;
                }
                await rpCol.InsertOneAsync(RolePermission.SystemGrant(roleId, permission.Id, Tenant97c5Id, SystemUser));
                granted++;
            }
        }

        Console.WriteLine($"tenant-97c5 workflow grant: ensured {permissions.Count} permissions across {targetRoleIds.Count} role(s); {granted} new grant(s).");
    }
}
