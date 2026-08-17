using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ModuleAssignments;
using Diten.Platform.Application.Features.ModuleAssignments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/modules/{moduleCode}/assignments")]
[Route("api/platform/module-catalog/{moduleCode}/assignments")]
[Authorize(Policy = "PlatformActor")]
public sealed class ModuleAssignmentsController : CustomBaseController
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly IMediator _mediator;

    public ModuleAssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("overview")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetOverview(string moduleCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleAssignmentOverviewQuery(moduleCode, GetCorrelationId()), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("plans")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetPlans(string moduleCode, [FromQuery] ModulePlanAssignmentFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModulePlanAssignmentsQuery(moduleCode, filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tenants")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetTenants(string moduleCode, [FromQuery] ModuleTenantAssignmentFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleTenantAssignmentsQuery(moduleCode, filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("tenants/{tenantCode}")]
    [HasPermission("platform.module-catalog.read")]
    public async Task<IActionResult> GetTenantDetail(string moduleCode, string tenantCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetModuleTenantAssignmentDetailQuery(moduleCode, tenantCode, GetCorrelationId()), ct);
        return CreateActionResultInstance(response);
    }

    private string GetCorrelationId() =>
        Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier;
}
