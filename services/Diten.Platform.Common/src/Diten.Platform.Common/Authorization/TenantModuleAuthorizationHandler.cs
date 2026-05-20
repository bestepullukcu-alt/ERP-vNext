using Microsoft.AspNetCore.Authorization;

namespace Diten.Platform.Common.Authorization;

public sealed class TenantModuleAuthorizationHandler : AuthorizationHandler<TenantModuleRequirement>
{
    private readonly IEntitlementChecker entitlementChecker;
    private readonly IEntitlementAuditSink auditSink;

    public TenantModuleAuthorizationHandler(
        IEntitlementChecker entitlementChecker,
        IEntitlementAuditSink auditSink)
    {
        this.entitlementChecker = entitlementChecker ?? throw new ArgumentNullException(nameof(entitlementChecker));
        this.auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantModuleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            Fail(context, "Authenticated user is required.");
            return;
        }

        var actorType = context.User.FindFirst("actor_type")?.Value;
        if (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        if (!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            Fail(context, "Only tenant_user actors can be checked for module entitlement in this phase.");
            return;
        }

        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            Fail(context, "A valid tenant_id claim is required.");
            return;
        }

        try
        {
            var result = await entitlementChecker.IsModuleEntitledAsync(
                tenantId,
                requirement.ModuleCode,
                CancellationToken.None);

            if (result.IsAllowed)
            {
                context.Succeed(requirement);
                return;
            }

            await LogDeniedAsync(tenantId, actorType, result, nameof(TenantModuleAuthorizationHandler), nameof(TenantModuleRequirement));
            Fail(context, FormatDenyReason(result));
        }
        catch (Exception ex)
        {
            Fail(context, $"Module entitlement check failed: {ex.GetType().Name}.");
        }
    }

    private void Fail(AuthorizationHandlerContext context, string message)
    {
        context.Fail(new AuthorizationFailureReason(this, message));
    }

    private async Task LogDeniedAsync(
        Guid tenantId,
        string? actorType,
        EntitlementCheckResult result,
        string source,
        string requirementName)
    {
        try
        {
            await auditSink.LogDeniedAsync(
                new EntitlementAuditDenyContext(
                    tenantId,
                    actorType,
                    EntitlementKind.Module,
                    result.Code,
                    result.DenyReason,
                    IsTransientFailure: !result.IsCacheable,
                    source,
                    requirementName),
                CancellationToken.None);
        }
        catch
        {
            // Audit failures must not change authorization decisions.
        }
    }

    private static string FormatDenyReason(EntitlementCheckResult result)
    {
        var denyReason = result.DenyReason?.ToString() ?? "Unknown";
        return $"Module entitlement denied. Kind={result.Kind}; Code={result.Code}; DenyReason={denyReason}; ExpiresAtUtc={result.ExpiresAtUtc?.ToString("O") ?? "null"}.";
    }
}
