using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// BL-409 — THE ONE PLACE a token's <c>actor_type</c> becomes an <see cref="AuditActorType"/>.
///
/// <para><b>⚠ ONE VOCABULARY, TWO POLICIES.</b> AuthService mints exactly three actor types
/// (TokenService: <c>tenant_user</c>, <c>platform_admin</c>, <c>partner_admin</c>). What differs between the
/// callers is only what "no authenticated principal" means: for an audited command it is a background job, an
/// event consumer or a startup task — the system itself (<see cref="ForCommand"/>); for a data export it means
/// nobody can be named and the export is refused (<see cref="ForDataExport"/>). The claim mapping itself is
/// <see cref="FromActorTypeClaim"/> and exists nowhere else — <c>AuditActorTypeResolverSourceGuardTests</c>
/// fails when a second copy of the switch appears in Platform production code.</para>
///
/// <para><b>⚠ A PRINCIPAL IS NEVER GUESSED.</b> An authenticated principal whose token carries no recognised
/// actor type resolves to <see cref="AuditActorType.Unknown"/> — never System (that would say the machine did
/// what a person did) and never TenantUser (that would name a kind of person the token does not). Note that
/// <c>AuditService</c> refuses to append an Unknown record, so what that refusal means is the caller's policy.</para>
/// </summary>
public static class AuditActorTypeResolver
{
    /// <summary>
    /// The token's <c>actor_type</c> claim value → the audit actor type. Case and surrounding whitespace are
    /// ignored; a missing, empty or unrecognised value is <see cref="AuditActorType.Unknown"/>.
    /// </summary>
    public static AuditActorType FromActorTypeClaim(string? actorTypeClaim)
    {
        return actorTypeClaim?.Trim().ToLowerInvariant() switch
        {
            "tenant_user" => AuditActorType.TenantUser,
            "platform_admin" => AuditActorType.PlatformAdministrator,
            "partner_admin" => AuditActorType.PartnerAdministrator,
            _ => AuditActorType.Unknown
        };
    }

    /// <summary>
    /// The actor of an audited command (<c>AuditBehavior</c>). No authenticated principal — no HTTP request at all
    /// (Hangfire job, event consumer, startup) or an anonymous internal call — is <see cref="AuditActorType.System"/>;
    /// an authenticated principal is whatever its token says, <see cref="AuditActorType.Unknown"/> included.
    /// </summary>
    public static AuditActorType ForCommand(ITenantAuthorizationContext principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.IsAuthenticated
            ? FromActorTypeClaim(principal.ActorType)
            : AuditActorType.System;
    }

    /// <summary>
    /// The actor of a data export (<c>DataExportAuditWriter</c>). An export is always a person taking data out,
    /// so no authenticated principal is <see cref="AuditActorType.Unknown"/> — and the writer refuses it.
    /// </summary>
    public static AuditActorType ForDataExport(ITenantAuthorizationContext principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.IsAuthenticated
            ? FromActorTypeClaim(principal.ActorType)
            : AuditActorType.Unknown;
    }
}
