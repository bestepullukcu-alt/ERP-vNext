using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContactAvailability.Commands;

/// <summary>
/// MOD-0150 FU07 write surface. Every command is scoped by <c>AccountContactLinkId</c> (or resolves it from the row
/// being edited) — ContactId/AccountId are always derived from the link, never accepted from the payload. There is
/// deliberately NO delete command: closing is Deactivate/Archive.
/// </summary>
public sealed record VisitPreferenceInput(
    int? PreferredVisitDurationMinutes = null,
    string? PreferredVisitStartTime = null,
    string? PreferredVisitEndTime = null,
    string? AvoidVisitStartTime = null,
    string? AvoidVisitEndTime = null,
    bool AppointmentRequired = false,
    int? AppointmentLeadTimeDays = null,
    string? PreferredContactMethod = null,
    string? Notes = null);

public sealed record CreateContactAvailabilityCommand(
    Guid AccountContactLinkId,
    string Weekday,
    string StartTime,
    string EndTime,
    string AvailabilityType,
    string Source,
    string? Status = null,
    VisitPreferenceInput? Preference = null,
    int? AverageVisitDurationMinutes = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null) : IRequest<Response<Guid>>;

public sealed record UpdateContactAvailabilityCommand(
    Guid AvailabilityId,
    string Weekday,
    string StartTime,
    string EndTime,
    string AvailabilityType,
    string Source,
    string? Status = null,
    VisitPreferenceInput? Preference = null,
    int? AverageVisitDurationMinutes = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null,
    string? Notes = null) : IRequest<Response<bool>>;

/// <summary>Closes a row without deleting it (status → inactive). History stays readable.</summary>
public sealed record DeactivateContactAvailabilityCommand(Guid AvailabilityId) : IRequest<Response<bool>>;

/// <summary>Archives a row (status → archived). Still not a delete.</summary>
public sealed record ArchiveContactAvailabilityCommand(Guid AvailabilityId) : IRequest<Response<bool>>;

public sealed record CreateContactAvailabilityExceptionCommand(
    Guid AccountContactLinkId,
    string Date,
    bool IsAvailable,
    string Source,
    string? StartTime = null,
    string? EndTime = null,
    string? Reason = null,
    string? Notes = null,
    string? Status = null) : IRequest<Response<Guid>>;

public sealed record UpdateContactAvailabilityExceptionCommand(
    Guid ExceptionId,
    string Date,
    bool IsAvailable,
    string Source,
    string? StartTime = null,
    string? EndTime = null,
    string? Reason = null,
    string? Notes = null,
    string? Status = null) : IRequest<Response<bool>>;

public sealed record DeactivateContactAvailabilityExceptionCommand(Guid ExceptionId) : IRequest<Response<bool>>;

public sealed record ArchiveContactAvailabilityExceptionCommand(Guid ExceptionId) : IRequest<Response<bool>>;
