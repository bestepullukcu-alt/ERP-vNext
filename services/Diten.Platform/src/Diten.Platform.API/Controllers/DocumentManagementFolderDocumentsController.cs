using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU01 — folder-detail attachments + Layer 2 folder access policy (sidecar). The access-policy
/// management endpoints are the MOD-0029 admin surface for Layer 2 grants; they are gated by the central Layer 1
/// <c>folder-documents.access.manage</c> key (they never live on the central permission screen).
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementFolderDocumentsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementFolderDocumentsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("folder-documents")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> FolderDocuments([FromQuery] Guid collectionInstanceId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetFolderDocumentsQuery(collectionInstanceId, CorrelationId), ct));

    [HttpGet("folder-documents/access")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.FolderDocumentsAccessManage)]
    public async Task<IActionResult> GetAccess([FromQuery] Guid collectionInstanceId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetFolderDocumentAccessQuery(collectionInstanceId, CorrelationId), ct));

    [HttpPost("folder-documents/access")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.FolderDocumentsAccessManage)]
    public async Task<IActionResult> UpsertAccess([FromBody] UpsertFolderAccessApiRequest request, CancellationToken ct)
    {
        var input = new UpsertFolderAccessInput(
            request.CollectionInstanceId, request.CompanyId, request.TargetType, request.TargetId,
            ApiRequestMapper.ToFolderPermissions(request.Permissions));
        return CreateActionResultInstance(await _mediator.Send(new UpsertFolderDocumentAccessCommand(input, CorrelationId), ct));
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
