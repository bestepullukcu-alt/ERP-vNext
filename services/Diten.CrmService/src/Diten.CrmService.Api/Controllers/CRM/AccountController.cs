using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

[Authorize]
[Route("api/crm/accounts")]
public sealed class AccountController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AccountController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("crm.account.read")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountListQuery(search, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("crm.account.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountByIdQuery(id), cancellationToken));

    [HttpGet("{id:guid}/overview")]
    [HasPermission("crm.account.overview.read")]
    public async Task<IActionResult> Overview(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountOverviewQuery(id), cancellationToken));

    [HttpGet("{id:guid}/hierarchy")]
    [HasPermission("crm.account.read")]
    public async Task<IActionResult> Hierarchy(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountHierarchyQuery(id), cancellationToken));

    [HttpGet("{id:guid}/territory-assignments")]
    [HasPermission("crm.territory.model.read")]
    public async Task<IActionResult> TerritoryAssignments(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountTerritoryAssignmentHistoryQuery(id), cancellationToken));

    [HttpGet("{id:guid}/territory-coverage-summary")]
    [HasPermission("crm.territory.model.read")]
    public async Task<IActionResult> TerritoryCoverageSummary(
        Guid id, [FromQuery] DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryCoverageSummaryQuery(id, effectiveAt), cancellationToken));

    [HttpPost]
    [HasPermission("crm.account.create")]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission("crm.account.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission("crm.account.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeleteAccountCommand(id), cancellationToken));

    [HttpDelete("bulk")]
    [HasPermission("crm.account.delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteAccountsRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new BulkDeleteAccountCommand(request.Ids), cancellationToken));

    [HttpPost("{id:guid}/parent")]
    [HasPermission("crm.account.hierarchy.manage")]
    public async Task<IActionResult> LinkParent(Guid id, [FromBody] LinkParentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new LinkParentAccountCommand(id, request.ParentAccountId), cancellationToken));

    [HttpDelete("{id:guid}/parent")]
    [HasPermission("crm.account.hierarchy.manage")]
    public async Task<IActionResult> UnlinkParent(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UnlinkParentAccountCommand(id), cancellationToken));

    [HttpPut("{id:guid}/attributes")]
    [HasPermission("crm.account.attribute.manage")]
    public async Task<IActionResult> UpsertAttribute(Guid id, [FromBody] UpsertAttributeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpsertAccountAttributeCommand(id, request.AttributeCode, request.Value), cancellationToken));

    // NOTE: /import and /export are declared in the MOD-0149 pack but intentionally NOT implemented in this
    // foundation slice (no fake/no-shell). They are tracked as follow-ups.
}
