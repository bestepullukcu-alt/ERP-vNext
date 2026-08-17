using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceSyncCheckpointHandler : IRequestHandler<GetTimeAttendanceSyncCheckpointQuery, Response<TimeAttendanceSyncCheckpointDto>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceSyncCheckpointHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<TimeAttendanceSyncCheckpointDto>> Handle(GetTimeAttendanceSyncCheckpointQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<TimeAttendanceSyncCheckpointDto>.Fail("Time attendance provider not found.", 404);
        }

        var item = await _repository.GetLatestSyncCheckpointAsync(request.ProviderProfileId, ct);
        return item == null
            ? Response<TimeAttendanceSyncCheckpointDto>.Fail("Time attendance sync checkpoint not found.", 404)
            : Response<TimeAttendanceSyncCheckpointDto>.Success(TimeAttendanceProviderMapper.ToDto(item));
    }
}
