using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU01 — TenantShell controlled-document API (Layer 1 enforced here via <c>[HasPermission]</c>; Layer 2
/// AccessPolicy is enforced in the services). Thin controller; dispatches via MediatR; downloads stream through
/// the content-storage seam after the handler authorizes access (no direct public file URL).
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementControlledDocumentsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;
    private readonly IContentStorageGateway _storage;

    public DocumentManagementControlledDocumentsController(IMediator mediator, ICorrelationContext correlationContext, IContentStorageGateway storage)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _storage = storage;
    }

    [HttpPost("controlled-documents")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateControlledDocumentApiRequest request, CancellationToken ct)
    {
        var input = new CreateControlledDocumentInput(
            request.CollectionInstanceId,
            request.CompanyId,
            request.Title,
            request.DocumentType,
            request.Description,
            request.Tags,
            request.Controlled,
            request.EffectiveDate,
            request.ReviewDate,
            request.ExpiryDate,
            ApiRequestMapper.ToFileInput(request.File),
            request.ChangeSummary,
            ApiRequestMapper.ToAccessPolicy(request.AccessPolicy));
        return CreateActionResultInstance(await _mediator.Send(new CreateControlledDocumentCommand(input, CorrelationId), ct));
    }

    [HttpGet("controlled-documents")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? collectionInstanceId,
        [FromQuery] bool includeNonEffective,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetControlledDocumentListQuery(collectionInstanceId, CorrelationId, includeNonEffective), ct));

    // Explorer: active instantiated Documentation Structures for the company (never raw published baselines).
    [HttpGet("documentation-structures")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> DocumentationStructures([FromQuery] Guid companyId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetActiveDocumentationStructuresQuery(companyId, CorrelationId), ct));

    // Explorer: server-side, permission-filtered mixed search within a selected structure.
    [HttpGet("controlled-documents/search")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid companyId,
        [FromQuery] Guid activeStructureId,
        [FromQuery] Guid? collectionInstanceId,
        [FromQuery] string? scope,
        [FromQuery] string? query,
        [FromQuery] string? documentType,
        [FromQuery] bool includeTemplates,
        [FromQuery] bool includeNonEffective,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var input = new ExplorerSearchInput(companyId, activeStructureId, collectionInstanceId, ParseScope(scope), query, documentType, includeTemplates, status, includeNonEffective);
        return CreateActionResultInstance(await _mediator.Send(new SearchControlledDocumentsQuery(input, CorrelationId), ct));
    }

    [HttpGet("controlled-documents/{documentId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> Detail(Guid documentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetControlledDocumentByIdQuery(documentId, CorrelationId), ct));

    [HttpPut("controlled-documents/{documentId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Edit(Guid documentId, [FromBody] EditControlledDocumentApiRequest request, CancellationToken ct)
    {
        var input = new EditControlledDocumentInput(request.Title, request.Description, request.Tags, request.EffectiveDate, request.ReviewDate, request.ExpiryDate);
        return CreateActionResultInstance(await _mediator.Send(new EditControlledDocumentCommand(documentId, input, CorrelationId), ct));
    }

    [HttpPost("controlled-documents/{documentId:guid}/versions")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionCreate)]
    public async Task<IActionResult> CreateVersion(Guid documentId, [FromBody] CreateVersionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new CreateControlledDocumentVersionCommand(documentId, ApiRequestMapper.ToFileInput(request.File), request.ChangeSummary, request.AllowUnchanged, CorrelationId), ct));

    [HttpGet("controlled-documents/{documentId:guid}/versions")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionView)]
    public async Task<IActionResult> Versions(Guid documentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetControlledDocumentVersionsQuery(documentId, CorrelationId), ct));

    [HttpGet("controlled-documents/{documentId:guid}/versions/{versionId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionView)]
    public async Task<IActionResult> Version(Guid documentId, Guid versionId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetControlledDocumentVersionByIdQuery(documentId, versionId, CorrelationId), ct));

    [HttpGet("controlled-documents/{documentId:guid}/versions/{versionId:guid}/download")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsVersionView)]
    public async Task<IActionResult> Download(Guid documentId, Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DownloadControlledDocumentVersionQuery(documentId, versionId, CorrelationId), ct);
        return await StreamAsync(response, ct);
    }

    [HttpPost("controlled-documents/{documentId:guid}/share")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsShare)]
    public async Task<IActionResult> Share(Guid documentId, [FromBody] ShareItemApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ShareControlledDocumentCommand(documentId, request.TargetCompanyId, request.ShareMode, CorrelationId), ct));

    // Soft delete (archive) — no hard delete (pack §4/§8). Gated by the create/manage Layer 1 key (no delete
    // key exists) + Layer 2 edit grant in the service.
    [HttpDelete("controlled-documents/{documentId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteControlledDocumentCommand(documentId, CorrelationId), ct));

    // Move a document to another folder within the same company (cross-company is a share, not a move).
    [HttpPost("controlled-documents/{documentId:guid}/move")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Move(Guid documentId, [FromBody] MoveDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new MoveControlledDocumentCommand(documentId, request.TargetCollectionInstanceId, CorrelationId), ct));

    // Per-user favorite ("star") toggle — only needs view access.
    [HttpPost("controlled-documents/{documentId:guid}/favorite")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> ToggleFavorite(Guid documentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ToggleControlledDocumentFavoriteCommand(documentId, CorrelationId), ct));

    // Copy a document into another authorized folder (same company); source unchanged.
    [HttpPost("controlled-documents/{documentId:guid}/copy")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Copy(Guid documentId, [FromBody] CopyDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CopyControlledDocumentCommand(documentId, request.TargetCollectionInstanceId, request.TitleOverride, CorrelationId), ct));

    private static ExplorerSearchScope ParseScope(string? scope) => (scope?.Trim().ToLowerInvariant()) switch
    {
        "currentfolder" or "current_folder" or "current-folder" => ExplorerSearchScope.CurrentFolder,
        "subtree" => ExplorerSearchScope.Subtree,
        _ => ExplorerSearchScope.Structure
    };

    private async Task<IActionResult> StreamAsync(Response<DocumentDownloadResult> response, CancellationToken ct)
    {
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        var stream = await _storage.OpenReadAsync(response.Data.StorageProvider, response.Data.ObjectKey, ct);
        if (!stream.IsSuccessful || stream.Data is null)
        {
            return CreateActionResultInstance(Response<DocumentDownloadResult>.Fail(
                stream.Errors, stream.StatusCode == 0 ? 503 : stream.StatusCode, stream.ReasonCode, response.CorrelationId));
        }

        return File(stream.Data.Content, response.Data.MediaType, response.Data.FileName);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
