using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidenceFlags;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitEvidenceFlags.VisitEvidenceFlagsPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (BE-b, §2.6, K-W3E-11) — <c>api/crm/visit-evidence-flags</c>, READ ONLY:
/// <list type="bullet">
/// <item><c>GET ?from=&amp;to=&amp;resourceId=&amp;flags=a,b&amp;accountId=&amp;onlyFlagged=&amp;page=&amp;pageSize</c> — the starts, newest first;</item>
/// <item><c>GET summary?from=&amp;to=&amp;resourceId=</c> — started / flagged / rate / per flag / per rep;</item>
/// <item><c>GET {executionId}</c> — one start (+ its exception).</item>
/// </list>
/// <c>crm.visit-evidence-flags.read</c> = the caller's team (+ the caller), <c>.read-all</c> = the tenant; outside ⇒ 404.
/// Days are UTC days (<c>yyyy-MM-dd</c>), at most 92 both included. No photo content is served here.
/// </summary>
[Authorize]
public sealed class VisitEvidenceFlagsController : CustomBaseController
{
    private const string Base = "api/crm/visit-evidence-flags";

    private readonly IMediator _mediator;

    public VisitEvidenceFlagsController(IMediator mediator) => _mediator = mediator;

    public static bool MayRead(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaims.HasPermission(user, Perms.Read) || PermissionClaims.HasPermission(user, Perms.ReadAll);

    [HttpGet(Base)]
    public async Task<IActionResult> List(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId, [FromQuery] string? flags,
        [FromQuery] Guid? accountId, [FromQuery] bool? onlyFlagged, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        if (!MayRead(User)) return Forbidden();
        if (!TryDay(from, out var f) || !TryDay(to, out var t)) return BadDay();
        var codes = string.IsNullOrWhiteSpace(flags) ? null : flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return CreateActionResultInstance(await _mediator.Send(
            new GetVisitEvidenceFlagsQuery(f, t, resourceId, codes, accountId, onlyFlagged == true, page, pageSize), ct));
    }

    [HttpGet(Base + "/summary")]
    public async Task<IActionResult> Summary([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? resourceId, CancellationToken ct)
    {
        if (!MayRead(User)) return Forbidden();
        if (!TryDay(from, out var f) || !TryDay(to, out var t)) return BadDay();
        return CreateActionResultInstance(await _mediator.Send(new GetVisitEvidenceFlagsSummaryQuery(f, t, resourceId), ct));
    }

    [HttpGet(Base + "/{executionId:guid}")]
    public async Task<IActionResult> Detail(Guid executionId, CancellationToken ct)
        => MayRead(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitEvidenceFlagDetailQuery(executionId), ct))
            : Forbidden();

    private static bool TryDay(string? raw, out DateOnly? day)
    {
        day = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) { day = d; return true; }
        return false;
    }

    private IActionResult BadDay()
        => CreateActionResultInstance(Response<object>.Fail(new[] { "from / to are yyyy-MM-dd.", VisitEvidenceFlagsErrors.RangeInvalid }, 400));

    private IActionResult Forbidden()
        => CreateActionResultInstance(Response<object>.Fail(new[] { $"This needs {Perms.Read} or {Perms.ReadAll}.", "permission_denied" }, 403));
}
