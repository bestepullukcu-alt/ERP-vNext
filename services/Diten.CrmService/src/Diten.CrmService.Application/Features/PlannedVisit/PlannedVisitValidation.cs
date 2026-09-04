using System.Globalization;
using System.Text.RegularExpressions;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 shared write-path validation. Kept in ONE place so create / update / confirm / cancel / archive can
/// never drift apart. Everything here is <b>structural and in-domain</b> (D2) and performs <b>no I/O</b>: the set rules
/// (code uniqueness, the overlap ban, the same-day-same-type ban) and the cross-aggregate lookups (target existence,
/// journey/stage validity) need other rows/modules and therefore live in the handlers and probes.
/// </summary>
public static class PlannedVisitValidation
{
    /// <summary>A rejected write: a message for the human, a machine code for the UI/smoke script, and the status the
    /// handler answers with. Nested so this file declares a single top-level public type.</summary>
    public sealed record Failure(string Message, string? Code, int StatusCode = 400);

    private static readonly Regex CodePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);
    private static readonly Regex TimePattern = new("^([01][0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static IReadOnlyList<string> ToErrors(Failure failure)
        => failure.Code is null ? new[] { failure.Message } : new[] { failure.Message, failure.Code };

    /// <summary>Parses an ISO "yyyy-MM-dd" (or a full date-time) into a <see cref="DateOnly"/>. Null on unparseable input.</summary>
    public static DateOnly? ParseDate(string? value)
    {
        var v = Trim(value);
        if (v is null)
        {
            return null;
        }

        if (DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto)
            ? DateOnly.FromDateTime(dto.UtcDateTime)
            : null;
    }

    public static Failure? ValidateVisitCode(string? visitCode)
    {
        var code = Trim(visitCode);
        if (code is null)
        {
            return new Failure("VisitCode is required.", PlannedVisitErrorCodes.CodeRequired);
        }

        if (code.Length > PlannedVisitLimits.MaxVisitCodeLength)
        {
            return new Failure(
                $"VisitCode must be at most {PlannedVisitLimits.MaxVisitCodeLength} characters.",
                PlannedVisitErrorCodes.CodeInvalid);
        }

        return CodePattern.IsMatch(code)
            ? null
            : new Failure(
                "VisitCode may contain only letters, digits, dot, underscore and hyphen.",
                PlannedVisitErrorCodes.CodeInvalid);
    }

    /// <summary>Fail-closed vocabulary check. An out-of-set value is refused (400) rather than quietly ignored.</summary>
    public static Failure? ValidateVocabulary(string fieldName, string? value, IReadOnlyList<string> allowed)
    {
        var v = Trim(value);
        if (v is null)
        {
            return new Failure($"{fieldName} is required.", PlannedVisitErrorCodes.UnsupportedVocabularyValue);
        }

        return allowed.Contains(v.ToLowerInvariant(), StringComparer.Ordinal)
            ? null
            : new Failure(
                $"Unsupported {fieldName} '{v}'. Known values: {string.Join(", ", allowed)}.",
                PlannedVisitErrorCodes.UnsupportedVocabularyValue);
    }

    /// <summary>The start/end wall-clock window: both given or both empty, both "HH:mm", End strictly after Start.</summary>
    public static Failure? ValidateTimeWindow(string? start, string? end)
    {
        var s = Trim(start);
        var e = Trim(end);

        if (s is null && e is null)
        {
            return null;
        }

        if (s is null || e is null)
        {
            return new Failure(
                "PlannedStartTime and PlannedEndTime must be given together or both left empty.",
                PlannedVisitErrorCodes.TimeWindowInvalid);
        }

        if (!TimePattern.IsMatch(s) || !TimePattern.IsMatch(e))
        {
            return new Failure("Times must be in HH:mm format.", PlannedVisitErrorCodes.TimeWindowInvalid);
        }

        return string.CompareOrdinal(e, s) > 0
            ? null
            : new Failure("PlannedEndTime must be after PlannedStartTime.", PlannedVisitErrorCodes.TimeWindowInvalid);
    }

    public static Failure? ValidateDuration(int? durationMinutes, string? start, string? end)
    {
        if (durationMinutes is not { } minutes)
        {
            return null;
        }

        if (minutes < PlannedVisitLimits.MinDurationMinutes || minutes > PlannedVisitLimits.MaxDurationMinutes)
        {
            return new Failure(
                $"PlannedDurationMinutes must be between {PlannedVisitLimits.MinDurationMinutes} and "
                + $"{PlannedVisitLimits.MaxDurationMinutes}.",
                PlannedVisitErrorCodes.DurationInvalid);
        }

        // If a window is given the duration cannot exceed it (a store-not-compute sanity check, not a computation).
        var s = Trim(start);
        var e = Trim(end);
        if (s is not null && e is not null && TimePattern.IsMatch(s) && TimePattern.IsMatch(e))
        {
            var windowMinutes = ToMinutes(e) - ToMinutes(s);
            if (windowMinutes > 0 && minutes > windowMinutes)
            {
                return new Failure(
                    "PlannedDurationMinutes cannot exceed the planned time window.",
                    PlannedVisitErrorCodes.DurationInvalid);
            }
        }

        return null;
    }

    private static int ToMinutes(string hhmm)
    {
        var parts = hhmm.Split(':');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture) * 60)
               + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    public static Failure? ValidateFreeText(string fieldName, string? value, int maxLength, string code)
    {
        var v = Trim(value);
        if (v is null)
        {
            return null;
        }

        return v.Length <= maxLength
            ? null
            : new Failure($"{fieldName} must be at most {maxLength} characters.", code);
    }

    /// <summary>The shape of a create/update, excluding cross-aggregate lookups and the set rules.</summary>
    public static Failure? ValidateShape(
        string? visitCode,
        string? targetType,
        Guid targetId,
        string? resourceId,
        string? resourceType,
        string? plannedStartTime,
        string? plannedEndTime,
        int? durationMinutes,
        string? visitPurpose,
        string? visitType,
        string? objective,
        string? notes,
        bool validateCode)
    {
        if (validateCode && ValidateVisitCode(visitCode) is { } codeFailure)
        {
            return codeFailure;
        }

        if (ValidateVocabulary("TargetType", targetType, PlannedVisitTargetType.All) is { } targetTypeFailure)
        {
            return targetTypeFailure;
        }

        if (targetId == Guid.Empty)
        {
            return new Failure("TargetId is required.", PlannedVisitErrorCodes.TargetRequired);
        }

        if (Trim(resourceId) is null)
        {
            return new Failure("Resource.ResourceId is required.", PlannedVisitErrorCodes.ResourceRequired);
        }

        if (Trim(resourceId)!.Length > PlannedVisitLimits.MaxResourceIdLength)
        {
            return new Failure(
                $"Resource.ResourceId must be at most {PlannedVisitLimits.MaxResourceIdLength} characters.",
                PlannedVisitErrorCodes.ResourceRequired);
        }

        if (ValidateVocabulary("Resource.ResourceType", resourceType, PlannedVisitResourceTypes.All)
            is { } resourceTypeFailure)
        {
            return resourceTypeFailure;
        }

        if (ValidateVocabulary("VisitPurpose", visitPurpose, PlannedVisitPurpose.All) is { } purposeFailure)
        {
            return purposeFailure;
        }

        if (ValidateVocabulary("VisitType", visitType, PlannedVisitType.All) is { } visitTypeFailure)
        {
            return visitTypeFailure;
        }

        if (ValidateTimeWindow(plannedStartTime, plannedEndTime) is { } windowFailure)
        {
            return windowFailure;
        }

        if (ValidateDuration(durationMinutes, plannedStartTime, plannedEndTime) is { } durationFailure)
        {
            return durationFailure;
        }

        if (ValidateFreeText("Objective", objective, PlannedVisitLimits.MaxObjectiveLength,
                PlannedVisitErrorCodes.UnsupportedVocabularyValue) is { } objectiveFailure)
        {
            return objectiveFailure;
        }

        return ValidateFreeText("Notes", notes, PlannedVisitLimits.MaxNotesLength,
            PlannedVisitErrorCodes.UnsupportedVocabularyValue);
    }

    /// <summary>Deterministic VisitPurpose → MOD-0164 consent Purpose (§4.7). The channel is ALWAYS <c>visit</c>.</summary>
    public static string ToConsentPurpose(string? visitPurpose) => PlannedVisitPurpose.Normalize(visitPurpose) switch
    {
        PlannedVisitPurpose.MedicalVisit => ConsentPurpose.MedicalVisit,
        PlannedVisitPurpose.FollowUp => ConsentPurpose.MedicalVisit,
        PlannedVisitPurpose.ProductInformation => ConsentPurpose.ProductInformation,
        PlannedVisitPurpose.Training => ConsentPurpose.Training,
        PlannedVisitPurpose.Campaign => ConsentPurpose.Campaign,
        PlannedVisitPurpose.Service => ConsentPurpose.Service,
        PlannedVisitPurpose.Compliance => ConsentPurpose.Compliance,
        _ => ConsentPurpose.Other
    };

    /// <summary>TargetType → MOD-0164 SubjectType (§4.7). <c>pharmacy</c> falls to <c>account</c> (D9) — MOD-0164 has no
    /// pharmacy subject, so consent is asked at the Account level.</summary>
    public static string ToConsentSubjectType(string? targetType) => PlannedVisitTargetType.Normalize(targetType) switch
    {
        PlannedVisitTargetType.Contact => ConsentSubjectType.Contact,
        PlannedVisitTargetType.AccountContactLink => ConsentSubjectType.AccountContactLink,
        _ => ConsentSubjectType.Account // account and pharmacy both ask at the account level
    };

    /// <summary>The subject id consent is asked about: the contact for a contact target, otherwise the account. For an
    /// account-contact-link the most specific consent subject is the LINK itself.</summary>
    public static Guid ConsentSubjectId(Domain.Entities.PlannedVisit plan) => PlannedVisitTargetType.Normalize(plan.TargetType) switch
    {
        PlannedVisitTargetType.Contact => plan.ContactId ?? plan.TargetId,
        PlannedVisitTargetType.AccountContactLink => plan.AccountContactLinkId ?? plan.TargetId,
        _ => plan.AccountId ?? plan.TargetId
    };

    // ── State machine (§12.2) ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Is <paramref name="to"/> a legal successor of <paramref name="from"/> (§12.2)? <c>archived</c> is
    /// terminal; <c>confirmed</c> cannot go back to <c>planned</c>.</summary>
    public static bool IsTransitionAllowed(string from, string to)
    {
        var f = PlannedVisitStatus.Normalize(from);
        var t = PlannedVisitStatus.Normalize(to);

        if (string.Equals(f, t, StringComparison.Ordinal))
        {
            return true;
        }

        return (f, t) switch
        {
            (PlannedVisitStatus.Draft, PlannedVisitStatus.Planned) => true,
            (PlannedVisitStatus.Planned, PlannedVisitStatus.Confirmed) => true,
            (PlannedVisitStatus.Draft, PlannedVisitStatus.Cancelled) => true,
            (PlannedVisitStatus.Planned, PlannedVisitStatus.Cancelled) => true,
            (PlannedVisitStatus.Confirmed, PlannedVisitStatus.Cancelled) => true,
            // archive is reachable from any non-archived status
            (_, PlannedVisitStatus.Archived) when !string.Equals(f, PlannedVisitStatus.Archived, StringComparison.Ordinal) => true,
            _ => false
        };
    }
}
