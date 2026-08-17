using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceProviderProfileListHandler : IRequestHandler<GetTimeAttendanceProviderProfileListQuery, Response<IReadOnlyList<TimeAttendanceProviderProfileDto>>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceProviderProfileListHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<TimeAttendanceProviderProfileDto>>> Handle(GetTimeAttendanceProviderProfileListQuery request, CancellationToken ct)
    {
        var items = await _repository.GetProviderProfilesAsync(ct);
        return Response<IReadOnlyList<TimeAttendanceProviderProfileDto>>.Success(items.Select(TimeAttendanceProviderMapper.ToDto).ToList());
    }
}
