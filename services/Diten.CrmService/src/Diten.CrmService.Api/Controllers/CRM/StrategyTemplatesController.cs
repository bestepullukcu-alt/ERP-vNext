using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.StrategyTemplate.StrategyTemplatePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0167 FU04 — StrategyTemplate authoring: a reusable playbook that BINDS a segment, a frequency intent, an MDM
/// product/SKU mix and MOD-0162 content, and produces nothing.
/// <para>There is <b>no DELETE and no PATCH</b> anywhere in this controller (closing a play is archive), and there is
/// deliberately <b>no /apply, /generate or /resolve</b> path: applying a play to a period is MOD-0155 (MicroTarget) and
/// membership resolution is MOD-0167 FU02. Those routes answer 404 here, and that 404 is part of the contract.</para>
/// <para>No endpoint returns a member, a member count or a subject id, so reading a play never implies the right to see
/// the people inside its segments. Under the documented DEV-ONLY fallback the activate separation of duty collapses
/// onto manage — a deliberate gap closed by F-RBAC.</para>
/// </summary>
[Authorize]
public sealed class StrategyTemplatesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public StrategyTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/strategy-templates")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? templateStatus,
        [FromQuery] string? subjectType,
        [FromQuery] string? businessUnitId,
        [FromQuery] string? templateCode,
        [FromQuery] Guid? segmentId,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListStrategyTemplatesQuery(
                templateStatus, subjectType, businessUnitId, templateCode, segmentId, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/strategy-templates/{templateId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetStrategyTemplateByIdQuery(templateId), cancellationToken));

    [HttpPost("api/crm/strategy-templates")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStrategyTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateStrategyTemplateCommand(
                request.TemplateCode, request.TemplateName, request.SubjectType,
                request.EffectiveFrom, request.EffectiveTo, request.BusinessUnitId,
                request.Description, request.Notes,
                MapSegments(request.SegmentBindings),
                request.FrequencyIntent?.ToInput(),
                MapProducts(request.ProductLines),
                MapContents(request.ContentBindings)),
            cancellationToken));

    [HttpPut("api/crm/strategy-templates/{templateId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid templateId, [FromBody] UpdateStrategyTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateStrategyTemplateCommand(
                templateId, request.TemplateName, request.EffectiveFrom, request.EffectiveTo,
                request.BusinessUnitId, request.Description, request.Notes,
                // A null list means "leave this binding alone" — that is what lets a frozen (active) play be renamed.
                MapSegments(request.SegmentBindings),
                request.FrequencyIntent?.ToInput(),
                MapProducts(request.ProductLines),
                MapContents(request.ContentBindings),
                request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/strategy-templates/{templateId:guid}/activate")]
    // Canonical crm.strategy-template.activate (F-RBAC); under the documented DEV-ONLY fallback it collapses onto
    // manage, so the author-is-not-activator separation of duty cannot be enforced in dev.
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Activate(
        Guid templateId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ActivateStrategyTemplateCommand(templateId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/strategy-templates/{templateId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid templateId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveStrategyTemplateCommand(templateId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/strategy-templates/{templateId:guid}/new-version")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> NewVersion(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateStrategyTemplateVersionCommand(templateId), cancellationToken));

    /// <summary>The read-only binding view with derived freshness hints. It returns no member and no member count: a
    /// play reports what it BINDS, never who is inside.</summary>
    [HttpGet("api/crm/strategy-templates/{templateId:guid}/bindings")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Bindings(
        Guid templateId, [FromQuery] DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetStrategyTemplateBindingsQuery(templateId, effectiveAt), cancellationToken));

    private static List<StrategyTemplateSegmentBindingInput>? MapSegments(
        List<StrategyTemplateSegmentBindingRequest>? bindings)
        => bindings?.Select(b => b.ToInput()).ToList();

    private static List<StrategyTemplateProductLineInput>? MapProducts(
        List<StrategyTemplateProductLineRequest>? lines)
        => lines?.Select(l => l.ToInput()).ToList();

    private static List<StrategyTemplateContentBindingInput>? MapContents(
        List<StrategyTemplateContentBindingRequest>? bindings)
        => bindings?.Select(b => b.ToInput()).ToList();
}
