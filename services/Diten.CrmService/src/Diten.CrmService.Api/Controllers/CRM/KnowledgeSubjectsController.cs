using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Subject.Commands;
using Diten.CrmService.Application.Features.Knowledge.Subject.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.KnowledgePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU02 — Subject taxonomy authoring. Canonical under <c>/api/crm/knowledge/subjects</c>. Reads use
/// <c>crm.knowledge.subject.read</c> and writes <c>crm.knowledge.subject.manage</c> canonically, both on the documented
/// territory fallback until MOD-0162-FU02-RBAC lands. <b>No delete endpoint</b>: closing a subject is Archive, and
/// Unarchive restores it as <c>inactive</c>.
/// </summary>
[Authorize]
public sealed class KnowledgeSubjectsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeSubjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/knowledge/subjects")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListSubjectsQuery(status, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/subjects/{subjectId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid subjectId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetSubjectQuery(subjectId), cancellationToken));

    [HttpPost("api/crm/knowledge/subjects")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubjectRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateSubjectCommand(
                request.SubjectCode, request.SubjectName, request.EffectiveFrom, request.ParentSubjectId,
                request.Description, request.Status, request.SortOrder, request.EffectiveTo, request.Alias,
                request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/knowledge/subjects/{subjectId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid subjectId, [FromBody] UpdateSubjectRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateSubjectCommand(
                subjectId, request.SubjectName, request.EffectiveFrom, request.ParentSubjectId, request.Description,
                request.Status, request.SortOrder, request.EffectiveTo, request.Alias, request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/knowledge/subjects/{subjectId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid subjectId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveSubjectCommand(subjectId), cancellationToken));

    [HttpPost("api/crm/knowledge/subjects/{subjectId:guid}/unarchive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Unarchive(Guid subjectId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UnarchiveSubjectCommand(subjectId), cancellationToken));
}
