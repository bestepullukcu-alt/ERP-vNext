using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ContactAvailability.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAvailability = Diten.CrmService.Domain.Entities.ContactAvailability;
using DomainException = Diten.CrmService.Domain.Entities.ContactAvailabilityException;

namespace Diten.CrmService.Application.Features.ContactAvailability.Handlers;

/// <summary>
/// Shared write-path plumbing: link resolution (the ONLY source of ContactId/AccountId), MOD-0048 validation and
/// the lifecycle transitions. Nothing here mutates the Contact or Account master, and no path deletes a row.
/// </summary>
internal sealed class ContactAvailabilityWriteContext
{
    public required Guid TenantId { get; init; }
    public required AccountContactLink Link { get; init; }
}

internal static class ContactAvailabilityWrite
{
    /// <summary>Resolves the owning link. Missing / cross-tenant / soft-deleted link → 404 (never a silent write).</summary>
    public static async Task<(AccountContactLink? Link, string? Error, int StatusCode)> ResolveLinkAsync(
        IAccountContactLinkRepository links, Guid tenantId, Guid linkId, CancellationToken cancellationToken)
    {
        var link = await links.GetByIdAsync(tenantId, linkId, cancellationToken);
        return link is null
            ? (null, "Account-contact link not found.", 404)
            : (link, null, 200);
    }

    /// <summary>Validates the three MOD-0048 sets this feature consumes. Fail-closed on an unpublished set.</summary>
    public static async Task<string?> ValidateReferencesAsync(
        IReferenceDataValidator validator, string availabilityType, string source, string? status, CancellationToken cancellationToken)
    {
        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                validator, ContactAvailabilityReferenceSets.Type, availabilityType, required: true, cancellationToken) is { } typeError)
        {
            return typeError;
        }

        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                validator, ContactAvailabilityReferenceSets.Source, source, required: true, cancellationToken) is { } sourceError)
        {
            return sourceError;
        }

        return await ContactAvailabilityValidation.ValidateReferenceAsync(
            validator, ContactAvailabilityReferenceSets.Status, status, required: false, cancellationToken);
    }

    public static VisitPreference ToPreference(VisitPreferenceInput? input) => input is null
        ? new VisitPreference()
        : new VisitPreference
        {
            PreferredVisitDurationMinutes = input.PreferredVisitDurationMinutes,
            PreferredVisitStartTime = AvailabilityWeekday.NormalizeTime(input.PreferredVisitStartTime),
            PreferredVisitEndTime = AvailabilityWeekday.NormalizeTime(input.PreferredVisitEndTime),
            AvoidVisitStartTime = AvailabilityWeekday.NormalizeTime(input.AvoidVisitStartTime),
            AvoidVisitEndTime = AvailabilityWeekday.NormalizeTime(input.AvoidVisitEndTime),
            AppointmentRequired = input.AppointmentRequired,
            AppointmentLeadTimeDays = input.AppointmentLeadTimeDays,
            PreferredContactMethod = string.IsNullOrWhiteSpace(input.PreferredContactMethod) ? null : input.PreferredContactMethod.Trim(),
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };

    /// <summary>Normalizes a requested status; only the three lifecycle values are accepted.</summary>
    public static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return AvailabilityLifecycle.Active;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return AvailabilityLifecycle.All.Contains(normalized) ? normalized : AvailabilityLifecycle.Active;
    }

    public static string? ValidateStatusValue(string? status)
        => string.IsNullOrWhiteSpace(status) || AvailabilityLifecycle.All.Contains(status.Trim().ToLowerInvariant())
            ? null
            : "Status must be one of active, inactive, archived. Availability is never hard-deleted.";
}

