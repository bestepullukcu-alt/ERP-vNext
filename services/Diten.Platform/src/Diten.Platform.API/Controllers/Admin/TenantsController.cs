using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantLifecycleRequest = Diten.Platform.API.Models.TenantLifecycleRequest;

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
    public async Task<IActionResult> GetTenants(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? region,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "-createdAt",
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTenantsQuery(search, status, region, page, pageSize, sort), ct);
        return CreateActionResultInstance(Response<PagedResult<TenantListItemDto>>.Success(result));
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantStatsQuery(), ct);
        return CreateActionResultInstance(Response<TenantRegistryStatsDto>.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTenantDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantDetailQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantDetailDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantDetailDto>.Success(result));
    }

    [HttpPost]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreateActionResultInstance(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] TenantUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantCommand(id, request), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPut("{id:guid}/branding")]
    public async Task<IActionResult> UpdateBranding(Guid id, [FromBody] TenantBrandingUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantBrandingCommand(id, request), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantDetailDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantDetailDto>.Success(result));
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] TenantLifecycleRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SuspendTenantCommand(id, body?.Reason), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] TenantLifecycleRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReactivateTenantCommand(id, body?.Reason), ct);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteTenantCommand(id), ct);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("bulk")]
    public async Task<IActionResult> BulkDeleteTenants([FromBody] TenantBulkDeleteRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new BulkDeleteTenantsCommand(request.Ids), ct);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{id:guid}/modules")]
    public async Task<IActionResult> GetModules(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantModulesQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantModulesSummaryDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantModulesSummaryDto>.Success(result));
    }

    [HttpGet("{id:guid}/users/summary")]
    public async Task<IActionResult> GetUsersSummary(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantUsersSummaryQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantUsersSummaryDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantUsersSummaryDto>.Success(result));
    }

    [HttpGet("{id:guid}/admin-users")]
    public async Task<IActionResult> GetAdminUsers(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantAdminUsersQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<IReadOnlyList<TenantAdminUserDto>>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<IReadOnlyList<TenantAdminUserDto>>.Success(result));
    }

    [HttpPost("{id:guid}/admin-users")]
    public async Task<IActionResult> CreateAdminUser(Guid id, [FromBody] TenantAdminUserUpsertRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateTenantAdminUserCommand(id, request), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPut("{id:guid}/admin-users/{adminUserId:guid}")]
    public async Task<IActionResult> UpdateAdminUser(Guid id, Guid adminUserId, [FromBody] TenantAdminUserUpsertRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantAdminUserCommand(id, adminUserId, request), ct);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("{id:guid}/admin-users/{adminUserId:guid}")]
    public async Task<IActionResult> DeleteAdminUser(Guid id, Guid adminUserId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteTenantAdminUserCommand(id, adminUserId), ct);
        return CreateActionResultInstance(result);
    }

    [HttpPost("{id:guid}/admin-users/{adminUserId:guid}/invite")]
    public async Task<IActionResult> InviteAdminUser(Guid id, Guid adminUserId, CancellationToken ct)
    {
        var result = await _mediator.Send(new InviteTenantAdminUserCommand(id, adminUserId), ct);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{id:guid}/settings")]
    public async Task<IActionResult> GetSettings(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantSettingsQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantSettingsDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantSettingsDto>.Success(result));
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] TenantSettingsUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantSettingsCommand(id, request), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantSettingsDto>.Fail("Tenant not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantSettingsDto>.Success(result));
    }

    [HttpGet("{id:guid}/login-settings")]
    public async Task<IActionResult> GetLoginSettings(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTenantLoginSettingsQuery(id), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant login settings not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Success(result));
    }

    [HttpPut("{id:guid}/login-settings")]
    public async Task<IActionResult> UpdateLoginSettings(Guid id, [FromBody] TenantLoginSettingsUpdateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateTenantLoginSettingsCommand(id, request), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant login settings not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Success(result));
    }
}
