using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementTraining;
using Diten.Platform.Application.Features.DocumentManagementTraining.Commands;
using Diten.Platform.Application.Features.DocumentManagementTraining.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU11 — TenantShell training matrix API (GMG-QMS-SOP-0001 §7.3, §17). Thin controller; dispatches via
/// MediatR. Training readiness feeds FU10 Gate 5. This is a matrix FOUNDATION, not an LMS. Layer 1 RBAC REUSES the
/// seeded controlled-documents create/view keys (no AuthService seed change); dedicated
/// <see cref="DocumentTrainingPermissions"/> keys should be seeded in FU06A hardening. TenantId is never read from the
/// client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementTrainingController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementTrainingController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("document-master-register/{id:guid}/training-matrix/resolve")]
    [HasPermission(DocumentTrainingPermissions.Manage)]
    public async Task<IActionResult> ResolveMatrix(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ResolveTrainingMatrixCommand(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/training-matrix/requirements")]
    [HasPermission(DocumentTrainingPermissions.View)]
    public async Task<IActionResult> Requirements(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTrainingRequirementsQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/training-matrix/requirements")]
    [HasPermission(DocumentTrainingPermissions.Manage)]
    public async Task<IActionResult> AddRequirement(Guid id, [FromBody] AddManualTrainingRequirementApiRequest request, CancellationToken ct)
    {
        var input = new AddManualTrainingRequirementInput(
            request.AudienceType, request.RequiredRole, request.RequiredUserId, request.RequiredDepartment, request.TrainingType,
            request.IsCriticalProcessUserRequirement, request.EffectivenessCheckRequired, request.AcknowledgementRequired, request.MandatoryBeforeEffective);
        return CreateActionResultInstance(await _mediator.Send(new AddManualTrainingRequirementCommand(id, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/training-assignments")]
    [HasPermission(DocumentTrainingPermissions.Manage)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTrainingApiRequest request, CancellationToken ct)
    {
        var input = new AssignTrainingInput(request.RequirementId, request.AssignedToUserId, request.AssignedToRole, request.AssignedToDepartment, request.DueDate);
        return CreateActionResultInstance(await _mediator.Send(new AssignTrainingCommand(id, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/training-assignments/{assignmentId:guid}/complete")]
    [HasPermission(DocumentTrainingPermissions.Verify)]
    public async Task<IActionResult> Complete(Guid id, Guid assignmentId, [FromBody] CompleteTrainingApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CompleteTrainingCommand(id, assignmentId, new CompleteTrainingInput(request.CompletionEvidenceReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/training-assignments/{assignmentId:guid}/effectiveness")]
    [HasPermission(DocumentTrainingPermissions.Verify)]
    public async Task<IActionResult> Effectiveness(Guid id, Guid assignmentId, [FromBody] RecordTrainingEffectivenessApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordTrainingEffectivenessCommand(id, assignmentId, new RecordEffectivenessInput(request.Passed, request.EvidenceReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/training-assignments/{assignmentId:guid}/restrict")]
    [HasPermission(DocumentTrainingPermissions.Manage)]
    public async Task<IActionResult> Restrict(Guid id, Guid assignmentId, [FromBody] RestrictTrainingApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RestrictTrainingCommand(id, assignmentId, new RestrictTrainingInput(request.Reason), CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/training-readiness")]
    [HasPermission(DocumentTrainingPermissions.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTrainingReadinessQuery(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
