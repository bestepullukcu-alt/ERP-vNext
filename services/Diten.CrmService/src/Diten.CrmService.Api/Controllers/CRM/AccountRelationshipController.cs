using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.AccountRelationship.Commands;
using Diten.CrmService.Application.Features.AccountRelationship.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 FU04 — Account↔Account relationship management + the Account 360 "Related Accounts" projection. Routes sit
/// under the existing Gateway wildcard (`/api/crm/accounts/{everything}`) — no new Gateway route. Read =
/// crm.account-relationship.read, mutate = crm.account-relationship.manage.
/// </summary>
[Authorize]
public sealed class AccountRelationshipController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AccountRelationshipController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/accounts/{accountId:guid}/relationships")]
    [HasPermission("crm.account-relationship.read")]
    public async Task<IActionResult> ListForAccount(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListRelationshipsForAccountQuery(accountId), cancellationToken));

    /// <summary>Account 360 "Related Accounts" projection alias (same read model, inverse display included).</summary>
    [HttpGet("api/crm/accounts/{accountId:guid}/related-accounts")]
    [HasPermission("crm.account-relationship.read")]
    public async Task<IActionResult> RelatedAccounts(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListRelationshipsForAccountQuery(accountId), cancellationToken));

    [HttpGet("api/crm/accounts/{accountId:guid}/relationships/{relationshipId:guid}")]
    [HasPermission("crm.account-relationship.read")]
    public async Task<IActionResult> GetRelationship(Guid accountId, Guid relationshipId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountRelationshipByIdQuery(accountId, relationshipId), cancellationToken));

    [HttpPost("api/crm/accounts/{accountId:guid}/relationships")]
    [HasPermission("crm.account-relationship.manage")]
    public async Task<IActionResult> Create(Guid accountId, [FromBody] CreateAccountRelationshipRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateAccountRelationshipCommand(accountId, request.TargetAccountId, request.RelationshipType, request.Status, request.ValidFrom, request.ValidTo, request.Notes, request.CrossCountryReason),
            cancellationToken));

    [HttpPut("api/crm/accounts/{accountId:guid}/relationships/{relationshipId:guid}")]
    [HasPermission("crm.account-relationship.manage")]
    public async Task<IActionResult> Update(Guid accountId, Guid relationshipId, [FromBody] UpdateAccountRelationshipRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateAccountRelationshipCommand(accountId, relationshipId, request.RelationshipType, request.Status, request.ValidFrom, request.ValidTo, request.Notes, request.CrossCountryReason),
            cancellationToken));

    [HttpDelete("api/crm/accounts/{accountId:guid}/relationships/{relationshipId:guid}")]
    [HasPermission("crm.account-relationship.manage")]
    public async Task<IActionResult> Delete(Guid accountId, Guid relationshipId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeleteAccountRelationshipCommand(accountId, relationshipId), cancellationToken));
}
