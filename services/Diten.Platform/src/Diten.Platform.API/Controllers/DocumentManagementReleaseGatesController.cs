using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Commands;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU10 — TenantShell non-waivable release gate API (GMG-QMS-SOP-0001 §19/§21). Thin controller; dispatches
/// via MediatR. Gate RESULTS are computed by the engine — a client can only record evidence, never set a result or the
/// (permanently-false) exception field. Layer 1 RBAC REUSES the seeded controlled-documents create/view keys (no
/// AuthService seed change); dedicated <see cref="DocumentReleaseGatePermissions"/> keys should be seeded in FU06A
/// hardening. TenantId is never read from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementReleaseGatesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementReleaseGatesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("document-master-register/{id:guid}/release-gates/evaluate")]
    [HasPermission(DocumentReleaseGatePermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateReleaseGatesCommand(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/release-gates")]
    [HasPermission(DocumentReleaseGatePermissions.View)]
    public async Task<IActionResult> Latest(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLatestReleaseGateEvaluationQuery(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/release-gates/history")]
    [HasPermission(DocumentReleaseGatePermissions.View)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReleaseGateHistoryQuery(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/release-readiness")]
    [HasPermission(DocumentReleaseGatePermissions.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReleaseReadinessQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/release-gates/{gateKey}/evidence")]
    [HasPermission(DocumentReleaseGatePermissions.RecordEvidence)]
    public async Task<IActionResult> RecordEvidence(Guid id, string gateKey, [FromBody] RecordReleaseGateEvidenceApiRequest request, CancellationToken ct)
    {
        var input = new RecordReleaseGateEvidenceInput(
            gateKey, request.EvidenceReference, request.VerifiedByUserId, request.VerifiedByRole, request.VerificationDate, request.Comment);
        return CreateActionResultInstance(await _mediator.Send(new RecordReleaseGateEvidenceCommand(id, input, CorrelationId), ct));
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
