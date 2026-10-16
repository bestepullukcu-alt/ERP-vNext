using System.Security.Claims;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.API.Security;

/// <summary>
/// BL-521 — the request's <see cref="IPlatformActorTenantScope"/>: the token says WHO (actor type and e-mail), the
/// administrator record says WHAT (whether it is a partner, and its allowed tenants). Scoped, so the record is read at
/// most once per request — by the two platform policies, the permission filter, the partner scope filter and the
/// handlers alike.
///
/// <para>Rules, each fail-closed:</para>
/// <list type="bullet">
/// <item>no principal, or a token that is neither <c>platform_admin</c> nor <c>partner_admin</c> → none.</item>
/// <item>no e-mail, no record, a deleted or inactive record → none — for a <c>platform_admin</c> token too (FIX1 F7).</item>
/// <item>a record that cannot be READ → <see cref="PlatformAdministratorRecordUnavailableException"/> (503), never none and
/// never all (FIX2 item 3).</item>
/// <item><c>platform_admin</c> token and a live record that is not a partner record → unrestricted.</item>
/// <item>otherwise — a <c>partner_admin</c> token, or a record that says PartnerAdmin whatever the token says (FIX1 F1)
/// → the record's <c>AllowedTenantIds</c> (never the platform system tenant).</item>
/// </list>
/// </summary>
public sealed class PlatformActorTenantScope : IPlatformActorTenantScope
{
    public const string PlatformAdminActorType = "platform_admin";
    public const string PartnerAdminActorType = "partner_admin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlatformAdministratorRepository _administrators;
    private Task<Resolution>? _resolution;

    public PlatformActorTenantScope(IHttpContextAccessor httpContextAccessor, IPlatformAdministratorRepository administrators)
    {
        _httpContextAccessor = httpContextAccessor;
        _administrators = administrators;
    }

    public async Task<PlatformTenantScope> GetAsync(CancellationToken ct = default) => (await ResolveOnceAsync(ct)).Scope;

    /// <summary>
    /// The administrator record this request's scope was decided from (null: no principal, no e-mail, no record; a record
    /// that could not be read throws, as <see cref="GetAsync"/> does). The permission filter uses it for the live-record
    /// check and the first-login stamp instead of reading the record a second time.
    /// </summary>
    public async Task<PlatformAdministrator?> GetRecordAsync(CancellationToken ct = default) => (await ResolveOnceAsync(ct)).Record;

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

    private Task<Resolution> ResolveOnceAsync(CancellationToken ct) => _resolution ??= ResolveAsync(ct);

    private async Task<Resolution> ResolveAsync(CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new Resolution(PlatformTenantScope.None, null);
        }

        var platformAdmin = IsPlatformAdmin(user);
        if (!platformAdmin && !IsPartnerAdmin(user))
        {
            return new Resolution(PlatformTenantScope.None, null);
        }

        var email = NormalizedEmailOf(user);
        var record = email is null ? null : await ReadRecordAsync(email, ct);

        // F7 — only a live record names anything, whatever the token says.
        if (record is null || record.IsDeleted || record.Status != AdministratorStatus.Active)
        {
            return new Resolution(PlatformTenantScope.None, record);
        }

        // F1 — the record decides whether this is a partner; a platform_admin token does not outrank it.
        if (platformAdmin && record.ActorType != ActorType.PartnerAdmin)
        {
            return new Resolution(PlatformTenantScope.Unrestricted, record);
        }

        return new Resolution(PlatformTenantScope.Only(record.AllowedTenantIds ?? []), record);
    }

    private async Task<PlatformAdministrator?> ReadRecordAsync(string normalizedEmail, CancellationToken ct)
    {
        try
        {
            return await _administrators.GetByNormalizedEmailAsync(normalizedEmail, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // FIX2 item 3 — unreadable is NO ANSWER: 503, not "none". Before, this returned null, so an outage (or a record
            // that no longer deserializes) answered a partner with a silent 403 / empty list and filed a false
            // PartnerTenantScopeViolation security event for every tenant it opened.
            throw new PlatformAdministratorRecordUnavailableException(ex);
        }
    }

    private sealed record Resolution(PlatformTenantScope Scope, PlatformAdministrator? Record);
}

/// <summary>
/// BL-521 — THE registration of the request's platform-actor scope, as production makes it (Program.cs calls this). Test
/// hosts call it too, so a registration that goes missing here is a red test, not a host that silently registers its own
/// copy (FIX1 F8-7). Both the interface and the concrete type resolve to the SAME scoped instance: the permission filter
/// asks the concrete type for the record it already read.
/// </summary>
public static class PlatformActorTenantScopeRegistration
{
    public static IServiceCollection AddPlatformActorTenantScope(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<PlatformActorTenantScope>();
        services.AddScoped<IPlatformActorTenantScope>(sp => sp.GetRequiredService<PlatformActorTenantScope>());
        return services;
    }
}
