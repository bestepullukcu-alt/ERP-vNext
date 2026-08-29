using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Commands;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU08 — TenantShell controlled document lifecycle API (GMG-QMS-SOP-0001 §6.2). Thin controller; dispatches
/// via MediatR through a GENERIC transition endpoint (target status in the body). Layer 1 RBAC REUSES the seeded
/// controlled-documents create/view keys (no AuthService seed change); the dedicated
/// <see cref="DocumentLifecyclePermissions"/> keys should be seeded in FU06A/FU09 hardening. TenantId is never read
/// from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementLifecycleController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementLifecycleController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("document-master-register/{id:guid}/lifecycle/transition")]
    [HasPermission(DocumentLifecyclePermissions.Manage)]
    public async Task<IActionResult> Transition(Guid id, [FromBody] TransitionDocumentLifecycleApiRequest request, CancellationToken ct)
    {
        var input = new TransitionDocumentLifecycleInput(
            request.TargetStatus, request.Reason, request.EvidenceReference, request.Comment,
            request.EffectiveDate, request.RelatedReplacementRegisterEntryId, request.ExpectedVersion);
        return CreateActionResultInstance(await _mediator.Send(new TransitionDocumentLifecycleCommand(id, input, CorrelationId), ct));
    }

    [HttpGet("document-master-register/{id:guid}/lifecycle")]
    [HasPermission(DocumentLifecyclePermissions.View)]
    public async Task<IActionResult> State(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLifecycleStateQuery(id, CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/lifecycle/transitions")]
    [HasPermission(DocumentLifecyclePermissions.View)]
    public async Task<IActionResult> Transitions(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLifecycleTransitionsQuery(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
