using Diten.InventoryService.Api.Contract;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers;

/// <summary>
/// LOCATION — MOD-0173-FU02 (base <c>/api/locations</c>, contract <c>location.openapi.yaml</c>):
/// <list type="bullet">
/// <item>frozen v1 reads: <c>getLocation</c>, <c>listLocations</c>, <c>getLocationPath</c>, <c>validateLocations</c>;</item>
/// <item>v1.1 owner operations: <c>createLocation</c>, <c>updateLocation</c> (PATCH), <c>changeLocationStatus</c>,
/// <c>bulkCreateBins</c>, <c>searchLocations</c>, <c>getLocationHistory</c>, <c>getLocationStockPresence</c>.</item>
/// </list>
/// Exactly these eleven routes; there is no PUT and no DELETE (a location is archived, never removed — K-04), and no
/// route writes history (K-11). Reads need <c>inventory.locations.read</c>, writes <c>inventory.locations.manage</c>
/// (§14). Every owner response carrying one location sends its version as <c>ETag: W/"n"</c>. No business rule here:
/// read → send → render.
/// </summary>
[Route("api/locations")]
[Authorize]
public sealed class LocationsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public LocationsController(IMediator mediator) => _mediator = mediator;

    // ── frozen v1 ─────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("{id}")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> GetLocation(string id, CancellationToken cancellationToken)
        => WithETag(await _mediator.Send(new GetLocationQuery(id), cancellationToken));

    [HttpGet("")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> ListLocations(
        [FromQuery] string? type,
        [FromQuery] string? parentId,
        [FromQuery] string? legalEntityId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListLocationsQuery(type, parentId, legalEntityId), cancellationToken));

    [HttpGet("{id}/path")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> GetLocationPath(string id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetLocationPathQuery(id), cancellationToken));

    [HttpPost("validate")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> ValidateLocations(CancellationToken cancellationToken)
    {
        var (ids, error) = await LocationRequestReader.ReadValidateAsync(Request.Body, cancellationToken);
        return ids is null
            ? ContractFailure(StatusCodes.Status400BadRequest, LocationErrorCodes.InvalidRequest, error!)
            : CreateActionResultInstance(await _mediator.Send(new ValidateLocationsQuery(ids), cancellationToken));
    }

    // ── v1.1 owner operations ─────────────────────────────────────────────────────────────────────────────

    [HttpPost("")]
    [HasPermission(InventoryPermissions.LocationsManage)]
    public async Task<IActionResult> CreateLocation(CancellationToken cancellationToken)
    {
        var (command, error) = await LocationRequestReader.ReadCreateAsync(
            Request.Body, Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return command is null
            ? ContractFailure(StatusCodes.Status400BadRequest, LocationErrorCodes.InvalidRequest, error!)
            : WithETag(await _mediator.Send(command, cancellationToken));
    }

    [HttpPatch("{id}")]
    [HasPermission(InventoryPermissions.LocationsManage)]
    public async Task<IActionResult> UpdateLocation(string id, CancellationToken cancellationToken)
    {
        var (command, error) = await LocationRequestReader.ReadUpdateAsync(
            Request.Body, id, Request.Headers[InventoryContract.IfMatchHeader].ToString(), cancellationToken);
        return command is null
            ? ContractFailure(StatusCodes.Status400BadRequest, LocationErrorCodes.InvalidRequest, error!)
            : WithETag(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("{id}/status")]
    [HasPermission(InventoryPermissions.LocationsManage)]
    public async Task<IActionResult> ChangeLocationStatus(string id, CancellationToken cancellationToken)
    {
        var (command, error) = await LocationRequestReader.ReadStatusAsync(
            Request.Body, id, Request.Headers[InventoryContract.IfMatchHeader].ToString(), cancellationToken);
        return command is null
            ? ContractFailure(StatusCodes.Status400BadRequest, LocationErrorCodes.InvalidRequest, error!)
            : WithETag(await _mediator.Send(command, cancellationToken));
    }

    [HttpPost("{id}/bins/bulk")]
    [HasPermission(InventoryPermissions.LocationsManage)]
    public async Task<IActionResult> BulkCreateBins(string id, CancellationToken cancellationToken)
    {
        var (command, error) = await LocationRequestReader.ReadBulkAsync(
            Request.Body, id, Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), cancellationToken);
        return command is null
            ? ContractFailure(StatusCodes.Status400BadRequest, LocationErrorCodes.InvalidRequest, error!)
            : CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    [HttpGet("search")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> SearchLocations(
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        [FromQuery] string? search,
        [FromQuery] string[]? type,
        [FromQuery] string[]? status,
        [FromQuery] string? isStockable,
        [FromQuery] string? parentId,
        [FromQuery] string? orderBy,
        [FromQuery] string? orderDir,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SearchLocationsQuery(page, pageSize, search, type ?? [], status ?? [], isStockable, parentId, orderBy, orderDir),
            cancellationToken));

    [HttpGet("{id}/history")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> GetLocationHistory(string id, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetLocationHistoryQuery(id, page, pageSize), cancellationToken));

    [HttpGet("{id}/stock-presence")]
    [HasPermission(InventoryPermissions.LocationsRead)]
    public async Task<IActionResult> GetLocationStockPresence(string id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetLocationStockPresenceQuery(id), cancellationToken));

    /// <summary>A successful single-location answer carries its version as <c>ETag: W/"n"</c> (§8).</summary>
    private IActionResult WithETag<T>(Response<T> response)
        where T : LocationRefDto
    {
        if (response.IsSuccessful && response.Data?.ETagVersion is { } version)
        {
            Response.Headers.ETag = LocationMapping.ETag(version);
        }

        return CreateActionResultInstance(response);
    }
}
