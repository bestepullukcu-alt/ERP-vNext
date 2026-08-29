using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Application.Features.Contact.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 FU01 — Contact Foundation. Contact master CRUD + 360 overview + minimal search. Account links
/// (AccountContactLink) and Account↔Account relationships are later FUs and are NOT exposed here.
/// </summary>
[Authorize]
[Route("api/crm/contacts")]
public sealed class ContactController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContactController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("crm.contact.read")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new ListContactsQuery(search, page, pageSize), cancellationToken));

    [HttpGet("search")]
    [HasPermission("crm.contact.read")]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new SearchContactsQuery(search, limit), cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission("crm.contact.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContactByIdQuery(id), cancellationToken));

    [HttpGet("{id:guid}/overview")]
    [HasPermission("crm.contact.overview.read")]
    public async Task<IActionResult> Overview(Guid id, CancellationToken cancellationToken)
    {
        // Consent/preference is a read-only MOD-0164 seam block gated by its own permissions; resolve them here so the
        // handler can mask the block without hard-denying the whole 360 page (base overview needs only overview.read).
        var canReadConsent = PermissionClaims.HasPermission(User, "crm.contact.consent.read");
        var canReadPreference = PermissionClaims.HasPermission(User, "crm.contact.preference.read");
        return CreateActionResultInstance(
            await _mediator.Send(new GetContactOverviewQuery(id, canReadConsent, canReadPreference), cancellationToken));
    }

    [HttpPost]
    [HasPermission("crm.contact.create")]
    public async Task<IActionResult> Create([FromBody] CreateContactCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission("crm.contact.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command with { Id = id }, cancellationToken));

    [HttpDelete("{id:guid}")]
    [HasPermission("crm.contact.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeleteContactCommand(id), cancellationToken));

    // NOTE: /import and /export (crm.contact.import / crm.contact.export) are declared in the MOD-0150 pack but
    // intentionally NOT implemented in this foundation slice (no fake/no-shell). Tracked as FU06.
}
