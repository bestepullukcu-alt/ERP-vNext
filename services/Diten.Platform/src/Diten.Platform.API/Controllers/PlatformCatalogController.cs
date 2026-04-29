using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/platform/catalog")]
[Authorize(Policy = "PlatformActor")]
public sealed class PlatformCatalogController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PlatformCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("domain-landscapes")]
    public async Task<ActionResult<Response<IReadOnlyList<DomainLandscapeDto>>>> GetDomainLandscapes(CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetDomainLandscapesQuery(), ct));
    }

    [HttpGet("suite-platforms")]
    public async Task<ActionResult<Response<IReadOnlyList<SuitePlatformDto>>>> GetSuitePlatforms([FromQuery] Guid? domainLandscapeId, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetSuitePlatformsQuery(domainLandscapeId), ct));
    }

    [HttpGet("capability-groups")]
    public async Task<ActionResult<Response<IReadOnlyList<CapabilityGroupDto>>>> GetCapabilityGroups(
        [FromQuery] Guid? domainLandscapeId,
        [FromQuery] Guid? suitePlatformId,
        CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetCapabilityGroupsQuery(domainLandscapeId, suitePlatformId), ct));
    }

    [HttpGet("modules")]
    public async Task<ActionResult<Response<ModuleDefinitionListResultDto>>> GetModules(
        [FromQuery] string? search,
        [FromQuery] Guid? domainLandscapeId,
        [FromQuery] Guid? suitePlatformId,
        [FromQuery] Guid? capabilityGroupId,
        [FromQuery] string? status,
        [FromQuery] bool? isTenantAssignable,
        [FromQuery] bool? isPlatformCore,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetModuleDefinitionsQuery(
            search,
            domainLandscapeId,
            suitePlatformId,
            capabilityGroupId,
            status,
            isTenantAssignable,
            isPlatformCore), ct);

        return OkResponse(result);
    }

    [HttpGet("modules/{moduleId}/pages")]
    public async Task<ActionResult<Response<IReadOnlyList<ModulePageDefinitionDto>>>> GetModulePages(string moduleId, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetModulePagesQuery(moduleId), ct));
    }

    [HttpGet("modules/{moduleId}/pages/{pageCode}")]
    public async Task<ActionResult<Response<ModulePageDefinitionDto>>> GetModulePageByCode(string moduleId, string pageCode, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetModulePageByCodeQuery(moduleId, pageCode), ct));
    }

    [HttpGet("modules/{moduleId}")]
    public async Task<ActionResult<Response<ModuleDefinitionDetailDto>>> GetModuleByModuleId(string moduleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetModuleDefinitionByModuleIdQuery(moduleId), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpGet("hierarchy")]
    public async Task<ActionResult<Response<ModuleCatalogHierarchyDto>>> GetHierarchy(CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(new GetModuleCatalogHierarchyQuery(), ct));
    }

    [HttpPost("domain-landscapes")]
    public async Task<ActionResult<Response<DomainLandscapeDto>>> CreateDomainLandscape([FromBody] CreateDomainLandscapeCommand command, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(command, ct), "Domain landscape created.");
    }

    [HttpPut("domain-landscapes/{id}")]
    public async Task<ActionResult<Response<DomainLandscapeDto>>> UpdateDomainLandscape(Guid id, [FromBody] UpdateDomainLandscapeCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Route ID does not match body ID.");
        return OkResponse(await _mediator.Send(command, ct), "Domain landscape updated.");
    }

    [HttpPost("suite-platforms")]
    public async Task<ActionResult<Response<SuitePlatformDto>>> CreateSuitePlatform([FromBody] CreateSuitePlatformCommand command, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(command, ct), "Suite platform created.");
    }

    [HttpPut("suite-platforms/{id}")]
    public async Task<ActionResult<Response<SuitePlatformDto>>> UpdateSuitePlatform(Guid id, [FromBody] UpdateSuitePlatformCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Route ID does not match body ID.");
        return OkResponse(await _mediator.Send(command, ct), "Suite platform updated.");
    }

    [HttpPost("capability-groups")]
    public async Task<ActionResult<Response<CapabilityGroupDto>>> CreateCapabilityGroup([FromBody] CreateCapabilityGroupCommand command, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(command, ct), "Capability group created.");
    }

    [HttpPut("capability-groups/{id}")]
    public async Task<ActionResult<Response<CapabilityGroupDto>>> UpdateCapabilityGroup(Guid id, [FromBody] UpdateCapabilityGroupCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Route ID does not match body ID.");
        return OkResponse(await _mediator.Send(command, ct), "Capability group updated.");
    }

    [HttpPost("modules")]
    public async Task<ActionResult<Response<ModuleDefinitionDetailDto>>> CreateModule([FromBody] CreateModuleDefinitionCommand command, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(command, ct), "Module created.");
    }

    [HttpPut("modules/{moduleId}")]
    public async Task<ActionResult<Response<ModuleDefinitionDetailDto>>> UpdateModule(string moduleId, [FromBody] UpdateModuleDefinitionCommand command, CancellationToken ct)
    {
        if (!string.Equals(moduleId, command.ModuleId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Route ModuleId does not match body ModuleId. ModuleId is immutable.");
            
        return OkResponse(await _mediator.Send(command, ct), "Module updated.");
    }

    [HttpPost("modules/{moduleId}/pages")]
    public async Task<ActionResult<Response<ModulePageDefinitionDto>>> CreateModulePage(
        string moduleId,
        [FromBody] CreateModulePageDefinitionCommand command,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(command.ModuleId) &&
            !string.Equals(moduleId, command.ModuleId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Route ModuleId does not match body ModuleId.");
        }

        var request = command with { ModuleId = moduleId };
        return CreatedResponse(await _mediator.Send(request, ct), "Module page created.");
    }

    [HttpPut("modules/{moduleId}/pages/{pageCode}")]
    public async Task<ActionResult<Response<ModulePageDefinitionDto>>> UpdateModulePage(
        string moduleId,
        string pageCode,
        [FromBody] UpdateModulePageDefinitionCommand command,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(command.ModuleId) &&
            !string.Equals(moduleId, command.ModuleId, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Route ModuleId does not match body ModuleId.");
        }

        if (!string.IsNullOrWhiteSpace(command.PageCode) &&
            !string.Equals(pageCode, command.PageCode, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Route PageCode does not match body PageCode. PageCode is immutable.");
        }

        var request = command with { ModuleId = moduleId, PageCode = pageCode };
        return OkResponse(await _mediator.Send(request, ct), "Module page updated.");
    }

    [HttpPost("import")]
    public async Task<ActionResult<Response<ModuleCatalogImportResultDto>>> Import([FromBody] ImportModuleCatalogCommand command, CancellationToken ct)
    {
        return OkResponse(await _mediator.Send(command, ct), "Catalog import completed.");
    }
}
