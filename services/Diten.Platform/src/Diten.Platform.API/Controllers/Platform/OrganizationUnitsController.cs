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
    [HasPermission("platform.organization-units.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationUnitsQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("platform.organization-units.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationUnitByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("platform.organization-units.create")]
    public async Task<IActionResult> Create([FromBody] OrganizationUnitRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateOrganizationUnitCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("platform.organization-units.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OrganizationUnitRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateOrganizationUnitCommand(id, request), ct));

    /*
     * ── MOD-0288-FU02 ─────────────────────────────────────────────────────────────────────────────────────
     *
     * ⚠ RE-PARENTING HAS ITS OWN KEY, AND THAT IS NOT BUREAUCRACY. Moving a unit under a different parent is a
     * structural decision — the manager's own control matrix gives "transfer to another parent" an approval
     * route distinct from "rename". Whoever may rename a unit must not thereby be able to re-hang it silently.
     * Note the consequence, which is deliberate: PUT above can no longer change either line, because a caller
     * holding only `…update` would otherwise reach the same effect through a wider door.
     */
    [HttpPut("{id:guid}/reporting-lines")]
    [HasPermission("platform.organization-units.reporting-line.update")]
    public async Task<IActionResult> UpdateReportingLines(
        Guid id,
        [FromBody] OrganizationUnitReportingLinesRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(
            await _mediator.Send(new UpdateOrganizationUnitReportingLinesCommand(id, request), ct));

    // ── Custom field DEFINITIONS — the shape of the tenant's data model ───────────────────────────────────

    [HttpGet("field-definitions")]
    [HasPermission("platform.organization-units.custom-fields.read")]
    public async Task<IActionResult> GetFieldDefinitions([FromQuery] bool includeInactive, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOrganizationFieldDefinitionsQuery(includeInactive), ct));

    [HttpPost("field-definitions")]
    [HasPermission("platform.organization-units.custom-fields.manage")]
    public async Task<IActionResult> CreateFieldDefinition(
        [FromBody] CreateOrganizationFieldDefinitionRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateOrganizationFieldDefinitionCommand(request), ct));

    [HttpPut("field-definitions/{definitionId:guid}")]
    [HasPermission("platform.organization-units.custom-fields.manage")]
    public async Task<IActionResult> UpdateFieldDefinition(
        Guid definitionId,
        [FromBody] UpdateOrganizationFieldDefinitionRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(
            await _mediator.Send(new UpdateOrganizationFieldDefinitionCommand(definitionId, request), ct));

    [HttpPost("field-definitions/{definitionId:guid}/deactivate")]
    [HasPermission("platform.organization-units.custom-fields.manage")]
    public async Task<IActionResult> DeactivateFieldDefinition(
        Guid definitionId,
        [FromQuery] int expectedVersion,
        CancellationToken ct) =>
        CreateActionResultInstance(
            await _mediator.Send(new DeactivateOrganizationFieldDefinitionCommand(definitionId, expectedVersion), ct));

    // ── Custom field VALUES — one datum on one unit ───────────────────────────────────────────────────────
    //
    // ⚠ DEFINING A FIELD IS NOT FILLING IT IN. `…manage` changes the shape of the tenant's data model;
    // `…write-value` records one datum. Most users need only the second, and giving them the first because the
    // endpoints sit next to each other is how a data model gets edited by accident.

    [HttpGet("{id:guid}/field-values")]
    [HasPermission("platform.organization-units.custom-fields.read")]
    public async Task<IActionResult> GetFieldValues(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(
            await _mediator.Send(new GetOrganizationFieldValuesQuery(OrganizationUnitId: id), ct));

    [HttpPost("field-values/query")]
    [HasPermission("platform.organization-units.custom-fields.read")]
    public async Task<IActionResult> QueryFieldValues(
        [FromBody] GetOrganizationFieldValuesQuery query,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(query, ct));

    [HttpPut("{id:guid}/field-values")]
    [HasPermission("platform.organization-units.custom-fields.write-value")]
    public async Task<IActionResult> SetFieldValue(
        Guid id,
        [FromBody] SetOrganizationFieldValueRequest request,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new SetOrganizationFieldValueCommand(id, request), ct));

    [HttpPost("{id:guid}/archive")]
    [HasPermission("platform.organization-units.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveOrganizationUnitCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission("platform.organization-units.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteOrganizationUnitCommand(id), ct));
}
