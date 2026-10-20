using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.OrganizationSeats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// WP-VW-W3L PLT-ORG-SEAT (K-L8) — a user's current PRIMARY seat (position → organization unit → legal entity), one user or
/// up to 100 at once; WP-VW-W6 PLT-W6 — the user's line managers (<c>{userId}/line-managers</c>). Tenant-scoped (the Gateway's tenant org paths). No <c>[HasPermission]</c> on purpose: a user may read
/// their OWN seat with no key; reading anybody else's needs <see cref="OrganizationSeatPermissions.Read"/>. Whether the
/// caller holds it is decided HERE from the claims (the same evaluator as <c>[HasPermission]</c>) and handed to the reader
/// as data; the reader refuses before it reads anything.
/// </summary>
[Route("api/platform/organization/seats")]
[Authorize]
public sealed class OrganizationSeatsController : CustomBaseController
{
    private readonly IMediator _mediator;

    // BL-410 — login-only by decision, not by omission: the OWN seat needs no key; anybody else's needs
    // platform.organization-seats.read, decided from the claims here and enforced by the reader before it reads.
    private const string ReadReason =
        "A user reads their own current primary seat with no key; another user's seat needs platform.organization-seats.read, which the reader enforces (403 before any read).";

    public OrganizationSeatsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{userId:guid}")]
    [LoginOnly(ReadReason)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationSeatQuery(userId, MayReadOthers()), ct));

    [HttpPost("lookup")]
    [LoginOnly(ReadReason)]
    public async Task<IActionResult> Lookup([FromBody] OrganizationSeatLookupRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new LookupOrganizationSeatsQuery(request?.UserIds, MayReadOthers()), ct));

    // WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-2) — the seat's sibling: whom the user reports to (ids, names, the seat they hold; no
    // e-mail). The same authority rule as the seat read, enforced by the reader before it reads.
    private const string LineManagersReason =
        "A user reads their own line managers with no key; another user's need platform.organization-seats.read, which the reader enforces (403 before any read).";

    [HttpGet("{userId:guid}/line-managers")]
    [LoginOnly(LineManagersReason)]
    public async Task<IActionResult> LineManagers(Guid userId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationLineManagersQuery(userId, MayReadOthers()), ct));

    /// <summary>Public + static so a test measures it on a principal: the caller holds the "read others" key.</summary>
    public static bool HoldsReadOthers(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaimEvaluator.Evaluate(user.Claims, OrganizationSeatPermissions.Read).IsSatisfied;

    private bool MayReadOthers() => HoldsReadOthers(User);
}

/// <summary>K-L8 — <c>{ userIds: [ … ] }</c> (at most 100).</summary>
public sealed record OrganizationSeatLookupRequest(IReadOnlyList<Guid>? UserIds);
