using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.ESignature;
using Diten.Platform.Application.Features.ESignature.Commands;
using Diten.Platform.Application.Features.ESignature.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/e-signatures/admin/tenants/{tenantId:guid}")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class ESignaturesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ESignaturesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("envelopes")]
    [HasPermission("platform.e-signatures.read")]
    public async Task<IActionResult> GetList(
        Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var response = await _mediator.Send(new GetSignatureEnvelopeListQuery(tenantId, page, pageSize), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("envelopes/{envelopeId:guid}")]
    [HasPermission("platform.e-signatures.read")]
    public async Task<IActionResult> GetById(Guid tenantId, Guid envelopeId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSignatureEnvelopeByIdQuery(tenantId, envelopeId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("envelopes")]
    [HasPermission("platform.e-signatures.verify")]
    public async Task<IActionResult> Create(
        Guid tenantId,
        [FromBody] CreateSignatureEnvelopeRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateSignatureEnvelopeCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("envelopes/{envelopeId:guid}/attestations")]
    [HasPermission("platform.e-signatures.verify")]
    public async Task<IActionResult> RecordAttestation(
        Guid tenantId,
        Guid envelopeId,
        [FromBody] RecordInternalAttestationRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new RecordInternalAttestationCommand(tenantId, envelopeId, request),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("envelopes/{envelopeId:guid}/cancel")]
    [HasPermission("platform.e-signatures.verify")]
    public async Task<IActionResult> Cancel(
        Guid tenantId,
        Guid envelopeId,
        [FromBody] CancelSignatureEnvelopeRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CancelSignatureEnvelopeCommand(tenantId, envelopeId, request),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("envelopes/{envelopeId:guid}/audit-export")]
    [HasPermission("platform.e-signatures.audit-export")]
    public async Task<IActionResult> GenerateAuditExport(
        Guid tenantId,
        Guid envelopeId,
        [FromQuery] Guid correlationId,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GenerateSignatureAuditExportCommand(tenantId, envelopeId, correlationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("artifacts/{artifactId:guid}")]
    [HasPermission("platform.e-signatures.read")]
    public async Task<IActionResult> DownloadArtifact(Guid tenantId, Guid artifactId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetSignatureArtifactQuery(tenantId, artifactId), ct);
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }
}
