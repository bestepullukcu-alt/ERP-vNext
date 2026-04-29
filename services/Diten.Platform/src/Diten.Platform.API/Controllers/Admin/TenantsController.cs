using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Admin;

[ApiController]
[Route("api/admin/tenants")]
[Authorize(Policy = "PlatformActor")]
public sealed class TenantsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<Response<PagedResult<TenantListItemDto>>>> GetTenants(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? region,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "-createdAt",
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTenantsQuery(search, status, region, page, pageSize, sort), ct);
        return OkResponse(result);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<Response<TenantRegistryStatsDto>>> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantStatsQuery(), ct);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Response<TenantDetailDto>>> GetTenantDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantDetailQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<Response<Guid>>> RegisterTenant([FromBody] RegisterTenantCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedResponse(id, "Tenant created and provisioning started.");
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<Response<TenantLifecycleResultDto>>> Suspend(Guid id, [FromBody] TenantLifecycleRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SuspendTenantCommand(id, body?.Reason), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<Response<TenantLifecycleResultDto>>> Reactivate(Guid id, [FromBody] TenantLifecycleRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReactivateTenantCommand(id, body?.Reason), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpGet("{id:guid}/modules")]
    public async Task<ActionResult<Response<TenantModulesSummaryDto>>> GetModules(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantModulesQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpGet("{id:guid}/users/summary")]
    public async Task<ActionResult<Response<TenantUsersSummaryDto>>> GetUsersSummary(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantUsersSummaryQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpGet("{id:guid}/settings")]
    public async Task<ActionResult<Response<TenantSettingsDto>>> GetSettings(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantSettingsQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<ActionResult<Response<TenantSettingsDto>>> UpdateSettings(Guid id, [FromBody] TenantSettingsUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantSettingsCommand(id, request), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result, "Tenant settings updated.");
    }

    [HttpGet("{id:guid}/login-settings")]
    public async Task<ActionResult<Response<TenantLoginSettingsDto>>> GetLoginSettings(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantLoginSettingsQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result);
    }

    [HttpPut("{id:guid}/login-settings")]
    public async Task<ActionResult<Response<TenantLoginSettingsDto>>> UpdateLoginSettings(Guid id, [FromBody] TenantLoginSettingsUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantLoginSettingsCommand(id, request), ct);
        if (result == null)
        {
            return NotFound();
        }

        return OkResponse(result, "Tenant login and security settings updated.");
    }
}
