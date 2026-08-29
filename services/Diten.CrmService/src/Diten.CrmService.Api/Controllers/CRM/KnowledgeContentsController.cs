using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Application.Features.Knowledge.Content.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.KnowledgePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU02 — KnowledgeContent authoring. Canonical under <c>/api/crm/knowledge/contents</c>, exposed through the
/// dedicated <c>knowledge</c> ocelot routes (no direct-to-5061 business surface). Permissions run on the documented
/// fallback (<c>crm.territory.read</c> reads, <c>crm.territory.model.manage</c> writes) until MOD-0162-FU02-RBAC lands.
/// <b>There is no delete endpoint</b>: closing content is Archive, so content history stays readable.
/// </summary>
[Authorize]
public sealed class KnowledgeContentsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeContentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/knowledge/contents")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? contentType,
        [FromQuery] string? contentStatus,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? topicId,
        [FromQuery] Guid? audienceProfileId,
        [FromQuery] string? languageCode,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? campaignId,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListKnowledgeContentQuery(
                contentType, contentStatus, subjectId, topicId, audienceProfileId, languageCode, brandId, productId,
                campaignId, effectiveAt, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/contents/{contentId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid contentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetKnowledgeContentQuery(contentId), cancellationToken));

    [HttpPost("api/crm/knowledge/contents")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgeContentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgeContentCommand(
                request.ContentCode, request.ContentTitle, request.ContentType, request.SubjectId,
                request.LanguageCode, request.ContentVersion, request.EffectiveFrom, request.ContentStatus,
                request.TopicId, request.AudienceProfileId, request.ConceptNodeId, request.BrandId, request.ProductId,
                request.CampaignId, request.SegmentId, request.Summary, request.ContentBodyRef, request.ContentAssetRef,
                request.FileRef, request.Url, request.EffectiveTo, request.Source, request.Tags,
                request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/knowledge/contents/{contentId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid contentId, [FromBody] UpdateKnowledgeContentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateKnowledgeContentCommand(
                contentId, request.ContentTitle, request.ContentType, request.SubjectId, request.LanguageCode,
                request.ContentVersion, request.EffectiveFrom, request.ContentStatus, request.TopicId,
                request.AudienceProfileId, request.ConceptNodeId, request.BrandId, request.ProductId,
                request.CampaignId, request.SegmentId, request.Summary, request.ContentBodyRef, request.ContentAssetRef,
                request.FileRef, request.Url, request.EffectiveTo, request.Source, request.Tags,
                request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/knowledge/contents/{contentId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid contentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgeContentCommand(contentId), cancellationToken));
}
