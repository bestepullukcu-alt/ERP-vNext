using Diten.MdmService.Application.Features.Lookups;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

// MOD-0220 FULL — MDM-local reference-data lookups feeding the Legal Entity wizard dropdowns
// (legal-form, organization-role, control-type, accounting-standard, tax-regime). Country + base currency
// come from the Platform lookup surface (/api/lookups/*). Reuses mdm.legal-entities.read (the form already holds it).
[Authorize]
[ApiController]
[Route("api/legal-entities/lookups")]
public sealed class LegalEntityLookupsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public LegalEntityLookupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{type}")]
    [HasPermission("mdm.legal-entities.read")]
    public async Task<IActionResult> Get(string type, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetLegalEntityLookupQuery(type), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
