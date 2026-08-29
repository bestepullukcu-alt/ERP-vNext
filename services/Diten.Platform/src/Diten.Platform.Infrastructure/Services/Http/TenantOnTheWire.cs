using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Infrastructure.Services.Http;

/// <summary>
/// The ONE answer to "which tenant id may this outbound call carry?", so the reference validators cannot drift
/// into two answers.
///
/// <para><b>⚠ Why this is not a <c>DelegatingHandler</c>, and must never become one. MEASURED 2026-08-28.</b>
/// Because a handler cannot see the request. <c>IHttpClientFactory</c> builds and CACHES each client's handler
/// chain in its OWN scope, so a handler that injects the request-scoped <see cref="ITenantContext"/> holds an
/// instance belonging to no request: it reports <c>IsResolved == false</c>, adds no header, and says nothing
/// anywhere. A shared tenant-propagation handler did exactly that on three services for its entire life; the two
/// reference validators were moved off it (BL-311) and the class itself was then deleted (BL-316). The caller
/// must therefore ask this question itself, from its own scope — the shape <c>RemoteWorkItemGateway</c> arrived
/// at by the same measurement.</para>
///
/// <para><b>⚠ A unit test cannot catch that failure, and did not.</b> A test container registers the tenant
/// context as a SINGLETON, so it proves the WIRING and never the LIFETIME — the tests that "covered" the deleted
/// handler passed green while it put nothing on the wire. It was caught only by calling a real module and reading
/// the tenant it echoed back: "(no tenant header)". Treat any claim that tenancy travels on some client as worth
/// exactly the live read behind it.</para>
///
/// <para><b>⚠ And a handler would be the wrong answer even on the day it did resolve.</b> The deleted one did
/// Remove-then-Add with <see cref="ITenantContext.TenantId"/>, so it would OVERWRITE a value a caller had
/// correctly computed here with — in a platform context — the login sentinel realm instead of the target tenant.
/// "Just fix the handler" is therefore not a cheaper alternative to this class; it is a second, quieter bug.</para>
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
    /// <summary>
    /// Why an outbound call carries no tenant id. Distinct values because "we could not name a tenant" and
    /// "the module did not answer" are different facts with different owners, and BOTH used to collapse into the
    /// same 404 with the same sentence — so an operator asked "why not found?" and the code had no answer.
    /// These are OPERATOR signals written to the log; they are deliberately NOT put on
    /// <c>Response&lt;T&gt;.ReasonCode</c>, because a reason_code on this platform feeds the frontend resx bridge
    /// and would become a new user-facing string owing seven translations. The reader's sentence is unchanged.
    /// </summary>
    public static class SkipReason
    {
        /// <summary>No tenant was resolved at all — the request never went through tenant resolution.</summary>
        public const string TenantContextUnresolved = "tenant-context-unresolved";

        /// <summary>
        /// A platform actor with no declared target tenant. Its token names the login sentinel realm
        /// (<c>00000000-0000-0000-0000-000000000001</c>), not a customer, so there is nothing honest to ask about.
        /// CT decided 2026-08-28 that acting-for-a-tenant will NOT be built: every route 403s a platform actor
        /// today, so this path has no customer — it stays fail-closed and merely says so out loud.
        /// </summary>
        public const string PlatformContextWithoutTargetTenant = "platform-context-without-target-tenant";
    }

    /// <summary>
    /// The tenant id this call may claim, or <c>null</c> with a named <paramref name="skipReason"/> when there is
    /// none — which callers must treat as "do not call", never as "call without a tenant".
    /// </summary>
    /// <remarks>
    /// ⚠ In a platform context this ALWAYS skips today: nothing populates
    /// <see cref="ITenantContext.TargetTenantId"/> with a real tenant (all three <c>TenantResolutionMiddleware</c>
    /// platform branches call <c>SetPlatformContext(Guid.Empty)</c>), and the production <c>TenantContext</c>
    /// cannot even express sentinel-vs-target — <c>SetPlatformContext</c> assigns BOTH fields the same value.
    /// Measured 2026-08-28; CT decided the same day not to build the missing concept. The target branch below is
    /// kept because it is the correct reading of the contract, not because a caller reaches it.
    /// </remarks>
    public static Guid? Resolve(ITenantContext tenantContext, out string? skipReason)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        skipReason = null;

        if (!tenantContext.IsResolved)
        {
            skipReason = SkipReason.TenantContextUnresolved;
            return null;
        }

        if (!tenantContext.IsPlatformContext)
        {
            if (tenantContext.TenantId == Guid.Empty)
            {
                skipReason = SkipReason.TenantContextUnresolved;
                return null;
            }

            return tenantContext.TenantId;
        }

        if (tenantContext.TargetTenantId is { } target && target != Guid.Empty)
        {
            return target;
        }

        skipReason = SkipReason.PlatformContextWithoutTargetTenant;
        return null;
    }
}
