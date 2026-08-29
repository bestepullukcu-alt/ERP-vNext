using Diten.CrmService.Application.Features.Contact.Contract;
using Diten.CrmService.Application.Features.ContactAvailability;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 contract surface (MOD-0149 / MOD-0151 parity): capability flags, MOD-0048 reference readiness,
/// permissions and honest limitations. It rides the existing <c>/api/crm/contacts/{everything}</c> Gateway wildcard,
/// so no `ocelot.json` change is required.
/// </summary>
[Authorize]
public sealed class ContactContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContactContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/contacts/contract")]
    [HasPermission(ContactAvailabilityPermissions.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContactContractQuery(), cancellationToken));
}
