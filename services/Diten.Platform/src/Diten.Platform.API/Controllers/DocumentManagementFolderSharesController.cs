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

/// <summary>MOD-0029-FU01 — folder/branch share dry-run → execute (same flow correlation id) + operation status.</summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementFolderSharesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementFolderSharesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("folder-shares/dry-run")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.FolderSharesCreate)]
    public async Task<IActionResult> DryRun([FromBody] FolderShareApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DryRunFolderShareCommand(ToInput(request), CorrelationId), ct));

    [HttpPost("folder-shares/execute")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.FolderSharesCreate)]
    public async Task<IActionResult> Execute([FromBody] FolderShareApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ExecuteFolderShareCommand(ToInput(request), CorrelationId), ct));

    [HttpGet("folder-shares/{operationId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.FolderSharesView)]
    public async Task<IActionResult> GetOperation(Guid operationId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetFolderShareOperationQuery(operationId, CorrelationId), ct));

    private static FolderShareInput ToInput(FolderShareApiRequest request) =>
        new(request.SourceBranchCollectionInstanceId, request.TargetCompanyId, request.IncludeTemplates, request.ShareMode);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
