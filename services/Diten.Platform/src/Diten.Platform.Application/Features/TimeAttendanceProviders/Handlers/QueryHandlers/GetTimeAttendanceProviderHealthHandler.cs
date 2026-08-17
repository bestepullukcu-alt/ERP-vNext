using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceProviderHealthHandler : IRequestHandler<GetTimeAttendanceProviderHealthQuery, Response<TimeAttendanceProviderHealthSnapshotDto>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceProviderHealthHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<TimeAttendanceProviderHealthSnapshotDto>> Handle(GetTimeAttendanceProviderHealthQuery request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<TimeAttendanceProviderHealthSnapshotDto>.Fail("Time attendance provider not found.", 404);
        }

        var item = await _repository.GetLatestHealthSnapshotAsync(request.ProviderProfileId, ct);
        return item == null
            ? Response<TimeAttendanceProviderHealthSnapshotDto>.Fail("Time attendance provider health not found.", 404)
            : Response<TimeAttendanceProviderHealthSnapshotDto>.Success(TimeAttendanceProviderMapper.ToDto(item));
    }
}
