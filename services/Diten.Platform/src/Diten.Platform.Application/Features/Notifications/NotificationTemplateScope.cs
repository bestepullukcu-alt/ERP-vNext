using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications;

/// <summary>
/// WP-PLATFORM-SCOPE-SMALL-01 (A) — WHICH TEMPLATE AN ENDPOINT MAY TOUCH BY ITS ID. A template is either a platform
/// default (no tenant) or one tenant's override. A route without a tenant addresses platform defaults only; a route
/// under <c>tenant-settings/{tenantId}</c> addresses that tenant's overrides only. A template found by id outside the
/// route's scope is answered as not found — the same 404 a missing id gets — and nothing is read or written.
/// (Platform administrators manage tenant overrides on purpose: the tenant routes create, list and update them; the
/// id-only routes used to reach every tenant's template without naming the tenant.)
/// </summary>
public static class NotificationTemplateScope
{
    public static bool Matches(NotificationTemplate template, Guid? routeTenantId) =>
        routeTenantId is { } tenantId
            ? !template.IsPlatformDefault && template.TenantId == tenantId
            : template.IsPlatformDefault && template.TenantId is null;
}
