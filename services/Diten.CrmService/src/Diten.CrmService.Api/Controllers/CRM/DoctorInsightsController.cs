using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.DoctorInsights.DoctorInsightsPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2) — <c>api/crm/doctor-insights</c>:
/// <list type="bullet">
/// <item><c>doctors/{contactId}/profile</c> GET (read | read-all; visibility §2.4, 404 outside) · PUT (manage; the
/// version from <c>If-Match</c> or the body's <c>expectedVersion</c>);</item>
/// <item><c>doctors/{contactId}/potentials?productIds=a,b</c> (read | read-all);</item>
/// <item><c>products/{productId}/indication-profiles</c> (read | read-all | thresholds.manage);</item>
/// <item><c>thresholds</c> GET + <c>thresholds/history</c> (read | read-all | thresholds.manage) · <c>PUT thresholds/{productId|default}</c>
/// (thresholds.manage);</item>
/// <item><c>opportunities?scope=mine|team&amp;productId=</c> (read | read-all);</item>
/// <item><c>aggregates?productId=</c> (read-aggregate | read-all; no doctor identity).</item>
/// </list>
/// TenantId is server-resolved; nothing tenant-shaped is read from the route, query or body.
/// </summary>
[Authorize]
public sealed class DoctorInsightsController : CustomBaseController
{
    private const string Base = "api/crm/doctor-insights";

    private readonly IMediator _mediator;

    public DoctorInsightsController(IMediator mediator) => _mediator = mediator;

    public static bool MayRead(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaims.HasPermission(user, Perms.Read) || PermissionClaims.HasPermission(user, Perms.ReadAll);

    public static bool MayReadThresholds(System.Security.Claims.ClaimsPrincipal user)
        => MayRead(user) || PermissionClaims.HasPermission(user, Perms.ThresholdsManage);

    public static bool MayReadAggregate(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaims.HasPermission(user, Perms.ReadAggregate) || PermissionClaims.HasPermission(user, Perms.ReadAll);

    [HttpGet(Base + "/doctors/{contactId:guid}/profile")]
    public async Task<IActionResult> GetProfile(Guid contactId, CancellationToken ct)
        => MayRead(User) ? CreateActionResultInstance(await _mediator.Send(new GetDoctorProfileQuery(contactId), ct)) : Forbidden(Perms.Read);

    [HttpPut(Base + "/doctors/{contactId:guid}/profile")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> PutProfile(Guid contactId, [FromBody] DoctorProfileInput input, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateDoctorProfileCommand(contactId, input with { ExpectedVersion = IfMatch(Request.Headers.IfMatch.ToString()) ?? input.ExpectedVersion }), ct));

    [HttpGet(Base + "/doctors/{contactId:guid}/potentials")]
    public async Task<IActionResult> GetPotentials(Guid contactId, [FromQuery] string? productIds, CancellationToken ct)
    {
        if (!MayRead(User)) return Forbidden(Perms.Read);
        var ids = new List<Guid>();
        foreach (var part in (productIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id) || id == Guid.Empty)
            {
                return CreateActionResultInstance(DoctorInsightsResponses.Fail<object>(DoctorInsightsErrors.Invalid, "productIds is a comma-separated list of Global Product ids.", 400));
            }

            ids.Add(id);
        }

        return CreateActionResultInstance(await _mediator.Send(new GetDoctorPotentialsQuery(contactId, ids), ct));
    }

    [HttpGet(Base + "/products/{productId:guid}/indication-profiles")]
    public async Task<IActionResult> GetIndicationProfiles(Guid productId, CancellationToken ct)
        => MayReadThresholds(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetProductIndicationProfilesQuery(productId), ct))
            : Forbidden(Perms.Read);

    [HttpGet(Base + "/thresholds")]
    public async Task<IActionResult> GetThresholds(CancellationToken ct)
        => MayReadThresholds(User) ? CreateActionResultInstance(await _mediator.Send(new GetPotentialThresholdsQuery(), ct)) : Forbidden(Perms.Read);

    [HttpGet(Base + "/thresholds/history")]
    public async Task<IActionResult> GetThresholdHistory([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => MayReadThresholds(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetPotentialThresholdHistoryQuery(page, pageSize), ct))
            : Forbidden(Perms.Read);

    /// <summary><paramref name="target"/> = a Global Product id, or <c>default</c> (the tenant default).</summary>
    [HttpPut(Base + "/thresholds/{target}")]
    [HasPermission(Perms.ThresholdsManage)]
    public async Task<IActionResult> PutThreshold(string target, [FromBody] PotentialThresholdInput input, CancellationToken ct)
    {
        Guid? productId = null;
        if (!string.Equals(target, "default", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(target, out var id) || id == Guid.Empty)
            {
                return CreateActionResultInstance(DoctorInsightsResponses.Fail<object>(DoctorInsightsErrors.Invalid, "The target is a Global Product id or 'default'.", 400));
            }

            productId = id;
        }

        return CreateActionResultInstance(await _mediator.Send(
            new UpdatePotentialThresholdCommand(productId, input with { ExpectedVersion = IfMatch(Request.Headers.IfMatch.ToString()) ?? input.ExpectedVersion }), ct));
    }

    [HttpGet(Base + "/opportunities")]
    public async Task<IActionResult> GetOpportunities([FromQuery] string? scope, [FromQuery] Guid? productId, CancellationToken ct)
        => MayRead(User) ? CreateActionResultInstance(await _mediator.Send(new ListDoctorOpportunitiesQuery(scope, productId), ct)) : Forbidden(Perms.Read);

    [HttpGet(Base + "/aggregates")]
    public async Task<IActionResult> GetAggregates([FromQuery] Guid? productId, CancellationToken ct)
        => MayReadAggregate(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetDoctorInsightAggregatesQuery(productId), ct))
            : Forbidden(Perms.ReadAggregate);

    /// <summary><c>If-Match: "3"</c> / <c>W/"3"</c> / <c>3</c> → 3; absent or unreadable → null.</summary>
    public static int? IfMatch(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        var value = header.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        value = value.Trim('"', ' ');
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) && version >= 0 ? version : null;
    }

    private IActionResult Forbidden(string key)
        => CreateActionResultInstance(Response<object>.Fail(new[] { $"This needs {key}.", "permission_denied" }, 403));
}
