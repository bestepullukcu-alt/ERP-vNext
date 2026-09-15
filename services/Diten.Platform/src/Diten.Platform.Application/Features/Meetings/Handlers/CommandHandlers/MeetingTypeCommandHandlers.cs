using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

internal static class MeetingTypeMapping
{
    public static MeetingTypeDto ToDto(MeetingType type) => new(
        type.Id, type.Name, type.AgendaTemplate, type.DefaultActionTaskTypeId,
        type.IsQualityRecord, type.RequiresESignature, type.AttendanceMandatory, type.Version);
}

public sealed class CreateMeetingTypeHandler : IRequestHandler<CreateMeetingTypeCommand, Response<MeetingTypeDto>>
{
    private readonly IMeetingTypeRepository _types;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateMeetingTypeHandler(
        IMeetingTypeRepository types, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _types = types;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<MeetingTypeDto>> Handle(CreateMeetingTypeCommand command, CancellationToken ct)
    {
        var name = command.Request.Name.Trim();
        if (await _types.FindByNameAsync(name, ct) is not null)
        {
            return Response<MeetingTypeDto>.Fail(
                "A meeting type with this name already exists.", 409, MeetingReasonCodes.TypeNameDuplicate, command.CorrelationId);
        }

        var type = await _types.CreateAsync(new MeetingType
        {
            TenantId = _tenantContext.TenantId,
            Name = name,
            AgendaTemplate = command.Request.AgendaTemplate?.ToList() ?? [],
            DefaultActionTaskTypeId = command.Request.DefaultActionTaskTypeId,
            IsQualityRecord = command.Request.IsQualityRecord,
            RequiresESignature = command.Request.RequiresESignature,
            AttendanceMandatory = command.Request.AttendanceMandatory,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<MeetingTypeDto>.Success(MeetingTypeMapping.ToDto(type), 201, command.CorrelationId);
    }
}

public sealed class UpdateMeetingTypeHandler : IRequestHandler<UpdateMeetingTypeCommand, Response<NoContent>>
{
    private readonly IMeetingTypeRepository _types;

    public UpdateMeetingTypeHandler(IMeetingTypeRepository types) => _types = types;

    public async Task<Response<NoContent>> Handle(UpdateMeetingTypeCommand command, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(command.Id, ct);
        if (type is null)
        {
            return Response<NoContent>.Fail("The meeting type does not exist.", 404, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        var name = command.Request.Name.Trim();
        var byName = await _types.FindByNameAsync(name, ct);
        if (byName is not null && byName.Id != command.Id)
        {
            return Response<NoContent>.Fail(
                "A meeting type with this name already exists.", 409, MeetingReasonCodes.TypeNameDuplicate, command.CorrelationId);
        }

        type.Name = name;
        type.AgendaTemplate = command.Request.AgendaTemplate?.ToList() ?? [];
        type.DefaultActionTaskTypeId = command.Request.DefaultActionTaskTypeId;
        type.IsQualityRecord = command.Request.IsQualityRecord;
        type.RequiresESignature = command.Request.RequiresESignature;
        type.AttendanceMandatory = command.Request.AttendanceMandatory;

        if (!await _types.UpdateAsync(type, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting type changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

public sealed class DeleteMeetingTypeHandler : IRequestHandler<DeleteMeetingTypeCommand, Response<NoContent>>
{
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingRepository _meetings;

    public DeleteMeetingTypeHandler(IMeetingTypeRepository types, IMeetingRepository meetings)
    {
        _types = types;
        _meetings = meetings;
    }

    public async Task<Response<NoContent>> Handle(DeleteMeetingTypeCommand command, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(command.Id, ct);
        if (type is null)
        {
            return Response<NoContent>.Fail("The meeting type does not exist.", 404, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        if (await _meetings.AnyByMeetingTypeIdAsync(command.Id, ct))
        {
            return Response<NoContent>.Fail(
                "This meeting type is used by at least one meeting and cannot be deleted.",
                409, MeetingReasonCodes.TypeInUse, command.CorrelationId);
        }

        await _types.DeleteAsync(command.Id, ct);
        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}