public sealed class CreateContactAvailabilityHandler : IRequestHandler<CreateContactAvailabilityCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAvailabilityRepository _availability;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public CreateContactAvailabilityHandler(
        ITenantContext tenant, IActorContext actor, IAccountContactLinkRepository links,
        IContactAvailabilityRepository availability, IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _links = links;
        _availability = availability;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContactAvailabilityCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var (link, linkError, linkStatus) = await ContactAvailabilityWrite.ResolveLinkAsync(
            _links, tenantId, request.AccountContactLinkId, cancellationToken);
        if (link is null)
        {
            return Response<Guid>.Fail(linkError!, linkStatus);
        }

        var status = ContactAvailabilityWrite.NormalizeStatus(request.Status);
        if (ContactAvailabilityWrite.ValidateStatusValue(request.Status) is { } statusError)
        {
            return Response<Guid>.Fail(statusError, 400);
        }

        // An inactive/ended link cannot receive NEW active availability; existing rows stay readable as history.
        if (status == AvailabilityLifecycle.Active && !ContactAvailabilityValidation.IsLinkOpen(link))
        {
            return Response<Guid>.Fail(
                "This account-contact link is not active; active availability cannot be created for it.", 400);
        }

        if (ContactAvailabilityValidation.ValidateWeekday(request.Weekday) is { } weekdayError)
        {
            return Response<Guid>.Fail(weekdayError, 400);
        }

        if (ContactAvailabilityValidation.ValidateWindow(request.StartTime, request.EndTime) is { } windowError)
        {
            return Response<Guid>.Fail(windowError, 400);
        }

        var startTime = AvailabilityWeekday.NormalizeTime(request.StartTime)!;
        var endTime = AvailabilityWeekday.NormalizeTime(request.EndTime)!;
        var preference = ContactAvailabilityWrite.ToPreference(request.Preference);

        if (ContactAvailabilityValidation.ValidatePreference(preference, startTime, endTime) is { } preferenceError)
        {
            return Response<Guid>.Fail(preferenceError, 400);
        }

        if (ContactAvailabilityValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo) is { } rangeError)
        {
            return Response<Guid>.Fail(rangeError, 400);
        }

        if (request.AverageVisitDurationMinutes is <= 0)
        {
            return Response<Guid>.Fail("AverageVisitDurationMinutes must be greater than zero.", 400);
        }

        if (await ContactAvailabilityWrite.ValidateReferencesAsync(
                _referenceValidator, request.AvailabilityType, request.Source, request.Status, cancellationToken) is { } referenceError)
        {
            return Response<Guid>.Fail(referenceError, 400);
        }

        var availabilityType = request.AvailabilityType.Trim();
        var source = request.Source.Trim();
        var weekday = AvailabilityWeekday.Normalize(request.Weekday);
        var existing = await _availability.ListByLinkAsync(tenantId, link.Id, cancellationToken);

        // Idempotency: an identical active row is a no-op, not a duplicate and not an error (pack §20.3).
        if (ContactAvailabilityValidation.FindIdentical(
                existing, weekday, startTime, endTime, availabilityType, source, request.EffectiveFrom, request.EffectiveTo) is { } identical)
        {
            return Response<Guid>.Success(identical.Id, 200);
        }

        // Overlap on the same (link, weekday) with an overlapping effective range is a controlled 409 that reports
        // BOTH identities — a silent merge or overwrite is forbidden.
        if (status == AvailabilityLifecycle.Active
            && ContactAvailabilityValidation.FindOverlap(
                existing, weekday, startTime, endTime, request.EffectiveFrom, request.EffectiveTo, excludeId: null) is { } conflict)
        {
            return Response<Guid>.Fail(
                $"An active availability window already overlaps this one on {weekday} " +
                $"(existing availabilityId={conflict.Id} {conflict.StartTime}-{conflict.EndTime}; " +
                $"requested {startTime}-{endTime}).", 409);
        }

        var row = new DomainAvailability
        {
            TenantId = tenantId,
            AccountContactLinkId = link.Id,
            // Derived from the link — never taken from the request payload (pack D8).
            ContactId = link.ContactId,
            AccountId = link.AccountId,
            Weekday = weekday,
            StartTime = startTime,
            EndTime = endTime,
            Preference = preference,
            AverageVisitDurationMinutes = request.AverageVisitDurationMinutes,
            AvailabilityType = availabilityType,
            Source = source,
            Status = status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = _actor.ActorName
        };

        await _availability.InsertAsync(row, cancellationToken);
        await _audit.PublishAsync("contact-availability.create", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} account={row.AccountId} weekday={row.Weekday}", cancellationToken);

        return Response<Guid>.Success(row.Id, 201);
    }
}

