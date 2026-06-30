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

/// <summary>MOD-0029-FU01 — TenantShell template API (Layer 1 via <c>[HasPermission]</c>; Layer 2 in services).</summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementTemplatesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;
    private readonly IContentStorageGateway _storage;

    public DocumentManagementTemplatesController(IMediator mediator, ICorrelationContext correlationContext, IContentStorageGateway storage)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _storage = storage;
    }

    [HttpPost("templates")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesCreate)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateApiRequest request, CancellationToken ct)
    {
        var input = new CreateTemplateInput(
            request.CompanyId,
            request.CollectionInstanceId,
            request.Title,
            request.Description,
            request.Tags,
            ApiRequestMapper.ToFlags(request.Flags),
            ApiRequestMapper.ToFileInput(request.File),
            request.ChangeSummary);
        return CreateActionResultInstance(await _mediator.Send(new CreateTemplateDocumentCommand(input, CorrelationId), ct));
    }

    [HttpGet("templates")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesView)]
    public async Task<IActionResult> List([FromQuery] Guid? collectionInstanceId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateListQuery(collectionInstanceId, CorrelationId), ct));

    [HttpGet("templates/{templateId:guid}")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesView)]
    public async Task<IActionResult> Detail(Guid templateId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateByIdQuery(templateId, CorrelationId), ct));

    [HttpPost("templates/{templateId:guid}/versions")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesVersionCreate)]
    public async Task<IActionResult> CreateVersion(Guid templateId, [FromBody] CreateVersionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new CreateTemplateVersionCommand(templateId, ApiRequestMapper.ToFileInput(request.File), request.ChangeSummary, request.AllowUnchanged, CorrelationId), ct));

    [HttpGet("templates/{templateId:guid}/versions")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesView)]
    public async Task<IActionResult> Versions(Guid templateId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateVersionsQuery(templateId, CorrelationId), ct));

    [HttpGet("templates/{templateId:guid}/versions/{versionId:guid}/download")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesView)]
    public async Task<IActionResult> Download(Guid templateId, Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DownloadTemplateVersionQuery(templateId, versionId, CorrelationId), ct);
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

    [HttpPost("templates/{templateId:guid}/share")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesShare)]
    public async Task<IActionResult> Share(Guid templateId, [FromBody] ShareItemApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ShareTemplateCommand(templateId, request.TargetCompanyId, request.ShareMode, CorrelationId), ct));

    [HttpPost("templates/{templateId:guid}/copy")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.TemplatesCreate)]
    public async Task<IActionResult> Copy(Guid templateId, [FromBody] CopyDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CopyTemplateCommand(templateId, request.TargetCollectionInstanceId, request.TitleOverride, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
