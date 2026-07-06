using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
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

            Console.WriteLine("Seeding default-tenant entitled-module grants (goldenslim + workflow)...");
            await SeedDefaultTenantEntitledModuleGrantsAsync(database);

            Console.WriteLine("Reconciling tenant Admin self-service grants (backfill)...");
            await ReconcileTenantAdminSelfServiceGrantsAsync(database);

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
            new("auth", "users", "create", "Create User", "Permission to create a new user", moduleOverride: "access-governance"),
            new("auth", "users", "read", "Read User", "Permission to view user lists and details", moduleOverride: "access-governance"),
            new("auth", "users", "update", "Update User", "Permission to edit user information", moduleOverride: "access-governance"),
            new("auth", "users", "delete", "Delete User", "Permission to delete users", moduleOverride: "access-governance"),
            new("auth", "users", "assign-role", "Assign Role", "Permission to assign roles to users", moduleOverride: "access-governance"),
            new("auth", "users", "lookup-validation", "Lookup Validation", "Permission to validate tenant user references", moduleOverride: "access-governance"),

            new("auth", "roles", "create", "Create Role", "Permission to create a new role", moduleOverride: "access-governance"),
            new("auth", "roles", "read", "Read Role", "Permission to view role lists", moduleOverride: "access-governance"),
            new("auth", "roles", "update", "Update Role", "Permission to edit roles", moduleOverride: "access-governance"),
            new("auth", "roles", "delete", "Delete Role", "Permission to delete roles", moduleOverride: "access-governance"),
            new("auth", "roles", "assign-permission", "Assign Permission", "Permission to assign permissions to roles", moduleOverride: "access-governance"),

            new("mdm", "legal-entities", "create", "Create Legal Entity", null, moduleOverride: "legal-entity"),
            new("mdm", "legal-entities", "read", "Read Legal Entity", null, moduleOverride: "legal-entity"),
            new("mdm", "legal-entities", "update", "Update Legal Entity", null, moduleOverride: "legal-entity"),
            new("mdm", "legal-entities", "delete", "Delete Legal Entity", null, moduleOverride: "legal-entity"),
            new("mdm", "legal-entities", "bulk-delete", "Bulk Delete", null, moduleOverride: "legal-entity"),
            new("mdm", "legal-entities", "export", "Export", null, moduleOverride: "legal-entity"),

            // FIX-PERM-MODULE-ATTRIBUTION — these are owned by the reference-data module (ReferenceDataManifestProvider,
            // ModuleCode: "reference-data"), not "platform"/"Platform". Key stays platform.businessreferencedata.* via
            // the first ctor arg; moduleOverride corrects only the Module attribution used for RoleAssignments grouping.
            new("platform", "BusinessReferenceData", "Read", "Read Business Reference Data", "Permission to view BusinessReferenceData stewardship screens and catalogs", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData", "Create", "Create Business Reference Data", "Permission to create BusinessReferenceData sets", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData", "Update", "Update Business Reference Data", "Permission to update BusinessReferenceData sets", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Create", "Create Business Reference Data Version", "Permission to create BusinessReferenceData versions", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Update", "Update Business Reference Data Version", "Permission to update BusinessReferenceData version content", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Validate", "Validate Business Reference Data Version", "Permission to validate BusinessReferenceData versions", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Submit", "Submit Business Reference Data Version", "Permission to submit BusinessReferenceData versions", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Approve", "Approve Business Reference Data Version", "Permission to approve BusinessReferenceData versions", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "Publish", "Publish Business Reference Data Version", "Permission to publish BusinessReferenceData versions", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Version", "PublishOverride", "Override Business Reference Data Publish", "Permission to publish BusinessReferenceData versions with governance override", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Import", "Preview", "Preview Business Reference Data Import", "Permission to preview BusinessReferenceData imports", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Import", "Commit", "Commit Business Reference Data Import", "Permission to commit BusinessReferenceData imports", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Usage", "Register", "Register Business Reference Data Usage", "Permission to register BusinessReferenceData usage", moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Consumer", "Read", "Read Published Business Reference Data", "Permission to consume published BusinessReferenceData values", moduleOverride: "reference-data"),

            // MOD-0288 — Organization, Person & Position Directory (platform-admin screens).
            // FIX-PERM-MODULE-ATTRIBUTION — organization-units.* is owned by the organization module
            // (OrganizationManifestProvider, ModuleCode: "organization"); Key stays platform.organization-units.*.
            new("platform", "organization-units", "read", "Read Organization Units", "Permission to view organization units", moduleOverride: "organization"),
            new("platform", "organization-units", "create", "Create Organization Unit", "Permission to create organization units", moduleOverride: "organization"),
            new("platform", "organization-units", "update", "Update Organization Unit", "Permission to edit organization units", moduleOverride: "organization"),
            new("platform", "organization-units", "archive", "Archive Organization Unit", "Permission to archive organization units", moduleOverride: "organization"),
            new("platform", "organization-units", "delete", "Delete Organization Unit", "Permission to delete organization units", moduleOverride: "organization"),
            // FIX-PERM-ATTRIBUTION-2 — positions.* and position-assignments.* are the same tenant-side
            // Organization/Position Directory as organization-units.* (all served by /OrganizationUnits,
            // /Positions, /PositionAssignments — none under /Platform/); Key stays platform.positions.* /
            // platform.position-assignments.*.
            new("platform", "positions", "read", "Read Positions", "Permission to view positions", moduleOverride: "organization"),
            new("platform", "positions", "create", "Create Position", "Permission to create positions", moduleOverride: "organization"),
            new("platform", "positions", "update", "Update Position", "Permission to edit positions", moduleOverride: "organization"),
            new("platform", "positions", "archive", "Archive Position", "Permission to archive positions", moduleOverride: "organization"),
            new("platform", "positions", "delete", "Delete Position", "Permission to delete positions", moduleOverride: "organization"),
            new("platform", "organization", "read-manager-chain", "Read Manager Chain", "Permission to view a position's manager chain"),
            new("platform", "position-assignments", "read", "Read Position Assignments", "Permission to view position assignments", moduleOverride: "organization"),
            new("platform", "position-assignments", "create", "Create Position Assignment", "Permission to create position assignments", moduleOverride: "organization"),
            new("platform", "position-assignments", "update", "Update Position Assignment", "Permission to edit position assignments", moduleOverride: "organization"),
            new("platform", "position-assignments", "delete", "Delete Position Assignment", "Permission to delete position assignments", moduleOverride: "organization"),

            // FEAT-ROLEPERMS-REDESIGN (Part A) — the tenant self-service Security/Menu Settings keys are owned by the
            // tenant-settings BASELINE module (TenantSettingsManifestProvider, ModuleCode: "tenant-settings"), not the
            // platform-admin umbrella. Key stays platform.tenant-security.* (immutable identity); only the Module
            // attribution changes so they group under their own "Tenant Settings" group in Role Permissions. The
            // catalog sync's UPDATE path refreshes DisplayName/Description only (Module is immutable there), so this
            // override survives every Platform startup; idempotent ReconcilePermissionModulesAsync updates existing rows.
            new("platform", "tenant-security", "read", "Read Tenant Security", "Permission to view tenant security settings", moduleOverride: "tenant-settings"),
            new("platform", "tenant-security", "manage", "Manage Tenant Security", "Permission to manage tenant security settings", moduleOverride: "tenant-settings"),

            // FIX-MENU-SETTINGS-ACCESS — tenant self-service Menu (navigation) Settings write. Same shape/pattern as
            // platform.tenant-security.manage: a tenant-scoped platform.* self-service key (the backend forces the
            // caller's tenant_id, so it is NOT an escalation) grouped under the "tenant-settings" BASELINE module.
            // Gates GET/PUT navigation preferences (the tenant-wide sidebar), while GET menu stays open to every user.
            new("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", "Permission to manage tenant navigation (menu) settings", moduleOverride: "tenant-settings"),

            new("goldenslim", "records", "read",   "Read Golden Slim",   "View Golden Slim records"),
            new("goldenslim", "records", "create", "Create Golden Slim", null),
            new("goldenslim", "records", "update", "Update Golden Slim", null),
            new("goldenslim", "records", "delete", "Delete Golden Slim", null),

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

            // MOD-0029-FU01 — Controlled Document Versioning & Template Sharing (Layer 1 central keys) (14).
            // Keys must match the constants in Diten.Platform DocumentManagementPermissions (platform.document-management.*).
            // Platform-scoped, so DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            // NOTE: per-folder/per-document access is MOD-0029 AccessPolicy domain data (Layer 2), NOT central catalog keys.
            new("platform", "document-management.controlled-documents", "view", "View Controlled Documents", "Permission to view controlled documents, details and version listings"),
            new("platform", "document-management.controlled-documents", "create", "Create Controlled Document", "Permission to create a controlled document attached to a CollectionInstance folder"),
            new("platform", "document-management.controlled-documents.version", "create", "Upload Controlled Document Version", "Permission to upload a new immutable version of a controlled document"),
            new("platform", "document-management.controlled-documents.version", "view", "View Controlled Document Versions", "Permission to view the version history of a controlled document"),
            new("platform", "document-management.controlled-documents", "share", "Share Controlled Document", "Permission to share a controlled document with another company/legal entity"),
            new("platform", "document-management.controlled-documents.access", "manage", "Manage Controlled Document Access", "Permission to manage a controlled document's resource-level access policy"),
            new("platform", "document-management.templates", "view", "View Templates", "Permission to view template documents and their details"),
            new("platform", "document-management.templates", "create", "Create Template", "Permission to create a reusable template document"),
            new("platform", "document-management.templates.version", "create", "Upload Template Version", "Permission to upload a new immutable version of a template document"),
            new("platform", "document-management.templates", "share", "Share Template", "Permission to share a template document with another company/legal entity"),
            new("platform", "document-management.folder-documents", "upload", "Upload Folder Document", "Permission for folder-level upload of a document/template into a CollectionInstance node"),
            new("platform", "document-management.folder-documents.access", "manage", "Manage Folder Document Access", "Permission to manage folder-level document access policies (FolderDocumentAccessPolicy)"),
            new("platform", "document-management.folder-shares", "create", "Create Folder Share", "Permission to dry-run and execute a folder/branch share with associated templates"),
            new("platform", "document-management.folder-shares", "view", "View Folder Share", "Permission to view folder/branch share operation status and per-item outcomes"),

            // MOD-0029-FU02 — Corporate Template Master Library Foundation (Layer 1 central keys) (6).
            // Keys must match the approved module pack contract (platform.document-management.template-masters.*).
            // Platform-scoped, so DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            new("platform", "document-management.template-masters", "view", "View Template Masters", "Permission to view corporate template masters and details"),
            new("platform", "document-management.template-masters", "create", "Create Template Master", "Permission to create a corporate template master"),
            new("platform", "document-management.template-masters", "version.publish", "Publish Template Master Version", "Permission to publish a new corporate template master version"),
            new("platform", "document-management.template-masters", "deprecate", "Deprecate Template Master", "Permission to deprecate a corporate template master"),
            new("platform", "document-management.template-masters", "impact.view", "View Template Master Impact", "Permission to view template master adoption impact summaries"),
            new("platform", "document-management.template-masters", "delete", "Delete Template Master", "Permission to soft-delete corporate template masters (single and bulk)"),
            new("platform", "document-management.template-masters", "manage", "Manage Template Masters", "Permission to manage corporate template masters"),

            // MOD-0029-FU03 — Template Variant Drift Foundation (Layer 1 central keys) (5).
            // Keys must match the approved module pack contract (platform.document-management.template-variants.*).
            // Platform-scoped, so DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            new("platform", "document-management.template-variants", "view", "View Template Variants", "Permission to view template variants and details"),
            new("platform", "document-management.template-variants", "create", "Create Template Variant", "Permission to create a template variant from a corporate template master"),
            new("platform", "document-management.template-variants", "compare", "Compare Template Variant", "Permission to compare template variant metadata against its source master"),
            new("platform", "document-management.template-variants", "rebase", "Rebase Template Variant", "Permission to perform metadata-only template variant rebase"),
            new("platform", "document-management.template-variants", "manage", "Manage Template Variants", "Permission to manage template variants"),

            // MOD-0029-FU04 — Folder Access & Permission Matrix Foundation (Layer 1 central keys) (4).
            // Keys must match the approved module pack contract (platform.document-management.access.*).
            // Platform-scoped, so DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            new("platform", "document-management.access", "view", "View Document Access Matrix", "Permission to view document management access policies and matrix"),
            new("platform", "document-management.access", "manage", "Manage Document Access Matrix", "Permission to create, update, disable and archive document management access policies"),
            new("platform", "document-management.access", "preview", "Preview Effective Document Access", "Permission to preview effective document management access for users/roles and targets"),
            new("platform", "document-management.access", "audit.view", "View Document Access Audit", "Permission to view document management access policy audit history"),

            // MOD-0023 — Workflow Config / Approval Templates permissions (13). Keys must match the
            // constants in Diten.Platform WorkflowPermissions (platform.workflow.*). Platform-scoped, so
            // the DefaultRolePermissionTemplate grants them to SuperAdmin (full catalog) only.
            new("platform", "workflow.definitions", "view", "View Workflow Definitions", "Permission to view workflow approval templates and versions", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.definitions", "manage", "Manage Workflow Definitions", "Permission to create and edit workflow approval templates", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.definitions", "publish", "Publish Workflow Definition", "Permission to publish an immutable workflow template version", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.instances", "start", "Start Workflow Instance", "Permission to start a workflow approval instance", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.instances", "view", "View Workflow Instances", "Permission to view workflow instances and approval tasks", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.tasks", "approve", "Approve Workflow Task", "Permission to approve a workflow approval task", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.tasks", "reject", "Reject Workflow Task", "Permission to reject a workflow approval task", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.tasks", "delegate", "Delegate Workflow Task", "Permission to delegate a workflow approval task", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.tasks", "request-info", "Request Info on Workflow Task", "Permission to request additional information on a workflow approval task", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.tasks", "cancel", "Cancel Workflow Task", "Permission to cancel a workflow approval task", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.transitions", "evaluate", "Evaluate Workflow Transition Gate", "Permission to evaluate the workflow integration transition gate", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.escalations", "manage", "Manage Workflow SLA Rules", "Permission to create and view workflow SLA escalation rules", moduleOverride: "workflow", scope: PermissionScope.PlatformAdmin),
            new("platform", "workflow.escalations", "run", "Run Workflow Escalations", "Permission to run the workflow escalation/timeout processor")
        };

        foreach (var p in permissions)
        {
            var filter = Builders<Permission>.Filter.Eq(x => x.Key, p.Key);
            var exists = await col.Find(filter).AnyAsync();
            if (!exists) await col.InsertOneAsync(p);
        }

        await ReconcilePermissionModulesAsync(col, permissions);
        await ReconcilePermissionScopesAsync(col, permissions);
        await ReconcilePermissionModuleCasingAsync(col);
    }

    // FIX-PERM-MODULE-CASE-CONSISTENCY — the seed writes Module lowercase, but the catalog sync historically stored
    // the Platform-uppercased ModuleCode (ACCESS-GOVERNANCE, GOLDENCOMPACT, WORKFLOW...), so the same module appeared
    // under two casings and the RoleAssignments "Module" grouping split. Unlike the Module reconcile (seed-list based),
    // this needs the WHOLE collection to reach self-registered/synced rows absent from the seed list, so it scans every
    // permission and lowercases Module. Idempotent (already-lowercase rows are skipped); Module-case only — the Key,
    // Scope and every other field are untouched, so the escalation boundary (Scope-based) cannot change.
    private static async Task ReconcilePermissionModuleCasingAsync(IMongoCollection<Permission> col)
    {
        var all = await col.Find(_ => true).ToListAsync();

        var writes = new List<WriteModel<Permission>>();
        foreach (var p in all)
        {
            var lower = (p.Module ?? string.Empty).ToLowerInvariant();
            if (string.Equals(p.Module, lower, StringComparison.Ordinal))
            {
                continue; // already lowercase
            }

            writes.Add(new UpdateOneModel<Permission>(
                Builders<Permission>.Filter.Eq(x => x.Id, p.Id),
                Builders<Permission>.Update.Set(x => x.Module, lower)));
        }

        if (writes.Count == 0) return;

        var result = await col.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
        if (result.ModifiedCount > 0)
        {
            Console.WriteLine($"Reconciled Module casing (→ lowercase) for {result.ModifiedCount} existing permission(s).");
        }
    }

    // FIX-PERM-MODULE-ATTRIBUTION — the seed list above is the single source of truth for each permission's
    // Module attribution. A literal fix here (e.g. organization-units.* -> "organization") only affects rows
    // inserted from a clean collection; rows already seeded in a running environment keep their old Module
    // forever otherwise. This reconciles existing rows by Key on every startup so a corrected literal takes
    // effect on restart without a DB wipe. Module-only — Key/DisplayName/Description/IsSystem untouched.
    private static async Task ReconcilePermissionModulesAsync(IMongoCollection<Permission> col, List<Permission> permissions)
    {
        var writes = new List<WriteModel<Permission>>();
        foreach (var p in permissions)
        {
            var filter = Builders<Permission>.Filter.And(
                Builders<Permission>.Filter.Eq(x => x.Key, p.Key),
                Builders<Permission>.Filter.Ne(x => x.Module, p.Module));
            var update = Builders<Permission>.Update.Set(x => x.Module, p.Module);
            writes.Add(new UpdateOneModel<Permission>(filter, update));
        }

        if (writes.Count == 0) return;

        var result = await col.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
        if (result.ModifiedCount > 0)
        {
            Console.WriteLine($"Reconciled Module attribution for {result.ModifiedCount} existing permission(s).");
        }
    }

    // İŞ3-FAZ1b — the Scope reconcile is SEED-LIST based (like the Module reconcile), using each seed literal's
    // authoritative Scope by Key. This is REQUIRED now that Module is the manifest ModuleCode: a Module-derived
    // ClassifyScope reconcile would wrongly force workflow (Module="workflow") to Tenant and leak a platform-admin
    // module into tenant roles. The seed literals carry the correct Scope explicitly where it diverges from the
    // Module classification (workflow → PlatformAdmin), so reconciling by Key keeps the boundary exact.
    // Synced-only permissions (not in the seed list) are intentionally NOT touched — their Scope is route-derived and
    // carried by the catalog sync (authoritative), so this reconcile must never overwrite it. Idempotent + Scope-only:
    // the server-side $ne skips rows already correct (and matches rows where the field is absent → legacy backfill).
    private static async Task ReconcilePermissionScopesAsync(IMongoCollection<Permission> col, List<Permission> permissions)
    {
        var writes = new List<WriteModel<Permission>>();
        foreach (var p in permissions)
        {
            var filter = Builders<Permission>.Filter.And(
                Builders<Permission>.Filter.Eq(x => x.Key, p.Key),
                Builders<Permission>.Filter.Ne(x => x.Scope, p.Scope));
            var update = Builders<Permission>.Update.Set(x => x.Scope, p.Scope);
            writes.Add(new UpdateOneModel<Permission>(filter, update));
        }

        if (writes.Count == 0) return;

        var result = await col.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false });
        if (result.ModifiedCount > 0)
        {
            Console.WriteLine($"Reconciled Scope for {result.ModifiedCount} existing permission(s).");
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

    // FEAT-ROLE-GRANT-RECONCILE — a permission newly added to TenantSelfServicePermissions only reaches an Admin
    // role at PROVISION time, so tenants provisioned BEFORE the key was added never receive it and their admins hit
    // 403 on the new capability (e.g. Menu Settings / platform.tenant-navigation.manage). This backfills EVERY
    // tenant's Admin role with any missing self-service permission on startup, mirroring the ReconcilePermission
    // ModulesAsync idempotent-reconcile pattern at the role-grant level.
    //   • Additive + idempotent: never revokes; grants already present are skipped (re-run adds 0).
    //   • Scope-safe: reconciles ONLY the curated self-service subset, NOT the full Admin template, so a tenant's
    //     customized Admin role is never clobbered. SuperAdmin (full catalog) and all other roles are untouched.
    //   • Each affected tenant's role-assignment version is bumped so cached permission snapshots invalidate (FU13).
    private static async Task ReconcileTenantAdminSelfServiceGrantsAsync(IMongoDatabase database)
    {
        var roleCol = database.GetCollection<Role>("roles");
        var permCol = database.GetCollection<Permission>("permissions");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");

        // Every tenant's Admin role (Name == "Admin"); NOT SuperAdmin, NOT Viewer, NOT custom roles. The planner
        // re-checks the name so it stays authoritative even though this query already narrows the feed.
        var adminRoles = await roleCol.Find(r => r.Name == DefaultRolePermissionTemplate.AdminRole).ToListAsync();
        if (adminRoles.Count == 0) return;

        var catalog = await permCol.Find(_ => true).ToListAsync();
        var roleIds = adminRoles.Select(r => r.Id).ToHashSet();
        var existingGrants = (await rpCol.Find(rp => roleIds.Contains(rp.RoleId)).ToListAsync())
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        var roleRefs = adminRoles.Select(r => new TenantAdminSelfServiceReconciler.RoleRef(r.Id, r.Name, r.TenantId));
        var planned = TenantAdminSelfServiceReconciler.PlanMissingGrants(roleRefs, catalog, existingGrants);
        if (planned.Count == 0) return;

        foreach (var g in planned)
        {
            await rpCol.InsertOneAsync(RolePermission.SystemGrant(g.RoleId, g.PermissionId, g.TenantId, SystemUser));
        }

        // Bump each affected tenant's role-assignment version (reuses the atomic $inc upsert so cached authz
        // snapshots invalidate exactly as a manual assign would).
        var affectedTenants = planned.Select(g => g.TenantId).ToHashSet();
        var versionService = new RoleAssignmentVersionRepository(database);
        foreach (var tenantId in affectedTenants)
        {
            await versionService.IncrementAsync(tenantId, CancellationToken.None);
        }

        Console.WriteLine($"Reconciled {planned.Count} missing tenant self-service grant(s) across {affectedTenants.Count} tenant(s).");
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

    // FIX-2 — the default/dev tenant's Admin role is entitled (via its plan) to goldenslim + workflow, but the
    // baseline template only grants auth+mdm, and dev runs with eventing/provisioning off — so the entitlement
    // bridge never fires here. Seed those modules as MODULE grants (SourceModuleCode) so devadmin sees them on
    // next login, exactly as the runtime EntitlementPermissionSyncService would produce them. Idempotent.
    private static async Task SeedDefaultTenantEntitledModuleGrantsAsync(IMongoDatabase database)
    {
        var roleCol = database.GetCollection<Role>("roles");
        var permCol = database.GetCollection<Permission>("permissions");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");

        var adminRole = await roleCol
            .Find(r => r.TenantId == DefaultTenantId && r.Name == DefaultRolePermissionTemplate.AdminRole && !r.IsDeleted)
            .FirstOrDefaultAsync();
        if (adminRole is null)
        {
            Console.WriteLine("Skipped default-tenant entitled-module grants: Admin role not found.");
            return;
        }

        // moduleCode → which catalog permissions belong to it (goldenslim by Module; workflow is platform-hosted
        // so matched by its known platform.workflow.* keys — the same set the resolver allow-list resolves).
        var modules = new (string ModuleCode, Func<Permission, bool> Match)[]
        {
            ("goldenslim", p => string.Equals(p.Module, "goldenslim", StringComparison.OrdinalIgnoreCase)),
            ("workflow",   p => WorkflowPermissionKeys.Contains(p.Key))
        };

        var catalog = await permCol.Find(p => !p.IsDeleted).ToListAsync();
        var current = await rpCol
            .Find(rp => rp.RoleId == adminRole.Id && rp.TenantId == DefaultTenantId && !rp.IsDeleted)
            .ToListAsync();
        var grantedPermissionIds = current.Select(rp => rp.PermissionId).ToHashSet();

        var granted = 0;
        foreach (var (moduleCode, match) in modules)
        {
            foreach (var permission in catalog.Where(match))
            {
                if (!grantedPermissionIds.Add(permission.Id))
                {
                    continue; // already granted to this role (any source) → idempotent skip
                }

                await rpCol.InsertOneAsync(
                    RolePermission.ModuleGrant(adminRole.Id, permission.Id, DefaultTenantId, SystemUser, moduleCode));
                granted++;
            }
        }

        Console.WriteLine($"default-tenant entitled-module grants: {granted} new Module-grant(s) on Admin role.");
    }
}
