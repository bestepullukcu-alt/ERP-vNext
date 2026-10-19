using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.ObjectionResponses.ObjectionResponsePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-BE-b, §2.3) — the objection library: list / get (<c>crm.objection-response.read</c>), create / update a
/// draft / retire (<c>…manage</c>). An approved answer never changes. TenantId is server-resolved. The rep never reads
/// this surface: approved answers reach the rep through the visit presentation read.
/// <para>WP-VW-W3 (W3-BE-c, §2.1) — approval through MOD-0023 + the Work Center: submit / withdraw (<c>…submit</c>),
/// decide (<c>…read</c>; the workflow task decides who may — the KP-5a rule), copy (<c>…manage</c>), usages; the list
/// pages and searches. The old direct approve answers 409 <c>approval_via_workflow_only</c>.</para>
/// </summary>
[Authorize]
public sealed class ObjectionResponsesController : CustomBaseController
{
    private const string Base = "api/crm/objection-responses";

    private readonly IMediator _mediator;

    public ObjectionResponsesController(IMediator mediator) => _mediator = mediator;

    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? productId, [FromQuery] string? categoryCode, [FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListObjectionResponsesQuery(productId, categoryCode, status, q, page, pageSize), cancellationToken));

    [HttpGet(Base + "/{id:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetObjectionResponseQuery(id), cancellationToken));

    [HttpGet(Base + "/{id:guid}/usages")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Usages(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetObjectionResponseUsagesQuery(id), cancellationToken));

    [HttpPost(Base)]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create([FromBody] ObjectionResponseRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateObjectionResponseCommand(request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds),
            cancellationToken));

    [HttpPut(Base + "/{id:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ObjectionResponseRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateObjectionResponseCommand(
                id, request.ProductId, request.CategoryCode, request.Question, request.Answer, request.ClaimIds, request.ExpectedVersion),
            cancellationToken));

    /// <summary>Retired (W3-BE-c): always 409 <c>approval_via_workflow_only</c>.</summary>
    [HttpPost(Base + "/{id:guid}/approve")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ObjectionResponseVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApproveObjectionResponseCommand(id, request?.ExpectedVersion), cancellationToken));

    [HttpPost(Base + "/{id:guid}/submit")]
    [HasPermission(Perms.Submit)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitObjectionResponseCommand(id), cancellationToken));

    [HttpPost(Base + "/{id:guid}/withdraw")]
    [HasPermission(Perms.Submit)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawObjectionResponseCommand(id), cancellationToken));

    [HttpPost(Base + "/{id:guid}/decision")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DecideObjectionResponseCommand(id, request.Decision, request.Comment), cancellationToken));

    [HttpPost(Base + "/{id:guid}/copy")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Copy(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CopyObjectionResponseCommand(id), cancellationToken));

    [HttpPost(Base + "/{id:guid}/retire")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Retire(Guid id, [FromBody] ObjectionResponseVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RetireObjectionResponseCommand(id, request?.ExpectedVersion), cancellationToken));
}

/// <summary>WP-VW-W3 (W3-BE-b) — create / update body of an objection answer (W3-BE-c: question / answer / category may
/// be empty on a draft).</summary>
public sealed record ObjectionResponseRequest(
    Guid ProductId,
    string? CategoryCode,
    string? Question,
    string? Answer,
    IReadOnlyList<Guid>? ClaimIds = null,
    int? ExpectedVersion = null);

public sealed record ObjectionResponseVersionRequest(int? ExpectedVersion = null);

/// <summary>WP-VW-W3 (W3-BE-c) — a reviewer's decision: <c>approve</c> | <c>return</c> (a return needs a comment).</summary>
public sealed record ApprovalDecisionRequest(string? Decision, string? Comment = null);
