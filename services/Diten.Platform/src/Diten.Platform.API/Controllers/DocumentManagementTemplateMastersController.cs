using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateMasters.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>MOD-0029-FU02 — TenantShell corporate template master API. Thin MediatR controller.</summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementTemplateMastersController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;
    private readonly IContentStorageGateway _storage;

    public DocumentManagementTemplateMastersController(IMediator mediator, ICorrelationContext correlationContext, IContentStorageGateway storage)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
        _storage = storage;
    }

    [HttpGet("template-masters")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? classification,
        [FromQuery] Guid? collectionDefinitionId,
        [FromQuery] string? canonicalId,
        [FromQuery] string? variantPolicy,
        CancellationToken ct)
    {
        var filter = new TemplateMasterListFilter(status, classification, collectionDefinitionId, canonicalId, variantPolicy);
        return CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterListQuery(filter, CorrelationId), ct));
    }

    [HttpGet("template-masters/{id:guid}")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterByIdQuery(id, CorrelationId), ct));

    [HttpPost("template-masters")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateMasterApiRequest request, CancellationToken ct)
    {
        var input = new CreateTemplateMasterInput(
            request.MasterCode,
            request.TemplateName,
            request.Description,
            request.Classification,
            request.CollectionDefinitionId,
            request.CanonicalId,
            request.VariantPolicy,
            request.OwnerCompanyId,
            request.OwnerUserId,
            request.EffectiveDate);
        return CreateActionResultInstance(await _mediator.Send(new CreateTemplateMasterCommand(input, CorrelationId), ct));
    }

    [HttpPost("template-masters/{id:guid}/versions/publish")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.VersionPublish)]
    public async Task<IActionResult> PublishVersion(Guid id, [FromBody] PublishTemplateMasterVersionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new PublishTemplateMasterVersionCommand(id, ApiRequestMapper.ToFileInput(request.File), request.ChangeSummary, request.AllowUnchanged, CorrelationId),
            ct));

    [HttpPost("template-masters/{id:guid}/deprecate")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.Deprecate)]
    public async Task<IActionResult> Deprecate(Guid id, [FromBody] DeprecateTemplateMasterApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeprecateTemplateMasterCommand(id, new DeprecateTemplateMasterInput(request.DeprecationReason), CorrelationId), ct));

    [HttpGet("template-masters/{id:guid}/versions")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.View)]
    public async Task<IActionResult> Versions(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterVersionsQuery(id, CorrelationId), ct));

    // Streams a version's file through the content-storage seam after the handler authorizes access (no direct
    // public file URL), mirroring the controlled-document / template download flow.
    [HttpGet("template-masters/{id:guid}/versions/{versionId:guid}/download")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.View)]
    public async Task<IActionResult> DownloadVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DownloadTemplateMasterVersionQuery(id, versionId, CorrelationId), ct);
        return await StreamAsync(response, ct);
    }

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

    [HttpGet("template-masters/{id:guid}/adoption-impact")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.ImpactView)]
    public async Task<IActionResult> AdoptionImpact(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterAdoptionImpactQuery(id, CorrelationId), ct));

    [HttpGet("template-master-options")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.View)]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterOptionsQuery(CorrelationId), ct));

    [HttpDelete("template-masters/bulk")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.Delete)]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new BulkDeleteTemplateMasterCommand(ids ?? [], CorrelationId), ct));

    [HttpDelete("template-masters/{id:guid}")]
    [HasPermission(DocumentManagementTemplateMasterPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteTemplateMasterCommand(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
