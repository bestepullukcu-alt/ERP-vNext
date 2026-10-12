using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Commands;
using Diten.InventoryService.Application.Features.Expiry.Queries;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Expiry;

/// <summary>
/// FEFO-BUNDLE v1.0.0 — MOD-0176 (base <c>/api/expiry</c>, contract <c>fefo.openapi.yaml</c>). Slice F1 serves the policy (get / history /
/// put) and the exception cases (list, get, open, take, comment, prepare-resolution, resolve, close). Every other operation of the
/// contract (panel, lists, FEFO order, evidence, date overrides, signable subjects) is F2–F5 and has no route yet. No DELETE; the only
/// PUT is the policy (master data, full replacement). No business rule here: the body is parsed, the key / If-Match headers read,
/// and the handler decides.
/// </summary>
[Route("api/expiry")]
[Authorize]
public sealed class ExpiryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ExpiryController(IMediator mediator) => _mediator = mediator;

    // ── policy ───────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("policy")]
    [HasPermission(ExpiryPermissions.PoliciesRead)]
    public async Task<IActionResult> GetPolicy(CancellationToken ct)
        => WithETag(await _mediator.Send(new GetExpiryPolicyQuery(), ct), p => p.Version);

    [HttpGet("policy/history")]
    [HasPermission(ExpiryPermissions.PoliciesRead)]
    public async Task<IActionResult> GetPolicyHistory([FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetExpiryPolicyHistoryQuery(page, pageSize), ct));

    [HttpPut("policy")]
    [HasPermission(ExpiryPermissions.PoliciesManage)]
    public async Task<IActionResult> UpdatePolicy(CancellationToken ct)
    {
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.PolicyMembers, b => new
        {
            Policy = new ExpiryPolicyInput(
                b.Int("approachingDays"), b.Int("criticalDays"), b.Enum<FefoOutboundMode>("fefoOutboundMode"),
                b.Enum<BelowMinRemainingMode>("belowMinRemainingMode"), b.Enum<DateOverrideMode>("dateOverrideMode"), b.SlaHours("slaHours"),
                b.Int("expiredOnHandThresholdDays")),
            Reason = b.String("reason"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (\"0\" before the first save).");
        return WithETag(await _mediator.Send(new UpdateExpiryPolicyCommand(body.Policy, body.Reason, key, version), ct), p => p.Version);
    }

    // ── exceptions ───────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("exceptions")]
    [HasPermission(ExpiryPermissions.ExceptionsRead)]
    public async Task<IActionResult> ListExceptions(
        [FromQuery] string? type, [FromQuery] string[]? status, [FromQuery] string? late, [FromQuery] string? warehouseId,
        [FromQuery] string? assignedToMe, [FromQuery] string? q, [FromQuery] string? sort, [FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListExpiryExceptionsQuery(type, status ?? [], late, warehouseId, assignedToMe, q, sort, page, pageSize), ct));

    [HttpGet("exceptions/{exceptionId}")]
    [HasPermission(ExpiryPermissions.ExceptionsRead)]
    public async Task<IActionResult> GetException(string exceptionId, CancellationToken ct)
        => await CompanyChecked(exceptionId, WithETag(await _mediator.Send(new GetExpiryExceptionQuery(exceptionId), ct), d => d.Version), ct);

    [HttpPost("exceptions")]
    [HasPermission(ExpiryPermissions.ExceptionsCreate)]
    public async Task<IActionResult> OpenException(CancellationToken ct)
    {
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.OpenMembers, b => new
        {
            Type = b.ExceptionType("type"),
            LotId = b.Uuid("lotId"),
            WarehouseId = b.OptionalUuid("warehouseId"),
            Quantity = b.Decimal("quantity"),
            Description = b.String("description"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(
            new OpenExpiryExceptionCommand(body.Type, body.LotId, body.WarehouseId, body.Quantity, body.Description, key), ct), d => d.Version);
    }

    [HttpPost("exceptions/{exceptionId}/take")]
    [HasPermission(ExpiryPermissions.ExceptionsReview)]
    public async Task<IActionResult> TakeException(string exceptionId, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(exceptionId, out var id)) return NotFound();
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(exceptionId, WithETag(await _mediator.Send(new TakeExpiryExceptionCommand(id, key, version), ct), d => d.Version), ct);
    }

    [HttpPost("exceptions/{exceptionId}/comments")]
    [HasPermission(ExpiryPermissions.ExceptionsComment)]
    public async Task<IActionResult> CommentException(string exceptionId, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(exceptionId, out var id)) return NotFound();
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.CommentMembers,
            b => new { Text = b.String("text"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(exceptionId, CreateActionResultInstance(await _mediator.Send(new CommentExpiryExceptionCommand(id, body.Text, key), ct)), ct);
    }

    [HttpPost("exceptions/{exceptionId}/pending-resolution")]
    [HasPermission(ExpiryPermissions.ExceptionsResolve)]
    public async Task<IActionResult> PrepareResolution(string exceptionId, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(exceptionId, out var id)) return NotFound();
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.PrepareMembers,
            b => new { Resolution = b.Enum<ExpiryResolution>("resolution"), Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(exceptionId, WithETag(await _mediator.Send(
            new PrepareExpiryResolutionCommand(id, body.Resolution, body.Reason, key, version), ct), p => p.Version), ct);
    }

    [HttpPost("exceptions/{exceptionId}/resolve")]
    [HasPermission(ExpiryPermissions.ExceptionsResolve)]
    public async Task<IActionResult> ResolveException(string exceptionId, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(exceptionId, out var id)) return NotFound();
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.ResolveMembers, b => new
        {
            Resolution = b.Enum<ExpiryResolution>("resolution"),
            Reason = b.String("reason"),
            SignatureId = b.String("signatureId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(exceptionId, WithETag(await _mediator.Send(
            new ResolveExpiryExceptionCommand(id, body.Resolution, body.Reason, body.SignatureId, key, version), ct), d => d.Version), ct);
    }

    [HttpPost("exceptions/{exceptionId}/close")]
    [HasPermission(ExpiryPermissions.ExceptionsClose)]
    public async Task<IActionResult> CloseException(string exceptionId, CancellationToken ct)
    {
        if (!ExpirySupport.TryParseId(exceptionId, out var id)) return NotFound();
        var read = await ExpiryRequestReader.ReadAsync(Request.Body, ExpiryRequestReader.CloseMembers,
            b => new { Note = b.String("note"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(exceptionId, WithETag(await _mediator.Send(new CloseExpiryExceptionCommand(id, body.Note, key, version), ct), d => d.Version), ct);
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pack §13 #8 (V23 pattern): a case id of ANOTHER company of the caller's tenant answers 403 CROSS_LE_FORBIDDEN; another tenant's
    /// stays 404. The handlers read scoped to the company; only their 404 is re-checked here.
    /// </summary>
    private async Task<IActionResult> CompanyChecked(string id, IActionResult result, CancellationToken ct)
    {
        if (result is ObjectResult { StatusCode: StatusCodes.Status404NotFound } && ExpirySupport.TryParseId(id, out var guid)
            && await HttpContext.RequestServices.GetRequiredService<IExpiryRepository>().ExistsInAnotherCompanyAsync(guid, ct))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, InventoryErrorCodes.CrossLeForbidden, "The record belongs to another company.");
        }

        return result;
    }

    private IActionResult WithETag<T>(Response<T> response, Func<T, int> version)
    {
        if (response.IsSuccessful && response.Data is not null)
        {
            Response.Headers.ETag = $"W/\"{version(response.Data)}\"";
        }

        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => ExpirySupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : ExpirySupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private new IActionResult NotFound()
        => ContractFailure(StatusCodes.Status404NotFound, Domain.Expiry.Rules.ExpiryErrorCodes.ExceptionNotFound, "Exception not found.");
}
