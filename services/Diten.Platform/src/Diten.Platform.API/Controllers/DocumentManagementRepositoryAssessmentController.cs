using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Commands;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU16 — TenantShell repository assessment / DMS boundary API (GMG-QMS-SOP-0001 §11). Thin controller;
/// dispatches via MediatR. It classifies a repository (validated DMS / approved interim / separate approval mechanism /
/// unapproved) and feeds FU10 Gate 2 — it makes NO validation claim and implements NO e-signature. Layer 1 RBAC REUSES
/// the seeded controlled-documents create/view keys (no AuthService seed change); dedicated
/// <see cref="DocumentRepositoryAssessmentPermissions"/> keys should be seeded in FU06A hardening. TenantId is never
/// read from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementRepositoryAssessmentController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementRepositoryAssessmentController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("repository-assessments")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.View)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryAssessmentsQuery(CorrelationId), ct));

    [HttpGet("repository-assessments/{id:guid}")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryAssessmentByIdQuery(id, CorrelationId), ct));

    [HttpGet("repository-assessments/{id:guid}/findings")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.View)]
    public async Task<IActionResult> Findings(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryAssessmentFindingsQuery(id, CorrelationId), ct));

    [HttpPost("repository-assessments")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] RepositoryAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateRepositoryAssessmentCommand(ToFields(request), CorrelationId), ct));

    [HttpPut("repository-assessments/{id:guid}")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RepositoryAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateRepositoryAssessmentCommand(id, ToFields(request), CorrelationId), ct));

    [HttpPost("repository-assessments/{id:guid}/evaluate")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Manage)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateRepositoryAssessmentCommand(id, CorrelationId), ct));

    [HttpPost("repository-assessments/{id:guid}/approve")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Approve)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRepositoryAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApproveRepositoryAssessmentCommand(id, new ApproveRepositoryAssessmentInput(request.ApprovedByRole, request.ValidUntil), CorrelationId), ct));

    [HttpPost("repository-assessments/{id:guid}/reject")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Approve)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRepositoryAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectRepositoryAssessmentCommand(id, new RejectRepositoryAssessmentInput(request.Reason), CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/repository-assessment")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.View)]
    public async Task<IActionResult> Linked(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLinkedRepositoryAssessmentQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/repository-assessment/link")]
    [HasPermission(DocumentRepositoryAssessmentPermissions.Manage)]
    public async Task<IActionResult> Link(Guid id, [FromBody] LinkRepositoryAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new LinkRepositoryAssessmentToRegisterEntryCommand(id, new LinkRepositoryAssessmentInput(request.RepositoryAssessmentId), CorrelationId), ct));

    private static RepositoryAssessmentFieldsInput ToFields(RepositoryAssessmentApiRequest r) => new(
        r.RepositoryName, r.RepositoryType, r.LocationType, r.RepositoryOwnerUserId, r.RepositoryOwnerRole, r.ExactLocation,
        r.AccessModelDescription, r.AccessReviewFrequency, r.BackupMethodDescription, r.RestoreTestFrequency,
        r.ApprovalMechanismDescription, r.EffectiveCopyControlDescription, r.AuditTrailDescription, r.ChangeControlDescription,
        r.ValidationEvidenceReference, r.MaxInterimPeriodDays, r.InterimCheckpointDueDate, r.MigrationReconciliationRequired,
        r.MigrationReconciliationReference, r.AssessmentEvidenceReference);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
