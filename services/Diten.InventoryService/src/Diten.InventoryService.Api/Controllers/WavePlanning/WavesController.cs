using Diten.InventoryService.Api.WavePlanning;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves;
using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Application.Features.Waves.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.WavePlanning;

/// <summary>
/// WMS-WAVE-BUNDLE v1 (draft §4.1) — MOD-0180 waves under <c>/api/warehouse</c> (pack §3: the base is the controller's, the template names the
/// whole path segment; the namespace is <c>Controllers.WavePlanning</c>, never MOD-0178's <c>Controllers.Warehouse</c>). GET / POST only:
/// update, cancel and the setting write are POST commands — no PUT, no DELETE (AC-8). Every action carries its <c>warehouse.waves.*</c> key;
/// the scope is the token's tenant + the selected company (another company's or tenant's wave is 404). No business rule here: the body is
/// parsed, the key / If-Match headers read, and the handler decides. The fixed segments (<c>candidates</c>, <c>settings</c>) match before
/// <c>{waveId:guid}</c>.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class WavesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public WavesController(IMediator mediator) => _mediator = mediator;

    // ── reads ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("waves")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> List([FromQuery] string? warehouseId, [FromQuery] string[]? status, [FromQuery] string? waveType, [FromQuery] string? q,
        [FromQuery] string? cutoffFrom, [FromQuery] string? cutoffTo, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWaveListQuery(warehouseId, status ?? [], waveType, q, cutoffFrom, cutoffTo, page, pageSize), ct));

    [HttpGet("waves/candidates")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Candidates([FromQuery] string? warehouseId, [FromQuery] string? q, [FromQuery] string? shipTo, [FromQuery] string? page,
        [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWaveCandidateListQuery(warehouseId, q, shipTo, page, pageSize), ct));

    [HttpGet("waves/settings/{warehouseId}")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Settings(string warehouseId, CancellationToken ct)
        => WaveSupport.TryParseId(warehouseId, out var id) ? WithETag(await _mediator.Send(new GetWaveWarehouseSettingsQuery(id), ct)) : UnknownWarehouse();

    [HttpGet("waves/{waveId:guid}")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Get(Guid waveId, CancellationToken ct) => WithETag(await _mediator.Send(new GetWaveByIdQuery(waveId), ct));

    [HttpGet("waves/{waveId:guid}/orders")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Orders(Guid waveId, [FromQuery] string[]? state, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWaveOrderListQuery(waveId, state ?? [], page, pageSize), ct));

    [HttpGet("waves/{waveId:guid}/suggestions")]
    [HasPermission(WavePermissions.Update)]
    public async Task<IActionResult> Suggestions(Guid waveId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWaveSuggestionListQuery(waveId), ct));

    [HttpGet("waves/{waveId:guid}/load")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Load(Guid waveId, CancellationToken ct) => CreateActionResultInstance(await _mediator.Send(new GetWaveLoadQuery(waveId), ct));

    [HttpGet("waves/{waveId:guid}/events")]
    [HasPermission(WavePermissions.Read)]
    public async Task<IActionResult> Events(Guid waveId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWaveEventListQuery(waveId, page, pageSize), ct));

    // ── writes ───────────────────────────────────────────────────────────────────────────────────────────

    [HttpPost("waves")]
    [HasPermission(WavePermissions.Create)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.CreateMembers, b => new FormBody(b.Form(), null, b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreateWaveCommand(body.Form, key), ct));
    }

    [HttpPost("waves/{waveId:guid}/update")]
    [HasPermission(WavePermissions.Update)]
    public async Task<IActionResult> Update(Guid waveId, CancellationToken ct)
    {
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.UpdateMembers,
            b => new FormBody(b.Form(), b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateWaveCommand(waveId, body.Form, body.Reason, key, version), ct));
    }

    [HttpPost("waves/{waveId:guid}/orders")]
    [HasPermission(WavePermissions.Update)]
    public async Task<IActionResult> AddOrders(Guid waveId, CancellationToken ct)
    {
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.AddOrdersMembers,
            b => new OrdersBody(b.Uuids("outboundIds"), b.String("via"), b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new AddWaveOrdersCommand(waveId, body.OutboundIds, body.Via, body.Reason, key, version), ct));
    }

    [HttpPost("waves/{waveId:guid}/orders/remove")]
    [HasPermission(WavePermissions.Update)]
    public async Task<IActionResult> RemoveOrders(Guid waveId, CancellationToken ct)
    {
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.RemoveOrdersMembers,
            b => new OrdersBody(b.Uuids("outboundIds"), null, b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new RemoveWaveOrdersCommand(waveId, body.OutboundIds, body.Reason, key, version), ct));
    }

    [HttpPost("waves/{waveId:guid}/cancel")]
    [HasPermission(WavePermissions.Cancel)]
    public async Task<IActionResult> Cancel(Guid waveId, CancellationToken ct)
    {
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.ReasonMembers,
            b => new ReasonBody(b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new CancelWaveCommand(waveId, body.Reason, key, version), ct));
    }

    [HttpPost("waves/settings/{warehouseId}/update")]
    [HasPermission(WavePermissions.SettingsConfigure)]
    public async Task<IActionResult> UpdateSettings(string warehouseId, CancellationToken ct)
    {
        if (!WaveSupport.TryParseId(warehouseId, out var id)) return UnknownWarehouse();
        var read = await WaveRequestReader.ReadAsync(Request.Body, WaveRequestReader.SettingsMembers,
            b => new SettingsBody(b.Settings(), b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateWaveWarehouseSettingsCommand(id, body.Settings, body.Reason, key, version), ct));
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record FormBody(WaveFormInput Form, string? Reason, string? Key);

    private sealed record OrdersBody(IReadOnlyList<Guid> OutboundIds, string? Via, string? Reason, string? Key);

    private sealed record ReasonBody(string? Reason, string? Key);

    private sealed record SettingsBody(WaveSettingsInput Settings, string? Reason, string? Key);

    private IActionResult WithETag(Response<WaveDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private IActionResult WithETag(Response<WaveOrdersResultDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Wave.Version}\"";
        return CreateActionResultInstance(response);
    }

    private IActionResult WithETag(Response<WaveWarehouseSettingsDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => WaveSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : WaveSupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private IActionResult UnknownWarehouse()
        => ContractFailure(StatusCodes.Status404NotFound, InventoryErrorCodes.UnknownLocation, "The warehouse does not exist as an active warehouse of the selected company.");
}
