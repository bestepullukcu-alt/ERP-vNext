using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.ContactAvailability;

/// <summary>
/// MOD-0150 FU07 validation rules shared by the availability and exception handlers. Every rule is fail-closed:
/// an unpublished MOD-0048 set, a malformed time or an inactive link is a controlled 400/409 — never a silent
/// default and never a hardcoded value list.
/// </summary>
internal static class ContactAvailabilityValidation
{
    /// <summary>Validates a value against a MOD-0048 set. Returns an error message or null.</summary>
    public static async Task<string?> ValidateReferenceAsync(
        IReferenceDataValidator validator, string setCode, string? value, bool required, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return required ? $"'{setCode}' is required." : null;
        }

        var result = await validator.ValidateAsync(setCode, value.Trim(), cancellationToken);
        return result.Status switch
        {
            ReferenceValidationStatus.InvalidValue => $"'{value.Trim()}' is not a valid published value of reference set '{setCode}'.",
            ReferenceValidationStatus.SetMissing => $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
            _ => null
        };
    }

    /// <summary>Weekday is a calendar fact, not tenant vocabulary — validated in-domain, never against MOD-0048.</summary>
    public static string? ValidateWeekday(string? weekday)
        => AvailabilityWeekday.IsValid(weekday)
            ? null
            : "Weekday must be one of monday, tuesday, wednesday, thursday, friday, saturday, sunday.";

    /// <summary>StartTime/EndTime must parse as "HH:mm" and Start must be strictly before End.</summary>
    public static string? ValidateWindow(string? startTime, string? endTime, string label = "Availability")
    {
        var start = AvailabilityWeekday.ParseTime(startTime);
        var end = AvailabilityWeekday.ParseTime(endTime);

        if (start is null || end is null)
        {
            return $"{label} window requires a valid StartTime and EndTime in HH:mm format.";
        }

        return start >= end ? $"{label} StartTime must be earlier than EndTime." : null;
    }

    /// <summary>
    /// The preferred window must sit INSIDE the available window (pack §20.3). The avoid window deliberately may
    /// overlap the available window — that is its purpose (pack D13) — so it is only checked for well-formedness.
    /// </summary>
    public static string? ValidatePreference(VisitPreference? preference, string startTime, string endTime)
    {
        if (preference is null)
        {
            return null;
        }

        var availableStart = AvailabilityWeekday.ParseTime(startTime)!.Value;
        var availableEnd = AvailabilityWeekday.ParseTime(endTime)!.Value;

        var hasPreferredStart = !string.IsNullOrWhiteSpace(preference.PreferredVisitStartTime);
        var hasPreferredEnd = !string.IsNullOrWhiteSpace(preference.PreferredVisitEndTime);
        if (hasPreferredStart != hasPreferredEnd)
        {
            return "A preferred window needs both PreferredVisitStartTime and PreferredVisitEndTime.";
        }

        if (hasPreferredStart)
        {
            if (ValidateWindow(preference.PreferredVisitStartTime, preference.PreferredVisitEndTime, "Preferred") is { } windowError)
            {
                return windowError;
            }

            var preferredStart = AvailabilityWeekday.ParseTime(preference.PreferredVisitStartTime)!.Value;
            var preferredEnd = AvailabilityWeekday.ParseTime(preference.PreferredVisitEndTime)!.Value;
            if (preferredStart < availableStart || preferredEnd > availableEnd)
            {
                return "The preferred window must be inside the available window.";
            }
        }

        var hasAvoidStart = !string.IsNullOrWhiteSpace(preference.AvoidVisitStartTime);
        var hasAvoidEnd = !string.IsNullOrWhiteSpace(preference.AvoidVisitEndTime);
        if (hasAvoidStart != hasAvoidEnd)
        {
            return "An avoid window needs both AvoidVisitStartTime and AvoidVisitEndTime.";
        }

        if (hasAvoidStart
            && ValidateWindow(preference.AvoidVisitStartTime, preference.AvoidVisitEndTime, "Avoid") is { } avoidError)
        {
            // The avoid window is a STRONGER constraint inside the available window, not the inverse of preferred:
            // it may overlap the available window freely, so only its own well-formedness is enforced here.
            return avoidError;
        }

        if (preference.AppointmentLeadTimeDays is < 0)
        {
            return "AppointmentLeadTimeDays cannot be negative.";
        }

        if (preference.PreferredVisitDurationMinutes is <= 0)
        {
            return "PreferredVisitDurationMinutes must be greater than zero.";
        }

        return null;
    }

    public static string? ValidateEffectiveRange(DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveFrom is { } from && effectiveTo is { } to && to < from
            ? "EffectiveTo cannot be earlier than EffectiveFrom."
            : null;

    /// <summary>An availability row is "open" when it is active (never-deleted rows are already filtered by the repo).</summary>
    public static bool IsOpen(Domain.Entities.ContactAvailability availability)
        => !AvailabilityLifecycle.IsClosed(availability.Status);

    /// <summary>
    /// The link must exist, belong to the tenant and still be open. Closed/ended links cannot receive NEW active
    /// availability (pack §20.3) — existing rows stay readable as history.
    /// </summary>
    public static bool IsLinkOpen(AccountContactLink link)
        => !link.IsDeleted && !RelationshipLifecycle.IsClosed(link.Status) && !IsValidityExpired(link);

    private static bool IsValidityExpired(AccountContactLink link)
        => link.ValidTo is { } validTo && validTo < DateTimeOffset.UtcNow;

    /// <summary>
    /// Overlap check for the same (link, weekday) among ACTIVE rows whose effective ranges also overlap. Returns the
    /// conflicting row so the caller can report BOTH identities — a silent merge/overwrite is forbidden.
    /// </summary>
    public static Domain.Entities.ContactAvailability? FindOverlap(
        IEnumerable<Domain.Entities.ContactAvailability> existing,
        string weekday,
        string startTime,
        string endTime,
        DateTimeOffset? effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludeId)
    {
        var start = AvailabilityWeekday.ParseTime(startTime)!.Value;
        var end = AvailabilityWeekday.ParseTime(endTime)!.Value;
        var normalizedWeekday = AvailabilityWeekday.Normalize(weekday);

        foreach (var row in existing)
        {
            if (excludeId is { } exclude && row.Id == exclude)
            {
                continue;
            }

            if (AvailabilityLifecycle.IsClosed(row.Status)
                || !string.Equals(row.Weekday, normalizedWeekday, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rowStart = AvailabilityWeekday.ParseTime(row.StartTime);
            var rowEnd = AvailabilityWeekday.ParseTime(row.EndTime);
            if (rowStart is null || rowEnd is null)
            {
                continue; // defensive: a malformed legacy row never blocks a well-formed write
            }

            if (!AvailabilityWeekday.Overlaps(start, end, rowStart.Value, rowEnd.Value))
            {
                continue;
            }

            if (EffectiveRangesOverlap(effectiveFrom, effectiveTo, row.EffectiveFrom, row.EffectiveTo))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>Exact-duplicate detection for idempotency: same link, weekday, window, type, source and effective
    /// range. A repeat POST of an identical row is a no-op, not a duplicate and not an error.</summary>
    public static Domain.Entities.ContactAvailability? FindIdentical(
        IEnumerable<Domain.Entities.ContactAvailability> existing,
        string weekday,
        string startTime,
        string endTime,
        string availabilityType,
        string source,
        DateTimeOffset? effectiveFrom,
        DateTimeOffset? effectiveTo)
    {
        var normalizedWeekday = AvailabilityWeekday.Normalize(weekday);
        return existing.FirstOrDefault(row =>
            !AvailabilityLifecycle.IsClosed(row.Status)
            && string.Equals(row.Weekday, normalizedWeekday, StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.StartTime, startTime, StringComparison.Ordinal)
            && string.Equals(row.EndTime, endTime, StringComparison.Ordinal)
            && string.Equals(row.AvailabilityType, availabilityType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.Source, source, StringComparison.OrdinalIgnoreCase)
            && Nullable.Equals(row.EffectiveFrom, effectiveFrom)
            && Nullable.Equals(row.EffectiveTo, effectiveTo));
    }

    /// <summary>Two open-ended effective ranges overlap unless one strictly ends before the other starts.</summary>
    public static bool EffectiveRangesOverlap(
        DateTimeOffset? aFrom, DateTimeOffset? aTo, DateTimeOffset? bFrom, DateTimeOffset? bTo)
    {
        if (aTo is { } aEnd && bFrom is { } bStart && aEnd < bStart)
        {
            return false;
        }

        if (bTo is { } bEnd && aFrom is { } aStart && bEnd < aStart)
        {
            return false;
        }

        return true;
    }

    /// <summary>Whether a row is effective on a calendar date (used by the lookup).</summary>
    public static bool IsEffectiveOn(DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo, DateOnly date)
    {
        if (effectiveFrom is { } from && date < DateOnly.FromDateTime(from.UtcDateTime))
        {
            return false;
        }

        if (effectiveTo is { } to && date > DateOnly.FromDateTime(to.UtcDateTime))
        {
            return false;
        }

        return true;
    }

    /// <summary>Parses a "yyyy-MM-dd" date; null when malformed.</summary>
    public static DateOnly? ParseDate(string? value)
        => DateOnly.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
