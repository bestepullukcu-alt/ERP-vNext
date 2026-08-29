using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.AccountContact.Commands;
using Diten.CrmService.Application.Features.AccountContact.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 FU03 — Account↔Contact link management + the Account 360 / Contact 360 read projections. Routes sit under
/// the existing Gateway wildcards (`/api/crm/accounts/{everything}`, `/api/crm/contacts/{everything}`) — no new Gateway
/// route needed. Read = crm.account-contact.read, mutate = crm.account-contact.manage.
/// </summary>
[Authorize]
public sealed class AccountContactController : CustomBaseController
{
    private readonly IMediator _mediator;

    public AccountContactController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---- Account-centric ----

    [HttpGet("api/crm/accounts/{accountId:guid}/contacts")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> ListForAccount(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListContactsForAccountQuery(accountId), cancellationToken));

    /// <summary>Account 360 "Related Contacts" projection alias.</summary>
    [HttpGet("api/crm/accounts/{accountId:guid}/related-contacts")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> RelatedContacts(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListContactsForAccountQuery(accountId), cancellationToken));

    [HttpGet("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> GetLink(Guid accountId, Guid linkId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetAccountContactLinkByIdQuery(accountId, linkId), cancellationToken));

    [HttpPost("api/crm/accounts/{accountId:guid}/contacts")]
    [HasPermission("crm.account-contact.manage")]
    public async Task<IActionResult> Link(Guid accountId, [FromBody] LinkContactToAccountRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new LinkContactToAccountCommand(accountId, request.ContactId, request.RoleCode, request.IsPrimary, request.ValidFrom, request.ValidTo, request.Notes, request.CrossCountryReason, request.ReportsToContactId),
            cancellationToken));

    [HttpPut("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}")]
    [HasPermission("crm.account-contact.manage")]
    public async Task<IActionResult> Update(Guid accountId, Guid linkId, [FromBody] UpdateAccountContactLinkRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateAccountContactLinkCommand(accountId, linkId, request.RoleCode, request.IsPrimary, request.ValidFrom, request.ValidTo, request.Notes, request.Status, request.CrossCountryReason, request.ReportsToContactId),
            cancellationToken));

    [HttpDelete("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}")]
    [HasPermission("crm.account-contact.manage")]
    public async Task<IActionResult> Unlink(Guid accountId, Guid linkId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeleteAccountContactLinkCommand(accountId, linkId), cancellationToken));

    // ---- Contact-centric ----

    [HttpGet("api/crm/contacts/{contactId:guid}/accounts")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> ListForContact(Guid contactId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListAccountsForContactQuery(contactId), cancellationToken));

    /// <summary>Contact 360 "Linked Accounts" projection alias.</summary>
    [HttpGet("api/crm/contacts/{contactId:guid}/linked-accounts")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> LinkedAccounts(Guid contactId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListAccountsForContactQuery(contactId), cancellationToken));
}
