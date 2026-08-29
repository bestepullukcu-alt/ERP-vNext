using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementApproval;
using Diten.Platform.Application.Features.DocumentManagementApproval.Commands;
using Diten.Platform.Application.Features.DocumentManagementApproval.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU09 — TenantShell approval route + evidence API (GMG-QMS-SOP-0001 §5, §7.2). Thin controller; dispatches
/// via MediatR. This is approval REQUIREMENT + EVIDENCE + SEGREGATION, not a workflow engine. Layer 1 RBAC REUSES the
/// seeded controlled-documents create/view keys (no AuthService seed change); the dedicated
/// <see cref="DocumentApprovalPermissions"/> keys should be seeded in FU06A hardening. TenantId is never read from the
/// client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementApprovalController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementApprovalController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("document-master-register/{id:guid}/approval-route/resolve")]
    [HasPermission(DocumentApprovalPermissions.Manage)]
    public async Task<IActionResult> ResolveRoute(Guid id, [FromBody] ResolveApprovalRouteApiRequest? request, CancellationToken ct)
    {
        var r = request ?? new ResolveApprovalRouteApiRequest();
        var input = new ResolveApprovalRouteInput(
            r.HasRaImpact, r.HasPvImpact, r.HasBatchReleaseImpact, r.HasDmsCsvImpact, r.HasQualityAgreementImpact,
            r.IsGroupGovernance, r.RequiresLegalReview, r.RequiresCeoEndorsement, r.RequiresIndependentTechnicalReview,
            r.AuthorUserId, r.RequestedByUserId);
        return CreateActionResultInstance(await _mediator.Send(new ResolveApprovalRouteCommand(id, input, CorrelationId), ct));
    }

    [HttpGet("document-master-register/{id:guid}/approval-requirements")]
    [HasPermission(DocumentApprovalPermissions.View)]
    public async Task<IActionResult> Requirements(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetApprovalRequirementsQuery(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/approval-readiness")]
    [HasPermission(DocumentApprovalPermissions.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetApprovalReadinessQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/approval-evidence")]
    [HasPermission(DocumentApprovalPermissions.RecordEvidence)]
    public async Task<IActionResult> RecordEvidence(Guid id, [FromBody] RecordApprovalEvidenceApiRequest request, CancellationToken ct)
    {
        var input = new RecordApprovalEvidenceInput(
            request.RequirementId, request.Action, request.PerformedByUserId, request.PerformedByRole,
            request.EvidenceReference, request.Comment);
        return CreateActionResultInstance(await _mediator.Send(new RecordApprovalEvidenceCommand(id, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/approval-evidence/reject")]
    [HasPermission(DocumentApprovalPermissions.RecordEvidence)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApprovalApiRequest request, CancellationToken ct)
    {
        var input = new RejectApprovalInput(request.RequirementId, request.PerformedByUserId, request.PerformedByRole, request.Reason, request.Comment);
        return CreateActionResultInstance(await _mediator.Send(new RejectApprovalCommand(id, input, CorrelationId), ct));
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
