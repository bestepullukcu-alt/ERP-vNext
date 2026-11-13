using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitExecution.VisitExecutionPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (BE-a) — the visit execution (§2.2, fixed: Web W3 and mobile depend on these routes, fields and codes):
/// start (multipart: server time, location against the institution, optional photo), the live capture, the finish, the
/// doctor's signature (multipart png), the reads, and the evidence streams. Every instant is the SERVER's. Ownership is
/// decided in the handlers (own visit; another rep's visit is 404; read-all reads). TenantId is server-resolved.
/// <para>No DELETE: a started visit is evidence. The evidence content ids never appear in a response.</para>
/// </summary>
[Authorize]
public sealed class VisitExecutionController : CustomBaseController
{
    /// <summary>The evidence ceiling (10 MB) plus room for the multipart envelope and the form fields.</summary>
    internal const long MaxRequestBytes = 11L * 1024 * 1024;

    private readonly IMediator _mediator;

    public VisitExecutionController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/visit-execution/contract")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitExecutionContractQuery(), cancellationToken));

    [HttpGet("api/crm/visit-execution/by-plan/{plannedVisitId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ByPlan(Guid plannedVisitId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitExecutionByPlanQuery(plannedVisitId), cancellationToken));

    [HttpGet("api/crm/visit-execution/by-plan/{plannedVisitId:guid}/brief")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Brief(Guid plannedVisitId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitBriefQuery(plannedVisitId), cancellationToken));

    /// <summary>Starts the visit. multipart/form-data: plannedVisitId, kind?, latitude?, longitude?, accuracyMeters?,
    /// locationError? (denied | unavailable | timeout), photo? (file), photoCapturedAt?. Numbers use the invariant
    /// culture ("41.0123"). WP-VW-W3E (W3E-BE-a): qrPayload? (the scanned institution QR text, ≤ 200, sent as read),
    /// qrScannedAt? (informative).</summary>
    [HttpPost("api/crm/visit-execution/start")]
    [HasPermission(Perms.Record)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Start(
        [FromForm] Guid plannedVisitId,
        [FromForm] string? kind,
        [FromForm] string? latitude,
        [FromForm] string? longitude,
        [FromForm] string? accuracyMeters,
        [FromForm] string? locationError,
        IFormFile? photo,
        [FromForm] string? photoCapturedAt,
        // WP-VW-W3E (BE-b, §2.5) — the device declaration (all optional)
        [FromForm] string? clientPlatform,
        [FromForm] string? appVersion,
        [FromForm] string? mockLocationDetected,
        [FromForm] string? integrityProvider,
        [FromForm] string? integrityVerdict,
        [FromForm] string? integrityToken,
        CancellationToken cancellationToken,
        [FromForm] string? qrPayload = null,
        [FromForm] string? qrScannedAt = null)
    {
        if (!TryNumber(latitude, out var lat) || !TryNumber(longitude, out var lng) || !TryNumber(accuracyMeters, out var accuracy))
        {
            return CreateActionResultInstance(Response<VisitExecutionDto>.Fail(
                new[] { "latitude / longitude / accuracyMeters must be numbers.", VisitExecutionErrorCodes.CaptureInvalid }, 400));
        }

        if (!TryBool(mockLocationDetected, out var mock))
        {
            return CreateActionResultInstance(Response<VisitExecutionDto>.Fail(
                new[] { "mockLocationDetected must be true or false.", VisitExecutionErrorCodes.CaptureInvalid }, 400));
        }

        var file = await ReadAsync(photo, cancellationToken);
        return CreateActionResultInstance(await _mediator.Send(
            new StartVisitExecutionCommand(plannedVisitId, kind, lat, lng, accuracy, locationError, file, photoCapturedAt,
                QrPayload: qrPayload, QrScannedAt: qrScannedAt,
                Device: DeviceOf(clientPlatform, appVersion, mock, integrityProvider, integrityVerdict, integrityToken)),
            cancellationToken));
    }

    /// <summary>W3E-BE-b — the device declaration; the integrity token is reduced to its BYTE COUNT here and goes no
    /// further (never to the handler, the database or a log). Null when nothing was declared.</summary>
    public static VisitDeviceEvidenceInput? DeviceOf(string? clientPlatform, string? appVersion, bool? mockLocationDetected,
        string? integrityProvider, string? integrityVerdict, string? integrityToken)
    {
        var tokenBytes = string.IsNullOrEmpty(integrityToken) ? (int?)null : System.Text.Encoding.UTF8.GetByteCount(integrityToken);
        return clientPlatform is null && appVersion is null && mockLocationDetected is null && integrityProvider is null
               && integrityVerdict is null && tokenBytes is null
            ? null
            : new VisitDeviceEvidenceInput(clientPlatform, appVersion, mockLocationDetected, integrityProvider, integrityVerdict, tokenBytes);
    }

    private static bool TryBool(string? raw, out bool? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (bool.TryParse(raw.Trim(), out var parsed)) { value = parsed; return true; }
        return false;
    }

    /// <summary>WP-VW-W3E (W3E-BE-c, §2.10) — the QR pre-check: what the start would answer for this QR, written nowhere
    /// (no execution, no scan counter, no event). The start's plan rules and errors; the payload is never logged.</summary>
    [HttpPost("api/crm/visit-execution/qr-check")]
    [HasPermission(Perms.Record)]
    public async Task<IActionResult> QrCheck([FromBody] VisitQrCheckInput? input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CheckVisitQrQuery(input?.PlannedVisitId ?? Guid.Empty, input?.QrPayload, input?.Kind), cancellationToken));

    [HttpPut("api/crm/visit-execution/{id:guid}/capture")]
    [HasPermission(Perms.Record)]
    public async Task<IActionResult> Capture(Guid id, [FromBody] VisitCaptureInput capture, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CaptureVisitExecutionCommand(id, capture), cancellationToken));

    /// <summary>Finishes the visit with the last capture (optional body). <c>finishedAt</c> is the server's.</summary>
    [HttpPost("api/crm/visit-execution/{id:guid}/finish")]
    [HasPermission(Perms.Record)]
    public async Task<IActionResult> Finish(Guid id, [FromBody] VisitCaptureInput? capture, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new FinishVisitExecutionCommand(id, capture), cancellationToken));