public sealed class UpdateContactAvailabilityHandler : IRequestHandler<UpdateContactAvailabilityCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContactAvailabilityRepository _availability;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public UpdateContactAvailabilityHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityRepository availability,
        IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _availability = availability;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateContactAvailabilityCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var row = await _availability.GetByIdAsync(tenantId, request.AvailabilityId, cancellationToken);
        if (row is null)
        {
            return Response<bool>.Fail("Contact availability not found.", 404);
        }

        if (ContactAvailabilityWrite.ValidateStatusValue(request.Status) is { } statusError)
        {
            return Response<bool>.Fail(statusError, 400);
        }

        if (ContactAvailabilityValidation.ValidateWeekday(request.Weekday) is { } weekdayError)
        {
            return Response<bool>.Fail(weekdayError, 400);
        }

        if (ContactAvailabilityValidation.ValidateWindow(request.StartTime, request.EndTime) is { } windowError)
        {
            return Response<bool>.Fail(windowError, 400);
        }

        var startTime = AvailabilityWeekday.NormalizeTime(request.StartTime)!;
        var endTime = AvailabilityWeekday.NormalizeTime(request.EndTime)!;
        var preference = ContactAvailabilityWrite.ToPreference(request.Preference);

        if (ContactAvailabilityValidation.ValidatePreference(preference, startTime, endTime) is { } preferenceError)
        {
            return Response<bool>.Fail(preferenceError, 400);
        }

        if (ContactAvailabilityValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo) is { } rangeError)
        {
            return Response<bool>.Fail(rangeError, 400);
        }

        if (request.AverageVisitDurationMinutes is <= 0)
        {
            return Response<bool>.Fail("AverageVisitDurationMinutes must be greater than zero.", 400);
        }

        if (await ContactAvailabilityWrite.ValidateReferencesAsync(
                _referenceValidator, request.AvailabilityType, request.Source, request.Status, cancellationToken) is { } referenceError)
        {
            return Response<bool>.Fail(referenceError, 400);
        }

        var status = ContactAvailabilityWrite.NormalizeStatus(request.Status ?? row.Status);
        var weekday = AvailabilityWeekday.Normalize(request.Weekday);
        var existing = await _availability.ListByLinkAsync(tenantId, row.AccountContactLinkId, cancellationToken);

        if (status == AvailabilityLifecycle.Active
            && ContactAvailabilityValidation.FindOverlap(
                existing, weekday, startTime, endTime, request.EffectiveFrom, request.EffectiveTo, excludeId: row.Id) is { } conflict)
        {
            return Response<bool>.Fail(
                $"An active availability window already overlaps this one on {weekday} " +
                $"(existing availabilityId={conflict.Id} {conflict.StartTime}-{conflict.EndTime}; " +
                $"requested {startTime}-{endTime}).", 409);
        }

        // The owning link (and therefore ContactId/AccountId) never changes on update — moving a schedule to another
        // location is a new row on that link, not an edit of this one.
        row.Weekday = weekday;
        row.StartTime = startTime;
        row.EndTime = endTime;
        row.Preference = preference;
        row.AverageVisitDurationMinutes = request.AverageVisitDurationMinutes;
        row.AvailabilityType = request.AvailabilityType.Trim();
        row.Source = request.Source.Trim();
        row.Status = status;
        row.EffectiveFrom = request.EffectiveFrom;
        row.EffectiveTo = request.EffectiveTo;
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = _actor.ActorName;

        await _availability.UpdateAsync(row, cancellationToken);
        await _audit.PublishAsync("contact-availability.update", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} availability={row.Id}", cancellationToken);

        return Response<bool>.Success(true);
    }
}

