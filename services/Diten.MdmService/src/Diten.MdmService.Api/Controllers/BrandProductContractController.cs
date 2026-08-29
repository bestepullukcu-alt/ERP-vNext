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

    // Guarded by the brand read permission. Products-only operators reach the same contract through the
    // frontend proxy, which accepts either read permission (pack §14).
    [HttpGet("contract")]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandProductContractQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
