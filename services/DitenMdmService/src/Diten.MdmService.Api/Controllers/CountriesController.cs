using Diten.MdmService.Application.Features.Countries.Commands;
using Diten.MdmService.Application.Features.Countries.Queries;
using Diten.MdmService.Application.Features.Countries.Requests;
using Diten.MdmService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[ApiController]
[Route("api/countries")]
// [Authorize]
[AllowAnonymous]
public sealed class CountriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CountriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllCountriesQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetCountryByIdQuery(id));
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("by-iso2/{iso2Code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByIso2Code(string iso2Code)
    {
        var countries = await _mediator.Send(new GetAllCountriesQuery());
        var result = countries.FirstOrDefault(c =>
            c.Iso2Code.Equals(iso2Code, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpGet("by-iso3/{iso3Code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByIso3Code(string iso3Code)
    {
        var countries = await _mediator.Send(new GetAllCountriesQuery());
        var result = countries.FirstOrDefault(c =>
            c.Iso3Code.Equals(iso3Code, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Country), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateCountryRequest request)
    {
        var command = new CreateCountryCommand(
            request.Name,
            request.NativeName,
            request.Iso2Code,
            request.Iso3Code,
            request.NumericCode,
            request.PhoneCode,
            request.CurrencyCode,
            request.CurrencyName,
            request.CurrencySymbol,
            request.Region,
            request.SubRegion,
            request.Capital,
            request.FlagEmoji,
            request.Latitude,
            request.Longitude,
            request.IsActive
        );

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCountryRequest request)
    {
        var command = new UpdateCountryCommand(
            id,
            request.Name,
            request.NativeName,
            request.Iso2Code,
            request.Iso3Code,
            request.NumericCode,
            request.PhoneCode,
            request.CurrencyCode,
            request.CurrencyName,
            request.CurrencySymbol,
            request.Region,
            request.SubRegion,
            request.Capital,
            request.FlagEmoji,
            request.Latitude,
            request.Longitude,
            request.IsActive
        );

        var result = await _mediator.Send(command);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteCountryCommand(id));
        if (!result)
            return NotFound();

        return NoContent();
    }
}