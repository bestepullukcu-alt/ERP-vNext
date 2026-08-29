using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ConsentPreference.Commands;
using Diten.CrmService.Application.Features.ConsentPreference.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ConsentPreference.ConsentPreferencePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0164 FU02 — Preference authoring. A preference NEVER substitutes for consent: it can only restrict further, and
/// it is consumed through the consent evaluate endpoint (there is no separate preference-evaluate surface, so a caller
/// can never read a preference as permission).
/// <para>
/// Distinct from MOD-0150 ContactAvailability: availability answers <i>when</i> the subject is available; a preference
/// answers <i>which channel / restriction / preference</i>. FU02 mutates no availability record.
/// </para>
/// <b>There is no delete endpoint.</b> Closing a record is Archive, so history stays readable.
/// </summary>
[Authorize]
public sealed class PreferencesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PreferencesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------------- Reads ----------------

    [HttpGet("api/crm/preferences")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? subjectType,
        [FromQuery] Guid? subjectId,
        [FromQuery] string? channel,
        [FromQuery] string? preferenceType,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListPreferenceRecordsQuery(subjectType, subjectId, channel, preferenceType, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/preferences/{preferenceId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid preferenceId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetPreferenceRecordQuery(preferenceId), cancellationToken));

    // ---------------- Writes ----------------

    [HttpPost("api/crm/preferences")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePreferenceRecordRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreatePreferenceRecordCommand(
                request.SubjectType, request.SubjectId, request.Channel, request.PreferenceType,
                request.PreferenceValue, request.Priority, request.EffectiveFrom, request.Source,
                request.EffectiveTo, request.Notes, request.ExternalReferences),
            cancellationToken));

    [HttpPut("api/crm/preferences/{preferenceId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid preferenceId, [FromBody] UpdatePreferenceRecordRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdatePreferenceRecordCommand(
                preferenceId, request.PreferenceValue, request.Priority, request.EffectiveFrom, request.Source,
                request.EffectiveTo, request.Notes, request.ExternalReferences),
            cancellationToken));

    [HttpPost("api/crm/preferences/{preferenceId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid preferenceId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchivePreferenceRecordCommand(preferenceId), cancellationToken));
}
