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

            // HCM module permissions — registered so SuperAdmin (full catalog) and tenant Admin roles
            // can see/use the HR modules. Keys mirror the *Guard permission constants used by the HCM API.
            new("hcm", "applicant-intake", "audit.read", "Applicant Intake Audit Read", null),
            new("hcm", "applicant-intake", "evaluate", "Applicant Intake Evaluate", null),
            new("hcm", "applicant-intake", "manage", "Applicant Intake Manage", null),
            new("hcm", "applicant-intake", "read", "Applicant Intake Read", null),

            new("hcm", "candidate-pipeline", "audit.read", "Candidate Pipeline Audit Read", null),
            new("hcm", "candidate-pipeline", "evaluate", "Candidate Pipeline Evaluate", null),
            new("hcm", "candidate-pipeline", "manage", "Candidate Pipeline Manage", null),
            new("hcm", "candidate-pipeline", "read", "Candidate Pipeline Read", null),

            new("hcm", "competency-skills", "audit.read", "Competency Skills Audit Read", null),
            new("hcm", "competency-skills", "evaluate", "Competency Skills Evaluate", null),
            new("hcm", "competency-skills", "manage", "Competency Skills Manage", null),
            new("hcm", "competency-skills", "read", "Competency Skills Read", null),

            new("hcm", "development-plans", "audit.read", "Development Plans Audit Read", null),
            new("hcm", "development-plans", "evaluate", "Development Plans Evaluate", null),
            new("hcm", "development-plans", "manage", "Development Plans Manage", null),
            new("hcm", "development-plans", "read", "Development Plans Read", null),

            new("hcm", "employee-onboarding", "audit.read", "Employee Onboarding Audit Read", null),
            new("hcm", "employee-onboarding", "evaluate", "Employee Onboarding Evaluate", null),
            new("hcm", "employee-onboarding", "manage", "Employee Onboarding Manage", null),
            new("hcm", "employee-onboarding", "read", "Employee Onboarding Read", null),

            new("hcm", "employee-projections", "archive", "Employee Projections Archive", null),
            new("hcm", "employee-projections", "manage", "Employee Projections Manage", null),
            new("hcm", "employee-projections", "read", "Employee Projections Read", null),
            new("hcm", "employee-projections", "source-link.manage", "Employee Projections Source Link Manage", null),

            new("hcm", "employment-changes", "audit.read", "Employment Changes Audit Read", null),
            new("hcm", "employment-changes", "evaluate", "Employment Changes Evaluate", null),
            new("hcm", "employment-changes", "manage", "Employment Changes Manage", null),
            new("hcm", "employment-changes", "read", "Employment Changes Read", null),

            new("hcm", "learning-training", "audit.read", "Learning Training Audit Read", null),
            new("hcm", "learning-training", "evaluate", "Learning Training Evaluate", null),
            new("hcm", "learning-training", "manage", "Learning Training Manage", null),
            new("hcm", "learning-training", "read", "Learning Training Read", null),

            new("hcm", "offboarding", "archive", "Offboarding Archive", null),
            new("hcm", "offboarding", "handoff.manage", "Offboarding Handoff Manage", null),
            new("hcm", "offboarding", "manage", "Offboarding Manage", null),
            new("hcm", "offboarding", "read", "Offboarding Read", null),
            new("hcm", "offboarding", "review", "Offboarding Review", null),

            new("hcm", "offer-management", "audit.read", "Offer Management Audit Read", null),
            new("hcm", "offer-management", "evaluate", "Offer Management Evaluate", null),
            new("hcm", "offer-management", "manage", "Offer Management Manage", null),
            new("hcm", "offer-management", "read", "Offer Management Read", null),

            new("hcm", "performance-reviews", "audit.read", "Performance Reviews Audit Read", null),
            new("hcm", "performance-reviews", "evaluate", "Performance Reviews Evaluate", null),
            new("hcm", "performance-reviews", "manage", "Performance Reviews Manage", null),
            new("hcm", "performance-reviews", "read", "Performance Reviews Read", null),

            new("hcm", "position-assignments", "archive", "Position Assignments Archive", null),
            new("hcm", "position-assignments", "manage", "Position Assignments Manage", null),
            new("hcm", "position-assignments", "read", "Position Assignments Read", null),
            new("hcm", "position-assignments", "reference-link.manage", "Position Assignments Reference Link Manage", null),

            new("hcm", "sensitive-access", "audit.read", "Sensitive Access Audit Read", null),
            new("hcm", "sensitive-access", "manage", "Sensitive Access Manage", null),
            new("hcm", "sensitive-access", "read", "Sensitive Access Read", null),
            new("hcm", "sensitive-access", "review", "Sensitive Access Review", null),

            new("hcm", "succession", "audit.read", "Succession Audit Read", null),
            new("hcm", "succession", "evaluate", "Succession Evaluate", null),
            new("hcm", "succession", "manage", "Succession Manage", null),
            new("hcm", "succession", "read", "Succession Read", null),

            new("hcm", "workforce-planning", "audit.read", "Workforce Planning Audit Read", null),
            new("hcm", "workforce-planning", "evaluate", "Workforce Planning Evaluate", null),
            new("hcm", "workforce-planning", "manage", "Workforce Planning Manage", null),
            new("hcm", "workforce-planning", "read", "Workforce Planning Read", null),

            new("hcm", "headcount-budget", "audit.read", "Headcount Budget Audit Read", null),
            new("hcm", "headcount-budget", "evaluate", "Headcount Budget Evaluate", null),
            new("hcm", "headcount-budget", "manage", "Headcount Budget Manage", null),
            new("hcm", "headcount-budget", "read", "Headcount Budget Read", null),

            new("hcm", "hr-kpi-analytics", "audit.read", "HR KPI Analytics Audit Read", null),
            new("hcm", "hr-kpi-analytics", "evaluate", "HR KPI Analytics Evaluate", null),
            new("hcm", "hr-kpi-analytics", "manage", "HR KPI Analytics Manage", null),
            new("hcm", "hr-kpi-analytics", "read", "HR KPI Analytics Read", null),

            new("hcm", "hr-documentation", "audit.read", "HR Documentation Audit Read", null),
            new("hcm", "hr-documentation", "evaluate", "HR Documentation Evaluate", null),
            new("hcm", "hr-documentation", "manage", "HR Documentation Manage", null),
            new("hcm", "hr-documentation", "read", "HR Documentation Read", null),

            new("hcm", "time-attendance-leave", "audit.read", "Time Attendance Leave Audit Read", null),
            new("hcm", "time-attendance-leave", "evaluate", "Time Attendance Leave Evaluate", null),
            new("hcm", "time-attendance-leave", "manage", "Time Attendance Leave Manage", null),
            new("hcm", "time-attendance-leave", "read", "Time Attendance Leave Read", null),

            new("hcm", "compensation-benefits", "audit.read", "Compensation Benefits Audit Read", null),
            new("hcm", "compensation-benefits", "evaluate", "Compensation Benefits Evaluate", null),
            new("hcm", "compensation-benefits", "manage", "Compensation Benefits Manage", null),
            new("hcm", "compensation-benefits", "read", "Compensation Benefits Read", null),

            new("hcm", "self-service", "audit.read", "Self Service Audit Read", null),
            new("hcm", "self-service", "evaluate", "Self Service Evaluate", null),
            new("hcm", "self-service", "manage", "Self Service Manage", null),
            new("hcm", "self-service", "read", "Self Service Read", null),

            new("hcm", "hr-case-management", "audit.read", "HR Case Management Audit Read", null),
            new("hcm", "hr-case-management", "evaluate", "HR Case Management Evaluate", null),
            new("hcm", "hr-case-management", "manage", "HR Case Management Manage", null),
            new("hcm", "hr-case-management", "read", "HR Case Management Read", null),

            new("hcm", "hr-compliance", "audit.read", "HR Compliance Audit Read", null),
            new("hcm", "hr-compliance", "evaluate", "HR Compliance Evaluate", null),
            new("hcm", "hr-compliance", "manage", "HR Compliance Manage", null),
            new("hcm", "hr-compliance", "read", "HR Compliance Read", null),

            new("tep", "talent-data-foundation", "audit.read", "Talent Data Foundation Audit Read", null),
            new("tep", "talent-data-foundation", "evaluate", "Talent Data Foundation Evaluate", null),
            new("tep", "talent-data-foundation", "manage", "Talent Data Foundation Manage", null),
            new("tep", "talent-data-foundation", "read", "Talent Data Foundation Read", null),

            new("tep", "hiring-risk-indicators", "audit.read", "Hiring Risk Indicators Audit Read", null),
            new("tep", "hiring-risk-indicators", "evaluate", "Hiring Risk Indicators Evaluate", null),
            new("tep", "hiring-risk-indicators", "manage", "Hiring Risk Indicators Manage", null),
            new("tep", "hiring-risk-indicators", "read", "Hiring Risk Indicators Read", null),

            new("tep", "early-warning-signals", "audit.read", "Early Warning Signals Audit Read", null),
            new("tep", "early-warning-signals", "evaluate", "Early Warning Signals Evaluate", null),
            new("tep", "early-warning-signals", "manage", "Early Warning Signals Manage", null),
            new("tep", "early-warning-signals", "read", "Early Warning Signals Read", null),

            new("tep", "restricted-integrity-registry", "audit.read", "Restricted Integrity Registry Audit Read", null),
            new("tep", "restricted-integrity-registry", "evaluate", "Restricted Integrity Registry Evaluate", null),
            new("tep", "restricted-integrity-registry", "manage", "Restricted Integrity Registry Manage", null),
            new("tep", "restricted-integrity-registry", "read", "Restricted Integrity Registry Read", null),

            new("tep", "professional-reputation-ledger", "audit.read", "Professional Reputation Ledger Audit Read", null),
            new("tep", "professional-reputation-ledger", "evaluate", "Professional Reputation Ledger Evaluate", null),
            new("tep", "professional-reputation-ledger", "manage", "Professional Reputation Ledger Manage", null),
            new("tep", "professional-reputation-ledger", "read", "Professional Reputation Ledger Read", null),

            new("tep", "industry-talent-pool", "audit.read", "Industry Talent Pool Audit Read", null),
            new("tep", "industry-talent-pool", "evaluate", "Industry Talent Pool Evaluate", null),
            new("tep", "industry-talent-pool", "manage", "Industry Talent Pool Manage", null),
            new("tep", "industry-talent-pool", "read", "Industry Talent Pool Read", null)
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

        var passwordHash = "$2a$12$kvAlA8eVqqZMPMwLAHTm4.BSPrCZ/mjE5eJ0GI8zpv8uw./BhUTLS"; // bcrypt("Admin123!")

        if (user == null)
        {
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
            user.UpdatePassword(passwordHash);
            user.ClearPasswordChangeRequirement();
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
