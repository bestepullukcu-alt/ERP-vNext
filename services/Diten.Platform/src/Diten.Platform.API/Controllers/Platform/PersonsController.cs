using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/v1/platform/persons")]
[Authorize]
public sealed class PersonsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PersonsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{personId:guid}")]
    [HasPermission("platform.person.view")]
    public async Task<IActionResult> GetById(Guid personId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPersonReferenceByIdQuery(personId), ct));

    [HttpGet]
    [HasPermission("platform.person.search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(new SearchPersonReferencesQuery(query, status, page, pageSize), ct));

    [HttpPost("lookup-validation")]
    [HasPermission("platform.person.lookup_validation")]
    public async Task<IActionResult> LookupValidation([FromBody] PersonReferenceLookupValidationRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ValidatePersonReferencesQuery(request), ct));
}
