using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Infrastructure.Services.Http;

/// <summary>
/// The ONE answer to "which tenant id may this outbound call carry?", so the reference validators cannot drift
/// into two answers.
///
/// <para><b>Why this is not a <c>DelegatingHandler</c>.</b> Because a handler cannot see the request. MEASURED
/// 2026-08-28: <c>IHttpClientFactory</c> builds and CACHES the handler chain in its own scope, so a handler that
/// injects the request-scoped <see cref="ITenantContext"/> holds an instance belonging to no request, reports
/// <c>IsResolved == false</c>, and silently adds nothing. That is what <c>TenantPropagationHandler</c> has been
/// doing. The caller must therefore ask this question itself, from its own scope — the shape
/// <c>RemoteWorkItemGateway</c> arrived at by the same measurement.</para>
///
/// <para><b>Why the platform sentinel must never travel.</b> A platform token's <c>tenant_id</c> claim is the
/// sentinel <c>00000000-0000-0000-0000-000000000001</c> (<c>PlatformLoginCommandHandler</c>), which is a login
/// realm and not a customer. MDM answers 400 "Tenant mismatch" whenever a JWT tenant and an <c>X-Tenant-Id</c>
/// are both present and differ, and it makes NO exception for a platform actor — it never reads
/// <c>actor_type</c> at all (measured in MDM's <c>TenantResolutionMiddleware</c>). So sending the sentinel says
/// the wrong thing and sending anything else next to it is a hard 400. In a platform context the only honest
/// value is the tenant the admin declared it is acting for.</para>
/// </summary>
public static class TenantOnTheWire
{
    /// <summary>
    /// The tenant id this call may claim, or <c>null</c> when there is none — which callers must treat as
    /// "do not call", never as "call without a tenant".
    /// </summary>
    /// <remarks>
    /// ⚠ In a platform context this returns null TODAY on every HTTP path, because nothing populates
    /// <see cref="ITenantContext.TargetTenantId"/> with a real tenant: all three
    /// <c>TenantResolutionMiddleware</c> platform branches call <c>SetPlatformContext(Guid.Empty)</c>, and
    /// <c>Guid.Empty</c> means "no specific target tenant" (the same reading <c>AuditService</c> already gives
    /// it). The mechanism below is the correct one for the day a platform admin can declare a target tenant;
    /// until then a platform actor simply cannot validate a tenant's reference, and fails closed saying so
    /// instead of being answered about the sentinel realm. Reported to CONTROL TOWER 2026-08-28 as an open
    /// question, deliberately NOT widened into a middleware change from this round.
    /// </remarks>
    public static Guid? Resolve(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!tenantContext.IsResolved)
        {
            return null;
        }

        if (!tenantContext.IsPlatformContext)
        {
            return tenantContext.TenantId == Guid.Empty ? null : tenantContext.TenantId;
        }

        return tenantContext.TargetTenantId is { } target && target != Guid.Empty ? target : null;
    }
}
