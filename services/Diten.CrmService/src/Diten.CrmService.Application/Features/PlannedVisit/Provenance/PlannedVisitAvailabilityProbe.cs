using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>
/// MOD-0150 read-only availability wrapper. Reads <see cref="IContactAvailabilityRepository"/> <b>in-process via DI</b>
/// and builds the <see cref="PlannedVisitAvailabilitySnapshot"/> for the plan day (D13). Availability is PER-CONTACT, so
/// a snapshot is produced only for a contact / account-contact-link target; an account / pharmacy target has no
/// per-contact availability and yields no snapshot.
/// <para><b>In FU01 a conflict is a WARNING, never a hard block (§12.5).</b> This probe never returns a failure and never
/// computes availability — it reads the stored window and records whether the planned window fits, plus reason codes.
/// The hard-constraint + override behaviour is FU05.</para>
/// </summary>
public sealed class PlannedVisitAvailabilityProbe
{
    private readonly ITenantContext _tenant;
    private readonly IContactAvailabilityRepository _availabilities;

    public PlannedVisitAvailabilityProbe(ITenantContext tenant, IContactAvailabilityRepository availabilities)
    {
        _tenant = tenant;
        _availabilities = availabilities;
    }

    public async Task<PlannedVisitAvailabilitySnapshot?> CaptureAsync(
        PlannedVisitEntity plan, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        // Per-contact only: an account / pharmacy target with no contact has no availability window here.
        IReadOnlyList<Domain.Entities.ContactAvailability> rows;
        if (plan.AccountContactLinkId is { } linkId && linkId != Guid.Empty)
        {
            rows = await _availabilities.ListByLinkAsync(tenantId, linkId, cancellationToken);
        }
        else if (plan.ContactId is { } contactId && contactId != Guid.Empty)
        {
            rows = await _availabilities.ListByContactAsync(tenantId, contactId, cancellationToken);
        }
        else
        {
            return null;
        }

        var weekday = plan.PlannedDate.DayOfWeek.ToString().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        var active = rows
            .Where(a => !a.IsDeleted
                        && string.Equals(a.Status, AvailabilityLifecycle.Active, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var forDay = active
            .Where(a => string.Equals(a.Weekday, weekday, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var reasonCodes = new List<string>();

        if (forDay.Count == 0)
        {
            // Not available on this weekday. A WARNING, not a block.
            reasonCodes.Add(PlannedVisitAvailabilityReasonCodes.ContactNotAvailableOnDay);
            return new PlannedVisitAvailabilitySnapshot
            {
                Weekday = weekday,
                WithinAvailableWindow = plan.PlannedStartTime is null ? null : false,
                ReasonCodes = reasonCodes,
                CapturedAt = now
            };
        }

        var window = forDay[0];
        var appointmentRequired = window.Preference.AppointmentRequired;
        if (appointmentRequired)
        {
            reasonCodes.Add(PlannedVisitAvailabilityReasonCodes.AppointmentRequired);
        }

        bool? within = null;
        if (plan.PlannedStartTime is { } start && plan.PlannedEndTime is { } end
            && !string.IsNullOrWhiteSpace(window.StartTime) && !string.IsNullOrWhiteSpace(window.EndTime))
        {
            // Planned window fits inside the available window when it starts no earlier and ends no later.
            within = string.CompareOrdinal(start, window.StartTime) >= 0
                     && string.CompareOrdinal(end, window.EndTime) <= 0;
            if (within == false)
            {
                reasonCodes.Add(PlannedVisitAvailabilityReasonCodes.OutsidePreferredWindow);
            }
        }

        return new PlannedVisitAvailabilitySnapshot
        {
            Weekday = weekday,
            AvailableStartTime = string.IsNullOrWhiteSpace(window.StartTime) ? null : window.StartTime,
            AvailableEndTime = string.IsNullOrWhiteSpace(window.EndTime) ? null : window.EndTime,
            AppointmentRequired = appointmentRequired,
            WithinAvailableWindow = within,
            ReasonCodes = reasonCodes,
            CapturedAt = now
        };
    }
}
