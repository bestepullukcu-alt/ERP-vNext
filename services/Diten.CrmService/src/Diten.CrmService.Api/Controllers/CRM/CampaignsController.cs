using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Campaign.CampaignPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0165 FU04 — Campaign + CampaignTarget authoring and the static target snapshot.
/// <para>
/// <b>Routing:</b> canonical under <c>/api/crm/campaigns</c>. The Gateway exposes the same paths through the dedicated
/// <c>campaigns</c> ocelot routes; there is no direct-to-5061 business surface.
/// </para>
/// <para>
/// <b>Permissions:</b> canonical keys are <c>crm.campaign.read</c> / <c>.manage</c> /
/// <c>crm.campaign.target.read</c> / <c>.manage</c> (<see cref="Perms"/>). The RBAC catalog does not carry them yet, so
/// the endpoints run on the documented fallback (<c>crm.territory.read</c> for reads,
/// <c>crm.territory.model.manage</c> for writes). The fallback widens nothing — every FU04 guard still runs.
/// Follow-up: MOD-0165-FU-RBAC.
/// </para>
/// <b>There is no delete endpoint</b> for either aggregate. Closing a campaign or a target is Archive, so targeting
/// history — including why someone was excluded — stays readable. The snapshot endpoint is additive: it never removes
/// an earlier target.
/// </summary>
[Authorize]
public sealed class CampaignsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CampaignsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------------- Campaign reads ----------------

    /// <summary>Contract surface (feature flags, vocabulary, consent-integration contract, reason codes, limitations).</summary>
    [HttpGet("api/crm/campaigns/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetCampaignContractQuery(), cancellationToken));

    /// <summary>Lists campaigns (archived history included by default).</summary>
    [HttpGet("api/crm/campaigns")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? campaignType,
        [FromQuery] string? campaignStatus,
        [FromQuery] string? targetingMode,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListCampaignsQuery(campaignType, campaignStatus, targetingMode, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/campaigns/{campaignId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid campaignId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetCampaignQuery(campaignId), cancellationToken));

    /// <summary>
    /// FU11 — the code the next auto-assigned campaign WOULD get. A pure read: it does not increment the sequence and
    /// does not create the sequence document, so opening the create form still consumes nothing.
    /// <para>The value is indicative. The create form shows it as a PLACEHOLDER and still posts an empty
    /// CampaignCode, so the real assignment happens at save and a stale peek cannot collide with anything.</para>
    /// <para>Returns 200 with no data when no free candidate is found within the retry budget.</para>
    /// </summary>
    [HttpGet("api/crm/campaigns/next-code")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> PeekNextCode(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new PeekNextCampaignCodeQuery(), cancellationToken));

    // ---------------- Campaign writes ----------------

    /// <summary>FU09 - the cascading scope selector's option source. A READ: it decides nothing about what may be saved.</summary>
    [HttpGet("api/crm/campaigns/scope-options")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ScopeOptions(
        [FromQuery] string? country,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCampaignScopeOptionsQuery(country, startDate, endDate), cancellationToken));

    /// <summary>
    /// FU09 - the cycle periods applicable to a campaign at the scope being edited. The applicability rule lives here
    /// rather than in the browser, so the picker and the write-path guard answer from one rule.
    /// </summary>
    [HttpGet("api/crm/campaigns/applicable-cycle-periods")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ApplicableCyclePeriods(
        [FromQuery] string? scopeType,
        [FromQuery] string? countryScope,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] string? businessUnitId,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetApplicableCyclePeriodsQuery(scopeType, countryScope, legalEntityId, businessUnitId),
            cancellationToken));

    [HttpPost("api/crm/campaigns")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCampaignRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateCampaignCommand(
                request.CampaignCode, request.CampaignName, request.CampaignType, request.StartDate,
                request.CampaignStatus, request.ObjectiveType, request.BusinessUnitId,
                request.DefaultConsentChannel, request.DefaultConsentPurpose, request.EndDate, request.Description,
                request.CyclePeriodId, request.ScopeType, request.CountryScope, request.LegalEntityId,
                request.TargetingMode, request.TargetedSegmentIds),
            cancellationToken));

    [HttpPut("api/crm/campaigns/{campaignId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid campaignId, [FromBody] UpdateCampaignRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateCampaignCommand(
                campaignId, request.CampaignName, request.CampaignType, request.StartDate, request.CampaignStatus,
                request.ObjectiveType, request.BusinessUnitId, request.DefaultConsentChannel,
                request.DefaultConsentPurpose, request.EndDate, request.Description, request.CyclePeriodId,
                request.ScopeType, request.CountryScope, request.LegalEntityId,
                request.TargetingMode, request.TargetedSegmentIds),
            cancellationToken));

    [HttpPost("api/crm/campaigns/{campaignId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid campaignId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveCampaignCommand(campaignId), cancellationToken));

    // ---------------- Target reads ----------------

    /// <summary>Lists the targets of a campaign. Excluded rows are included by default — an excluded target with its
    /// reason IS the audit trail of why someone was left out.</summary>
    [HttpGet("api/crm/campaigns/{campaignId:guid}/targets")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ListTargets(
        Guid campaignId,
        [FromQuery] string? targetType,
        [FromQuery] string? targetStatus,
        [FromQuery] string? targetSource,
        [FromQuery] Guid? snapshotBatchId,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListCampaignTargetsQuery(
                campaignId, targetType, targetStatus, targetSource, snapshotBatchId, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetTarget(
        Guid campaignId, Guid campaignTargetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetCampaignTargetQuery(campaignId, campaignTargetId), cancellationToken));

    // ---------------- Target writes ----------------

    [HttpPost("api/crm/campaigns/{campaignId:guid}/targets")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> CreateTarget(
        Guid campaignId, [FromBody] CreateCampaignTargetRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateCampaignTargetCommand(
                campaignId, request.TargetType, request.TargetId, request.TargetSource, request.SelectionReason,
                request.EffectiveFrom, request.TargetDisplayName, request.TargetStatus, request.SourceReferenceType,
                request.SourceReferenceId, request.Priority, request.PriorityLevel, request.EffectiveTo,
                request.ExclusionReason, request.Notes, request.ReasonCodes, request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> UpdateTarget(
        Guid campaignId,
        Guid campaignTargetId,
        [FromBody] UpdateCampaignTargetRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateCampaignTargetCommand(
                campaignId, campaignTargetId, request.TargetSource, request.SelectionReason, request.EffectiveFrom,
                request.TargetDisplayName, request.TargetStatus, request.SourceReferenceType,
                request.SourceReferenceId, request.Priority, request.PriorityLevel, request.EffectiveTo,
                request.ExclusionReason, request.Notes, request.ReasonCodes, request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> ArchiveTarget(
        Guid campaignId, Guid campaignTargetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveCampaignTargetCommand(campaignId, campaignTargetId), cancellationToken));

    /// <summary>
    /// Static target snapshot. Normalizes the caller-supplied items into campaign targets and asks MOD-0164 whether each
    /// person-shaped target may be contacted. Additive (never removes an earlier target), idempotent per source, and
    /// never silently unfiltered — see the contract endpoint's <c>consentIntegration</c> block.
    /// </summary>
    [HttpPost("api/crm/campaigns/{campaignId:guid}/targets/snapshot")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> CreateTargetSnapshot(
        Guid campaignId,
        [FromBody] CreateCampaignTargetSnapshotRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateCampaignTargetSnapshotCommand(
                campaignId, request.SourceType, request.TargetItems, request.SelectionReason,
                request.ApplyConsentFilter, request.SourceReferenceType, request.SourceReferenceId,
                request.ConsentChannel, request.ConsentPurpose, request.EffectiveAt, request.EffectiveTo,
                request.ReasonCodes),
            cancellationToken));
}
