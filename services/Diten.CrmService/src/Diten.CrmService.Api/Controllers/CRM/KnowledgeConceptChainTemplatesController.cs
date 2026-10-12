using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;
using PathPerms = Diten.CrmService.Application.Features.Knowledge.Path.KnowledgePathPermissions;
using JourneyPerms = Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.ContentEngagementJourneyPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>MOD-0162 FU03 — Concept chain template authoring. Canonical under
/// <c>/api/crm/knowledge/concept-chain-templates</c>. No delete.</summary>
[Authorize]
public sealed class KnowledgeConceptChainTemplatesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptChainTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-chain-templates")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConceptChainTemplatesQuery(subjectId, status, effectiveAt, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/concept-chain-templates/{templateId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptChainTemplateQuery(templateId), cancellationToken));

    /// <summary>WP-KP-CH-1 — the "Outputs" read: the knowledge paths built from this chain version (KP-1 ChainRef), the
    /// journeys using them (KP-3 usage rule) and the COUNT of upcoming planned visits telling them. Read only. Without
    /// the path / journey read permission that section is its count only (<c>…Restricted</c>).</summary>
    [HttpGet("api/crm/knowledge/concept-chain-templates/{templateId:guid}/outputs")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Outputs(
        Guid templateId, [FromQuery] bool includeOtherVersions = false, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptChainTemplateOutputsQuery(
                templateId,
                includeOtherVersions,
                CanReadPaths: PermissionClaims.HasPermission(User, PathPerms.Read),
                CanReadJourneys: PermissionClaims.HasPermission(User, JourneyPerms.Read)
                                 || PermissionClaims.HasPermission(User, JourneyPerms.ReadFallback)),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-chain-templates")]
    [HasPermission(Perms.TemplateManage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConceptChainTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConceptChainTemplateCommand(
                request.SubjectId, request.ChainCode, request.ChainName, request.OrderedConceptTypes,
                request.EffectiveFrom, request.Description, request.Status, request.ChainVersion, request.EffectiveTo,
                ToBranchInputs(request.Branches), request.ModeratorRoleType, request.ForWhomAudienceProfileIds,
                request.IgnoredNonConformingRelationshipIds),
            cancellationToken));

    [HttpPut("api/crm/knowledge/concept-chain-templates/{templateId:guid}")]
    [HasPermission(Perms.TemplateManage)]
    public async Task<IActionResult> Update(
        Guid templateId, [FromBody] UpdateConceptChainTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConceptChainTemplateCommand(
                templateId, request.ChainName, request.OrderedConceptTypes, request.EffectiveFrom, request.Description,
                request.Status, request.ChainVersion, request.EffectiveTo, ToBranchInputs(request.Branches),
                request.ModeratorRoleType, request.ForWhomAudienceProfileIds, request.IgnoredNonConformingRelationshipIds),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-chain-templates/{templateId:guid}/archive")]
    [HasPermission(Perms.TemplateManage)]
    public async Task<IActionResult> Archive(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConceptChainTemplateCommand(templateId), cancellationToken));

    // WP-CT-BE-B — immediate "Yok say" write on a saved, non-published template (published → 409). Records the
    // decision only; relationships are untouched (D8).
    [HttpPut("api/crm/knowledge/concept-chain-templates/{templateId:guid}/conformance-resolutions")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ConformanceResolutions(
        Guid templateId, [FromBody] ChainTemplateConformanceResolutionsRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SetConceptChainTemplateConformanceResolutionsCommand(templateId, request.IgnoredRelationshipIds),
            cancellationToken));

    // WP-CT-BE-A — read-only diagnostics against a SUPPLIED (possibly unsaved) spine. POST only because the spine travels
    // in the body; nothing is written. Read permission, not TemplateManage.
    [HttpPost("api/crm/knowledge/concept-chain-templates/conformance-diagnostics")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ConformanceDiagnostics(
        [FromBody] ChainTemplateConformanceDiagnosticsRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetChainTemplateConformanceDiagnosticsQuery(request.SubjectId, request.OrderedConceptTypeIds),
            cancellationToken));

    // SCMM-10 (③) — maps the API branch request shape onto the application command input. Null stays null (legacy mode).
    private static IReadOnlyList<ConceptChainBranchInput>? ToBranchInputs(IReadOnlyList<ConceptChainBranchRequest>? branches)
        => branches?.Select(b => new ConceptChainBranchInput(
            b.BranchCode,
            (b.Steps ?? Array.Empty<ConceptChainStepRequest>()).Select(s => new ConceptChainStepInput(
                s.ConceptTypeId, s.MinSelection, s.MaxSelection)).ToList(),
            b.BranchName,
            b.SortOrder)).ToList();
}
