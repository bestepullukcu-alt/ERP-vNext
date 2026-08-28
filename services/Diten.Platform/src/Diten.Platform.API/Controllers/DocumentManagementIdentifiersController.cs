using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Commands;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU07 — TenantShell identifier allocation API (GMG-QMS-SOP-0001 §6.3, §9.2). Thin controller; dispatches
/// via MediatR. Layer 1 RBAC REUSES the seeded controlled-documents create/view keys (no AuthService seed change);
/// dedicated <see cref="DocumentIdentifierPermissions"/> keys should be seeded in FU06A/FU07 hardening. TenantId is
/// never read from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementIdentifiersController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementIdentifiersController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("document-master-register/{id:guid}/allocate-uid")]
    [HasPermission(DocumentIdentifierPermissions.Allocate)]
    public async Task<IActionResult> AllocateUid(Guid id, [FromBody] AllocateIdentifierApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new AllocateUidCommand(id, new AllocateIdentifierInput(request?.AllocationReason), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/allocate-code")]
    [HasPermission(DocumentIdentifierPermissions.Allocate)]
    public async Task<IActionResult> AllocateCode(Guid id, [FromBody] AllocateIdentifierApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new AllocateCodeCommand(id, new AllocateIdentifierInput(request?.AllocationReason), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/allocate-identifiers")]
    [HasPermission(DocumentIdentifierPermissions.Allocate)]
    public async Task<IActionResult> AllocateIdentifiers(Guid id, [FromBody] AllocateIdentifierApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new AllocateIdentifiersCommand(id, new AllocateIdentifierInput(request?.AllocationReason), CorrelationId), ct));

    [HttpPost("document-identifiers/reserve")]
    [HasPermission(DocumentIdentifierPermissions.Reserve)]
    public async Task<IActionResult> Reserve([FromBody] ReserveIdentifierApiRequest request, CancellationToken ct)
    {
        var input = new ReserveIdentifierInput(
            request.IdentifierType, request.IdentifierValue, request.RegisterEntryId,
            request.AllocationReason, request.LegacyCode, request.SourceSystem, request.SourceLegacyId);
        return CreateActionResultInstance(await _mediator.Send(new ReserveIdentifierCommand(input, CorrelationId), ct));
    }

    [HttpPost("document-identifiers/{allocationId:guid}/cancel")]
    [HasPermission(DocumentIdentifierPermissions.Cancel)]
    public async Task<IActionResult> Cancel(Guid allocationId, [FromBody] CancelIdentifierApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new CancelIdentifierCommand(allocationId, new CancelIdentifierInput(request?.CancellationReason), CorrelationId), ct));

    [HttpGet("document-identifiers")]
    [HasPermission(DocumentIdentifierPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? identifierType,
        [FromQuery] string? allocationStatus,
        [FromQuery] Guid? registerEntryId,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetIdentifierAllocationsQuery(identifierType, allocationStatus, registerEntryId, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
