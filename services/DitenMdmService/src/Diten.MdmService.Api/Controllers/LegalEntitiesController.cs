using Diten.MdmService.Application.Features.LegalEntities.Commands;
using Diten.MdmService.Application.Features.LegalEntities.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

[ApiController]
[Route("api/legal-entities")]
// [Authorize]
[AllowAnonymous]
public sealed class LegalEntitiesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;

    public LegalEntitiesController(IMediator mediator, IConfiguration configuration)
    {
        _mediator = mediator;
        _configuration = configuration;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllLegalEntitiesQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetLegalEntityByIdQuery(id));
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateLegalEntityResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLegalEntityRequest request)
    {
        var command = new CreateLegalEntityCommand(
            request.Title,
            request.TaxOffice,
            request.TaxNumber,
            request.Email,
            request.Phone,
            request.Website,
            request.Address,
            request.CompanyType,
            request.Sector,
            request.ContactPerson,
            request.PrimaryCurrency,
            request.DefaultTimeZone,
            request.ParentLegalEntityId,
            request.DefaultCommunicationLanguage,
            request.OrganizationRole,
            request.LogoUrl,
            request.FiscalYearStart,
            request.RegistrationDate,
            request.EffectiveFromDate,
            request.TaxJurisdiction
        );

        var result = await _mediator.Send(command);
        var publicBaseUrl = _configuration["PublicBaseUrl"];

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return Created($"/api/legal-entities/{result.Id}", result);
        }

        var location = $"{publicBaseUrl.TrimEnd('/')}/api/legal-entities/{result.Id}";
        return Created(location, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateLegalEntityRequest request)
    {
        var command = new UpdateLegalEntityCommand(
            id,
            request.Title,
            request.TaxOffice,
            request.TaxNumber,
            request.Email,
            request.Phone,
            request.Website,
            request.Address,
            request.CompanyType,
            request.Sector,
            request.ContactPerson,
            request.PrimaryCurrency,
            request.DefaultTimeZone,
            request.ParentLegalEntityId,
            request.DefaultCommunicationLanguage,
            request.OrganizationRole,
            request.LogoUrl,
            request.FiscalYearStart,
            request.RegistrationDate,
            request.EffectiveFromDate,
            request.TaxJurisdiction,
            request.IsActive
        );

        var result = await _mediator.Send(command);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteLegalEntityCommand(id));
        return NoContent();
    }

    [HttpDelete("bulk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteLegalEntitiesRequest request)
    {
        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequest("At least one ID is required.");

        var deletedCount = await _mediator.Send(new BulkDeleteLegalEntitiesCommand(request.Ids));
        return Ok(new { deletedCount });
    }
}

public sealed record BulkDeleteLegalEntitiesRequest(List<Guid> Ids);

public sealed record CreateLegalEntityRequest(
    string Title,
    string TaxOffice,
    string TaxNumber,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? CompanyType,
    string? Sector,
    string? ContactPerson,
    string? PrimaryCurrency,
    string? DefaultTimeZone,
    Guid? ParentLegalEntityId,
    string? DefaultCommunicationLanguage,
    string? OrganizationRole,
    string? LogoUrl,
    string? FiscalYearStart,
    DateTimeOffset? RegistrationDate,
    DateTimeOffset? EffectiveFromDate,
    string? TaxJurisdiction
);

public sealed record UpdateLegalEntityRequest(
    string Title,
    string TaxOffice,
    string TaxNumber,
    string? Email,
    string? Phone,
    string? Website,
    string? Address,
    string? CompanyType,
    string? Sector,
    string? ContactPerson,
    string? PrimaryCurrency,
    string? DefaultTimeZone,
    Guid? ParentLegalEntityId,
    string? DefaultCommunicationLanguage,
    string? OrganizationRole,
    string? LogoUrl,
    string? FiscalYearStart,
    DateTimeOffset? RegistrationDate,
    DateTimeOffset? EffectiveFromDate,
    string? TaxJurisdiction,
    bool IsActive
);
