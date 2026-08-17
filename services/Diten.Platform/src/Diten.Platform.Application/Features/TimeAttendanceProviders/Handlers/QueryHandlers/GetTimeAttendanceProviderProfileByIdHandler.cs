using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.QueryHandlers;

public sealed class GetTimeAttendanceProviderProfileByIdHandler : IRequestHandler<GetTimeAttendanceProviderProfileByIdQuery, Response<TimeAttendanceProviderProfileDto>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public GetTimeAttendanceProviderProfileByIdHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<TimeAttendanceProviderProfileDto>> Handle(GetTimeAttendanceProviderProfileByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetProviderProfileByIdAsync(request.Id, ct);
        return item == null
            ? Response<TimeAttendanceProviderProfileDto>.Fail("Time attendance provider not found.", 404)
            : Response<TimeAttendanceProviderProfileDto>.Success(TimeAttendanceProviderMapper.ToDto(item));
    }
}
