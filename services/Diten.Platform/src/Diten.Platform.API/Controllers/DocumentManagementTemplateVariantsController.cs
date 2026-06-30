using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;
using Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>MOD-0029-FU03 — TenantShell template variant governance + drift API. Thin MediatR controller.</summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementTemplateVariantsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementTemplateVariantsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("template-variants")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? templateMasterId,
        [FromQuery] string? scopeType,
        [FromQuery] Guid? scopeId,
        [FromQuery] string? status,
        [FromQuery] string? approvalStatus,
        CancellationToken ct)
    {
        var filter = new TemplateVariantListFilter(templateMasterId, scopeType, scopeId, status, approvalStatus);
        return CreateActionResultInstance(await _mediator.Send(new GetTemplateVariantListQuery(filter, CorrelationId), ct));
    }

    [HttpGet("template-variants/{id:guid}")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateVariantByIdQuery(id, CorrelationId), ct));

    [HttpPost("template-variants")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateVariantApiRequest request, CancellationToken ct)
    {
        var input = new CreateTemplateVariantInput(
            request.TemplateMasterId,
            request.TemplateMasterVersionId,
            request.VariantCode,
            request.VariantName,
            request.Description,
            request.ScopeType,
            request.ScopeId,
            request.TargetCollectionInstanceId,
            request.ContentSource,
            // Only forward a file input when one was actually uploaded; ToFileInput(null) would otherwise return a
            // non-null empty payload and trip the "local file not allowed under MasterVersion" guard.
            request.LocalFile is null ? null : ApiRequestMapper.ToFileInput(request.LocalFile),
            request.OwnerCompanyId,
            request.OwnerUserId,
            request.Status);
        return CreateActionResultInstance(await _mediator.Send(new CreateTemplateVariantCommand(input, CorrelationId), ct));
    }

    [HttpGet("template-variants/{id:guid}/compare")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.Compare)]
    public async Task<IActionResult> Compare(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateVariantCompareQuery(id, CorrelationId), ct));

    [HttpPost("template-variants/{id:guid}/rebase")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.Rebase)]
    public async Task<IActionResult> Rebase(Guid id, [FromBody] RebaseTemplateVariantApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new RebaseTemplateVariantCommand(id, new RebaseTemplateVariantInput(request?.TargetMasterVersionId), CorrelationId), ct));

    [HttpGet("template-variant-options")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.View)]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateVariantOptionsQuery(CorrelationId), ct));

    [HttpGet("template-masters/{id:guid}/variants")]
    [HasPermission(DocumentManagementTemplateVariantPermissions.View)]
    public async Task<IActionResult> ByMaster(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemplateMasterVariantsQuery(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