    /// <summary>The doctor's signature. multipart/form-data: file (png), confirmed (bool), context (approval | sample).</summary>
    [HttpPost("api/crm/visit-execution/{id:guid}/signature")]
    [HasPermission(Perms.Record)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Signature(
        Guid id, IFormFile? file, [FromForm] bool confirmed, [FromForm] string? context, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SignVisitExecutionCommand(id, await ReadAsync(file, cancellationToken), confirmed, context), cancellationToken));

    [HttpGet("api/crm/visit-execution/{id:guid}/photo")]
    [HasPermission(Perms.Read)]
    public Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
        => StreamAsync(id, VisitEvidenceKinds.Photo, cancellationToken);

    [HttpGet("api/crm/visit-execution/{id:guid}/signature")]
    [HasPermission(Perms.Read)]
    public Task<IActionResult> SignatureImage(Guid id, CancellationToken cancellationToken)
        => StreamAsync(id, VisitEvidenceKinds.Signature, cancellationToken);

    /// <summary>K-W2 — the tenant's evidence rules (stored or the defaults; the read writes nothing). WP-VW-W3 (W3-BE-c,
    /// K-C4) — read with crm.visit-evidence-policy.read OR .manage.</summary>
    [HttpGet("api/crm/visit-evidence-policy")]
    public async Task<IActionResult> GetPolicy(CancellationToken cancellationToken)
        => VisitEvidenceSettingsController.MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitEvidencePolicyQuery(), cancellationToken))
            : CreateActionResultInstance(Diten.CrmService.Application.Common.Models.Response<object>.Fail(new[]
            {
                "Reading the visit evidence policy needs crm.visit-evidence-policy.read or .manage.", "permission_denied"
            }, 403));

    [HttpPut("api/crm/visit-evidence-policy")]
    [HasPermission(Perms.PolicyManage)]
    public async Task<IActionResult> PutPolicy([FromBody] VisitEvidencePolicyInput input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateVisitEvidencePolicyCommand(input), cancellationToken));

    private async Task<IActionResult> StreamAsync(Guid id, string kind, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetVisitEvidenceContentQuery(id, kind), cancellationToken);
        return !response.IsSuccessful || response.Data is null
            ? CreateActionResultInstance(response)
            : File(response.Data.Content, response.Data.MediaType, response.Data.FileName);
    }

    internal static async Task<VisitEvidenceFile?> ReadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new VisitEvidenceFile(file.FileName, file.ContentType ?? string.Empty, buffer.ToArray());
    }

    private static bool TryNumber(string? raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
