using Diten.MdmService.Application.Features.Countries.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Diten.MdmService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/countries")]
public class CountriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CountriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllCountriesQuery());
        return Ok(new { data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCountryRequest request)
    {
        var id = await _mediator.Send(request);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteCountryRequest(id));
        return NoContent();
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteCountriesRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }
}
