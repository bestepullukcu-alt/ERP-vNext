using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Commands;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU12 — TenantShell periodic review API (GMG-QMS-SOP-0001 §9.15, §15). Thin controller; dispatches via
/// MediatR. Layer 1 RBAC REUSES the seeded controlled-documents create/view keys (no AuthService seed change);
/// dedicated <see cref="DocumentPeriodicReviewPermissions"/> keys should be seeded in FU06A hardening. TenantId is
/// never read from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementPeriodicReviewController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementPeriodicReviewController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("document-master-register/{id:guid}/periodic-review")]
    [HasPermission(DocumentPeriodicReviewPermissions.View)]
    public async Task<IActionResult> Schedule(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPeriodicReviewScheduleQuery(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/periodic-review/escalations")]
    [HasPermission(DocumentPeriodicReviewPermissions.EscalationView)]
    public async Task<IActionResult> Escalations(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPeriodicReviewEscalationsQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/periodic-review/initiate")]
    [HasPermission(DocumentPeriodicReviewPermissions.Manage)]
    public async Task<IActionResult> Initiate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new InitiatePeriodicReviewCommand(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/periodic-review/{reviewId:guid}/complete")]
    [HasPermission(DocumentPeriodicReviewPermissions.Manage)]
    public async Task<IActionResult> Complete(Guid id, Guid reviewId, [FromBody] CompletePeriodicReviewApiRequest request, CancellationToken ct)
    {
        var input = new CompletePeriodicReviewInput(request.Decision, request.ReviewEvidenceReference, request.ImpactAssessmentReference, request.Comment);
        return CreateActionResultInstance(await _mediator.Send(new CompletePeriodicReviewCommand(id, reviewId, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/periodic-review/{reviewId:guid}/extension/request")]
    [HasPermission(DocumentPeriodicReviewPermissions.Manage)]
    public async Task<IActionResult> RequestExtension(Guid id, Guid reviewId, [FromBody] RequestPeriodicReviewExtensionApiRequest request, CancellationToken ct)
    {
        var input = new RequestPeriodicReviewExtensionInput(request.ExtensionDays, request.RiskAssessmentReference, request.Justification);
        return CreateActionResultInstance(await _mediator.Send(new RequestPeriodicReviewExtensionCommand(id, reviewId, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/periodic-review/{reviewId:guid}/extension/{extensionId:guid}/approve")]
    [HasPermission(DocumentPeriodicReviewPermissions.ApproveExtension)]
    public async Task<IActionResult> ApproveExtension(Guid id, Guid reviewId, Guid extensionId, [FromBody] ApprovePeriodicReviewExtensionApiRequest request, CancellationToken ct)
    {
        var input = new ApprovePeriodicReviewExtensionInput(request.ApproverRole, request.ManagementReviewEscalated, request.Comment);
        return CreateActionResultInstance(await _mediator.Send(new ApprovePeriodicReviewExtensionCommand(id, reviewId, extensionId, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/periodic-review/{reviewId:guid}/extension/{extensionId:guid}/reject")]
    [HasPermission(DocumentPeriodicReviewPermissions.ApproveExtension)]
    public async Task<IActionResult> RejectExtension(Guid id, Guid reviewId, Guid extensionId, [FromBody] RejectPeriodicReviewExtensionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectPeriodicReviewExtensionCommand(id, reviewId, extensionId, new RejectPeriodicReviewExtensionInput(request.Reason), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/periodic-review/evaluate-overdue")]
    [HasPermission(DocumentPeriodicReviewPermissions.Manage)]
    public async Task<IActionResult> EvaluateOverdue(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluatePeriodicReviewOverdueCommand(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
