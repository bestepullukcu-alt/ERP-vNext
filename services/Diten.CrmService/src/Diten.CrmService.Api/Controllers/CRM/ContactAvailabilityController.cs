using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContactAvailability;
using Diten.CrmService.Application.Features.ContactAvailability.Commands;
using Diten.CrmService.Application.Features.ContactAvailability.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 FU07 — AccountContactLink-scoped contact availability / visit preference master.
/// <para>
/// <b>Routing:</b> every endpoint is exposed twice. The canonical shape (<c>/api/crm/account-contact-links/…</c>,
/// <c>/api/crm/contact-availability/…</c>) is the target contract, and a Gateway-reachable alias lives under the
/// EXISTING <c>/api/crm/contacts/{everything}</c> and <c>/api/crm/accounts/{everything}</c> wildcards. `ocelot.json`
/// is `integration-agent`-owned and was NOT modified here, so the aliases are what actually works over the Gateway
/// today; adding the three canonical wildcards is a follow-up for that agent.
/// </para>
/// <para>
/// <b>Permissions:</b> the canonical keys are <c>crm.contact.availability.read</c> / <c>.manage</c>
/// (<see cref="ContactAvailabilityPermissions"/>), but the RBAC catalog does not carry them yet, so the endpoints run
/// on the documented fallback — <c>crm.contact.read</c> for reads and <c>crm.contact.update</c> for writes. The
/// fallback widens nothing: every FU07 guard still runs. Follow-up: MOD-0150-FU-RBAC.
/// </para>
/// <b>There is no delete endpoint.</b> Closing a row is Deactivate/Archive, so history stays readable.
/// </summary>
[Authorize]
public sealed class ContactAvailabilityController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContactAvailabilityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------------- Reads ----------------

    /// <summary>Every availability row across all links of one contact, grouped by link/account.</summary>
    [HttpGet("api/crm/contacts/{contactId:guid}/availability")]
    [HasPermission(ContactAvailabilityPermissions.ReadFallback)]
    public async Task<IActionResult> ListForContact(Guid contactId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListContactAvailabilityQuery(contactId), cancellationToken));

    /// <summary>The location-scoped schedule of one AccountContactLink (canonical + Gateway-reachable alias).</summary>
    [HttpGet("api/crm/account-contact-links/{linkId:guid}/availability")]
    [HttpGet("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}/availability")]
    [HttpGet("api/crm/contacts/links/{linkId:guid}/availability")]
    [HasPermission(ContactAvailabilityPermissions.ReadFallback)]
    public async Task<IActionResult> GetForLink(Guid linkId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetLinkAvailabilityQuery(linkId), cancellationToken));

    /// <summary>Every contact's availability at one account/location.</summary>
    [HttpGet("api/crm/accounts/{accountId:guid}/contact-availability")]
    [HasPermission(ContactAvailabilityPermissions.ReadFallback)]
    public async Task<IActionResult> ListForAccount(Guid accountId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListAccountContactAvailabilityQuery(accountId), cancellationToken));

    /// <summary>
    /// Readiness lookup for one date — the MOD-0151 FU09A / MOD-0155 seam. Date-specific exceptions are already
    /// applied. Returns rows + reason codes; never an ordering, a score or a plan. No availability data yields
    /// <c>unknown</c>, never <c>unavailable</c>.
    /// </summary>
    [HttpGet("api/crm/contact-availability/lookup")]
    [HttpGet("api/crm/contacts/availability-lookup")]
    [HasPermission(ContactAvailabilityPermissions.ReadFallback)]
    public async Task<IActionResult> Lookup(
        [FromQuery] string date,
        [FromQuery] Guid? contactId,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? accountContactLinkId,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new LookupContactAvailabilityQuery(date, contactId, accountId, accountContactLinkId), cancellationToken));

    // ---------------- Availability writes ----------------

    [HttpPost("api/crm/account-contact-links/{linkId:guid}/availability")]
    [HttpPost("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}/availability")]
    [HttpPost("api/crm/contacts/links/{linkId:guid}/availability")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> Create(Guid linkId, [FromBody] CreateContactAvailabilityRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContactAvailabilityCommand(
                linkId, request.Weekday, request.StartTime, request.EndTime, request.AvailabilityType, request.Source,
                request.Status, ToInput(request.Preference), request.AverageVisitDurationMinutes,
                request.EffectiveFrom, request.EffectiveTo, request.Notes),
            cancellationToken));

    [HttpPut("api/crm/contact-availability/{availabilityId:guid}")]
    [HttpPut("api/crm/contacts/availability/{availabilityId:guid}")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> Update(Guid availabilityId, [FromBody] UpdateContactAvailabilityRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContactAvailabilityCommand(
                availabilityId, request.Weekday, request.StartTime, request.EndTime, request.AvailabilityType, request.Source,
                request.Status, ToInput(request.Preference), request.AverageVisitDurationMinutes,
                request.EffectiveFrom, request.EffectiveTo, request.Notes),
            cancellationToken));

    [HttpPost("api/crm/contact-availability/{availabilityId:guid}/deactivate")]
    [HttpPost("api/crm/contacts/availability/{availabilityId:guid}/deactivate")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> Deactivate(Guid availabilityId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeactivateContactAvailabilityCommand(availabilityId), cancellationToken));

    [HttpPost("api/crm/contact-availability/{availabilityId:guid}/archive")]
    [HttpPost("api/crm/contacts/availability/{availabilityId:guid}/archive")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> Archive(Guid availabilityId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveContactAvailabilityCommand(availabilityId), cancellationToken));

    // ---------------- Exception writes ----------------

    [HttpPost("api/crm/account-contact-links/{linkId:guid}/availability-exceptions")]
    [HttpPost("api/crm/accounts/{accountId:guid}/contacts/{linkId:guid}/availability-exceptions")]
    [HttpPost("api/crm/contacts/links/{linkId:guid}/availability-exceptions")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> CreateException(
        Guid linkId, [FromBody] CreateContactAvailabilityExceptionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContactAvailabilityExceptionCommand(
                linkId, request.Date, request.IsAvailable, request.Source,
                request.StartTime, request.EndTime, request.Reason, request.Notes, request.Status),
            cancellationToken));

    [HttpPut("api/crm/contact-availability-exceptions/{exceptionId:guid}")]
    [HttpPut("api/crm/contacts/availability-exceptions/{exceptionId:guid}")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> UpdateException(
        Guid exceptionId, [FromBody] UpdateContactAvailabilityExceptionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContactAvailabilityExceptionCommand(
                exceptionId, request.Date, request.IsAvailable, request.Source,
                request.StartTime, request.EndTime, request.Reason, request.Notes, request.Status),
            cancellationToken));

    [HttpPost("api/crm/contact-availability-exceptions/{exceptionId:guid}/deactivate")]
    [HttpPost("api/crm/contacts/availability-exceptions/{exceptionId:guid}/deactivate")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> DeactivateException(Guid exceptionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new DeactivateContactAvailabilityExceptionCommand(exceptionId), cancellationToken));

    [HttpPost("api/crm/contact-availability-exceptions/{exceptionId:guid}/archive")]
    [HttpPost("api/crm/contacts/availability-exceptions/{exceptionId:guid}/archive")]
    [HasPermission(ContactAvailabilityPermissions.ManageFallback)]
    public async Task<IActionResult> ArchiveException(Guid exceptionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveContactAvailabilityExceptionCommand(exceptionId), cancellationToken));

    private static VisitPreferenceInput? ToInput(VisitPreferenceRequest? request) => request is null
        ? null
        : new VisitPreferenceInput(
            request.PreferredVisitDurationMinutes,
            request.PreferredVisitStartTime,
            request.PreferredVisitEndTime,
            request.AvoidVisitStartTime,
            request.AvoidVisitEndTime,
            request.AppointmentRequired,
            request.AppointmentLeadTimeDays,
            request.PreferredContactMethod,
            request.Notes);
}