/// <summary>Deactivate/Archive share one code path: both are status transitions, neither deletes anything.</summary>
public abstract class ContactAvailabilityLifecycleHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly IActorContext Actor;
    protected readonly IContactAvailabilityRepository Availability;
    protected readonly IContactAuditPublisher Audit;

    protected ContactAvailabilityLifecycleHandlerBase(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityRepository availability, IContactAuditPublisher audit)
    {
        Tenant = tenant;
        Actor = actor;
        Availability = availability;
        Audit = audit;
    }

    protected async Task<Response<bool>> TransitionAsync(Guid availabilityId, string targetStatus, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var row = await Availability.GetByIdAsync(tenantId, availabilityId, cancellationToken);
        if (row is null)
        {
            return Response<bool>.Fail("Contact availability not found.", 404);
        }

        if (string.Equals(row.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Success(true); // idempotent transition
        }

        row.Status = targetStatus;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = Actor.ActorName;

        await Availability.UpdateAsync(row, cancellationToken);
        await Audit.PublishAsync($"contact-availability.{targetStatus}", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} availability={row.Id}", cancellationToken);

        return Response<bool>.Success(true);
    }
}

public sealed class DeactivateContactAvailabilityHandler
    : ContactAvailabilityLifecycleHandlerBase, IRequestHandler<DeactivateContactAvailabilityCommand, Response<bool>>
{
    public DeactivateContactAvailabilityHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityRepository availability, IContactAuditPublisher audit)
        : base(tenant, actor, availability, audit)
    {
    }

    public Task<Response<bool>> Handle(DeactivateContactAvailabilityCommand request, CancellationToken cancellationToken)
        => TransitionAsync(request.AvailabilityId, AvailabilityLifecycle.Inactive, cancellationToken);
}

public sealed class ArchiveContactAvailabilityHandler
    : ContactAvailabilityLifecycleHandlerBase, IRequestHandler<ArchiveContactAvailabilityCommand, Response<bool>>
{
    public ArchiveContactAvailabilityHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityRepository availability, IContactAuditPublisher audit)
        : base(tenant, actor, availability, audit)
    {
    }

    public Task<Response<bool>> Handle(ArchiveContactAvailabilityCommand request, CancellationToken cancellationToken)
        => TransitionAsync(request.AvailabilityId, AvailabilityLifecycle.Archived, cancellationToken);
}

