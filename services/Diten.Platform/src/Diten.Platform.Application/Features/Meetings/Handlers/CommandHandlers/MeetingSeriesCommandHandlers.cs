using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

// MOD-0357 S11 — the recurring-series RULE's own CRUD, mirroring MeetingTypeCommandHandlers.cs exactly (S8):
// tenant-unique Name (pre-check + the storage-level unique index is the real guarantee), optimistic-concurrency
// update, delete refuses nothing (KS8 — deleting the RULE never touches meetings it already produced, because
// nothing here ever reads or writes a Meeting row at all).

internal static class MeetingSeriesMapping
{
    public static MeetingSeriesDto ToDto(MeetingSeries series, string? meetingTypeName) => new(
        series.Id, series.Name, series.MeetingTypeId, meetingTypeName, series.Frequency, series.Interval,
        series.StartsAt, series.EndsAt, series.DurationMinutes, series.Location, series.OrganizerUserId,
        series.AttendeeUserIds, series.LeadTimeDays, series.ChainAsFollowUp,
        series.LastGeneratedMeetingId, series.LastGeneratedAt, series.IsActive, series.Version);

    /// <summary>The field-shape rules pack §12 states for a series, checked identically on create and update.
    /// Returns the first violation's (message, reasonCode), or null when the request is well-formed.</summary>
    public static (string Message, string ReasonCode)? Validate(
        DateTimeOffset startsAt, DateTimeOffset? endsAt, int interval, int leadTimeDays, Guid organizerUserId)
    {
        // KS3 — OrganizerUserId is a non-nullable Guid on the wire, so the only way a caller "omits" it is
        // Guid.Empty; refused explicitly rather than silently accepted as a real (if unlikely) user id.
        if (organizerUserId == Guid.Empty)
        {
            return ("An organizer is required.", MeetingReasonCodes.SeriesOrganizerRequired);
        }

        if (endsAt is { } ends && ends <= startsAt)
        {
            return ("The series end must be after its start.", MeetingReasonCodes.SeriesInvalidWindow);
        }

        if (interval < 1)
        {
            return ("The interval must be at least 1.", MeetingReasonCodes.SeriesIntervalInvalid);
        }

        if (leadTimeDays is < 1 or > 90)
        {
            return ("The lead time must be between 1 and 90 days.", MeetingReasonCodes.SeriesInvalidWindow);
        }

        return null;
    }
}

