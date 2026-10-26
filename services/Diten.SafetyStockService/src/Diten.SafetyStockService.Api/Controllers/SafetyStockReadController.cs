using Diten.SafetyStockService.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.SafetyStockService.Api.Controllers;

[Authorize]
[Route("api/v1/safety-stock/policies")]
public sealed class SafetyStockReadController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SafetyStockReadController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "safety-stock.policies.read")]
    public Task<IActionResult> List(CancellationToken cancellationToken)
        => ReadAsync(PolicyReadKind.List, null, null, cancellationToken);

    [HttpGet("{policyId:guid}")]
    [Authorize(Policy = "safety-stock.policies.read")]
    public Task<IActionResult> Policy(Guid policyId, CancellationToken cancellationToken)
        => ReadAsync(PolicyReadKind.Policy, policyId, null, cancellationToken);

    [HttpGet("{policyId:guid}/revisions/{revisionId:guid}")]
    [Authorize(Policy = "safety-stock.policies.read")]
    public Task<IActionResult> Revision(Guid policyId, Guid revisionId, CancellationToken cancellationToken)
        => ReadAsync(PolicyReadKind.Revision, policyId, revisionId, cancellationToken);

    [HttpGet("{policyId:guid}/evaluations/{evaluationId:guid}")]
    [Authorize(Policy = "safety-stock.evaluations.read")]
    public Task<IActionResult> Evaluation(Guid policyId, Guid evaluationId, CancellationToken cancellationToken)
        => ReadAsync(PolicyReadKind.Evaluation, policyId, evaluationId, cancellationToken);

    private async Task<IActionResult> ReadAsync(PolicyReadKind kind, Guid? policyId, Guid? childId, CancellationToken cancellationToken)
    {
        var tenant = SingleGuidClaim("tenant_id");
        var actor = SingleGuidClaim("sub");
        var companyHeader = Request.Headers["X-Legal-Entity-Id"];
        if (tenant is null || actor is null || companyHeader.Count != 1
            || !Guid.TryParse(companyHeader[0], out var company) || company == Guid.Empty)
            return StatusCode(StatusCodes.Status403Forbidden);

        var result = await _mediator.Send(new PolicyReadQuery(kind, tenant.Value, actor.Value, company, policyId, childId), cancellationToken);
        return ReadResponse(result);
    }

    private Guid? SingleGuidClaim(string type)
    {
        var matches = User.Claims.Where(claim => claim.Type == type).ToArray();
        return matches.Length == 1 && Guid.TryParse(matches[0].Value, out var value) && value != Guid.Empty
            ? value : null;
    }
}
