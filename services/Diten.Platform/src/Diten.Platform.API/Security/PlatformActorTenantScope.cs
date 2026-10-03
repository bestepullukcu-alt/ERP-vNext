using System.Security.Claims;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-521 — the request's <see cref="IPlatformActorTenantScope"/>: the token says WHO (actor type and e-mail), the
/// administrator record says WHAT (allowed tenants). Scoped, so the record is read at most once per request.
///
/// <para>Rules, each fail-closed:</para>
/// <list type="bullet">
/// <item><c>platform_admin</c> → unrestricted, unless its own record says it is a partner administrator (then the
/// record's list applies).</item>
/// <item><c>partner_admin</c> → the record's <c>AllowedTenantIds</c>; no record, a deleted or inactive one, or an empty
/// list → none.</item>
/// <item>anything else (no principal, a tenant user) → none.</item>
/// </list>
/// </summary>
public sealed class PlatformActorTenantScope : IPlatformActorTenantScope
{
    public const string PlatformAdminActorType = "platform_admin";
    public const string PartnerAdminActorType = "partner_admin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlatformAdministratorRepository _administrators;
    private Task<PlatformTenantScope>? _scope;

    public PlatformActorTenantScope(IHttpContextAccessor httpContextAccessor, IPlatformAdministratorRepository administrators)
    {
        _httpContextAccessor = httpContextAccessor;
        _administrators = administrators;
    }

    public Task<PlatformTenantScope> GetAsync(CancellationToken ct = default) => _scope ??= ResolveAsync(ct);

    /// <summary>The token's actor type is a partner administrator.</summary>
    public static bool IsPartnerAdmin(ClaimsPrincipal? user) =>
        string.Equals(user?.FindFirst("actor_type")?.Value, PartnerAdminActorType, StringComparison.OrdinalIgnoreCase);

    /// <summary>The token's actor type is a platform administrator.</summary>
    public static bool IsPlatformAdmin(ClaimsPrincipal? user) =>
        string.Equals(user?.FindFirst("actor_type")?.Value, PlatformAdminActorType, StringComparison.OrdinalIgnoreCase);

    /// <summary>The e-mail the administrator record is looked up by, as HasPermissionAttribute reads it.</summary>
    public static string? NormalizedEmailOf(ClaimsPrincipal user)
    {
        var email = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }

    private async Task<PlatformTenantScope> ResolveAsync(CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return PlatformTenantScope.None;
        }

        var platformAdmin = IsPlatformAdmin(user);
        var partnerAdmin = IsPartnerAdmin(user);
        if (!platformAdmin && !partnerAdmin)
        {
            return PlatformTenantScope.None;
        }

        var email = NormalizedEmailOf(user);
        var record = email is null ? null : await ReadRecordAsync(email, ct);

        if (platformAdmin && record?.ActorType != ActorType.PartnerAdmin)
        {
            return PlatformTenantScope.Unrestricted;
        }

        // A partner — by its token or by its own record. Only an active, live record names tenants.
        if (record is null || record.IsDeleted || record.Status != AdministratorStatus.Active)
        {
            return PlatformTenantScope.None;
        }

        return PlatformTenantScope.Only(record.AllowedTenantIds ?? []);
    }

    private async Task<Domain.Entities.PlatformAdministrator?> ReadRecordAsync(string normalizedEmail, CancellationToken ct)
    {
        try
        {
            return await _administrators.GetByNormalizedEmailAsync(normalizedEmail, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null; // unreadable → a partner sees nothing (fail closed); a platform admin is unrestricted by its token
        }
    }
}
