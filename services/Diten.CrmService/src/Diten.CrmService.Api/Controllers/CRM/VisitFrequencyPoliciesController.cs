using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Commands;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Contract;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.VisitFrequencyPolicy.VisitFrequencyPolicyPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0165 FU03 — Visit Frequency / Call-Cycle Policy authoring + read-only resolve provider.
/// <para>
/// <b>Routing:</b> canonical under <c>/api/crm/visit-frequency-policies</c>. The Gateway exposes the same paths
/// through the dedicated <c>visit-frequency-policies</c> ocelot routes.
/// </para>
/// <para>
/// <b>Permissions:</b> canonical keys are <c>crm.visit-frequency-policy.read</c> / <c>.manage</c> / <c>.resolve</c>
/// (<see cref="Perms"/>). The RBAC catalog does not carry them yet, so the endpoints run on the documented fallback
/// (<c>crm.territory.read</c> for reads/resolve, <c>crm.territory.model.manage</c> for writes). The fallback widens
/// nothing — every FU03 guard still runs. Follow-up: MOD-0165-FU-RBAC.
/// </para>
/// <b>There is no delete endpoint.</b> Closing a policy is Archive, so history stays readable. The <c>resolve</c>
/// endpoint is GET/read-only and performs no writes.
/// </summary>
[Authorize]
public sealed class VisitFrequencyPoliciesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public VisitFrequencyPoliciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------------- Reads ----------------

    /// <summary>Contract surface (feature flags, supported vocabulary, permissions, limitations).</summary>
    [HttpGet("api/crm/visit-frequency-policies/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitFrequencyContractQuery(), cancellationToken));

    /// <summary>Lists policies (any status, incl. archived history). Optional filters: targetType, targetId, status, source.</summary>
    [HttpGet("api/crm/visit-frequency-policies")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? targetType,
        [FromQuery] Guid? targetId,
        [FromQuery] string? status,
        [FromQuery] string? source,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListVisitFrequencyPoliciesQuery(targetType, targetId, status, source), cancellationToken));

    /// <summary>
    /// Read-only resolve — "how often should this target be visited?". Returns the selected policy + candidate
    /// diagnostics + reason codes. NEVER writes, and NEVER returns due/overdue, last-visit, route/order or consent.
    /// </summary>
    [HttpGet("api/crm/visit-frequency-policies/resolve")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Resolve(
        [FromQuery] string targetType,
        [FromQuery] Guid targetId,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? businessUnit,
        [FromQuery] Guid? territoryNodeId,
        [FromQuery] Guid? campaignId,
        [FromQuery] Guid? segmentId,
        [FromQuery] Guid? brandId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? conceptNodeId,
        [FromQuery] Guid? audienceProfileId,
        [FromQuery] bool includeDiagnostics = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveVisitFrequencyPolicyQuery(
                targetType, targetId, effectiveAt, businessUnit, territoryNodeId, campaignId, segmentId,
                brandId, productId, conceptNodeId, audienceProfileId, includeDiagnostics),
            cancellationToken));

    /// <summary>A single policy by id (any status; archived rows are readable).</summary>
    [HttpGet("api/crm/visit-frequency-policies/{policyId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid policyId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitFrequencyPolicyQuery(policyId), cancellationToken));

    // ---------------- Writes ----------------

    [HttpPost("api/crm/visit-frequency-policies")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create([FromBody] CreateVisitFrequencyPolicyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateVisitFrequencyPolicyCommand(
                request.PolicyCode, request.PolicyName, request.TargetType, request.TargetId,
                request.FrequencyType, request.RequiredVisitCount, request.PeriodType, request.EffectiveFrom,
                request.Priority, request.Source, request.Status, request.Description, request.BusinessUnit,
                request.TerritoryNodeId, request.CampaignId, request.SegmentId, request.BrandId, request.ProductId,
                request.CycleId, request.CyclePeriodId, request.EffectiveTo, request.Notes),
            cancellationToken));

    [HttpPut("api/crm/visit-frequency-policies/{policyId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(Guid policyId, [FromBody] UpdateVisitFrequencyPolicyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateVisitFrequencyPolicyCommand(
                policyId, request.PolicyName, request.FrequencyType, request.RequiredVisitCount, request.PeriodType,
                request.EffectiveFrom, request.Priority, request.Source, request.Status, request.Description,
                request.BusinessUnit, request.TerritoryNodeId, request.CampaignId, request.SegmentId, request.BrandId,
                request.ProductId, request.CycleId, request.CyclePeriodId, request.EffectiveTo, request.Notes),
            cancellationToken));

    [HttpPost("api/crm/visit-frequency-policies/{policyId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid policyId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveVisitFrequencyPolicyCommand(policyId), cancellationToken));
}
