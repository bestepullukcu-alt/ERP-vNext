using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class RecordTimeAttendanceSyncCheckpointHandler : IRequestHandler<RecordTimeAttendanceSyncCheckpointCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public RecordTimeAttendanceSyncCheckpointHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordTimeAttendanceSyncCheckpointCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<Guid>.Fail("Time-attendance provider not found.", 404);
        }

        var checkpoint = new TimeAttendanceSyncCheckpoint
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            SyncRunId = request.Request.SyncRunId.Trim(),
            SyncMode = request.Request.SyncMode,
            StartedAt = request.Request.StartedAt,
            CompletedAt = request.Request.CompletedAt,
            CheckpointReference = string.IsNullOrWhiteSpace(request.Request.CheckpointReference) ? null : request.Request.CheckpointReference.Trim(),
            RecordsSeen = request.Request.RecordsSeen,
            RecordsAccepted = request.Request.RecordsAccepted,
            RecordsRejected = request.Request.RecordsRejected,
            Status = request.Request.Status,
            ErrorSummary = string.IsNullOrWhiteSpace(request.Request.ErrorSummary) ? null : request.Request.ErrorSummary.Trim()
        };

        await _repository.CreateSyncCheckpointAsync(checkpoint, ct);
        return Response<Guid>.Success(checkpoint.Id, 201);
    }
}