public sealed class CreateContactAvailabilityExceptionHandler
    : IRequestHandler<CreateContactAvailabilityExceptionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAvailabilityExceptionRepository _exceptions;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public CreateContactAvailabilityExceptionHandler(
        ITenantContext tenant, IActorContext actor, IAccountContactLinkRepository links,
        IContactAvailabilityExceptionRepository exceptions, IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _links = links;
        _exceptions = exceptions;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContactAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var (link, linkError, linkStatus) = await ContactAvailabilityWrite.ResolveLinkAsync(
            _links, tenantId, request.AccountContactLinkId, cancellationToken);
        if (link is null)
        {
            return Response<Guid>.Fail(linkError!, linkStatus);
        }

        if (ContactAvailabilityWrite.ValidateStatusValue(request.Status) is { } statusError)
        {
            return Response<Guid>.Fail(statusError, 400);
        }

        var status = ContactAvailabilityWrite.NormalizeStatus(request.Status);
        if (status == AvailabilityLifecycle.Active && !ContactAvailabilityValidation.IsLinkOpen(link))
        {
            return Response<Guid>.Fail(
                "This account-contact link is not active; active availability exceptions cannot be created for it.", 400);
        }

        if (ContactAvailabilityValidation.ParseDate(request.Date) is not { } date)
        {
            return Response<Guid>.Fail("Date must be a calendar date in yyyy-MM-dd format.", 400);
        }

        var (startTime, endTime, windowError) = NormalizeExceptionWindow(request.IsAvailable, request.StartTime, request.EndTime);
        if (windowError is not null)
        {
            return Response<Guid>.Fail(windowError, 400);
        }

        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                _referenceValidator, ContactAvailabilityReferenceSets.Source, request.Source, required: true, cancellationToken) is { } sourceError)
        {
            return Response<Guid>.Fail(sourceError, 400);
        }

        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                _referenceValidator, ContactAvailabilityReferenceSets.Status, request.Status, required: false, cancellationToken) is { } statusRefError)
        {
            return Response<Guid>.Fail(statusRefError, 400);
        }

        var normalizedDate = date.ToString("yyyy-MM-dd");
        var existing = await _exceptions.ListByLinkAsync(tenantId, link.Id, cancellationToken);

        if (status == AvailabilityLifecycle.Active
            && existing.FirstOrDefault(e =>
                !AvailabilityLifecycle.IsClosed(e.Status)
                && string.Equals(e.Date, normalizedDate, StringComparison.Ordinal)) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"An active availability exception already exists for this link on {normalizedDate} " +
                $"(exceptionId={duplicate.Id}). Update that row instead of creating a second one.", 409);
        }

        var row = new DomainException
        {
            TenantId = tenantId,
            AccountContactLinkId = link.Id,
            ContactId = link.ContactId,
            AccountId = link.AccountId,
            Date = normalizedDate,
            IsAvailable = request.IsAvailable,
            StartTime = startTime,
            EndTime = endTime,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Source = request.Source.Trim(),
            Status = status,
            CreatedBy = _actor.ActorName
        };

        await _exceptions.InsertAsync(row, cancellationToken);
        await _audit.PublishAsync("contact-availability-exception.create", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} date={row.Date} available={row.IsAvailable}", cancellationToken);

        return Response<Guid>.Success(row.Id, 201);
    }

    /// <summary>An unavailable exception needs no window; an available one needs a well-formed one.</summary>
    internal static (string? Start, string? End, string? Error) NormalizeExceptionWindow(bool isAvailable, string? start, string? end)
    {
        if (!isAvailable)
        {
            // "Not available that day" is a whole-day statement; any supplied window is dropped rather than half-honoured.
            return (null, null, null);
        }

        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
        {
            return (null, null, null); // available all day, per the weekly pattern's own bounds
        }

        if (ContactAvailabilityValidation.ValidateWindow(start, end, "Exception") is { } error)
        {
            return (null, null, error);
        }

        return (AvailabilityWeekday.NormalizeTime(start), AvailabilityWeekday.NormalizeTime(end), null);
    }
}

public sealed class UpdateContactAvailabilityExceptionHandler
    : IRequestHandler<UpdateContactAvailabilityExceptionCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IContactAvailabilityExceptionRepository _exceptions;
    private readonly IReferenceDataValidator _referenceValidator;
    private readonly IContactAuditPublisher _audit;

    public UpdateContactAvailabilityExceptionHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityExceptionRepository exceptions,
        IReferenceDataValidator referenceValidator, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _actor = actor;
        _exceptions = exceptions;
        _referenceValidator = referenceValidator;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(UpdateContactAvailabilityExceptionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var row = await _exceptions.GetByIdAsync(tenantId, request.ExceptionId, cancellationToken);
        if (row is null)
        {
            return Response<bool>.Fail("Contact availability exception not found.", 404);
        }

        if (ContactAvailabilityWrite.ValidateStatusValue(request.Status) is { } statusError)
        {
            return Response<bool>.Fail(statusError, 400);
        }

        if (ContactAvailabilityValidation.ParseDate(request.Date) is not { } date)
        {
            return Response<bool>.Fail("Date must be a calendar date in yyyy-MM-dd format.", 400);
        }

        var (startTime, endTime, windowError) =
            CreateContactAvailabilityExceptionHandler.NormalizeExceptionWindow(request.IsAvailable, request.StartTime, request.EndTime);
        if (windowError is not null)
        {
            return Response<bool>.Fail(windowError, 400);
        }

        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                _referenceValidator, ContactAvailabilityReferenceSets.Source, request.Source, required: true, cancellationToken) is { } sourceError)
        {
            return Response<bool>.Fail(sourceError, 400);
        }

        if (await ContactAvailabilityValidation.ValidateReferenceAsync(
                _referenceValidator, ContactAvailabilityReferenceSets.Status, request.Status, required: false, cancellationToken) is { } statusRefError)
        {
            return Response<bool>.Fail(statusRefError, 400);
        }

        var status = ContactAvailabilityWrite.NormalizeStatus(request.Status ?? row.Status);
        var normalizedDate = date.ToString("yyyy-MM-dd");
        var existing = await _exceptions.ListByLinkAsync(tenantId, row.AccountContactLinkId, cancellationToken);

        if (status == AvailabilityLifecycle.Active
            && existing.FirstOrDefault(e =>
                e.Id != row.Id
                && !AvailabilityLifecycle.IsClosed(e.Status)
                && string.Equals(e.Date, normalizedDate, StringComparison.Ordinal)) is { } duplicate)
        {
            return Response<bool>.Fail(
                $"An active availability exception already exists for this link on {normalizedDate} " +
                $"(exceptionId={duplicate.Id}).", 409);
        }

        row.Date = normalizedDate;
        row.IsAvailable = request.IsAvailable;
        row.StartTime = startTime;
        row.EndTime = endTime;
        row.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        row.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        row.Source = request.Source.Trim();
        row.Status = status;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = _actor.ActorName;

        await _exceptions.UpdateAsync(row, cancellationToken);
        await _audit.PublishAsync("contact-availability-exception.update", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} exception={row.Id}", cancellationToken);

        return Response<bool>.Success(true);
    }
}