public sealed class CreateMeetingSeriesHandler : IRequestHandler<CreateMeetingSeriesCommand, Response<MeetingSeriesDto>>
{
    private readonly IMeetingSeriesRepository _series;
    private readonly IMeetingTypeRepository _types;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateMeetingSeriesHandler(
        IMeetingSeriesRepository series, IMeetingTypeRepository types,
        ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _series = series;
        _types = types;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<MeetingSeriesDto>> Handle(CreateMeetingSeriesCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var name = request.Name.Trim();

        if (await _series.FindByNameAsync(name, ct) is not null)
        {
            return Response<MeetingSeriesDto>.Fail(
                "A meeting series with this name already exists.", 409, MeetingReasonCodes.SeriesNameDuplicate, command.CorrelationId);
        }

        var type = await _types.GetByIdAsync(request.MeetingTypeId, ct);
        if (type is null)
        {
            return Response<MeetingSeriesDto>.Fail(
                "The meeting type does not exist.", 400, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        if (MeetingSeriesMapping.Validate(request.StartsAt, request.EndsAt, request.Interval, request.LeadTimeDays, request.OrganizerUserId)
            is { } invalid)
        {
            return Response<MeetingSeriesDto>.Fail(invalid.Message, 400, invalid.ReasonCode, command.CorrelationId);
        }

        var series = await _series.CreateAsync(new MeetingSeries
        {
            TenantId = _tenantContext.TenantId,
            Name = name,
            MeetingTypeId = request.MeetingTypeId,
            Frequency = request.Frequency,
            Interval = request.Interval,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            DurationMinutes = request.DurationMinutes,
            Location = request.Location,
            OrganizerUserId = request.OrganizerUserId,
            AttendeeUserIds = request.AttendeeUserIds?.Distinct().ToList() ?? [],
            LeadTimeDays = request.LeadTimeDays,
            ChainAsFollowUp = request.ChainAsFollowUp,
            IsActive = request.IsActive,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<MeetingSeriesDto>.Success(
            MeetingSeriesMapping.ToDto(series, type.Name), 201, command.CorrelationId);
    }
}

public sealed class UpdateMeetingSeriesHandler : IRequestHandler<UpdateMeetingSeriesCommand, Response<NoContent>>
{
    private readonly IMeetingSeriesRepository _series;
    private readonly IMeetingTypeRepository _types;

    public UpdateMeetingSeriesHandler(IMeetingSeriesRepository series, IMeetingTypeRepository types)
    {
        _series = series;
        _types = types;
    }

    public async Task<Response<NoContent>> Handle(UpdateMeetingSeriesCommand command, CancellationToken ct)
    {
        var series = await _series.GetByIdAsync(command.Id, ct);
        if (series is null)
        {
            return Response<NoContent>.Fail("The meeting series does not exist.", 404, MeetingReasonCodes.SeriesNotFound, command.CorrelationId);
        }

        var request = command.Request;
        var name = request.Name.Trim();
        var byName = await _series.FindByNameAsync(name, ct);
        if (byName is not null && byName.Id != command.Id)
        {
            return Response<NoContent>.Fail(
                "A meeting series with this name already exists.", 409, MeetingReasonCodes.SeriesNameDuplicate, command.CorrelationId);
        }

        if (await _types.GetByIdAsync(request.MeetingTypeId, ct) is null)
        {
            return Response<NoContent>.Fail(
                "The meeting type does not exist.", 400, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        if (MeetingSeriesMapping.Validate(request.StartsAt, request.EndsAt, request.Interval, request.LeadTimeDays, request.OrganizerUserId)
            is { } invalid)
        {
            return Response<NoContent>.Fail(invalid.Message, 400, invalid.ReasonCode, command.CorrelationId);
        }

        series.Name = name;
        series.MeetingTypeId = request.MeetingTypeId;
        series.Frequency = request.Frequency;
        series.Interval = request.Interval;
        series.StartsAt = request.StartsAt;
        series.EndsAt = request.EndsAt;
        series.DurationMinutes = request.DurationMinutes;
        series.Location = request.Location;
        series.OrganizerUserId = request.OrganizerUserId;
        series.AttendeeUserIds = request.AttendeeUserIds?.Distinct().ToList() ?? [];
        series.LeadTimeDays = request.LeadTimeDays;
        series.ChainAsFollowUp = request.ChainAsFollowUp;
        // KS8 — flipping this to false (or true) never touches a meeting this rule already produced; the sweep
        // simply stops (or resumes) generating the NEXT one.
        series.IsActive = request.IsActive;

        if (!await _series.UpdateAsync(series, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting series changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

/// <summary>KS8 — deletes only the RULE row. No check against, and no cascade onto, any meeting it already
/// generated: <see cref="MeetingSeries.LastGeneratedMeetingId"/> is a display pointer, never a foreign key this
/// handler enforces.</summary>
public sealed class DeleteMeetingSeriesHandler : IRequestHandler<DeleteMeetingSeriesCommand, Response<NoContent>>
{
    private readonly IMeetingSeriesRepository _series;

    public DeleteMeetingSeriesHandler(IMeetingSeriesRepository series) => _series = series;

    public async Task<Response<NoContent>> Handle(DeleteMeetingSeriesCommand command, CancellationToken ct)
    {
        var series = await _series.GetByIdAsync(command.Id, ct);
        if (series is null)
        {
            return Response<NoContent>.Fail("The meeting series does not exist.", 404, MeetingReasonCodes.SeriesNotFound, command.CorrelationId);
        }

        await _series.DeleteAsync(command.Id, ct);
        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}
