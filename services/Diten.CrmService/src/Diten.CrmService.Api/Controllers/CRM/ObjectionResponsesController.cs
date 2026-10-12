using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.ObjectionResponses.ObjectionResponsePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — the objection library: list / get (<c>crm.objection-response.read</c>), create / update a
/// draft / retire (<c>…manage</c>), approve (<c>…approve</c>, never the author — SoD). An approved answer never changes.
/// TenantId is server-resolved. The rep never reads this surface: approved answers reach the rep through the visit
/// presentation read.
/// </summary>
[Authorize]
public sealed class ObjectionResponsesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ObjectionResponsesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/objection-responses")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? productId, [FromQuery] string? categoryCode, [FromQuery] string? status,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListObjectionResponsesQuery(productId, categoryCode, status), cancellationToken));

    [HttpGet("api/crm/objection-responses/{id:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetObjectionResponseQuery(id), cancellationToken));

    [HttpPost("api/crm/objection-responses")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create([FromBody] ObjectionResponseRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateObjectionResponseCommand(request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds),
            cancellationToken));

    [HttpPut("api/crm/objection-responses/{id:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ObjectionResponseRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateObjectionResponseCommand(
                id, request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/objection-responses/{id:guid}/approve")]
    [HasPermission(Perms.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ObjectionResponseVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApproveObjectionResponseCommand(id, request?.ExpectedVersion), cancellationToken));

    [HttpPost("api/crm/objection-responses/{id:guid}/retire")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Retire(Guid id, [FromBody] ObjectionResponseVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RetireObjectionResponseCommand(id, request?.ExpectedVersion), cancellationToken));
}

/// <summary>WP-VW-W3 (W3-BE-b) — create / update body of an objection answer.</summary>
public sealed record ObjectionResponseRequest(
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds = null,
    int? ExpectedVersion = null);

public sealed record ObjectionResponseVersionRequest(int? ExpectedVersion = null);
