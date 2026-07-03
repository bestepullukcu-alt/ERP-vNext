using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.PlatformAccount.Commands;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Domain.Enums;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

// FEAT-AUDIT-BIZ-CRITICAL — one representative per instrumented AuditCategory, verifying the self-declared
// central-audit metadata contract (Category / Operation / EntityType / EntityId / TargetTenantId). Request params
// that GetAuditMetadata does not read are passed as null! (the metadata is derived from the command's own ids).
public sealed class BizCriticalAuditMetadataTests
{
    private static AuditRequestMetadata Meta(object command)
    {
        Assert.IsAssignableFrom<IAuditableCommand>(command);
        return ((IAuditMetadataProvider)command).GetAuditMetadata();
    }

    [Fact]
    public void TenantAdministration_delete_tenant()
    {
        var tenantId = Guid.NewGuid();
        var m = Meta(new DeleteTenantCommand(tenantId));
        Assert.Equal(AuditCategory.TenantAdministration, m.Category);
        Assert.Equal(AuditOperation.Delete, m.Operation);
        Assert.Equal("Tenant", m.EntityType);
        Assert.Equal(tenantId, m.EntityId);
        Assert.Equal(tenantId, m.TargetTenantId);
    }

    [Fact]
    public void IdentityAccess_delete_tenant_admin_user()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var m = Meta(new DeleteTenantAdminUserCommand(tenantId, adminId));
        Assert.Equal(AuditCategory.IdentityAccess, m.Category);
        Assert.Equal(AuditOperation.Delete, m.Operation);
        Assert.Equal("TenantAdminUser", m.EntityType);
        Assert.Equal(adminId, m.EntityId);          // the entity is the admin user
        Assert.Equal(tenantId, m.TargetTenantId);    // scoped to its tenant
    }

    [Fact]
    public void SubscriptionBilling_activate_subscription()
    {
        var tenantId = Guid.NewGuid();
        var subId = Guid.NewGuid();
        var m = Meta(new ActivateTenantSubscriptionCommand(tenantId, subId, null!));
        Assert.Equal(AuditCategory.SubscriptionBilling, m.Category);
        Assert.Equal(AuditOperation.Activate, m.Operation);
        Assert.Equal("TenantSubscription", m.EntityType);
        Assert.Equal(subId, m.EntityId);
        Assert.Equal(tenantId, m.TargetTenantId);
    }

    [Fact]
    public void ModuleCatalog_delete_item()
    {
        var id = Guid.NewGuid();
        var m = Meta(new DeleteModuleCatalogItemCommand(id));
        Assert.Equal(AuditCategory.ModuleCatalog, m.Category);
        Assert.Equal(AuditOperation.Delete, m.Operation);
        Assert.Equal("ModuleCatalogItem", m.EntityType);
        Assert.Equal(id, m.EntityId);
        Assert.True(m.IsPlatformGlobal);   // module catalog is platform-global — no tenant context to scope to
    }

    [Fact]
    public void System_update_audit_retention_is_platform_global()
    {
        var m = Meta(new UpdateAuditRetentionCommand(null!));
        Assert.Equal(AuditCategory.System, m.Category);
        Assert.Equal(AuditOperation.Update, m.Operation);
        Assert.Equal("AuditRetentionPolicy", m.EntityType);
        Assert.True(m.IsPlatformGlobal);
    }

    [Fact]
    public void PlatformConfiguration_update_account_settings_is_platform_global()
    {
        var m = Meta(new UpdatePlatformAccountSettingsCommand(null!));
        Assert.Equal(AuditCategory.PlatformConfiguration, m.Category);
        Assert.Equal(AuditOperation.Update, m.Operation);
        Assert.Equal("PlatformAccountSettings", m.EntityType);
        Assert.True(m.IsPlatformGlobal);
    }
}