public abstract class ContactAvailabilityExceptionLifecycleHandlerBase
{
    protected readonly ITenantContext Tenant;
    protected readonly IActorContext Actor;
    protected readonly IContactAvailabilityExceptionRepository Exceptions;
    protected readonly IContactAuditPublisher Audit;

    protected ContactAvailabilityExceptionLifecycleHandlerBase(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityExceptionRepository exceptions, IContactAuditPublisher audit)
    {
        Tenant = tenant;
        Actor = actor;
        Exceptions = exceptions;
        Audit = audit;
    }

    protected async Task<Response<bool>> TransitionAsync(Guid exceptionId, string targetStatus, CancellationToken cancellationToken)
    {
        if (Tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var row = await Exceptions.GetByIdAsync(tenantId, exceptionId, cancellationToken);
        if (row is null)
        {
            return Response<bool>.Fail("Contact availability exception not found.", 404);
        }

        if (string.Equals(row.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Success(true);
        }

        row.Status = targetStatus;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = Actor.ActorName;

        await Exceptions.UpdateAsync(row, cancellationToken);
        await Audit.PublishAsync($"contact-availability-exception.{targetStatus}", tenantId, row.ContactId,
            $"link={row.AccountContactLinkId} exception={row.Id}", cancellationToken);

        return Response<bool>.Success(true);
    }
}

public sealed class DeactivateContactAvailabilityExceptionHandler
    : ContactAvailabilityExceptionLifecycleHandlerBase, IRequestHandler<DeactivateContactAvailabilityExceptionCommand, Response<bool>>
{
    public DeactivateContactAvailabilityExceptionHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityExceptionRepository exceptions, IContactAuditPublisher audit)
        : base(tenant, actor, exceptions, audit)
    {
    }

    public Task<Response<bool>> Handle(DeactivateContactAvailabilityExceptionCommand request, CancellationToken cancellationToken)
        => TransitionAsync(request.ExceptionId, AvailabilityLifecycle.Inactive, cancellationToken);
}

public sealed class ArchiveContactAvailabilityExceptionHandler
    : ContactAvailabilityExceptionLifecycleHandlerBase, IRequestHandler<ArchiveContactAvailabilityExceptionCommand, Response<bool>>
{
    public ArchiveContactAvailabilityExceptionHandler(
        ITenantContext tenant, IActorContext actor, IContactAvailabilityExceptionRepository exceptions, IContactAuditPublisher audit)
        : base(tenant, actor, exceptions, audit)
    {
    }

    public Task<Response<bool>> Handle(ArchiveContactAvailabilityExceptionCommand request, CancellationToken cancellationToken)
        => TransitionAsync(request.ExceptionId, AvailabilityLifecycle.Archived, cancellationToken);
}
