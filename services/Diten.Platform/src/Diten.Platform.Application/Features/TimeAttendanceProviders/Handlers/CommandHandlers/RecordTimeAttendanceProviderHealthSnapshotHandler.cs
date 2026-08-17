using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class RecordTimeAttendanceProviderHealthSnapshotHandler : IRequestHandler<RecordTimeAttendanceProviderHealthSnapshotCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public RecordTimeAttendanceProviderHealthSnapshotHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordTimeAttendanceProviderHealthSnapshotCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<Guid>.Fail("Time-attendance provider not found.", 404);
        }

        var snapshot = new TimeAttendanceProviderHealthSnapshot
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            HealthState = request.Request.HealthState,
            CheckedAt = request.Request.CheckedAt,
            RedactedMessage = string.IsNullOrWhiteSpace(request.Request.RedactedMessage) ? null : request.Request.RedactedMessage.Trim(),
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateHealthSnapshotAsync(snapshot, ct);
        return Response<Guid>.Success(snapshot.Id, 201);
    }
}
