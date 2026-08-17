using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Application.Features.Audit.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// Phase 5 baseline: audit read/export/redaction/retention endpoints are restricted to
/// platform_admin actor type only. Partner admin audit scope support (per-tenant filter
/// for query/export, partner-scoped redaction, per-tenant retention) is follow-up.
/// Reason: AuditFilterParser and RedactActorPiiForPlatformCrossTenantAsync do not yet
/// enforce partner allowed-tenant scope; permitting partner_admin would allow
/// cross-partner data leak (Phase 5A review H1/H2).
/// </summary>
[ApiController]
[Route("api/platform/audit")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformAuditController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PlatformAuditController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("events")]
    [HasPermission("platform.audit.read")]
    public async Task<IActionResult> GetEvents([FromQuery] AuditEventFilterRequest filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetAuditEventListQuery(filter), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("events/{id:guid}")]
    [HasPermission("platform.audit.read")]
    public async Task<IActionResult> GetEventById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetAuditEventByIdQuery(id), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("export")]
    [HasPermission("platform.audit.export")]
    public async Task<IActionResult> Export([FromQuery] AuditExportRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ExportAuditEventsQuery(request), ct);
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        Response.Headers["X-Audit-Export-Row-Count"] = response.Data.RowCount.ToString();
        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }

    [HttpGet("export-limits")]
    [HasPermission("platform.audit.read")]
    public IActionResult GetExportLimits()
    {
        return CreateActionResultInstance(Response<AuditExportLimitsDto>.Success(
            new AuditExportLimitsDto(AuditExportLimits.MaxRows, AuditExportLimits.MaxDays)));
    }

    [HttpGet("retention")]
    [HasPermission("platform.audit.retention.update")]
    public async Task<IActionResult> GetRetentionPolicies(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetAuditRetentionPoliciesQuery(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("retention")]
    [HasPermission("platform.audit.retention.update")]
    public async Task<IActionResult> UpdateRetention([FromBody] UpdateAuditRetentionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateAuditRetentionCommand(request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("redact-actor")]
    [HasPermission("platform.audit.redact-actor")]
    public async Task<IActionResult> RedactActor([FromBody] RedactAuditActorRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RedactAuditActorCommand(request), ct);
        return CreateActionResultInstance(response);
    }
}
