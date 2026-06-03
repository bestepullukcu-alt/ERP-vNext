using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[Route("api/platform/organization-units")]
[Authorize]
public sealed class OrganizationUnitsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public OrganizationUnitsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("Modules.OrganizationUnit.Read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationUnitsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("Modules.OrganizationUnit.Read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationUnitByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("Modules.OrganizationUnit.Create")]
    public async Task<IActionResult> Create([FromBody] OrganizationUnitRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateOrganizationUnitCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("Modules.OrganizationUnit.Update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OrganizationUnitRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateOrganizationUnitCommand(id, request), ct));

    [HttpPost("{id:guid}/archive")]
    [HasPermission("Modules.OrganizationUnit.Archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveOrganizationUnitCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission("Modules.OrganizationUnit.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteOrganizationUnitCommand(id), ct));
}
