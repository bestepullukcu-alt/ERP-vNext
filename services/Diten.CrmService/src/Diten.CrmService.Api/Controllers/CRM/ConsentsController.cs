using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ConsentPreference.Commands;
using Diten.CrmService.Application.Features.ConsentPreference.Contract;
using Diten.CrmService.Application.Features.ConsentPreference.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ConsentPreference.ConsentPreferencePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0164 FU02 — Consent authoring + read-only evaluation provider.
/// <para>
/// <b>Routing:</b> canonical under <c>/api/crm/consents</c>. The Gateway exposes the same paths through the dedicated
/// <c>consents</c> ocelot routes; there is no direct-to-5061 business surface.
/// </para>
/// <para>
/// <b>Permissions:</b> canonical keys are <c>crm.consent.read</c> / <c>.manage</c> / <c>.evaluate</c>
/// (<see cref="Perms"/>). The RBAC catalog does not carry them yet, so the endpoints run on the documented fallback
/// (<c>crm.territory.read</c> for reads/evaluate, <c>crm.territory.model.manage</c> for writes). The fallback widens
/// nothing — every FU02 guard still runs. Follow-up: MOD-0164-FU-RBAC.
/// </para>
/// <b>There is no delete endpoint.</b> Closing a record is Archive, so consent history (including withdrawals) stays
/// readable. The <c>evaluate</c> endpoint is GET/read-only and performs no writes.
/// </summary>
[Authorize]
public sealed class ConsentsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ConsentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------------- Reads ----------------

    /// <summary>Contract surface (feature flags, supported vocabulary, permissions, limitations).</summary>
    [HttpGet("api/crm/consents/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetConsentContractQuery(), cancellationToken));

    /// <summary>Lists consent records (archived history included by default). Optional filters: subjectType, subjectId,
    /// channel, purpose, consentStatus, includeArchived.</summary>
    [HttpGet("api/crm/consents")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? subjectType,
        [FromQuery] Guid? subjectId,
        [FromQuery] string? channel,
        [FromQuery] string? purpose,
        [FromQuery] string? consentStatus,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConsentRecordsQuery(subjectType, subjectId, channel, purpose, consentStatus, includeArchived),
            cancellationToken));

    /// <summary>
    /// Read-only evaluation — "may this subject be reached on this channel, for this purpose, in this scope, at this
    /// instant?". Returns the eligibility/decision + matched records + reason codes + candidate diagnostics. NEVER
    /// writes, and NEVER returns a campaign target, visit plan, route, due status, last-visit date or frequency value.
    /// </summary>
    [HttpGet("api/crm/consents/evaluate")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Evaluate(
        [FromQuery] string subjectType,
        [FromQuery] Guid subjectId,
        [FromQuery] string channel,
        [FromQuery] string purpose,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? scopeType,
        [FromQuery] Guid? scopeId,
        [FromQuery] bool includeDiagnostics = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new EvaluateConsentQuery(
                subjectType, subjectId, channel, purpose, effectiveAt, scopeType, scopeId, includeDiagnostics),
            cancellationToken));

    /// <summary>A single consent record by id (archived rows are readable).</summary>
    [HttpGet("api/crm/consents/{consentId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid consentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetConsentRecordQuery(consentId), cancellationToken));

    // ---------------- Writes ----------------

    [HttpPost("api/crm/consents")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConsentRecordRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConsentRecordCommand(
                request.SubjectType, request.SubjectId, request.Channel, request.Purpose, request.LegalBasis,
                request.ConsentStatus, request.EffectiveFrom, request.Source, request.ScopeType, request.ScopeId,
                request.EffectiveTo, request.EvidenceRef, request.WithdrawalReason, request.Notes,
                request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/consents/{consentId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid consentId, [FromBody] UpdateConsentRecordRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConsentRecordCommand(
                consentId, request.LegalBasis, request.ConsentStatus, request.EffectiveFrom, request.Source,
                request.EffectiveTo, request.EvidenceRef, request.WithdrawalReason, request.Notes,
                request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/consents/{consentId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid consentId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConsentRecordCommand(consentId), cancellationToken));
}
