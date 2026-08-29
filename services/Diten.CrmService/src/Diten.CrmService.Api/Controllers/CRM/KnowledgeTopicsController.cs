using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Topic.Commands;
using Diten.CrmService.Application.Features.Knowledge.Topic.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.KnowledgePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU02 — Topic taxonomy authoring (subject-scoped, hierarchical). Canonical under
/// <c>/api/crm/knowledge/topics</c>. A cross-subject parent, self-parent or parent cycle is rejected 400.
/// <b>No delete endpoint</b>: closing a topic is Archive.
/// </summary>
[Authorize]
public sealed class KnowledgeTopicsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeTopicsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/knowledge/topics")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListTopicsQuery(subjectId, status, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/topics/{topicId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid topicId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTopicQuery(topicId), cancellationToken));

    [HttpPost("api/crm/knowledge/topics")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateTopicCommand(
                request.SubjectId, request.TopicCode, request.TopicName, request.EffectiveFrom, request.ParentTopicId,
                request.Description, request.Status, request.SortOrder, request.EffectiveTo, request.Alias,
                request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/knowledge/topics/{topicId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateTopicCommand(
                topicId, request.TopicName, request.EffectiveFrom, request.ParentTopicId, request.Description,
                request.Status, request.SortOrder, request.EffectiveTo, request.Alias, request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/knowledge/topics/{topicId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid topicId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveTopicCommand(topicId), cancellationToken));

    [HttpPost("api/crm/knowledge/topics/{topicId:guid}/unarchive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Unarchive(Guid topicId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UnarchiveTopicCommand(topicId), cancellationToken));
}
