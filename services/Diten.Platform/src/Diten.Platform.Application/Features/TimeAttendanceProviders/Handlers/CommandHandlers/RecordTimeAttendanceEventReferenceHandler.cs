using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class RecordTimeAttendanceEventReferenceHandler : IRequestHandler<RecordTimeAttendanceEventReferenceCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public RecordTimeAttendanceEventReferenceHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordTimeAttendanceEventReferenceCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<Guid>.Fail("Time-attendance provider not found.", 404);
        }

        var eventReference = new TimeAttendanceEventReference
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            ExternalEventId = request.Request.ExternalEventId.Trim(),
            EventType = request.Request.EventType,
            ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim(),
            ProviderTimestamp = request.Request.ProviderTimestamp,
            ProviderTimeZoneId = string.IsNullOrWhiteSpace(request.Request.ProviderTimeZoneId) ? null : request.Request.ProviderTimeZoneId.Trim(),
            ProcessingState = request.Request.ProcessingState,
            IdempotencyKey = request.Request.IdempotencyKey.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateEventReferenceAsync(eventReference, ct);
        return Response<Guid>.Success(eventReference.Id, 201);
    }
}
