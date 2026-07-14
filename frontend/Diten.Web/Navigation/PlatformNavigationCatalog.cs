namespace Diten.Web.Navigation;

// FEAT-CTRLK-PLATFORM-DYNAMIC — the SINGLE server-side source of truth for the platform-admin navigation. Both the
// sidebar (_LayoutPlatformAdmin.cshtml) and Ctrl+K search (PlatformSearchController) render from this list, so they
// can never drift and a new platform screen is added in exactly one place. Platform screens are fixed MVC pages
// (not self-registered), so this is a static catalog rather than an API-driven one.
public sealed record PlatformNavItem(
    string Key,
    string LabelResourceKey,
    string Url,
    string Icon,
    string? CreateUrl = null,
    string? CreateLabelResourceKey = null,
    IReadOnlyList<string>? Keywords = null);

public static class PlatformNavigationCatalog
{
    /// <summary>SharedLocalizer key for the single sidebar/section header.</summary>
    public const string SectionResourceKey = "PlatformAdministration";

    // Order + values MUST mirror the sidebar exactly (labels via SharedLocalizer keys; icons are the sidebar bx-*).
    public static readonly IReadOnlyList<PlatformNavItem> Items = new PlatformNavItem[]
    {
        new("Tenants", "TenantManagementMenu", "/Platform/Tenants", "bx-buildings",
            "/Platform/Tenants/Create", "AddTenant", new[] { "tenant", "tenants", "companies", "customers", "registry" }),
        new("SubscriptionPlans", "SubscriptionPlans", "/Platform/SubscriptionPlans", "bx-credit-card",
            "/Platform/SubscriptionPlans/Create", "AddSubscriptionPlan", new[] { "subscription", "plan", "plans", "billing" }),
        new("SubscriptionFeatures", "SubscriptionFeatures", "/Platform/SubscriptionFeatures", "bx-package",
            null, null, new[] { "feature", "features", "subscription" }),
        new("ModuleCatalog", "ModuleCatalog", "/Platform/ModuleCatalog", "bx-grid-alt",
            "/Platform/ModuleCatalog/Create", "AddModule", new[] { "module", "modules", "catalog" }),
        new("DomainManagement", "DomainManagement", "/Platform/DomainManagement", "bx-category",
            null, null, new[] { "domain", "domains", "taxonomy" }),
        new("ServiceManagement", "ServiceManagement", "/Platform/ServiceManagement", "bx-server",
            null, null, new[] { "service", "services", "taxonomy" }),
        new("InterfaceRegistry", "InterfaceRegistry", "/Platform/InterfaceRegistry", "bx-git-branch",
            null, null, new[] { "interface", "registry", "integration" }),
        new("Administrators", "PlatformAdministrators", "/Platform/Administrators", "bx-user-check",
            null, null, new[] { "admin", "administrator", "administrators", "operators" }),
        new("AuditLog", "AuditLogMenu", "/Platform/AuditLog", "bx-history",
            null, null, new[] { "audit", "log", "events", "trail" }),
        new("AuditRetention", "AuditRetentionMenu", "/Platform/AuditRetention", "bx-shield-quarter",
            null, null, new[] { "audit", "retention", "policy" }),
        // MOD-0027-FU02 — Platform Admin notification management (templates / tenant messaging settings /
        // read-only dispatch monitoring). Backend authorizes every action via [HasPermission] on
        // platform.notifications.*; a restricted actor hitting a direct URL/action is fail-closed at the API.
        new("NotificationTemplates", "NotificationTemplatesMenu", "/Platform/NotificationTemplates", "bx-envelope",
            "/Platform/NotificationTemplates/Create", "AddNotificationTemplate",
            new[] { "notification", "notifications", "template", "templates", "email", "mail" }),
        new("NotificationSettings", "NotificationSettingsMenu", "/Platform/NotificationSettings", "bx-cog",
            "/Platform/NotificationSettings/Create", "AddNotificationSettings",
            new[] { "notification", "notifications", "messaging", "smtp", "provider", "settings" }),
        new("NotificationDispatches", "NotificationDispatchesMenu", "/Platform/NotificationDispatches", "bx-send",
            null, null, new[] { "notification", "notifications", "dispatch", "dispatches", "delivery", "outbox" }),
        // MOD-0027-FU03 — read-only Notification Event Catalog. Backend authorizes via platform.notifications.events.*;
        // a restricted actor hitting the direct URL is fail-closed at the API.
        new("NotificationEvents", "NotificationEventsMenu", "/Platform/NotificationEvents", "bx-broadcast",
            null, null, new[] { "notification", "notifications", "event", "events", "catalog", "template", "binding", "manifest" }),
        new("SelfAccess", "SelfAccessMenu", "/Platform/SelfAccess", "bx-user-check",
            null, null, new[] { "access", "permissions", "self", "effective" }),
    };
}
