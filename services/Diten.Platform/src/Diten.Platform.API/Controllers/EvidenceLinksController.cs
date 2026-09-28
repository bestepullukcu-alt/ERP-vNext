using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.EvidenceLinking;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.EvidenceLinking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0031 Evidence Linking — slice 1 HTTP surface (<c>api/v1/evidence</c>). Thin: maps and dispatches via MediatR.
/// Layer 1 = <c>platform.evidence.links.read/manage</c>; Layer 2 (can the caller read the document?) is re-checked in
/// the handlers through the MOD-0029 access evaluator. There is no update and no delete endpoint: a link is immutable
/// and is closed with <c>remove</c> (the record stays).
/// </summary>
[ApiController]
[Route("api/v1/evidence")]
[Authorize]
public sealed class EvidenceLinksController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public EvidenceLinksController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("links")]
    [HasPermission(EvidenceLinkPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateEvidenceLinkRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEvidenceLinkCommand(
            request.ObjectRef is null
                ? null
                : new EvidenceObjectRefInput(request.ObjectRef.Module, request.ObjectRef.ObjectType,
                    request.ObjectRef.ObjectId, request.ObjectRef.ObjectVersion),
            request.DocumentKind,
            request.DocumentId,
            request.DocumentVersionId,
            request.EvidenceTypeCode,
            request.Locator is null
                ? null
                : new EvidenceLocatorInput(request.Locator.Quote, request.Locator.Section, request.Locator.Page,
                    request.Locator.Table),
            request.SupportedSpans?.Select(s => new EvidenceSupportedSpanInput(s.LanguageCode, s.Text, s.Start, s.End))
                .ToList(),
            CorrelationId), ct));

    [HttpPost("links/{id:guid}/remove")]
    [HasPermission(EvidenceLinkPermissions.Manage)]
    public async Task<IActionResult> Remove(Guid id, [FromBody] RemoveEvidenceLinkRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new RemoveEvidenceLinkCommand(id, request?.Reason, CorrelationId), ct));

    [HttpGet("links")]
    [HasPermission(EvidenceLinkPermissions.Read)]
    public async Task<IActionResult> ListByObject(
        [FromQuery] string? module,
        [FromQuery] string? objectType,
        [FromQuery] string? objectId,
        [FromQuery] string? objectVersion,
        [FromQuery] bool includeRemoved = false,
        CancellationToken ct = default) =>
        CreateActionResultInstance(await _mediator.Send(new GetEvidenceLinksByObjectQuery(
            module, objectType, objectId, objectVersion, includeRemoved, CorrelationId), ct));

    [HttpGet("links/{id:guid}")]
    [HasPermission(EvidenceLinkPermissions.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEvidenceLinkByIdQuery(id, CorrelationId), ct));

    [HttpGet("links/by-document/{documentId:guid}")]
    [HasPermission(EvidenceLinkPermissions.Read)]
    public async Task<IActionResult> ByDocument(Guid documentId, [FromQuery] Guid? versionId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetEvidenceLinksByDocumentQuery(documentId, versionId, CorrelationId), ct));

    [HttpGet("document-options")]
    [HasPermission(EvidenceLinkPermissions.Read)]
    public async Task<IActionResult> DocumentOptions(
        [FromQuery] string? search, [FromQuery] string? kind, [FromQuery] int? take, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetEvidenceDocumentOptionsQuery(search, kind, take, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
