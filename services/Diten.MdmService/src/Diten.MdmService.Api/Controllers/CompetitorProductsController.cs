using Diten.MdmService.Application.Features.CompetitorProducts;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// WP-VW-W4 (W4-BE-b2, §2.6) — competitor products (MOD-0290, a separate record from our Global Products). Read:
/// <c>mdm.competitor-products.read</c>; direct writes (create / update incl. prices / deactivate / merge two active records)
/// and the proposal decision: <c>.manage</c>; a rep's "not in the list" proposal: <c>.propose</c>. There is deliberately NO
/// DELETE: a record ends inactive, merged (→ survivor) or rejected. The decision on a proposal is approved through MOD-0023
/// (template MDM-COMP-PROPOSAL) — the MDM applies it when the approval outcome arrives.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/competitor-products")]
public sealed class CompetitorProductsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CompetitorProductsController(IMediator mediator) => _mediator = mediator;

    // ── reads ──

    [HttpGet]
    [HasPermission(CompetitorPermissions.Read)]
    public async Task<IActionResult> List([FromQuery] Guid? productId, [FromQuery] string? company, [FromQuery] string? ingredient,
        [FromQuery] string? market, [FromQuery] string? reimbursement, [FromQuery] string? status, [FromQuery] string? q,
        [FromQuery] string? includeProposed, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListCompetitorProductsQuery(productId, company, ingredient, market,
            reimbursement, status, q, includeProposed, page, pageSize), ct));

    /// <summary>The report's short list (active + the caller's own proposals; the market card).</summary>
    [HttpGet("lookup")]
    [HasPermission(CompetitorPermissions.Read)]
    public async Task<IActionResult> Lookup([FromQuery] Guid? productId, [FromQuery] Guid? indicationNodeId, [FromQuery] string? market,
        [FromQuery] string? q, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new LookupCompetitorProductsQuery(productId, indicationNodeId, market, q), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CompetitorPermissions.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCompetitorProductQuery(id), ct));

    [HttpGet("{id:guid}/usage")]
    [HasPermission(CompetitorPermissions.Read)]
    public async Task<IActionResult> Usage(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCompetitorUsageQuery(id), ct));

    [HttpGet("merge-candidates")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> MergeCandidates([FromQuery] Guid? sourceId, [FromQuery] string? q, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CompetitorMergeCandidatesQuery(sourceId, q), ct));

    // ── direct writes (no approval) ──

    [HttpPost]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CompetitorProductWriteRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreateCompetitorProductCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CompetitorProductWriteRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdateCompetitorProductCommand(id, request), ct));

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] CompetitorVersionRequest? request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DeactivateCompetitorProductCommand(id, request?.ExpectedVersion), ct));

    /// <summary>Two ACTIVE records, directly (<c>confirm: true</c> — irreversible).</summary>
    [HttpPost("{id:guid}/merge")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Merge(Guid id, [FromBody] CompetitorMergeRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new MergeCompetitorProductCommand(id, request), ct));

    // ── the rep proposal + the decision ──

    [HttpPost("proposals")]
    [HasPermission(CompetitorPermissions.Propose)]
    public async Task<IActionResult> Propose([FromBody] CompetitorProposalRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ProposeCompetitorProductCommand(request), ct));

    [HttpGet("proposals")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Proposals(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListCompetitorProposalsQuery(), ct));

    [HttpGet("proposals/{proposalId:guid}")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> Proposal(Guid proposalId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCompetitorProposalQuery(proposalId), ct));

    [HttpPut("proposals/{proposalId:guid}/decision")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> SaveDecision(Guid proposalId, [FromBody] CompetitorDecisionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SaveCompetitorDecisionCommand(proposalId, request), ct));

    [HttpPost("proposals/{proposalId:guid}/decision/submit")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> SubmitDecision(Guid proposalId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SubmitCompetitorDecisionCommand(proposalId), ct));

    [HttpPost("proposals/{proposalId:guid}/decision/withdraw")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> WithdrawDecision(Guid proposalId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawCompetitorDecisionCommand(proposalId), ct));

    /// <summary>The reviewer's decision on the SAME Work Center task: <c>approve</c> | <c>return</c> (a return needs a note).</summary>
    [HttpPost("proposals/{proposalId:guid}/decision/approve-reject")]
    [HasPermission(CompetitorPermissions.Manage)]
    public async Task<IActionResult> ReviewDecision(Guid proposalId, [FromBody] CompetitorReviewDecisionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ReviewCompetitorDecisionCommand(proposalId, request), ct));
}
