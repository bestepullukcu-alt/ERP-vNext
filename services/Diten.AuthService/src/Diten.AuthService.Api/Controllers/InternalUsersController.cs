using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

/// <summary>
/// Internal S2S endpoint that resolves DISPLAY NAMES ONLY for a supplied set of user ids.
///
/// <para>MOD-0024 (DEV-2, EA-approved 2026-07-25). The Task Center must show who a task is assigned to, but
/// Platform holds no user names: <c>AssigneeUserId</c> is an AuthService identity and MOD-0288's
/// <c>PersonReference</c> has no user id. No existing route could serve this — Platform's only AuthService
/// credential is the internal key, and the three controllers accepting it expose tenant activation, admin
/// invitation, permission sync and module lists; the only user-listing route is JWT-gated behind
/// <c>auth.users.read</c>, which this feature explicitly refuses to grant.</para>
///
/// <para>Deliberately minimal, and the boundaries are the point:</para>
/// <list type="bullet">
/// <item>READ ONLY — there is no write path here.</item>
/// <item>The response carries <b>id and display name and nothing else</b>. No email, phone, role, status or
/// last-login: this endpoint answers "what is this person called", not "tell me about this person".</item>
/// <item><b>Tenant scoping is enforced here, server-side</b>, from the caller-supplied tenant id: the lookup
/// runs against that tenant's users only, so a request can never resolve another tenant's names — asking for
/// a foreign id simply returns nothing for it.</item>
/// <item>No new permission key; the shared internal-key guard is the authorization, exactly as on
/// <c>internal/permissions/modules</c>.</item>
/// </list>
/// </summary>
[ApiController]
[Route("internal/users")]
public sealed class InternalUsersController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>Page size used to sweep a tenant's users; large enough that a realistic tenant needs one read.</summary>
    private const int SweepPageSize = 500;

    /// <summary>Bounds the sweep so a pathological tenant cannot turn one request into unbounded paging.</summary>
    private const int MaxSweepPages = 40;

    /// <summary>Caps the ids one call may ask about; the caller chunks beyond this.</summary>
    private const int MaxRequestedIds = 500;

    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<InternalUsersController> _logger;

    public InternalUsersController(
        IInternalEventAuthService internalEventAuthService,
        IUserRepository userRepository,
        ILogger<InternalUsersController> logger)
    {
        _internalEventAuthService = internalEventAuthService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// <c>GET internal/users/display-names?tenantId={guid}&amp;ids=guid,guid</c> →
    /// <c>[{ "id": "...", "displayName": "..." }]</c>.
    /// </summary>
    [HttpGet("display-names")]
    public async Task<IActionResult> GetDisplayNames(
        [FromQuery] Guid tenantId,
        [FromQuery] string? ids,
        CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (tenantId == Guid.Empty)
        {
            return BadRequest(new { message = "tenantId is required" });
        }

        var requested = ParseIds(ids);
        if (requested.Count == 0)
        {
            // An explicit id set is required: this endpoint resolves names the caller already knows about,
            // it is not a directory dump.
            return BadRequest(new { message = "ids is required" });
        }

        if (requested.Count > MaxRequestedIds)
        {
            return BadRequest(new { message = $"ids exceeds the maximum of {MaxRequestedIds}" });
        }

        var resolved = new List<InternalUserDisplayNameDto>(requested.Count);

        // Sweep the TENANT's users and keep only the requested ids. Scoping by tenant first is what makes
        // cross-tenant resolution impossible: a foreign id is never in this set.
        for (var page = 1; page <= MaxSweepPages && resolved.Count < requested.Count; page++)
        {
            var batch = (await _userRepository.GetAllByTenantAsync(tenantId, page, SweepPageSize, ct)).ToList();
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var user in batch.Where(u => requested.Contains(u.Id)))
            {
                resolved.Add(new InternalUserDisplayNameDto(user.Id, BuildDisplayName(user.FirstName, user.LastName, user.UserName)));
            }

            if (batch.Count < SweepPageSize)
            {
                break;
            }
        }

        _logger.LogDebug(
            "Resolved {ResolvedCount}/{RequestedCount} display names for TenantId={TenantId}.",
            resolved.Count, requested.Count, tenantId);

        return Ok(resolved);
    }

    private static HashSet<Guid> ParseIds(string? ids)
    {
        var parsed = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(ids))
        {
            return parsed;
        }

        foreach (var candidate in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(candidate, out var id) && id != Guid.Empty)
            {
                parsed.Add(id);
            }
        }

        return parsed;
    }

    /// <summary>Full name when known; the username is the last resort so a caller never has to show a raw id.</summary>
    private static string BuildDisplayName(string firstName, string lastName, string userName)
    {
        var full = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(full) ? userName : full;
    }
}

/// <summary>Id and display name — the entire contract. Adding a field here widens what S2S callers can read.</summary>
public sealed record InternalUserDisplayNameDto(Guid Id, string DisplayName);
