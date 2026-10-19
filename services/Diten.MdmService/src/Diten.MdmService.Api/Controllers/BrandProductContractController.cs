using Diten.MdmService.Application.Features.BrandProductContract.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0290-FU02 — capability contract for the Brand/Product master. The UI gates every action and populates
/// every dropdown from this response, so it never hardcodes a vocabulary or assumes a capability.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/brand-products")]
public sealed class BrandProductContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BrandProductContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // Pack §14 — the contract is read by either module's reader: a products-only operator needs the same vocabulary and
    // capability flags as a brand reader. WP-MDM-BRAND-ACCEPT-01 FIX1 (item 11): enforced HERE, not only by the web proxy.
    [HttpGet("contract")]
    [HasPermission("mdm.brands.read", "mdm.products.read")]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandProductContractQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
